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
        /// *** ИСПРАВЛЕННАЯ ВАЛИДАЦИЯ ПЕРЕХОДОВ СТАТУСОВ ***
        /// </summary>
        private bool CanTransitionTo(StageExecutionStatus currentStatus, StageExecutionStatus newStatus)
        {
            return currentStatus switch
            {
                StageExecutionStatus.Pending => newStatus == StageExecutionStatus.InProgress ||
                                               newStatus == StageExecutionStatus.Waiting ||
                                               newStatus == StageExecutionStatus.Error,

                StageExecutionStatus.Waiting => newStatus == StageExecutionStatus.Pending ||
                                               newStatus == StageExecutionStatus.Error,

                StageExecutionStatus.InProgress => newStatus == StageExecutionStatus.Paused ||
                                                  newStatus == StageExecutionStatus.Completed ||
                                                  newStatus == StageExecutionStatus.Error,

                StageExecutionStatus.Paused => newStatus == StageExecutionStatus.InProgress ||
                                              newStatus == StageExecutionStatus.Completed ||
                                              newStatus == StageExecutionStatus.Error,

                StageExecutionStatus.Completed => false, // Завершенный этап нельзя изменить
                StageExecutionStatus.Error => newStatus == StageExecutionStatus.Pending, // Можно перезапустить
                _ => false
            };
        }

        /// <summary>
        /// *** ИСПРАВЛЕННЫЙ ЗАПУСК ЭТАПА ***
        /// </summary>
        public async Task StartStageExecution(int stageExecutionId, string operatorId = null, string deviceId = null)
        {
            try
            {
                var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
                if (stageExecution == null)
                    throw new Exception("Stage execution not found");

                _logger.LogInformation("Попытка запуска этапа {StageId}, текущий статус: {Status}",
                    stageExecutionId, stageExecution.Status);

                // Валидация перехода статуса
                if (!CanTransitionTo(stageExecution.Status, StageExecutionStatus.InProgress))
                {
                    throw new Exception($"Cannot start stage from status: {stageExecution.Status}");
                }

                // *** КРИТИЧЕСКИ ВАЖНО: Проверка последовательности этапов ***
                if (!stageExecution.IsSetup)
                {
                    var canStart = await CheckAllPreviousStagesCompleted(stageExecution);
                    if (!canStart)
                    {
                        _logger.LogWarning("Нельзя запустить этап {StageId}: предыдущие этапы не завершены", stageExecutionId);
                        throw new Exception("Cannot start stage: previous stages are not completed");
                    }
                }

                // Проверка этапа переналадки
                if (!stageExecution.IsSetup)
                {
                    var setupStage = await _batchRepo.GetSetupStageForMainStageAsync(stageExecutionId);
                    if (setupStage != null && setupStage.Status != StageExecutionStatus.Completed)
                    {
                        _logger.LogWarning("Нельзя запустить этап {StageId}: переналадка не завершена", stageExecutionId);
                        throw new Exception("Cannot start main stage: setup stage is not completed");
                    }
                }

                // Проверка занятости станка
                if (stageExecution.MachineId.HasValue)
                {
                    var currentStageOnMachine = await _batchRepo.GetCurrentStageOnMachineAsync(stageExecution.MachineId.Value);
                    if (currentStageOnMachine != null && currentStageOnMachine.Id != stageExecutionId)
                    {
                        _logger.LogWarning("Станок {MachineId} занят этапом {CurrentStageId}",
                            stageExecution.MachineId.Value, currentStageOnMachine.Id);
                        throw new Exception($"Machine {stageExecution.MachineId.Value} is busy with another stage");
                    }
                }

                var timeInPreviousState = stageExecution.StatusChangedTimeUtc.HasValue
                    ? DateTime.UtcNow - stageExecution.StatusChangedTimeUtc.Value
                    : (TimeSpan?)null;

                // Обновляем статус и время
                var previousStatus = stageExecution.Status;
                stageExecution.Status = StageExecutionStatus.InProgress;
                stageExecution.StartTimeUtc = DateTime.UtcNow;
                stageExecution.StatusChangedTimeUtc = DateTime.UtcNow;
                stageExecution.StartAttempts = (stageExecution.StartAttempts ?? 0) + 1;

                if (!string.IsNullOrEmpty(operatorId))
                    stageExecution.OperatorId = operatorId;

                if (!string.IsNullOrEmpty(deviceId))
                    stageExecution.DeviceId = deviceId;

                // *** ИСПРАВЛЕНИЕ: Используем try-catch для сохранения ***
                try
                {
                    await _batchRepo.UpdateStageExecutionAsync(stageExecution);
                }
                catch (DbUpdateException dbEx)
                {
                    _logger.LogError(dbEx, "Ошибка при сохранении этапа {StageId} в базу данных", stageExecutionId);
                    throw new Exception($"Database error while updating stage: {dbEx.InnerException?.Message ?? dbEx.Message}");
                }

                // Логируем запуск этапа
                await _eventLogService.LogStageStartedAsync(
                    stageExecutionId, operatorId, operatorId, deviceId,
                    timeInPreviousState: timeInPreviousState,
                    isAutomatic: string.IsNullOrEmpty(operatorId));

                _logger.LogInformation("Этап {StageId} успешно запущен", stageExecutionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при запуске этапа {StageId}", stageExecutionId);

                // Попытка записать ошибку в этап (если возможно)
                try
                {
                    var stage = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
                    if (stage != null)
                    {
                        stage.LastErrorMessage = ex.Message;
                        await _batchRepo.UpdateStageExecutionAsync(stage);
                    }
                }
                catch
                {
                    // Игнорируем ошибки при записи ошибки
                }

                throw;
            }
        }

        /// <summary>
        /// *** ИСПРАВЛЕННАЯ ПРИОСТАНОВКА ЭТАПА ***
        /// </summary>
        public async Task PauseStageExecution(int stageExecutionId, string operatorId = null, string reasonNote = null, string deviceId = null)
        {
            try
            {
                var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
                if (stageExecution == null)
                    throw new Exception("Stage execution not found");

                _logger.LogInformation("Попытка приостановки этапа {StageId}, текущий статус: {Status}",
                    stageExecutionId, stageExecution.Status);

                // *** ИСПРАВЛЕНИЕ: Проверяем возможность приостановки ***
                if (stageExecution.Status == StageExecutionStatus.Completed)
                {
                    _logger.LogWarning("Попытка приостановить завершенный этап {StageId}", stageExecutionId);
                    throw new Exception("Cannot pause completed stage");
                }

                if (stageExecution.Status == StageExecutionStatus.Error)
                {
                    _logger.LogWarning("Попытка приостановить отмененный этап {StageId}", stageExecutionId);
                    throw new Exception("Cannot pause cancelled stage");
                }

                // Валидация перехода статуса
                if (!CanTransitionTo(stageExecution.Status, StageExecutionStatus.Paused))
                {
                    throw new Exception($"Cannot pause stage from status: {stageExecution.Status}");
                }

                var timeInProgress = stageExecution.StartTimeUtc.HasValue
                    ? DateTime.UtcNow - stageExecution.StartTimeUtc.Value
                    : (TimeSpan?)null;

                var previousStatus = stageExecution.Status;
                stageExecution.Status = StageExecutionStatus.Paused;
                stageExecution.PauseTimeUtc = DateTime.UtcNow;
                stageExecution.StatusChangedTimeUtc = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(operatorId))
                    stageExecution.OperatorId = operatorId;

                if (!string.IsNullOrEmpty(reasonNote))
                    stageExecution.ReasonNote = reasonNote;

                if (!string.IsNullOrEmpty(deviceId))
                    stageExecution.DeviceId = deviceId;

                // *** ИСПРАВЛЕНИЕ: Обработка ошибок сохранения ***
                try
                {
                    await _batchRepo.UpdateStageExecutionAsync(stageExecution);
                }
                catch (DbUpdateException dbEx)
                {
                    _logger.LogError(dbEx, "Ошибка при сохранении приостановки этапа {StageId}", stageExecutionId);
                    throw new Exception($"Database error while pausing stage: {dbEx.InnerException?.Message ?? dbEx.Message}");
                }

                // Логируем приостановку этапа
                await _eventLogService.LogStagePausedAsync(
                    stageExecutionId, reasonNote ?? "Приостановка оператором",
                    operatorId, operatorId, deviceId, timeInProgress,
                    isAutomatic: string.IsNullOrEmpty(operatorId));

                _logger.LogInformation("Этап {StageId} приостановлен. Причина: {Reason}",
                    stageExecutionId, reasonNote ?? "Не указана");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при приостановке этапа {StageId}", stageExecutionId);
                throw;
            }
        }

        /// <summary>
        /// *** ИСПРАВЛЕННОЕ ВОЗОБНОВЛЕНИЕ ЭТАПА ***
        /// </summary>
        public async Task ResumeStageExecution(int stageExecutionId, string operatorId = null, string reasonNote = null, string deviceId = null)
        {
            try
            {
                var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
                if (stageExecution == null)
                    throw new Exception("Stage execution not found");

                _logger.LogInformation("Попытка возобновления этапа {StageId}, текущий статус: {Status}",
                    stageExecutionId, stageExecution.Status);

                // Валидация перехода статуса
                if (!CanTransitionTo(stageExecution.Status, StageExecutionStatus.InProgress))
                {
                    throw new Exception($"Cannot resume stage from status: {stageExecution.Status}");
                }

                var timeInPause = stageExecution.PauseTimeUtc.HasValue
                    ? DateTime.UtcNow - stageExecution.PauseTimeUtc.Value
                    : (TimeSpan?)null;

                stageExecution.Status = StageExecutionStatus.InProgress;
                stageExecution.ResumeTimeUtc = DateTime.UtcNow;
                stageExecution.StatusChangedTimeUtc = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(operatorId))
                    stageExecution.OperatorId = operatorId;

                if (!string.IsNullOrEmpty(reasonNote))
                    stageExecution.ReasonNote = reasonNote;

                if (!string.IsNullOrEmpty(deviceId))
                    stageExecution.DeviceId = deviceId;

                // *** ИСПРАВЛЕНИЕ: Обработка ошибок сохранения ***
                try
                {
                    await _batchRepo.UpdateStageExecutionAsync(stageExecution);
                }
                catch (DbUpdateException dbEx)
                {
                    _logger.LogError(dbEx, "Ошибка при сохранении возобновления этапа {StageId}", stageExecutionId);
                    throw new Exception($"Database error while resuming stage: {dbEx.InnerException?.Message ?? dbEx.Message}");
                }

                // Логируем возобновление этапа
                await _eventLogService.LogStageResumedAsync(
                    stageExecutionId, operatorId, operatorId, deviceId, reasonNote, timeInPause,
                    isAutomatic: string.IsNullOrEmpty(operatorId));

                _logger.LogInformation("Этап {StageId} возобновлен", stageExecutionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при возобновлении этапа {StageId}", stageExecutionId);
                throw;
            }
        }

        /// <summary>
        /// *** ИСПРАВЛЕННОЕ ЗАВЕРШЕНИЕ ЭТАПА ***
        /// </summary>
        public async Task CompleteStageExecution(int stageExecutionId, string operatorId = null, string reasonNote = null, string deviceId = null)
        {
            try
            {
                var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
                if (stageExecution == null)
                    throw new Exception("Stage execution not found");

                _logger.LogInformation("Попытка завершения этапа {StageId}, текущий статус: {Status}",
                    stageExecutionId, stageExecution.Status);

                // Валидация перехода статуса
                if (!CanTransitionTo(stageExecution.Status, StageExecutionStatus.Completed))
                {
                    throw new Exception($"Cannot complete stage from status: {stageExecution.Status}");
                }

                var timeInProgress = stageExecution.StartTimeUtc.HasValue
                    ? DateTime.UtcNow - stageExecution.StartTimeUtc.Value
                    : (TimeSpan?)null;

                stageExecution.Status = StageExecutionStatus.Completed;
                stageExecution.EndTimeUtc = DateTime.UtcNow;
                stageExecution.StatusChangedTimeUtc = DateTime.UtcNow;
                stageExecution.IsProcessedByScheduler = false; // Сбрасываем флаг для обработки планировщиком

                if (!string.IsNullOrEmpty(operatorId))
                    stageExecution.OperatorId = operatorId;

                if (!string.IsNullOrEmpty(reasonNote))
                    stageExecution.ReasonNote = reasonNote;

                if (!string.IsNullOrEmpty(deviceId))
                    stageExecution.DeviceId = deviceId;

                // *** ИСПРАВЛЕНИЕ: Обработка ошибок сохранения ***
                try
                {
                    await _batchRepo.UpdateStageExecutionAsync(stageExecution);
                }
                catch (DbUpdateException dbEx)
                {
                    _logger.LogError(dbEx, "Ошибка при сохранении завершения этапа {StageId}", stageExecutionId);
                    throw new Exception($"Database error while completing stage: {dbEx.InnerException?.Message ?? dbEx.Message}");
                }

                // Логируем завершение этапа
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

                        _logger.LogInformation("Основной этап {MainStageId} переведен в статус Pending после завершения переналадки",
                            mainStage.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при завершении этапа {StageId}", stageExecutionId);
                throw;
            }
        }

        /// <summary>
        /// *** ИСПРАВЛЕННАЯ ОТМЕНА ЭТАПА ***
        /// </summary>
        public async Task CancelStageExecution(int stageExecutionId, string reason, string operatorId = null, string deviceId = null)
        {
            try
            {
                var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
                if (stageExecution == null)
                    throw new Exception("Stage execution not found");

                _logger.LogInformation("Попытка отмены этапа {StageId}, текущий статус: {Status}",
                    stageExecutionId, stageExecution.Status);

                // Валидация возможности отмены
                if (stageExecution.Status == StageExecutionStatus.Completed)
                {
                    throw new Exception("Cannot cancel completed stage");
                }

                if (stageExecution.Status == StageExecutionStatus.Error)
                {
                    throw new Exception("Stage is already cancelled");
                }

                stageExecution.Status = StageExecutionStatus.Error; // Используем Error как "Cancelled"
                stageExecution.StatusChangedTimeUtc = DateTime.UtcNow;
                stageExecution.ReasonNote = reason;
                stageExecution.IsProcessedByScheduler = true; // Помечаем как обработанный

                if (!string.IsNullOrEmpty(operatorId))
                    stageExecution.OperatorId = operatorId;

                if (!string.IsNullOrEmpty(deviceId))
                    stageExecution.DeviceId = deviceId;

                // *** ИСПРАВЛЕНИЕ: Обработка ошибок сохранения ***
                try
                {
                    await _batchRepo.UpdateStageExecutionAsync(stageExecution);
                }
                catch (DbUpdateException dbEx)
                {
                    _logger.LogError(dbEx, "Ошибка при сохранении отмены этапа {StageId}", stageExecutionId);
                    throw new Exception($"Database error while cancelling stage: {dbEx.InnerException?.Message ?? dbEx.Message}");
                }

                // Логируем отмену этапа
                await _eventLogService.LogStageCancelledAsync(
                    stageExecutionId, reason, operatorId, operatorId, deviceId,
                    isAutomatic: string.IsNullOrEmpty(operatorId));

                _logger.LogInformation("Этап {StageId} отменен. Причина: {Reason}", stageExecutionId, reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отмене этапа {StageId}", stageExecutionId);
                throw;
            }
        }

        /// <summary>
        /// *** КРИТИЧЕСКИ ВАЖНАЯ ПРОВЕРКА ПОСЛЕДОВАТЕЛЬНОСТИ ЭТАПОВ ***
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

                // Получаем все этапы маршрута для этой детали
                var route = await _routeRepo.GetByIdAsync(currentStage.RouteId);
                if (route == null)
                {
                    _logger.LogError("Маршрут не найден для этапа {StageId}", stageExecution.Id);
                    return false;
                }

                // Находим все предыдущие этапы маршрута
                var previousStageOrders = route.Stages
                    .Where(s => s.Order < currentStage.Order)
                    .Select(s => s.Order)
                    .ToList();

                if (!previousStageOrders.Any())
                {
                    _logger.LogInformation("Этап {StageId} является первым в маршруте", stageExecution.Id);
                    return true; // нет предыдущих этапов
                }

                // Проверяем, что для каждого предыдущего этапа маршрута
                // есть завершенное выполнение в подпартии
                foreach (var prevOrder in previousStageOrders)
                {
                    var prevStageExecution = subBatch.StageExecutions
                        .FirstOrDefault(se => !se.IsSetup && se.RouteStage.Order == prevOrder);

                    if (prevStageExecution == null)
                    {
                        _logger.LogWarning("Предыдущий этап порядка {Order} не найден для этапа {StageId}",
                            prevOrder, stageExecution.Id);
                        return false;
                    }

                    if (prevStageExecution.Status != StageExecutionStatus.Completed)
                    {
                        _logger.LogWarning("Предыдущий этап {PrevStageId} (порядок {Order}) не завершен для этапа {StageId}. Статус: {Status}",
                            prevStageExecution.Id, prevOrder, stageExecution.Id, prevStageExecution.Status);
                        return false;
                    }
                }

                _logger.LogInformation("Все предыдущие этапы завершены для этапа {StageId}", stageExecution.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке предыдущих этапов для {StageId}", stageExecution.Id);
                return false;
            }
        }

        /// <summary>
        /// Проверка необходимости переналадки
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

        // Остальные методы (GetAllStagesForGanttChart, CalculatePlannedDuration) остаются без изменений
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