using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Project.Application.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace Project.Application.BackgroundServices
{
    /// <summary>
    /// Улучшенная фоновая служба с автоматическим управлением этапами
    /// </summary>
    public class ProductionSchedulerBackgroundService : BackgroundService
    {
        private readonly ILogger<ProductionSchedulerBackgroundService> _logger;
        private readonly IServiceProvider _serviceProvider;

        // Интервал проверки (30 секунд)
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);

        // Рабочие часы предприятия (местное время)
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

            var isWorkingTime = IsWorkingTime(DateTime.Now);

            _logger.LogDebug("Проверка планирования. Рабочее время: {IsWorking}", isWorkingTime);

            try
            {
                // 1. АВТОМАТИЧЕСКОЕ ЗАВЕРШЕНИЕ ПРОСРОЧЕННЫХ ЭТАПОВ
                await AutoCompleteOverdueStages(stageService, batchRepo);

                // 2. УПРАВЛЕНИЕ ЭТАПАМИ ВНЕ РАБОЧЕГО ВРЕМЕНИ
                if (!isWorkingTime)
                {
                    await PauseStagesOutsideWorkingHours(stageService, batchRepo);
                }
                else
                {
                    // 3. ВОЗОБНОВЛЕНИЕ ЭТАПОВ В РАБОЧЕЕ ВРЕМЯ
                    await ResumeStagesDuringWorkingHours(stageService, batchRepo);
                }

                // 4. ПЛАНИРОВАНИЕ ЭТАПОВ В ОЧЕРЕДИ (только в рабочее время)
                if (isWorkingTime)
                {
                    var stagesInQueue = await batchRepo.GetAllStagesInQueueAsync();
                    foreach (var stage in stagesInQueue)
                    {
                        _logger.LogDebug("Планирование этапа в очереди: {StageId}", stage.Id);
                        await schedulerService.ScheduleStageExecutionAsync(stage.Id);
                    }

                    // 5. АВТОЗАПУСК ГОТОВЫХ ЭТАПОВ
                    var pendingStages = await batchRepo.GetPendingStagesAsync();
                    foreach (var stage in pendingStages.Take(5)) // Ограничиваем количество для предотвращения перегрузки
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
                }

                // 6. ОБРАБОТКА ЗАВЕРШЕННЫХ ЭТАПОВ
                var completedStages = await batchRepo.GetRecentlyCompletedStagesAsync();
                foreach (var stage in completedStages)
                {
                    _logger.LogDebug("Обработка завершенного этапа: {StageId}", stage.Id);
                    await schedulerService.HandleStageCompletionAsync(stage.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в цикле планирования");
            }
        }

        /// <summary>
        /// Автоматическое завершение просроченных этапов
        /// </summary>
        private async Task AutoCompleteOverdueStages(StageExecutionService stageService, Domain.Repositories.IBatchRepository batchRepo)
        {
            try
            {
                var inProgressStages = await batchRepo.GetAllStageExecutionsAsync();

                var overdueStages = inProgressStages.Where(s =>
                    s.Status == Domain.Entities.StageExecutionStatus.InProgress &&
                    s.StartTimeUtc.HasValue &&
                    IsStageOverdue(s)).ToList();

                foreach (var stage in overdueStages)
                {
                    try
                    {
                        // Рассчитываем плановое время завершения
                        var plannedDuration = stage.IsSetup
                            ? TimeSpan.FromHours(stage.RouteStage.SetupTime)
                            : TimeSpan.FromHours(stage.RouteStage.NormTime * stage.SubBatch.Quantity);

                        var plannedEndTime = stage.StartTimeUtc.Value.Add(plannedDuration);
                        var now = DateTime.UtcNow;
                        var overdueTime = now - plannedEndTime;

                        _logger.LogWarning("Автоматическое завершение просроченного этапа {StageId}. " +
                            "Просрочка: {OverdueHours:F1} часов", stage.Id, overdueTime.TotalHours);

                        await stageService.CompleteStageExecution(
                            stage.Id,
                            operatorId: "AUTO_SYSTEM",
                            reasonNote: $"Автозавершение. Просрочка: {overdueTime.TotalHours:F1}ч",
                            deviceId: "AUTO_SCHEDULER");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при автозавершении этапа {StageId}", stage.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке просроченных этапов");
            }
        }

        /// <summary>
        /// Проверка, является ли этап просроченным
        /// </summary>
        private bool IsStageOverdue(Domain.Entities.StageExecution stage)
        {
            if (!stage.StartTimeUtc.HasValue) return false;

            var plannedDuration = stage.IsSetup
                ? TimeSpan.FromHours(stage.RouteStage.SetupTime)
                : TimeSpan.FromHours(stage.RouteStage.NormTime * stage.SubBatch.Quantity);

            var plannedEndTime = stage.StartTimeUtc.Value.Add(plannedDuration);
            var now = DateTime.UtcNow;

            // Считаем просроченным, если прошло более 2 часов сверх плана
            return now > plannedEndTime.AddHours(2);
        }

        /// <summary>
        /// Приостановка этапов вне рабочего времени
        /// </summary>
        private async Task PauseStagesOutsideWorkingHours(StageExecutionService stageService, Domain.Repositories.IBatchRepository batchRepo)
        {
            try
            {
                var inProgressStages = await batchRepo.GetAllStageExecutionsAsync();

                var stagesToPause = inProgressStages.Where(s =>
                    s.Status == Domain.Entities.StageExecutionStatus.InProgress &&
                    !ShouldContinueOutsideWorkingHours(s)).ToList();

                foreach (var stage in stagesToPause)
                {
                    try
                    {
                        _logger.LogInformation("Автоприостановка этапа {StageId} вне рабочего времени", stage.Id);

                        await stageService.PauseStageExecution(
                            stage.Id,
                            operatorId: "AUTO_SYSTEM",
                            reasonNote: "Автоматическая приостановка вне рабочего времени",
                            deviceId: "AUTO_SCHEDULER");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при автоприостановке этапа {StageId}", stage.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при приостановке этапов вне рабочего времени");
            }
        }

        /// <summary>
        /// Проверка, должен ли этап продолжаться вне рабочего времени
        /// </summary>
        private bool ShouldContinueOutsideWorkingHours(Domain.Entities.StageExecution stage)
        {
            // Этапы переналадки могут продолжаться вне рабочего времени
            if (stage.IsSetup) return true;

            // Критически важные этапы (с высоким приоритетом)
            if (stage.Priority > 5) return true;

            // Этапы, которые скоро завершатся (менее 30 минут)
            if (stage.StartTimeUtc.HasValue)
            {
                var plannedDuration = TimeSpan.FromHours(stage.RouteStage.NormTime * stage.SubBatch.Quantity);
                var elapsed = DateTime.UtcNow - stage.StartTimeUtc.Value;
                var remaining = plannedDuration - elapsed;

                if (remaining <= TimeSpan.FromMinutes(30)) return true;
            }

            return false;
        }

        /// <summary>
        /// Возобновление этапов в рабочее время
        /// </summary>
        private async Task ResumeStagesDuringWorkingHours(StageExecutionService stageService, Domain.Repositories.IBatchRepository batchRepo)
        {
            try
            {
                var pausedStages = await batchRepo.GetAllStageExecutionsAsync();

                var stagesToResume = pausedStages.Where(s =>
                    s.Status == Domain.Entities.StageExecutionStatus.Paused &&
                    WasPausedBySystem(s)).ToList();

                foreach (var stage in stagesToResume)
                {
                    try
                    {
                        _logger.LogInformation("Автовозобновление этапа {StageId} в рабочее время", stage.Id);

                        await stageService.ResumeStageExecution(
                            stage.Id,
                            operatorId: "AUTO_SYSTEM",
                            reasonNote: "Автоматическое возобновление в рабочее время",
                            deviceId: "AUTO_SCHEDULER");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при автовозобновлении этапа {StageId}", stage.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при возобновлении этапов в рабочее время");
            }
        }

        /// <summary>
        /// Проверка, был ли этап приостановлен системой
        /// </summary>
        private bool WasPausedBySystem(Domain.Entities.StageExecution stage)
        {
            if (string.IsNullOrEmpty(stage.ReasonNote)) return false;

            var systemPauseReasons = new[]
            {
                "рабочего времени",
                "AUTO_SYSTEM",
                "автоматическая приостановка"
            };

            return systemPauseReasons.Any(reason =>
                stage.ReasonNote.Contains(reason, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Проверка рабочего времени (улучшенная)
        /// </summary>
        private bool IsWorkingTime(DateTime dateTime)
        {
            try
            {
                // Используем местное время
                var localTime = dateTime;
                var timeOfDay = localTime.TimeOfDay;
                var dayOfWeek = localTime.DayOfWeek;

                // Не работаем в выходные
                if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
                {
                    _logger.LogDebug("Выходной день: {DayOfWeek}", dayOfWeek);
                    return false;
                }

                // Обеденный перерыв 12:00-13:00
                if (timeOfDay >= _lunchStart && timeOfDay < _lunchEnd)
                {
                    _logger.LogDebug("Обеденный перерыв: {Time}", timeOfDay);
                    return false;
                }

                // Перерыв на ужин 21:00-21:30
                if (timeOfDay >= _dinnerStart && timeOfDay < _dinnerEnd)
                {
                    _logger.LogDebug("Перерыв на ужин: {Time}", timeOfDay);
                    return false;
                }

                // Рабочее время: 08:00-01:30 (следующего дня)
                // Обрабатываем переход через полночь
                if (_workEnd < _workStart) // переход через полночь
                {
                    var isWorking = timeOfDay >= _workStart || timeOfDay <= _workEnd;
                    _logger.LogDebug("Проверка рабочего времени с переходом через полночь: {Time}, результат: {IsWorking}",
                        timeOfDay, isWorking);
                    return isWorking;
                }
                else
                {
                    var isWorking = timeOfDay >= _workStart && timeOfDay <= _workEnd;
                    _logger.LogDebug("Проверка рабочего времени: {Time}, результат: {IsWorking}",
                        timeOfDay, isWorking);
                    return isWorking;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке рабочего времени");
                return true; // По умолчанию считаем рабочим временем
            }
        }
    }
}