using Microsoft.Extensions.Logging;
using Project.Contracts.ModelDTO;
using Project.Domain.Entities;
using Project.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Project.Application.Services
{
    public class StageExecutionService
    {
        private readonly IRouteRepository _routeRepo;
        private readonly IMachineRepository _machineRepo;
        private readonly IBatchRepository _batchRepo;
        private readonly ISetupTimeRepository _setupTimeRepo;
        private readonly IDetailRepository _detailRepo;
        private readonly EventLogService _eventLogService;
        private readonly ILogger<StageExecutionService> _logger;

        private readonly object _lockObject = new object();

        public StageExecutionService(
            IRouteRepository routeRepo,
            IMachineRepository machineRepo,
            IBatchRepository batchRepo,
            ISetupTimeRepository setupTimeRepo,
            IDetailRepository detailRepo,
            EventLogService eventLogService,
            ILogger<StageExecutionService> logger)
        {
            _routeRepo = routeRepo;
            _machineRepo = machineRepo;
            _batchRepo = batchRepo;
            _setupTimeRepo = setupTimeRepo;
            _detailRepo = detailRepo;
            _eventLogService = eventLogService;
            _logger = logger;
        }

        /// <summary>
        /// Генерация этапов маршрута для подпартии
        /// </summary>
        public async Task GenerateStageExecutionsForSubBatch(int subBatchId)
        {
            try
            {
                var subBatch = await _batchRepo.GetSubBatchByIdAsync(subBatchId);
                if (subBatch == null) throw new Exception("SubBatch not found");

                var batch = subBatch.Batch;
                var detail = batch.Detail;

                var route = await _routeRepo.GetByDetailIdAsync(detail.Id);
                if (route == null) throw new Exception($"Route not found for Detail {detail.Name}");

                // Создаем этапы выполнения в той же последовательности, что и в маршруте
                foreach (var stage in route.Stages.OrderBy(s => s.Order))
                {
                    var stageExecution = new StageExecution
                    {
                        SubBatchId = subBatchId,
                        RouteStageId = stage.Id,
                        Status = StageExecutionStatus.Pending,
                        IsSetup = false,
                        StatusChangedTimeUtc = DateTime.UtcNow,
                        Priority = 0
                    };

                    subBatch.StageExecutions.Add(stageExecution);
                }

                await _batchRepo.UpdateSubBatchAsync(subBatch);

                // Логируем создание этапов
                foreach (var stageExecution in subBatch.StageExecutions.Where(se => !se.IsSetup))
                {
                    await _eventLogService.LogStageCreatedAsync(stageExecution.Id, isAutomatic: true);
                }

                _logger.LogInformation("Созданы этапы для подпартии {SubBatchId}: {Count} этапов",
                    subBatchId, subBatch.StageExecutions.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при генерации этапов для подпартии {SubBatchId}", subBatchId);
                throw;
            }
        }

        /// <summary>
        /// Назначение этапа на конкретный станок
        /// </summary>
        public async Task AssignStageToMachine(int stageExecutionId, int machineId)
        {
            try
            {
                var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
                if (stageExecution == null) throw new Exception("Stage execution not found");

                var machine = await _machineRepo.GetByIdAsync(machineId);
                if (machine == null) throw new Exception("Machine not found");

                var routeStage = stageExecution.RouteStage;

                // Валидация статуса перед назначением
                if (stageExecution.Status != StageExecutionStatus.Pending &&
                    stageExecution.Status != StageExecutionStatus.Waiting)
                {
                    throw new Exception($"Cannot assign machine to stage in status: {stageExecution.Status}");
                }

                // Проверка соответствия типа станка
                if (machine.MachineTypeId != routeStage.MachineTypeId)
                    throw new Exception($"Machine type mismatch: required {routeStage.MachineType.Name}, got {machine.MachineType.Name}");

                // Проверяем, нужна ли переналадка
                var setupStage = await CheckAndCreateSetupStageIfNeeded(stageExecution, machine);

                var previousMachineId = stageExecution.MachineId;

                // Привязываем основной этап к станку
                stageExecution.MachineId = machineId;
                stageExecution.Status = setupStage != null ?
                    StageExecutionStatus.Waiting : // ждем завершения переналадки
                    StageExecutionStatus.Pending;  // готов к запуску
                stageExecution.StatusChangedTimeUtc = DateTime.UtcNow;

                await _batchRepo.UpdateStageExecutionAsync(stageExecution);

                // Логируем назначение на станок
                await _eventLogService.LogStageAssignedAsync(stageExecution.Id, machineId, isAutomatic: true);

                // Логируем переназначение, если был другой станок
                if (previousMachineId.HasValue && previousMachineId.Value != machineId)
                {
                    await _eventLogService.LogStageReassignedAsync(
                        stageExecution.Id, previousMachineId.Value, machineId,
                        "Automatic reassignment", isAutomatic: true);
                }

                _logger.LogInformation("Этап {StageId} назначен на станок {MachineId}",
                    stageExecutionId, machineId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при назначении этапа {StageId} на станок {MachineId}",
                    stageExecutionId, machineId);
                throw;
            }
        }

        /// <summary>
        /// Валидация переходов статусов
        /// </summary>
        private bool CanTransitionTo(StageExecutionStatus currentStatus, StageExecutionStatus newStatus)
        {
            // Матрица разрешенных переходов
            var allowedTransitions = new Dictionary<StageExecutionStatus, List<StageExecutionStatus>>
            {
                [StageExecutionStatus.Pending] = new() {
                    StageExecutionStatus.InProgress,
                    StageExecutionStatus.Waiting,
                    StageExecutionStatus.Error
                },
                [StageExecutionStatus.Waiting] = new() {
                    StageExecutionStatus.Pending,
                    StageExecutionStatus.Error
                },
                [StageExecutionStatus.InProgress] = new() {
                    StageExecutionStatus.Paused,
                    StageExecutionStatus.Completed,
                    StageExecutionStatus.Error
                },
                [StageExecutionStatus.Paused] = new() {
                    StageExecutionStatus.InProgress,
                    StageExecutionStatus.Completed,
                    StageExecutionStatus.Error
                },
                [StageExecutionStatus.Completed] = new() { }, // Завершенный этап нельзя изменить
                [StageExecutionStatus.Error] = new() {
                    StageExecutionStatus.Pending
                } // Можно перезапустить
            };

            return allowedTransitions.ContainsKey(currentStatus) &&
                   allowedTransitions[currentStatus].Contains(newStatus);
        }

        /// <summary>
        /// ЗАПУСК ЭТАПА с улучшенной обработкой ошибок
        /// </summary>
        public async Task StartStageExecution(int stageExecutionId, string operatorId = null, string deviceId = null)
        {
            StageExecution stageExecution = null;

            try
            {
                // 1. Получаем этап с блокировкой
                lock (_lockObject)
                {
                    stageExecution = _batchRepo.GetStageExecutionByIdAsync(stageExecutionId).Result;
                    if (stageExecution == null)
                        throw new Exception("Stage execution not found");

                    _logger.LogInformation("Запуск этапа {StageId}, текущий статус: {Status}",
                        stageExecutionId, stageExecution.Status);

                    // 2. Валидация статуса
                    if (!CanTransitionTo(stageExecution.Status, StageExecutionStatus.InProgress))
                    {
                        throw new Exception($"Нельзя запустить этап из статуса: {stageExecution.Status}");
                    }
                }

                // 3. Проверка зависимостей (без блокировки)
                if (!stageExecution.IsSetup)
                {
                    var canStart = await CheckAllPreviousStagesCompleted(stageExecution);
                    if (!canStart)
                    {
                        throw new Exception("Предыдущие этапы не завершены");
                    }
                }

                // 4. Проверка переналадки
                if (!stageExecution.IsSetup)
                {
                    var setupStage = await _batchRepo.GetSetupStageForMainStageAsync(stageExecutionId);
                    if (setupStage != null && setupStage.Status != StageExecutionStatus.Completed)
                    {
                        throw new Exception("Переналадка не завершена");
                    }
                }

                // 5. Проверка занятости станка
                if (stageExecution.MachineId.HasValue)
                {
                    var currentStageOnMachine = await _batchRepo.GetCurrentStageOnMachineAsync(stageExecution.MachineId.Value);
                    if (currentStageOnMachine != null && currentStageOnMachine.Id != stageExecutionId)
                    {
                        throw new Exception($"Станок занят другим этапом: {currentStageOnMachine.Id}");
                    }
                }

                // 6. Обновление этапа
                var timeInPreviousState = stageExecution.StatusChangedTimeUtc.HasValue
                    ? DateTime.UtcNow - stageExecution.StatusChangedTimeUtc.Value
                    : (TimeSpan?)null;

                stageExecution.Status = StageExecutionStatus.InProgress;
                stageExecution.StartTimeUtc = DateTime.UtcNow;
                stageExecution.StatusChangedTimeUtc = DateTime.UtcNow;
                stageExecution.StartAttempts = (stageExecution.StartAttempts ?? 0) + 1;
                stageExecution.OperatorId = operatorId ?? "SYSTEM";
                stageExecution.DeviceId = deviceId ?? "AUTO";

                await _batchRepo.UpdateStageExecutionAsync(stageExecution);

                // 7. Логирование
                await _eventLogService.LogStageStartedAsync(
                    stageExecutionId, operatorId, operatorId, deviceId,
                    timeInPreviousState: timeInPreviousState,
                    isAutomatic: string.IsNullOrEmpty(operatorId));

                _logger.LogInformation("Этап {StageId} успешно запущен", stageExecutionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при запуске этапа {StageId}: {Error}", stageExecutionId, ex.Message);

                // Записываем ошибку в этап
                if (stageExecution != null)
                {
                    try
                    {
                        stageExecution.LastErrorMessage = ex.Message;
                        await _batchRepo.UpdateStageExecutionAsync(stageExecution);
                    }
                    catch { /* Игнорируем вторичные ошибки */ }
                }

                throw new Exception($"Ошибка при запуске этапа: {ex.Message}");
            }
        }

        /// <summary>
        /// ПРИОСТАНОВКА ЭТАПА с улучшенной обработкой
        /// </summary>
        public async Task PauseStageExecution(int stageExecutionId, string operatorId = null, string reasonNote = null, string deviceId = null)
        {
            StageExecution stageExecution = null;

            try
            {
                // 1. Получаем этап
                stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
                if (stageExecution == null)
                    throw new Exception("Stage execution not found");

                _logger.LogInformation("Приостановка этапа {StageId}, текущий статус: {Status}",
                    stageExecutionId, stageExecution.Status);

                // 2. Валидация статуса
                if (!CanTransitionTo(stageExecution.Status, StageExecutionStatus.Paused))
                {
                    throw new Exception($"Нельзя приостановить этап из статуса: {stageExecution.Status}");
                }

                // 3. Расчет времени в работе
                var timeInProgress = stageExecution.StartTimeUtc.HasValue
                    ? DateTime.UtcNow - stageExecution.StartTimeUtc.Value
                    : (TimeSpan?)null;

                // 4. Обновление этапа
                stageExecution.Status = StageExecutionStatus.Paused;
                stageExecution.PauseTimeUtc = DateTime.UtcNow;
                stageExecution.StatusChangedTimeUtc = DateTime.UtcNow;
                stageExecution.OperatorId = operatorId ?? "SYSTEM";
                stageExecution.ReasonNote = reasonNote ?? "Приостановлен пользователем";
                stageExecution.DeviceId = deviceId ?? "AUTO";

                await _batchRepo.UpdateStageExecutionAsync(stageExecution);

                // 5. Логирование
                await _eventLogService.LogStagePausedAsync(
                    stageExecutionId,
                    reasonNote ?? "Приостановлен",
                    operatorId,
                    operatorId,
                    deviceId,
                    timeInProgress,
                    isAutomatic: string.IsNullOrEmpty(operatorId));

                _logger.LogInformation("Этап {StageId} приостановлен", stageExecutionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при приостановке этапа {StageId}: {Error}", stageExecutionId, ex.Message);
                throw new Exception($"Ошибка при приостановке этапа: {ex.Message}");
            }
        }

        /// <summary>
        /// ВОЗОБНОВЛЕНИЕ ЭТАПА
        /// </summary>
        public async Task ResumeStageExecution(int stageExecutionId, string operatorId = null, string reasonNote = null, string deviceId = null)
        {
            try
            {
                var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
                if (stageExecution == null)
                    throw new Exception("Stage execution not found");

                _logger.LogInformation("Возобновление этапа {StageId}, текущий статус: {Status}",
                    stageExecutionId, stageExecution.Status);

                // Валидация статуса
                if (!CanTransitionTo(stageExecution.Status, StageExecutionStatus.InProgress))
                {
                    throw new Exception($"Нельзя возобновить этап из статуса: {stageExecution.Status}");
                }

                var timeInPause = stageExecution.PauseTimeUtc.HasValue
                    ? DateTime.UtcNow - stageExecution.PauseTimeUtc.Value
                    : (TimeSpan?)null;

                stageExecution.Status = StageExecutionStatus.InProgress;
                stageExecution.ResumeTimeUtc = DateTime.UtcNow;
                stageExecution.StatusChangedTimeUtc = DateTime.UtcNow;
                stageExecution.OperatorId = operatorId ?? "SYSTEM";
                stageExecution.ReasonNote = reasonNote ?? "Возобновлен";
                stageExecution.DeviceId = deviceId ?? "AUTO";

                await _batchRepo.UpdateStageExecutionAsync(stageExecution);

                await _eventLogService.LogStageResumedAsync(
                    stageExecutionId, operatorId, operatorId, deviceId, reasonNote, timeInPause,
                    isAutomatic: string.IsNullOrEmpty(operatorId));

                _logger.LogInformation("Этап {StageId} возобновлен", stageExecutionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при возобновлении этапа {StageId}: {Error}", stageExecutionId, ex.Message);
                throw new Exception($"Ошибка при возобновлении этапа: {ex.Message}");
            }
        }

        /// <summary>
        /// ЗАВЕРШЕНИЕ ЭТАПА
        /// </summary>
        public async Task CompleteStageExecution(int stageExecutionId, string operatorId = null, string reasonNote = null, string deviceId = null)
        {
            try
            {
                var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
                if (stageExecution == null)
                    throw new Exception("Stage execution not found");

                _logger.LogInformation("Завершение этапа {StageId}, текущий статус: {Status}",
                    stageExecutionId, stageExecution.Status);

                // Валидация статуса
                if (!CanTransitionTo(stageExecution.Status, StageExecutionStatus.Completed))
                {
                    throw new Exception($"Нельзя завершить этап из статуса: {stageExecution.Status}");
                }

                var timeInProgress = stageExecution.StartTimeUtc.HasValue
                    ? DateTime.UtcNow - stageExecution.StartTimeUtc.Value
                    : (TimeSpan?)null;

                stageExecution.Status = StageExecutionStatus.Completed;
                stageExecution.EndTimeUtc = DateTime.UtcNow;
                stageExecution.StatusChangedTimeUtc = DateTime.UtcNow;
                stageExecution.IsProcessedByScheduler = false; // Для обработки планировщиком
                stageExecution.OperatorId = operatorId ?? "SYSTEM";
                stageExecution.ReasonNote = reasonNote ?? "Завершен";
                stageExecution.DeviceId = deviceId ?? "AUTO";

                await _batchRepo.UpdateStageExecutionAsync(stageExecution);

                await _eventLogService.LogStageCompletedAsync(
                    stageExecutionId, operatorId, operatorId, deviceId, reasonNote, timeInProgress,
                    isAutomatic: string.IsNullOrEmpty(operatorId));

                _logger.LogInformation("Этап {StageId} завершен", stageExecutionId);

                // После завершения переналадки делаем доступным основной этап
                if (stageExecution.IsSetup)
                {
                    var mainStage = await _batchRepo.GetMainStageForSetupAsync(stageExecutionId);
                    if (mainStage != null && mainStage.Status == StageExecutionStatus.Waiting)
                    {
                        mainStage.Status = StageExecutionStatus.Pending;
                        mainStage.StatusChangedTimeUtc = DateTime.UtcNow;
                        await _batchRepo.UpdateStageExecutionAsync(mainStage);

                        _logger.LogInformation("Основной этап {MainStageId} готов к запуску после переналадки",
                            mainStage.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при завершении этапа {StageId}: {Error}", stageExecutionId, ex.Message);
                throw new Exception($"Ошибка при завершении этапа: {ex.Message}");
            }
        }

        /// <summary>
        /// ОТМЕНА ЭТАПА
        /// </summary>
        public async Task CancelStageExecution(int stageExecutionId, string reason, string operatorId = null, string deviceId = null)
        {
            try
            {
                var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
                if (stageExecution == null)
                    throw new Exception("Stage execution not found");

                if (stageExecution.Status == StageExecutionStatus.Completed)
                {
                    throw new Exception("Нельзя отменить завершенный этап");
                }

                stageExecution.Status = StageExecutionStatus.Error;
                stageExecution.StatusChangedTimeUtc = DateTime.UtcNow;
                stageExecution.ReasonNote = reason ?? "Отменен";
                stageExecution.IsProcessedByScheduler = true;
                stageExecution.OperatorId = operatorId ?? "SYSTEM";
                stageExecution.DeviceId = deviceId ?? "AUTO";

                await _batchRepo.UpdateStageExecutionAsync(stageExecution);

                await _eventLogService.LogStageCancelledAsync(
                    stageExecutionId, reason, operatorId, operatorId, deviceId,
                    isAutomatic: string.IsNullOrEmpty(operatorId));

                _logger.LogInformation("Этап {StageId} отменен", stageExecutionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отмене этапа {StageId}: {Error}", stageExecutionId, ex.Message);
                throw new Exception($"Ошибка при отмене этапа: {ex.Message}");
            }
        }

        /// <summary>
        /// УЛУЧШЕННАЯ проверка зависимостей между этапами
        /// </summary>
        private async Task<bool> CheckAllPreviousStagesCompleted(StageExecution stageExecution)
        {
            try
            {
                // Если это этап переналадки, предыдущие не проверяем
                if (stageExecution.IsSetup)
                    return true;

                var subBatch = stageExecution.SubBatch;
                var currentStage = stageExecution.RouteStage;

                // Получаем маршрут
                var route = await _routeRepo.GetByIdAsync(currentStage.RouteId);
                if (route == null)
                {
                    _logger.LogError("Маршрут не найден для этапа {StageId}", stageExecution.Id);
                    return false;
                }

                // Если это первый этап маршрута
                var isFirstStage = !route.Stages.Any(s => s.Order < currentStage.Order);
                if (isFirstStage)
                {
                    _logger.LogInformation("Этап {StageId} является первым в маршруте", stageExecution.Id);
                    return true;
                }

                // Проверяем предыдущие этапы
                var previousStages = route.Stages
                    .Where(s => s.Order < currentStage.Order)
                    .OrderBy(s => s.Order)
                    .ToList();

                foreach (var prevStage in previousStages)
                {
                    var prevStageExecution = subBatch.StageExecutions
                        .FirstOrDefault(se => !se.IsSetup && se.RouteStageId == prevStage.Id);

                    if (prevStageExecution == null)
                    {
                        _logger.LogWarning("Предыдущий этап {PrevStageId} не найден для этапа {StageId}",
                            prevStage.Id, stageExecution.Id);
                        return false;
                    }

                    if (prevStageExecution.Status != StageExecutionStatus.Completed)
                    {
                        _logger.LogWarning("Предыдущий этап {PrevStageId} не завершен (статус: {Status}) для этапа {StageId}",
                            prevStageExecution.Id, prevStageExecution.Status, stageExecution.Id);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке зависимостей для этапа {StageId}", stageExecution.Id);
                return false;
            }
        }

        /// <summary>
        /// Проверка и создание этапа переналадки при необходимости
        /// </summary>
        private async Task<StageExecution?> CheckAndCreateSetupStageIfNeeded(StageExecution stageExecution, Machine machine)
        {
            try
            {
                var currentDetailId = stageExecution.SubBatch.Batch.DetailId;
                var lastStageOnMachine = await _batchRepo.GetLastCompletedStageOnMachineAsync(machine.Id);

                if (lastStageOnMachine == null || lastStageOnMachine.SubBatch.Batch.DetailId == currentDetailId)
                    return null;

                var previousDetailId = lastStageOnMachine.SubBatch.Batch.DetailId;
                var setupTime = await _setupTimeRepo.GetSetupTimeAsync(machine.Id, previousDetailId, currentDetailId);

                if (setupTime == null)
                {
                    var setupDuration = stageExecution.RouteStage.SetupTime;
                    setupTime = new SetupTime
                    {
                        MachineId = machine.Id,
                        FromDetailId = previousDetailId,
                        ToDetailId = currentDetailId,
                        Time = setupDuration
                    };
                    await _setupTimeRepo.AddAsync(setupTime);
                }

                var setupStage = new StageExecution
                {
                    SubBatchId = stageExecution.SubBatchId,
                    RouteStageId = stageExecution.RouteStageId,
                    MachineId = machine.Id,
                    Status = StageExecutionStatus.Pending,
                    IsSetup = true,
                    StatusChangedTimeUtc = DateTime.UtcNow
                };

                var subBatch = stageExecution.SubBatch;
                subBatch.StageExecutions.Add(setupStage);
                await _batchRepo.UpdateSubBatchAsync(subBatch);

                await _eventLogService.LogStageCreatedAsync(setupStage.Id, isAutomatic: true);

                return setupStage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании этапа переналадки");
                throw;
            }
        }

        // Остальные методы без изменений
        public async Task<List<GanttStageDto>> GetAllStagesForGanttChart(DateTime? startDate = null, DateTime? endDate = null)
        {
            var allStages = await _batchRepo.GetAllStageExecutionsAsync();

            if (startDate.HasValue)
                allStages = allStages.Where(s => !s.EndTimeUtc.HasValue || s.EndTimeUtc >= startDate).ToList();

            if (endDate.HasValue)
                allStages = allStages.Where(s => !s.StartTimeUtc.HasValue || s.StartTimeUtc <= endDate).ToList();

            var result = new List<GanttStageDto>();

            foreach (var stage in allStages)
            {
                var subBatch = stage.SubBatch;
                var batch = subBatch.Batch;
                var detail = batch.Detail;
                var routeStage = stage.RouteStage;

                result.Add(new GanttStageDto
                {
                    Id = stage.Id,
                    BatchId = batch.Id,
                    SubBatchId = subBatch.Id,
                    DetailName = detail.Name,
                    StageName = stage.IsSetup ? $"Переналадка: {detail.Name}" : routeStage.Name,
                    MachineId = stage.MachineId,
                    MachineName = stage.Machine?.Name,
                    StartTime = stage.StartTimeUtc,
                    EndTime = stage.EndTimeUtc,
                    Status = stage.Status.ToString(),
                    IsSetup = stage.IsSetup,
                    PlannedDuration = CalculatePlannedDuration(stage, subBatch.Quantity),
                    ScheduledStartTime = stage.ScheduledStartTimeUtc,
                    ScheduledEndTime = stage.ScheduledStartTimeUtc.HasValue
                        ? stage.ScheduledStartTimeUtc.Value.Add(CalculatePlannedDuration(stage, subBatch.Quantity))
                        : null,
                    QueuePosition = stage.QueuePosition,
                    Priority = stage.Priority,
                    OperatorId = stage.OperatorId,
                    ReasonNote = stage.ReasonNote
                });
            }

            return result;
        }

        private TimeSpan CalculatePlannedDuration(StageExecution stage, int quantity)
        {
            if (stage.IsSetup)
            {
                var setupTime = stage.RouteStage.SetupTime;
                return TimeSpan.FromHours(setupTime);
            }
            else
            {
                var normTime = stage.RouteStage.NormTime;
                return TimeSpan.FromHours(normTime * quantity);
            }
        }
    }
}