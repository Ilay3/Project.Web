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
    /// Фоновая служба планирования производства согласно ТЗ
    /// </summary>
    public class ProductionSchedulerBackgroundService : BackgroundService
    {
        private readonly ILogger<ProductionSchedulerBackgroundService> _logger;
        private readonly IServiceProvider _serviceProvider;

        // Интервал проверки (30 секунд)
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);

        // Рабочие часы предприятия (согласно ТЗ)
        private readonly TimeSpan _workStart = new TimeSpan(8, 0, 0);   // 08:00
        private readonly TimeSpan _workEnd = new TimeSpan(1, 30, 0);     // 01:30 следующего дня
        private readonly TimeSpan _lunchStart = new TimeSpan(12, 0, 0); // 12:00
        private readonly TimeSpan _lunchEnd = new TimeSpan(13, 0, 0);   // 13:00
        private readonly TimeSpan _dinnerStart = new TimeSpan(21, 0, 0); // 21:00
        private readonly TimeSpan _dinnerEnd = new TimeSpan(21, 30, 0);  // 21:30

        // Тайм-зона предприятия (можно настроить через конфигурацию)
        private readonly TimeZoneInfo _enterpriseTimeZone = TimeZoneInfo.Local;

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

            // Ждем немного, чтобы приложение полностью инициализировалось
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessSchedulingCycleAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Критическая ошибка в цикле планирования производства");
                }

                // Ожидание перед следующей проверкой
                try
                {
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("Служба автоматического планирования производства остановлена");
        }

        private async Task ProcessSchedulingCycleAsync()
        {
            using var scope = _serviceProvider.CreateScope();

            var schedulerService = scope.ServiceProvider.GetRequiredService<ProductionSchedulerService>();
            var stageService = scope.ServiceProvider.GetRequiredService<StageExecutionService>();
            var batchRepo = scope.ServiceProvider.GetRequiredService<Domain.Repositories.IBatchRepository>();

            var currentTime = GetCurrentEnterpriseTime();
            var isWorkingTime = IsWorkingTime(currentTime);

            _logger.LogDebug("Цикл планирования: {Time}, рабочее время: {IsWorking}",
                currentTime.ToString("yyyy-MM-dd HH:mm:ss"), isWorkingTime);

            try
            {
                // 1. АВТОМАТИЧЕСКОЕ ЗАВЕРШЕНИЕ ПРОСРОЧЕННЫХ ЭТАПОВ (согласно ТЗ: более 2 часов сверх нормы)
                await AutoCompleteOverdueStagesAsync(stageService, batchRepo);

                // 2. УПРАВЛЕНИЕ ЭТАПАМИ ВНЕ РАБОЧЕГО ВРЕМЕНИ
                if (!isWorkingTime)
                {
                    await HandleStagesOutsideWorkingHoursAsync(stageService, batchRepo);
                }
                else
                {
                    // 3. ВОЗОБНОВЛЕНИЕ ЭТАПОВ В РАБОЧЕЕ ВРЕМЯ
                    await ResumeStagesDuringWorkingHoursAsync(stageService, batchRepo);

                    // 4. ПЛАНИРОВАНИЕ ЭТАПОВ В ОЧЕРЕДИ (только в рабочее время)
                    await ProcessQueuedStagesAsync(schedulerService, batchRepo);

                    // 5. АВТОЗАПУСК ГОТОВЫХ ЭТАПОВ
                    await AutoStartPendingStagesAsync(schedulerService, batchRepo);
                }

                // 6. ОБРАБОТКА ЗАВЕРШЕННЫХ ЭТАПОВ (в любое время)
                await ProcessCompletedStagesAsync(schedulerService, batchRepo);

                // 7. ОПТИМИЗАЦИЯ ПЛАНИРОВАНИЯ (в рабочее время, раз в 5 минут)
                if (isWorkingTime && DateTime.UtcNow.Minute % 5 == 0)
                {
                    await OptimizeSchedulingAsync(schedulerService);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в цикле планирования");
            }
        }

        /// <summary>
        /// Автоматическое завершение просроченных этапов (согласно ТЗ: более 2 часов сверх нормы)
        /// </summary>
        private async Task AutoCompleteOverdueStagesAsync(StageExecutionService stageService, Domain.Repositories.IBatchRepository batchRepo)
        {
            try
            {
                var inProgressStages = await batchRepo.GetAllStageExecutionsAsync();

                var overdueStages = inProgressStages.Where(s =>
                    s.Status == Domain.Entities.StageExecutionStatus.InProgress &&
                    s.IsOverdue).ToList();

                foreach (var stage in overdueStages)
                {
                    try
                    {
                        var overdueTime = stage.TimeDeviation ?? TimeSpan.Zero;

                        _logger.LogWarning("Автозавершение просроченного этапа {StageId}. " +
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

                if (overdueStages.Any())
                {
                    _logger.LogInformation("Автоматически завершено {Count} просроченных этапов", overdueStages.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке просроченных этапов");
            }
        }

        /// <summary>
        /// Обработка этапов вне рабочего времени (согласно ТЗ)
        /// </summary>
        private async Task HandleStagesOutsideWorkingHoursAsync(StageExecutionService stageService, Domain.Repositories.IBatchRepository batchRepo)
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

                if (stagesToPause.Any())
                {
                    _logger.LogInformation("Приостановлено {Count} этапов вне рабочего времени", stagesToPause.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при приостановке этапов вне рабочего времени");
            }
        }

        /// <summary>
        /// Возобновление этапов в рабочее время
        /// </summary>
        private async Task ResumeStagesDuringWorkingHoursAsync(StageExecutionService stageService, Domain.Repositories.IBatchRepository batchRepo)
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

                if (stagesToResume.Any())
                {
                    _logger.LogInformation("Возобновлено {Count} этапов в рабочее время", stagesToResume.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при возобновлении этапов в рабочее время");
            }
        }

        /// <summary>
        /// Обработка этапов в очереди
        /// </summary>
        private async Task ProcessQueuedStagesAsync(ProductionSchedulerService schedulerService, Domain.Repositories.IBatchRepository batchRepo)
        {
            try
            {
                var stagesInQueue = await batchRepo.GetAllStagesInQueueAsync();
                var processedCount = 0;

                foreach (var stage in stagesInQueue.Take(10)) // Ограничиваем для производительности
                {
                    try
                    {
                        _logger.LogDebug("Планирование этапа в очереди: {StageId}", stage.Id);
                        await schedulerService.ScheduleStageExecutionAsync(stage.Id);
                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при планировании этапа {StageId}", stage.Id);
                    }
                }

                if (processedCount > 0)
                {
                    _logger.LogDebug("Обработано {Count} этапов из очереди", processedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке очереди этапов");
            }
        }

        /// <summary>
        /// Автозапуск готовых этапов
        /// </summary>
        private async Task AutoStartPendingStagesAsync(ProductionSchedulerService schedulerService, Domain.Repositories.IBatchRepository batchRepo)
        {
            try
            {
                var pendingStages = await batchRepo.GetPendingStagesAsync();
                var startedCount = 0;

                foreach (var stage in pendingStages.Take(5)) // Ограничиваем количество одновременных запусков
                {
                    try
                    {
                        bool canStart = await schedulerService.CanStartStageAsync(stage.Id);

                        if (canStart)
                        {
                            _logger.LogDebug("Автоматический запуск готового этапа: {StageId}", stage.Id);

                            if (stage.MachineId.HasValue)
                            {
                                var started = await schedulerService.StartPendingStageAsync(stage.Id);
                                if (started) startedCount++;
                            }
                            else
                            {
                                await schedulerService.ScheduleStageExecutionAsync(stage.Id);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при автозапуске этапа {StageId}", stage.Id);
                    }
                }

                if (startedCount > 0)
                {
                    _logger.LogInformation("Автоматически запущено {Count} этапов", startedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при автозапуске этапов");
            }
        }

        /// <summary>
        /// Обработка завершенных этапов
        /// </summary>
        private async Task ProcessCompletedStagesAsync(ProductionSchedulerService schedulerService, Domain.Repositories.IBatchRepository batchRepo)
        {
            try
            {
                var completedStages = await batchRepo.GetRecentlyCompletedStagesAsync();
                var processedCount = 0;

                foreach (var stage in completedStages)
                {
                    try
                    {
                        _logger.LogDebug("Обработка завершенного этапа: {StageId}", stage.Id);
                        await schedulerService.HandleStageCompletionAsync(stage.Id);
                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при обработке завершенного этапа {StageId}", stage.Id);
                    }
                }

                if (processedCount > 0)
                {
                    _logger.LogDebug("Обработано {Count} завершенных этапов", processedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке завершенных этапов");
            }
        }

        /// <summary>
        /// Оптимизация планирования
        /// </summary>
        private async Task OptimizeSchedulingAsync(ProductionSchedulerService schedulerService)
        {
            try
            {
                await schedulerService.OptimizeQueueAsync();
                _logger.LogDebug("Выполнена оптимизация планирования");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при оптимизации планирования");
            }
        }

        /// <summary>
        /// Проверка, должен ли этап продолжаться вне рабочего времени (согласно ТЗ)
        /// </summary>
        private bool ShouldContinueOutsideWorkingHours(Domain.Entities.StageExecution stage)
        {
            // Этапы переналадки могут продолжаться вне рабочего времени (согласно ТЗ)
            if (stage.IsSetup) return true;

            // Критически важные этапы (высокий приоритет > 7)
            if (stage.Priority > 7 || stage.IsCritical) return true;

            // Этапы, которые скоро завершатся (менее 30 минут) (согласно ТЗ)
            if (stage.StartTimeUtc.HasValue)
            {
                var elapsed = DateTime.UtcNow - stage.StartTimeUtc.Value;
                var remaining = stage.PlannedDuration - elapsed;

                if (remaining <= TimeSpan.FromMinutes(30)) return true;
            }

            return false;
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
                "автоматическая приостановка",
                "вне рабочего времени"
            };

            return systemPauseReasons.Any(reason =>
                stage.ReasonNote.Contains(reason, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Получение текущего времени предприятия
        /// </summary>
        private DateTime GetCurrentEnterpriseTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _enterpriseTimeZone);
        }

        /// <summary>
        /// Проверка рабочего времени (согласно ТЗ)
        /// </summary>
        private bool IsWorkingTime(DateTime dateTime)
        {
            try
            {
                var timeOfDay = dateTime.TimeOfDay;
                var dayOfWeek = dateTime.DayOfWeek;

                // Не работаем в выходные (согласно ТЗ: суббота, воскресенье)
                if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
                {
                    _logger.LogDebug("Выходной день: {DayOfWeek}", dayOfWeek);
                    return false;
                }

                // Обеденный перерыв 12:00-13:00 (согласно ТЗ)
                if (timeOfDay >= _lunchStart && timeOfDay < _lunchEnd)
                {
                    _logger.LogDebug("Обеденный перерыв: {Time}", timeOfDay);
                    return false;
                }

                // Перерыв на ужин 21:00-21:30 (согласно ТЗ)
                if (timeOfDay >= _dinnerStart && timeOfDay < _dinnerEnd)
                {
                    _logger.LogDebug("Перерыв на ужин: {Time}", timeOfDay);
                    return false;
                }

                // Рабочее время: 08:00-01:30 следующего дня (согласно ТЗ)
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