using Microsoft.Extensions.Logging;
using Project.Contracts.ModelDTO;
using Project.Domain.Entities;
using Project.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class StageExecutionService
    {
        private readonly IRouteRepository _routeRepo;
        private readonly IMachineRepository _machineRepo;
        private readonly IBatchRepository _batchRepo;
        private readonly ISetupTimeRepository _setupTimeRepo;
        private readonly IDetailRepository _detailRepo;
        private readonly EventLogService _eventLogService; // *** ИСПРАВЛЕНИЕ: Добавлен EventLogService ***
        private readonly ILogger<StageExecutionService> _logger;

        public StageExecutionService(
            IRouteRepository routeRepo,
            IMachineRepository machineRepo,
            IBatchRepository batchRepo,
            ISetupTimeRepository setupTimeRepo,
            IDetailRepository detailRepo,
            EventLogService eventLogService, // *** ИСПРАВЛЕНИЕ: Добавлен параметр ***
            ILogger<StageExecutionService> logger)
        {
            _routeRepo = routeRepo;
            _machineRepo = machineRepo;
            _batchRepo = batchRepo;
            _setupTimeRepo = setupTimeRepo;
            _detailRepo = detailRepo;
            _eventLogService = eventLogService; // *** ИСПРАВЛЕНИЕ: Внедрение сервиса ***
            _logger = logger;
        }

        // Генерация этапов маршрута для подпартии на основе маршрута детали
        public async Task GenerateStageExecutionsForSubBatch(int subBatchId)
        {
            var subBatch = await _batchRepo.GetSubBatchByIdAsync(subBatchId);
            if (subBatch == null) throw new Exception("SubBatch not found");

            var batch = subBatch.Batch;
            var detail = batch.Detail;

            // Получаем маршрут для детали
            var route = await _routeRepo.GetByDetailIdAsync(detail.Id);
            if (route == null) throw new Exception($"Route not found for Detail {detail.Name}");

            // Создаем этапы выполнения в той же последовательности, что и в маршруте
            foreach (var stage in route.Stages.OrderBy(s => s.Order))
            {
                var stageExecution = new StageExecution
                {
                    SubBatchId = subBatchId,
                    RouteStageId = stage.Id,
                    Status = StageExecutionStatus.Pending, // ожидание
                    IsSetup = false, // это основной этап, не переналадка
                    StatusChangedTimeUtc = DateTime.UtcNow // *** ИСПРАВЛЕНИЕ: Добавляем время изменения статуса ***
                };

                subBatch.StageExecutions.Add(stageExecution);
            }

            await _batchRepo.UpdateSubBatchAsync(subBatch);

            // *** ИСПРАВЛЕНИЕ: Логируем создание этапов ***
            foreach (var stageExecution in subBatch.StageExecutions.Where(se => !se.IsSetup))
            {
                await _eventLogService.LogStageCreatedAsync(
                    stageExecution.Id,
                    isAutomatic: true);
            }
        }

        // Назначение этапа на конкретный станок
        public async Task AssignStageToMachine(int stageExecutionId, int machineId)
        {
            var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
            if (stageExecution == null) throw new Exception("Stage execution not found");

            var machine = await _machineRepo.GetByIdAsync(machineId);
            if (machine == null) throw new Exception("Machine not found");

            var routeStage = stageExecution.RouteStage;

            // *** ИСПРАВЛЕНИЕ: Валидация статуса перед назначением ***
            if (stageExecution.Status != StageExecutionStatus.Pending &&
                stageExecution.Status != StageExecutionStatus.Waiting)
            {
                throw new Exception($"Cannot assign machine to stage in status: {stageExecution.Status}");
            }

            // Проверка соответствия типа станка и типа в маршруте
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

            // *** ИСПРАВЛЕНИЕ: Логируем назначение на станок ***
            await _eventLogService.LogStageAssignedAsync(
                stageExecution.Id,
                machineId,
                isAutomatic: true);

            // *** ИСПРАВЛЕНИЕ: Логируем переназначение, если был другой станок ***
            if (previousMachineId.HasValue && previousMachineId.Value != machineId)
            {
                await _eventLogService.LogStageReassignedAsync(
                    stageExecution.Id,
                    previousMachineId.Value,
                    machineId,
                    "Automatic reassignment",
                    isAutomatic: true);
            }
        }

        // Проверка необходимости переналадки при смене детали на станке
        private async Task<StageExecution?> CheckAndCreateSetupStageIfNeeded(StageExecution stageExecution, Machine machine)
        {
            // Текущая деталь, которую будем обрабатывать
            var currentDetailId = stageExecution.SubBatch.Batch.DetailId;

            // Получаем последнюю деталь, которая была на этом станке
            var lastStageOnMachine = await _batchRepo.GetLastCompletedStageOnMachineAsync(machine.Id);

            // Если на станке не было операций или там была та же деталь - переналадка не нужна
            if (lastStageOnMachine == null || lastStageOnMachine.SubBatch.Batch.DetailId == currentDetailId)
                return null;

            var previousDetailId = lastStageOnMachine.SubBatch.Batch.DetailId;

            // Ищем время переналадки в справочнике
            var setupTime = await _setupTimeRepo.GetSetupTimeAsync(
                machineId: machine.Id,
                fromDetailId: previousDetailId,
                toDetailId: currentDetailId);

            if (setupTime == null)
            {
                // Если нет конкретного времени, берем стандартное время из этапа маршрута
                var setupDuration = stageExecution.RouteStage.SetupTime;

                // Создаем запись в справочнике для будущего использования
                setupTime = new SetupTime
                {
                    MachineId = machine.Id,
                    FromDetailId = previousDetailId,
                    ToDetailId = currentDetailId,
                    Time = setupDuration
                };

                await _setupTimeRepo.AddAsync(setupTime);
            }

            // Создаем этап переналадки перед основным этапом
            var setupStage = new StageExecution
            {
                SubBatchId = stageExecution.SubBatchId,
                RouteStageId = stageExecution.RouteStageId, // используем тот же этап маршрута
                MachineId = machine.Id,
                Status = StageExecutionStatus.Pending,
                IsSetup = true, // это переналадка
                StatusChangedTimeUtc = DateTime.UtcNow
            };

            // Добавляем этап переналадки в подпартию
            var subBatch = stageExecution.SubBatch;
            subBatch.StageExecutions.Add(setupStage);
            await _batchRepo.UpdateSubBatchAsync(subBatch);

            // *** ИСПРАВЛЕНИЕ: Логируем создание этапа переналадки ***
            await _eventLogService.LogStageCreatedAsync(
                setupStage.Id,
                isAutomatic: true);

            return setupStage;
        }

        // *** ИСПРАВЛЕНИЕ: Валидация переходов статусов ***
        private bool CanTransitionTo(StageExecutionStatus currentStatus, StageExecutionStatus newStatus)
        {
            return currentStatus switch
            {
                StageExecutionStatus.Pending => newStatus == StageExecutionStatus.InProgress ||
                                               newStatus == StageExecutionStatus.Waiting,
                StageExecutionStatus.Waiting => newStatus == StageExecutionStatus.Pending ||
                                               newStatus == StageExecutionStatus.Error,
                StageExecutionStatus.InProgress => newStatus == StageExecutionStatus.Paused ||
                                                  newStatus == StageExecutionStatus.Completed ||
                                                  newStatus == StageExecutionStatus.Error,
                StageExecutionStatus.Paused => newStatus == StageExecutionStatus.InProgress ||
                                              newStatus == StageExecutionStatus.Error,
                StageExecutionStatus.Completed => false, // Завершенный этап нельзя изменить
                StageExecutionStatus.Error => newStatus == StageExecutionStatus.Pending, // Можно перезапустить
                _ => false
            };
        }

        // Запуск этапа в работу
        public async Task StartStageExecution(int stageExecutionId, string operatorId = null, string deviceId = null)
        {
            var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
            if (stageExecution == null) throw new Exception("Stage execution not found");

            try
            {
                // *** ИСПРАВЛЕНИЕ: Валидация перехода статуса ***
                if (!CanTransitionTo(stageExecution.Status, StageExecutionStatus.InProgress))
                {
                    throw new Exception($"Cannot start stage from status: {stageExecution.Status}");
                }

                var timeInPreviousState = stageExecution.StatusChangedTimeUtc.HasValue
                    ? DateTime.UtcNow - stageExecution.StatusChangedTimeUtc.Value
                    : (TimeSpan?)null;

                // *** ИСПРАВЛЕНИЕ: Проверка предыдущих этапов ***
                if (!stageExecution.IsSetup)
                {
                    var allPreviousCompleted = await CheckAllPreviousStagesCompleted(stageExecution);
                    if (!allPreviousCompleted)
                        throw new Exception("Cannot start stage: previous stages are not completed");
                }

                // *** ИСПРАВЛЕНИЕ: Проверка этапа переналадки ***
                if (!stageExecution.IsSetup)
                {
                    var setupStage = await _batchRepo.GetSetupStageForMainStageAsync(stageExecutionId);
                    if (setupStage != null && setupStage.Status != StageExecutionStatus.Completed)
                        throw new Exception("Cannot start main stage: setup stage is not completed");
                }

                // Обновляем статус и время
                var previousStatus = stageExecution.Status;
                stageExecution.Status = StageExecutionStatus.InProgress;
                stageExecution.StartTimeUtc = DateTime.UtcNow;
                stageExecution.StatusChangedTimeUtc = DateTime.UtcNow;
                stageExecution.StartAttempts++;

                // Сохраняем идентификатор оператора и устройства, если предоставлены
                if (!string.IsNullOrEmpty(operatorId))
                    stageExecution.OperatorId = operatorId;

                if (!string.IsNullOrEmpty(deviceId))
                    stageExecution.DeviceId = deviceId;

                await _batchRepo.UpdateStageExecutionAsync(stageExecution);

                // *** ИСПРАВЛЕНИЕ: Логируем запуск этапа ***
                await _eventLogService.LogStageStartedAsync(
                    stageExecutionId,
                    operatorId,
                    operatorId, // operatorName - можно улучшить, получая из справочника
                    deviceId,
                    timeInPreviousState: timeInPreviousState,
                    isAutomatic: string.IsNullOrEmpty(operatorId));

                _logger.LogInformation("Этап {StageId} успешно запущен", stageExecutionId);
            }
            catch (Exception ex)
            {
                // Сохраняем информацию об ошибке
                stageExecution.LastErrorMessage = ex.Message;
                await _batchRepo.UpdateStageExecutionAsync(stageExecution);

                _logger.LogError(ex, "Ошибка при запуске этапа {StageId}", stageExecutionId);
                throw;
            }
        }

        // Приостановка этапа
        public async Task PauseStageExecution(int stageExecutionId, string operatorId = null, string reasonNote = null, string deviceId = null)
        {
            var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
            if (stageExecution == null) throw new Exception("Stage execution not found");

            try
            {
                // *** ИСПРАВЛЕНИЕ: Валидация перехода статуса ***
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

                // Сохраняем идентификатор оператора и примечание, если предоставлены
                if (!string.IsNullOrEmpty(operatorId))
                    stageExecution.OperatorId = operatorId;

                if (!string.IsNullOrEmpty(reasonNote))
                    stageExecution.ReasonNote = reasonNote;

                if (!string.IsNullOrEmpty(deviceId))
                    stageExecution.DeviceId = deviceId;

                await _batchRepo.UpdateStageExecutionAsync(stageExecution);

                // *** ИСПРАВЛЕНИЕ: Логируем приостановку этапа ***
                await _eventLogService.LogStagePausedAsync(
                    stageExecutionId,
                    reasonNote ?? "Приостановка оператором",
                    operatorId,
                    operatorId, // operatorName
                    deviceId,
                    timeInProgress,
                    isAutomatic: string.IsNullOrEmpty(operatorId));

                _logger.LogInformation("Этап {StageId} приостановлен. Причина: {Reason}",
                    stageExecutionId, reasonNote ?? "Не указана");
            }
            catch (Exception ex)
            {
                // Сохраняем информацию об ошибке
                stageExecution.LastErrorMessage = ex.Message;
                await _batchRepo.UpdateStageExecutionAsync(stageExecution);

                _logger.LogError(ex, "Ошибка при приостановке этапа {StageId}", stageExecutionId);
                throw;
            }
        }

        // Возобновление приостановленного этапа
        public async Task ResumeStageExecution(int stageExecutionId, string operatorId = null, string reasonNote = null, string deviceId = null)
        {
            var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
            if (stageExecution == null) throw new Exception("Stage execution not found");

            try
            {
                // *** ИСПРАВЛЕНИЕ: Валидация перехода статуса ***
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

                // Сохраняем идентификатор оператора и примечание, если предоставлены
                if (!string.IsNullOrEmpty(operatorId))
                    stageExecution.OperatorId = operatorId;

                if (!string.IsNullOrEmpty(reasonNote))
                    stageExecution.ReasonNote = reasonNote;

                if (!string.IsNullOrEmpty(deviceId))
                    stageExecution.DeviceId = deviceId;

                await _batchRepo.UpdateStageExecutionAsync(stageExecution);

                // *** ИСПРАВЛЕНИЕ: Логируем возобновление этапа ***
                await _eventLogService.LogStageResumedAsync(
                    stageExecutionId,
                    operatorId,
                    operatorId, // operatorName
                    deviceId,
                    reasonNote,
                    timeInPause,
                    isAutomatic: string.IsNullOrEmpty(operatorId));

                _logger.LogInformation("Этап {StageId} возобновлен", stageExecutionId);
            }
            catch (Exception ex)
            {
                // Сохраняем информацию об ошибке
                stageExecution.LastErrorMessage = ex.Message;
                await _batchRepo.UpdateStageExecutionAsync(stageExecution);

                _logger.LogError(ex, "Ошибка при возобновлении этапа {StageId}", stageExecutionId);
                throw;
            }
        }

        // Завершение этапа
        public async Task CompleteStageExecution(int stageExecutionId, string operatorId = null, string reasonNote = null, string deviceId = null)
        {
            var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
            if (stageExecution == null) throw new Exception("Stage execution not found");

            try
            {
                // *** ИСПРАВЛЕНИЕ: Валидация перехода статуса ***
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
                stageExecution.IsProcessedByScheduler = false; // Сбрасываем флаг, чтобы планировщик обработал завершение

                // Сохраняем идентификатор оператора и примечание, если предоставлены
                if (!string.IsNullOrEmpty(operatorId))
                    stageExecution.OperatorId = operatorId;

                if (!string.IsNullOrEmpty(reasonNote))
                    stageExecution.ReasonNote = reasonNote;

                if (!string.IsNullOrEmpty(deviceId))
                    stageExecution.DeviceId = deviceId;

                await _batchRepo.UpdateStageExecutionAsync(stageExecution);

                // *** ИСПРАВЛЕНИЕ: Логируем завершение этапа ***
                await _eventLogService.LogStageCompletedAsync(
                    stageExecutionId,
                    operatorId,
                    operatorId, // operatorName
                    deviceId,
                    reasonNote,
                    timeInProgress,
                    isAutomatic: string.IsNullOrEmpty(operatorId));

                _logger.LogInformation("Этап {StageId} завершен", stageExecutionId);

                // *** ИСПРАВЛЕНИЕ: После завершения переналадки делаем доступным основной этап ***
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
                // Сохраняем информацию об ошибке
                stageExecution.LastErrorMessage = ex.Message;
                await _batchRepo.UpdateStageExecutionAsync(stageExecution);

                _logger.LogError(ex, "Ошибка при завершении этапа {StageId}", stageExecutionId);
                throw;
            }
        }

        public async Task CancelStageExecution(int stageExecutionId, string reason, string operatorId = null, string deviceId = null)
        {
            var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
            if (stageExecution == null) throw new Exception("Stage execution not found");

            try
            {
                // *** ИСПРАВЛЕНИЕ: Валидация возможности отмены ***
                if (stageExecution.Status != StageExecutionStatus.Pending &&
                    stageExecution.Status != StageExecutionStatus.Waiting)
                {
                    throw new Exception($"Cannot cancel stage in status: {stageExecution.Status}");
                }

                // Создаем новый статус для отмененных этапов
                stageExecution.Status = StageExecutionStatus.Error; // Используем Error как "Cancelled"
                stageExecution.StatusChangedTimeUtc = DateTime.UtcNow;
                stageExecution.ReasonNote = reason;
                stageExecution.IsProcessedByScheduler = true; // Помечаем как обработанный, чтобы планировщик его игнорировал

                // Сохраняем идентификатор оператора, если предоставлен
                if (!string.IsNullOrEmpty(operatorId))
                    stageExecution.OperatorId = operatorId;

                if (!string.IsNullOrEmpty(deviceId))
                    stageExecution.DeviceId = deviceId;

                await _batchRepo.UpdateStageExecutionAsync(stageExecution);

                // *** ИСПРАВЛЕНИЕ: Логируем отмену этапа ***
                await _eventLogService.LogStageCancelledAsync(
                    stageExecutionId,
                    reason,
                    operatorId,
                    operatorId, // operatorName
                    deviceId,
                    isAutomatic: string.IsNullOrEmpty(operatorId));

                _logger.LogInformation("Этап {StageId} отменен. Причина: {Reason}", stageExecutionId, reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отмене этапа {StageId}", stageExecutionId);
                throw;
            }
        }

        // Проверка, что все предыдущие этапы завершены
        private async Task<bool> CheckAllPreviousStagesCompleted(StageExecution stageExecution)
        {
            var subBatch = stageExecution.SubBatch;

            // Находим номер текущего этапа в маршруте
            var currentStage = stageExecution.RouteStage;

            // Получаем все этапы маршрута для этой детали
            var route = await _routeRepo.GetByIdAsync(currentStage.RouteId);
            if (route == null) return false;

            // Находим все предыдущие этапы маршрута
            var previousStages = route.Stages
                .Where(s => s.Order < currentStage.Order)
                .OrderBy(s => s.Order)
                .ToList();

            if (!previousStages.Any()) return true; // нет предыдущих этапов

            // Проверяем, что для каждого предыдущего этапа маршрута 
            // есть завершенное выполнение в подпартии
            foreach (var prevStage in previousStages)
            {
                var prevExecution = subBatch.StageExecutions
                    .FirstOrDefault(se => se.RouteStageId == prevStage.Id && !se.IsSetup);

                if (prevExecution == null || prevExecution.Status != StageExecutionStatus.Completed)
                    return false;
            }

            return true;
        }

        // Получение всех этапов для диаграммы Ганта
        public async Task<List<GanttStageDto>> GetAllStagesForGanttChart(DateTime? startDate = null, DateTime? endDate = null)
        {
            var allStages = await _batchRepo.GetAllStageExecutionsAsync();

            // Фильтрация по датам, если указаны
            if (startDate.HasValue)
                allStages = allStages.Where(s => !s.EndTimeUtc.HasValue || s.EndTimeUtc >= startDate).ToList();

            if (endDate.HasValue)
                allStages = allStages.Where(s => !s.StartTimeUtc.HasValue || s.StartTimeUtc <= endDate).ToList();

            // Преобразуем в DTO для Ганта
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
                    StageName = stage.IsSetup ? $"Переналадка на {detail.Name}" : routeStage.Name,
                    MachineId = stage.MachineId,
                    MachineName = stage.Machine?.Name,
                    StartTime = stage.StartTimeUtc,
                    EndTime = stage.EndTimeUtc,
                    Status = stage.Status.ToString(),
                    IsSetup = stage.IsSetup,
                    // Расчет плановой длительности
                    PlannedDuration = CalculatePlannedDuration(stage, subBatch.Quantity),
                    // Дополнительные поля
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

        // Расчет плановой длительности этапа
        private TimeSpan CalculatePlannedDuration(StageExecution stage, int quantity)
        {
            if (stage.IsSetup)
            {
                // Для переналадки берем время из этапа маршрута или из справочника
                var setupTime = stage.RouteStage.SetupTime;
                return TimeSpan.FromHours(setupTime);
            }
            else
            {
                // Для основной операции - норма времени * количество деталей
                var normTime = stage.RouteStage.NormTime;
                return TimeSpan.FromHours(normTime * quantity);
            }
        }
    }
}