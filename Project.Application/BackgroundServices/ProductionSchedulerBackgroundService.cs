using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Project.Application.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Project.Application.BackgroundServices
{
    /// <summary>
    /// Улучшенная фоновая служба с автоматическим завершением этапов
    /// </summary>
    public class ProductionSchedulerBackgroundService : BackgroundService
    {
        private readonly ILogger<ProductionSchedulerBackgroundService> _logger;
        private readonly IServiceProvider _serviceProvider;

        // Интервал проверки (10 секунд)
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(10);

        // Рабочие часы предприятия (Саратов UTC+4)
        private readonly TimeSpan _workStart = new TimeSpan(8, 0, 0);   // 08:00
        private readonly TimeSpan _lunchStart = new TimeSpan(12, 0, 0); // 12:00
        private readonly TimeSpan _lunchEnd = new TimeSpan(13, 0, 0);   // 13:00
        private readonly TimeSpan _dinnerStart = new TimeSpan(21, 0, 0); // 21:00
        private readonly TimeSpan _dinnerEnd = new TimeSpan(21, 30, 0);  // 21:30
        private readonly TimeSpan _workEnd = new TimeSpan(1, 30, 0);     // 01:30 следующего дня

        public ProductionSchedulerBackgroundService(
            ILogger<ProductionSchedulerBackgroundService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Служба автоматического планирования производства запущена");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessSchedulingAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при выполнении автоматического планирования производства");
                }

                // Ожидание перед следующей проверкой
                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("Служба автоматического планирования производства остановлена");
        }

        private async Task ProcessSchedulingAsync()
        {
            using var scope = _serviceProvider.CreateScope();

            var schedulerService = scope.ServiceProvider.GetRequiredService<ProductionSchedulerService>();
            var stageService = scope.ServiceProvider.GetRequiredService<StageExecutionService>();
            var batchRepo = scope.ServiceProvider.GetRequiredService<Domain.Repositories.IBatchRepository>();

            // 1. АВТОМАТИЧЕСКОЕ ЗАВЕРШЕНИЕ ПРОСРОЧЕННЫХ ЭТАПОВ
            await AutoCompleteOverdueStages(stageService, batchRepo);

            // 2. ПРИОСТАНОВКА ЭТАПОВ ВНЕ РАБОЧЕГО ВРЕМЕНИ
            await PauseStagesOutsideWorkingHours(stageService, batchRepo);

            // 3. ВОЗОБНОВЛЕНИЕ ЭТАПОВ В РАБОЧЕЕ ВРЕМЯ
            await ResumeStagesDuringWorkingHours(stageService, batchRepo);

            // 4. Проверяем этапы, ожидающие в очереди
            var stagesInQueue = await batchRepo.GetAllStagesInQueueAsync();
            foreach (var stage in stagesInQueue)
            {
                _logger.LogInformation("Планирование этапа в очереди: {StageId}", stage.Id);
                await schedulerService.ScheduleStageExecutionAsync(stage.Id);
            }

            // 5. Проверяем новые этапы, готовые к запуску
            var pendingStages = await batchRepo.GetPendingStagesAsync();
            foreach (var stage in pendingStages)
            {
                bool canStart = await schedulerService.CanStartStageAsync(stage.Id);

                if (canStart)
                {
                    _logger.LogInformation("Автоматический запуск готового этапа: {StageId}", stage.Id);

                    if (stage.MachineId.HasValue)
                    {
                        await schedulerService.StartPendingStageAsync(stage.Id);
                    }
                    else
                    {
                        await schedulerService.ScheduleStageExecutionAsync(stage.Id);
                    }
                }
            }

            // 6. Обрабатываем завершенные этапы
            var completedStages = await batchRepo.GetRecentlyCompletedStagesAsync();
            foreach (var stage in completedStages)
            {
                _logger.LogInformation("Обработка завершенного этапа: {StageId}", stage.Id);
                await schedulerService.HandleStageCompletionAsync(stage.Id);
            }
        }

        /// <summary>
        /// Автоматическое завершение просроченных этапов
        /// </summary>
        private async Task AutoCompleteOverdueStages(StageExecutionService stageService, Domain.Repositories.IBatchRepository batchRepo)
        {
            var inProgressStages = await batchRepo.GetAllStageExecutionsAsync();

            foreach (var stage in inProgressStages.Where(s => s.Status == Domain.Entities.StageExecutionStatus.InProgress))
            {
                if (!stage.StartTimeUtc.HasValue) continue;

                // Рассчитываем плановое время завершения
                var plannedDuration = stage.IsSetup
                    ? TimeSpan.FromHours(stage.RouteStage.SetupTime)
                    : TimeSpan.FromHours(stage.RouteStage.NormTime * stage.SubBatch.Quantity);

                var plannedEndTime = stage.StartTimeUtc.Value.Add(plannedDuration);
                var now = DateTime.UtcNow;

                // Если этап просрочен более чем на 2 часа - автоматически завершаем
                if (now > plannedEndTime.AddHours(2))
                {
                    try
                    {
                        _logger.LogWarning("Автоматическое завершение просроченного этапа: {StageId}. Запланировано до: {PlannedEnd}, текущее время: {Now}",
                            stage.Id, plannedEndTime, now);

                        await stageService.CompleteStageExecution(
                            stage.Id,
                            operatorId: "SYSTEM",
                            reasonNote: "Автоматическое завершение по истечении планового времени",
                            deviceId: "AUTO_SCHEDULER");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при автоматическом завершении этапа {StageId}", stage.Id);
                    }
                }
            }
        }

        /// <summary>
        /// Приостановка этапов вне рабочего времени
        /// </summary>
        private async Task PauseStagesOutsideWorkingHours(StageExecutionService stageService, Domain.Repositories.IBatchRepository batchRepo)
        {
            if (IsWorkingTime(DateTime.Now)) return;

            var inProgressStages = await batchRepo.GetAllStageExecutionsAsync();

            foreach (var stage in inProgressStages.Where(s => s.Status == Domain.Entities.StageExecutionStatus.InProgress))
            {
                try
                {
                    _logger.LogInformation("Приостановка этапа {StageId} вне рабочего времени", stage.Id);

                    await stageService.PauseStageExecution(
                        stage.Id,
                        operatorId: "SYSTEM",
                        reasonNote: "Автоматическая приостановка вне рабочего времени",
                        deviceId: "AUTO_SCHEDULER");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при приостановке этапа {StageId}", stage.Id);
                }
            }
        }

        /// <summary>
        /// Возобновление этапов в рабочее время
        /// </summary>
        private async Task ResumeStagesDuringWorkingHours(StageExecutionService stageService, Domain.Repositories.IBatchRepository batchRepo)
        {
            if (!IsWorkingTime(DateTime.Now)) return;

            var pausedStages = await batchRepo.GetAllStageExecutionsAsync();

            foreach (var stage in pausedStages.Where(s => s.Status == Domain.Entities.StageExecutionStatus.Paused))
            {
                // Возобновляем только если была приостановлена системой
                if (stage.ReasonNote?.Contains("рабочего времени") == true)
                {
                    try
                    {
                        _logger.LogInformation("Возобновление этапа {StageId} в рабочее время", stage.Id);

                        await stageService.ResumeStageExecution(
                            stage.Id,
                            operatorId: "SYSTEM",
                            reasonNote: "Автоматическое возобновление в рабочее время",
                            deviceId: "AUTO_SCHEDULER");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при возобновлении этапа {StageId}", stage.Id);
                    }
                }
            }
        }

        /// <summary>
        /// Проверка, является ли текущее время рабочим (с учетом часового пояса Саратова UTC+4)
        /// </summary>
        private bool IsWorkingTime(DateTime dateTime)
        {
            // Конвертируем в местное время Саратова (UTC+4)
            var saratovTime = dateTime.ToUniversalTime().AddHours(4);
            var timeOfDay = saratovTime.TimeOfDay;
            var dayOfWeek = saratovTime.DayOfWeek;

            // Не работаем в выходные
            if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
                return false;

            // Обеденный перерыв 12:00-13:00
            if (timeOfDay >= _lunchStart && timeOfDay < _lunchEnd)
                return false;

            // Перерыв на ужин 21:00-21:30
            if (timeOfDay >= _dinnerStart && timeOfDay < _dinnerEnd)
                return false;

            // Рабочее время: 08:00-01:30 (следующего дня)
            if (timeOfDay >= _workStart || timeOfDay <= _workEnd)
                return true;

            return false;
        }
    }
}