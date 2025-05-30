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
    /// <summary>
    /// Сервис управления выполнением этапов согласно ТЗ
    /// </summary>
    public class StageExecutionService
    {
        private readonly IRouteRepository _routeRepo;
        private readonly IMachineRepository _machineRepo;
        private readonly IBatchRepository _batchRepo;
        private readonly ISetupTimeRepository _setupTimeRepo;
        private readonly IDetailRepository _detailRepo;
        private readonly ILogger<StageExecutionService> _logger;

        private readonly object _lockObject = new object();

        // Рабочее время согласно ТЗ
        private readonly TimeSpan _workStart = new TimeSpan(8, 0, 0);
        private readonly TimeSpan _workEnd = new TimeSpan(1, 30, 0);
        private readonly TimeSpan _lunchStart = new TimeSpan(12, 0, 0);
        private readonly TimeSpan _lunchEnd = new TimeSpan(13, 0, 0);
        private readonly TimeSpan _dinnerStart = new TimeSpan(21, 0, 0);
        private readonly TimeSpan _dinnerEnd = new TimeSpan(21, 30, 0);

        public StageExecutionService(
            IRouteRepository routeRepo,
            IMachineRepository machineRepo,
            IBatchRepository batchRepo,
            ISetupTimeRepository setupTimeRepo,
            IDetailRepository detailRepo,
            ILogger<StageExecutionService> logger)
        {
            _routeRepo = routeRepo;
            _machineRepo = machineRepo;
            _batchRepo = batchRepo;
            _setupTimeRepo = setupTimeRepo;
            _detailRepo = detailRepo;
            _logger = logger;
        }

        #region Генерация и назначение этапов

        /// <summary>
        /// Генерация этапов маршрута для подпартии
        /// </summary>
        public async Task GenerateStageExecutionsForSubBatch(int subBatchId)
        {
            try
            {
                var subBatch = await _batchRepo.GetSubBatchByIdAsync(subBatchId);
                if (subBatch == null)
                    throw new Exception($"Подпартия {subBatchId} не найдена");

                var batch = subBatch.Batch;
                var detail = batch.Detail;

                var route = await _routeRepo.GetByDetailIdAsync(detail.Id);
                if (route == null)
                    throw new Exception($"Маршрут для детали {detail.Name} не найден");

                var currentTime = DateTime.UtcNow;

                _logger.LogInformation("Генерация этапов для подпартии {SubBatchId}, деталь: {DetailName}, количество: {Quantity}",
                    subBatchId, detail.Name, subBatch.Quantity);

                // Создаем этапы выполнения в последовательности маршрута
                foreach (var stage in route.Stages.OrderBy(s => s.Order))
                {
                    var stageExecution = new StageExecution
                    {
                        SubBatchId = subBatchId,
                        RouteStageId = stage.Id,
                        Status = StageExecutionStatus.Pending,
                        IsSetup = false,
                        StatusChangedTimeUtc = currentTime,
                        CreatedUtc = currentTime,
                        Priority = 0,
                        IsCritical = false,
                        IsProcessedByScheduler = false,
                        PlannedStartTimeUtc = null // Будет установлено при планировании
                    };

                    subBatch.StageExecutions.Add(stageExecution);
                }

                await _batchRepo.UpdateSubBatchAsync(subBatch);

                _logger.LogInformation("Создано {Count} этапов для подпартии {SubBatchId}",
                    subBatch.StageExecutions.Count, subBatchId);
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
                if (stageExecution == null)
                    throw new Exception($"Этап выполнения {stageExecutionId} не найден");

                var machine = await _machineRepo.GetByIdAsync(machineId);
                if (machine == null)
                    throw new Exception($"Станок {machineId} не найден");

                var routeStage = stageExecution.RouteStage;

                _logger.LogInformation("Назначение этапа {StageId} ({StageName}) на станок {MachineName} ({MachineId})",
                    stageExecutionId, routeStage.Name, machine.Name, machineId);

                // Валидация статуса перед назначением
                if (!CanAssignStageToMachine(stageExecution))
                {
                    throw new Exception($"Нельзя назначить этап в статусе {stageExecution.Status} на станок");
                }

                // Проверка совместимости типа станка
                if (machine.MachineTypeId != routeStage.MachineTypeId)
                {
                    throw new Exception($"Тип станка не соответствует требованиям этапа. " +
                        $"Требуется: {routeStage.MachineType?.Name}, предоставлен: {machine.MachineType?.Name}");
                }

                // Проверяем, свободен ли станок
                var currentStageOnMachine = await _batchRepo.GetCurrentStageOnMachineAsync(machineId);
                if (currentStageOnMachine != null && currentStageOnMachine.Id != stageExecutionId)
                {
                    throw new Exception($"Станок {machine.Name} уже занят этапом {currentStageOnMachine.Id}");
                }

                // Проверяем, нужна ли переналадка и создаем её при необходимости
                var setupStage = await CheckAndCreateSetupStageIfNeeded(stageExecution, machine);

                var previousMachineId = stageExecution.MachineId;

                // Назначаем основной этап на станок
                stageExecution.MachineId = machineId;
                stageExecution.Status = setupStage != null ?
                    StageExecutionStatus.Waiting : // Ждем завершения переналадки
                    StageExecutionStatus.Pending;  // Готов к запуску
                stageExecution.StatusChangedTimeUtc = DateTime.UtcNow;
                stageExecution.UpdateLastModified();

                // Если есть переналадка, связываем этапы
                if (setupStage != null)
                {
                    stageExecution.SetupStageId = setupStage.Id;
                    setupStage.MainStageId = stageExecution.Id;
                }

                await _batchRepo.UpdateStageExecutionAsync(stageExecution);

                _logger.LogInformation("Этап {StageId} назначен на станок {MachineId}. Переналадка: {SetupRequired}",
                    stageExecutionId, machineId, setupStage != null);

                // Логируем событие переназначения, если станок изменился
                if (previousMachineId.HasValue && previousMachineId != machineId)
                {
                    _logger.LogInformation("Этап {StageId} переназначен со станка {OldMachineId} на станок {NewMachineId}",
                        stageExecutionId, previousMachineId, machineId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при назначении этапа {StageId} на станок {MachineId}",
                    stageExecutionId, machineId);
                throw;
            }
        }

        /// <summary>
        /// Проверка возможности назначения этапа на станок
        /// </summary>
        private bool CanAssignStageToMachine(StageExecution stage)
        {
            var allowedStatuses = new[]
            {
                StageExecutionStatus.Pending,
                StageExecutionStatus.Waiting,
                StageExecutionStatus.Paused
            };

            return allowedStatuses.Contains(stage.Status);
        }

        #endregion

        #region Управление выполнением этапов

        /// <summary>
        /// ЗАПУСК ЭТАПА с полной валидацией согласно ТЗ (ИСПРАВЛЕННАЯ ВЕРСИЯ)
        /// </summary>
        public async Task StartStageExecution(int stageExecutionId, string operatorId = null, string deviceId = null)
        {
            StageExecution stageExecution = null;

            try
            {
                // Получаем этап с проверкой существования
                stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
                if (stageExecution == null)
                    throw new Exception($"Этап выполнения {stageExecutionId} не найден");

                _logger.LogInformation("Запуск этапа {StageId} ({StageName}), текущий статус: {Status}",
                    stageExecutionId, stageExecution.RouteStage?.Name, stageExecution.Status);

                // Валидация возможности запуска
                ValidateStageStart(stageExecution);

                // Проверки вне рабочего времени (согласно ТЗ)
                if (!IsWorkingTime(DateTime.Now) && !CanRunOutsideWorkingHours(stageExecution))
                {
                    throw new Exception("Нельзя запустить этап вне рабочего времени");
                }

                // Проверка зависимостей (последовательность этапов) - только для основных этапов
                if (!stageExecution.IsSetup)
                {
                    var canStart = await CheckAllPreviousStagesCompleted(stageExecution);
                    if (!canStart)
                    {
                        throw new Exception("Не все предыдущие этапы завершены");
                    }
                }

                // Проверка завершения переналадки для основных этапов
                if (!stageExecution.IsSetup && stageExecution.SetupStageId.HasValue)
                {
                    var setupStage = await _batchRepo.GetStageExecutionByIdAsync(stageExecution.SetupStageId.Value);
                    if (setupStage != null && setupStage.Status != StageExecutionStatus.Completed)
                    {
                        throw new Exception("Переналадка не завершена");
                    }
                }

                // Проверка занятости станка
                if (stageExecution.MachineId.HasValue)
                {
                    var currentStageOnMachine = await _batchRepo.GetCurrentStageOnMachineAsync(stageExecution.MachineId.Value);
                    if (currentStageOnMachine != null && currentStageOnMachine.Id != stageExecutionId)
                    {
                        throw new Exception($"Станок занят другим этапом: {currentStageOnMachine.Id}");
                    }
                }

                // Обновление этапа
                var previousStatus = stageExecution.Status;

                stageExecution.Status = StageExecutionStatus.InProgress;
                stageExecution.StartTimeUtc = DateTime.UtcNow;
                stageExecution.StatusChangedTimeUtc = DateTime.UtcNow;
                stageExecution.StartAttempts = (stageExecution.StartAttempts ?? 0) + 1;
                stageExecution.OperatorId = operatorId ?? "SYSTEM";
                stageExecution.DeviceId = deviceId ?? "AUTO";
                stageExecution.LastErrorMessage = null;
                stageExecution.ReasonNote = "Запущен в работу";

                // Очищаем время паузы и возобновления при новом запуске
                if (previousStatus != StageExecutionStatus.Paused)
                {
                    stageExecution.PauseTimeUtc = null;
                    stageExecution.ResumeTimeUtc = null;
                }

                stageExecution.UpdateLastModified();

                await _batchRepo.UpdateStageExecutionAsync(stageExecution);

                _logger.LogInformation("Этап {StageId} успешно запущен. Оператор: {OperatorId}, Устройство: {DeviceId}",
                    stageExecutionId, operatorId ?? "SYSTEM", deviceId ?? "AUTO");

                // Дополнительное логирование для переналадок
                if (stageExecution.IsSetup)
                {
                    _logger.LogInformation("Запущена переналадка для этапа {MainStageId} на станке {MachineId}",
                        stageExecution.MainStageId, stageExecution.MachineId);
                }
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
                        stageExecution.UpdateLastModified();
                        await _batchRepo.UpdateStageExecutionAsync(stageExecution);
                    }
                    catch (Exception updateEx)
                    {
                        _logger.LogError(updateEx, "Ошибка при обновлении информации об ошибке для этапа {StageId}", stageExecutionId);
                    }
                }

                throw;
            }
        }

        /// <summary>
        /// Валидация возможности запуска этапа
        /// </summary>
        private void ValidateStageStart(StageExecution stageExecution)
        {
            // Проверка статуса
            if (!stageExecution.CanTransitionTo(StageExecutionStatus.InProgress))
            {
                throw new Exception($"Нельзя запустить этап из статуса: {stageExecution.Status}");
            }

            // Проверка назначения на станок
            if (!stageExecution.MachineId.HasValue)
            {
                throw new Exception("Этап не назначен на станок");
            }

            // Проверка существования маршрута
            if (stageExecution.RouteStage == null)
            {
                throw new Exception("Информация о маршруте этапа не найдена");
            }
        }

        /// <summary>
        /// ПРИОСТАНОВКА ЭТАПА согласно ТЗ
        /// </summary>
        public async Task PauseStageExecution(int stageExecutionId, string operatorId = null, string reasonNote = null, string deviceId = null)
        {
            try
            {
                var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
                if (stageExecution == null)
                    throw new Exception($"Этап выполнения {stageExecutionId} не найден");

                _logger.LogInformation("Приостановка этапа {StageId} ({StageName}), текущий статус: {Status}",
                    stageExecutionId, stageExecution.RouteStage?.Name, stageExecution.Status);

                // Валидация статуса
                if (!stageExecution.CanTransitionTo(StageExecutionStatus.Paused))
                {
                    throw new Exception($"Нельзя приостановить этап из статуса: {stageExecution.Status}");
                }

                // Расчет времени в работе
                var timeInProgress = stageExecution.ActualWorkingTime;

                // Обновление этапа
                stageExecution.Status = StageExecutionStatus.Paused;
                stageExecution.PauseTimeUtc = DateTime.UtcNow;
                stageExecution.StatusChangedTimeUtc = DateTime.UtcNow;
                stageExecution.OperatorId = operatorId ?? "SYSTEM";
                stageExecution.ReasonNote = reasonNote ?? "Приостановлен пользователем";
                stageExecution.DeviceId = deviceId ?? "AUTO";
                stageExecution.UpdateLastModified();

                await _batchRepo.UpdateStageExecutionAsync(stageExecution);

                _logger.LogInformation("Этап {StageId} приостановлен. Время в работе: {WorkingTime}, Причина: {Reason}",
                    stageExecutionId, timeInProgress?.ToString(@"hh\:mm\:ss") ?? "неизвестно", reasonNote ?? "не указана");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при приостановке этапа {StageId}: {Error}", stageExecutionId, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// ВОЗОБНОВЛЕНИЕ ЭТАПА согласно ТЗ
        /// </summary>
        public async Task ResumeStageExecution(int stageExecutionId, string operatorId = null, string reasonNote = null, string deviceId = null)
        {
            try
            {
                var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
                if (stageExecution == null)
                    throw new Exception($"Этап выполнения {stageExecutionId} не найден");

                _logger.LogInformation("Возобновление этапа {StageId} ({StageName}), текущий статус: {Status}",
                    stageExecutionId, stageExecution.RouteStage?.Name, stageExecution.Status);

                // Валидация статуса
                if (!stageExecution.CanTransitionTo(StageExecutionStatus.InProgress))
                {
                    throw new Exception($"Нельзя возобновить этап из статуса: {stageExecution.Status}");
                }

                // Проверки рабочего времени (согласно ТЗ)
                if (!IsWorkingTime(DateTime.Now) && !CanRunOutsideWorkingHours(stageExecution))
                {
                    throw new Exception("Нельзя возобновить этап вне рабочего времени");
                }

                // Проверка занятости станка
                if (stageExecution.MachineId.HasValue)
                {
                    var currentStageOnMachine = await _batchRepo.GetCurrentStageOnMachineAsync(stageExecution.MachineId.Value);
                    if (currentStageOnMachine != null && currentStageOnMachine.Id != stageExecutionId)
                    {
                        throw new Exception($"Станок занят другим этапом: {currentStageOnMachine.Id}");
                    }
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
                stageExecution.UpdateLastModified();

                await _batchRepo.UpdateStageExecutionAsync(stageExecution);

                _logger.LogInformation("Этап {StageId} возобновлен. Время паузы: {PauseTime}, Оператор: {OperatorId}",
                    stageExecutionId, timeInPause?.ToString(@"hh\:mm\:ss") ?? "неизвестно", operatorId ?? "SYSTEM");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при возобновлении этапа {StageId}: {Error}", stageExecutionId, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// ЗАВЕРШЕНИЕ ЭТАПА согласно ТЗ
        /// </summary>
        public async Task CompleteStageExecution(int stageExecutionId, string operatorId = null, string reasonNote = null, string deviceId = null)
        {
            try
            {
                var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
                if (stageExecution == null)
                    throw new Exception($"Этап выполнения {stageExecutionId} не найден");

                _logger.LogInformation("Завершение этапа {StageId} ({StageName}), текущий статус: {Status}",
                    stageExecutionId, stageExecution.RouteStage?.Name, stageExecution.Status);

                // Валидация статуса
                if (!stageExecution.CanTransitionTo(StageExecutionStatus.Completed))
                {
                    throw new Exception($"Нельзя завершить этап из статуса: {stageExecution.Status}");
                }

                var timeInProgress = stageExecution.ActualWorkingTime;
                var plannedDuration = stageExecution.PlannedDuration;
                var deviation = stageExecution.TimeDeviation;

                stageExecution.Status = StageExecutionStatus.Completed;
                stageExecution.EndTimeUtc = DateTime.UtcNow;
                stageExecution.StatusChangedTimeUtc = DateTime.UtcNow;
                stageExecution.IsProcessedByScheduler = false; // Для обработки планировщиком
                stageExecution.OperatorId = operatorId ?? "SYSTEM";
                stageExecution.ReasonNote = reasonNote ?? "Завершен";
                stageExecution.DeviceId = deviceId ?? "AUTO";
                stageExecution.CompletionPercentage = 100;
                stageExecution.UpdateLastModified();

                await _batchRepo.UpdateStageExecutionAsync(stageExecution);

                _logger.LogInformation("Этап {StageId} завершен. Время выполнения: {ActualTime}, Плановое время: {PlannedTime}, Отклонение: {Deviation}",
                    stageExecutionId,
                    timeInProgress?.ToString(@"hh\:mm\:ss") ?? "неизвестно",
                    plannedDuration.ToString(@"hh\:mm\:ss"),
                    deviation?.ToString(@"hh\:mm\:ss") ?? "неизвестно");

                // Дополнительная обработка после завершения переналадки
                if (stageExecution.IsSetup && stageExecution.MainStageId.HasValue)
                {
                    await HandleSetupStageCompletion(stageExecution.MainStageId.Value);
                }

                // Уведомление о просрочке, если есть
                if (deviation.HasValue && deviation.Value.TotalHours > 1)
                {
                    _logger.LogWarning("Этап {StageId} завершен с просрочкой {Overdue} часов",
                        stageExecutionId, deviation.Value.TotalHours);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при завершении этапа {StageId}: {Error}", stageExecutionId, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// ОТМЕНА ЭТАПА согласно ТЗ
        /// </summary>
        public async Task CancelStageExecution(int stageExecutionId, string reason, string operatorId = null, string deviceId = null)
        {
            try
            {
                var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
                if (stageExecution == null)
                    throw new Exception($"Этап выполнения {stageExecutionId} не найден");

                if (stageExecution.Status == StageExecutionStatus.Completed)
                {
                    throw new Exception("Нельзя отменить завершенный этап");
                }

                _logger.LogInformation("Отмена этапа {StageId} ({StageName}). Причина: {Reason}",
                    stageExecutionId, stageExecution.RouteStage?.Name, reason);

                var timeInProgress = stageExecution.ActualWorkingTime;

                stageExecution.Status = StageExecutionStatus.Error;
                stageExecution.StatusChangedTimeUtc = DateTime.UtcNow;
                stageExecution.ReasonNote = reason ?? "Отменен";
                stageExecution.IsProcessedByScheduler = true;
                stageExecution.OperatorId = operatorId ?? "SYSTEM";
                stageExecution.DeviceId = deviceId ?? "AUTO";
                stageExecution.LastErrorMessage = reason;
                stageExecution.UpdateLastModified();

                // Если этап был в работе, записываем время окончания
                if (stageExecution.Status == StageExecutionStatus.InProgress)
                {
                    stageExecution.EndTimeUtc = DateTime.UtcNow;
                }

                await _batchRepo.UpdateStageExecutionAsync(stageExecution);

                _logger.LogInformation("Этап {StageId} отменен. Время в работе: {WorkingTime}",
                    stageExecutionId, timeInProgress?.ToString(@"hh\:mm\:ss") ?? "не был запущен");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отмене этапа {StageId}: {Error}", stageExecutionId, ex.Message);
                throw;
            }
        }

        #endregion

        #region Обработка переналадок

        /// <summary>
        /// Проверка и создание этапа переналадки при необходимости (ИСПРАВЛЕННАЯ ВЕРСИЯ)
        /// </summary>
        private async Task<StageExecution?> CheckAndCreateSetupStageIfNeeded(StageExecution mainStage, Machine machine)
        {
            try
            {
                // Для этапов переналадки не создаем вложенные переналадки
                if (mainStage.IsSetup) return null;

                var currentDetailId = mainStage.SubBatch.Batch.DetailId;
                var lastStageOnMachine = await _batchRepo.GetLastCompletedStageOnMachineAsync(machine.Id);

                // Если это первая деталь на станке или та же деталь, переналадка не нужна
                if (lastStageOnMachine == null || lastStageOnMachine.SubBatch.Batch.DetailId == currentDetailId)
                {
                    _logger.LogDebug("Переналадка не требуется для этапа {StageId} на станке {MachineId}",
                        mainStage.Id, machine.Id);
                    return null;
                }

                var previousDetailId = lastStageOnMachine.SubBatch.Batch.DetailId;

                // Получаем или создаем запись времени переналадки
                var setupTimeRecord = await _setupTimeRepo.GetSetupTimeAsync(machine.Id, previousDetailId, currentDetailId);

                double setupDuration;
                if (setupTimeRecord == null)
                {
                    // Используем стандартное время переналадки из маршрута
                    setupDuration = mainStage.RouteStage.SetupTime;

                    // Создаем запись для будущего использования
                    var newSetupTimeRecord = new SetupTime
                    {
                        MachineId = machine.Id,
                        FromDetailId = previousDetailId,
                        ToDetailId = currentDetailId,
                        Time = setupDuration
                    };
                    await _setupTimeRepo.AddAsync(newSetupTimeRecord);

                    _logger.LogDebug("Создана запись времени переналадки: {FromDetailId} -> {ToDetailId} на станке {MachineId}, время: {Time}ч",
                        previousDetailId, currentDetailId, machine.Id, setupDuration);
                }
                else
                {
                    setupDuration = setupTimeRecord.Time;
                }

                // Создаем этап переналадки
                var setupStage = new StageExecution
                {
                    SubBatchId = mainStage.SubBatchId,
                    RouteStageId = mainStage.RouteStageId,
                    MachineId = machine.Id,
                    Status = StageExecutionStatus.Pending,
                    IsSetup = true,
                    StatusChangedTimeUtc = DateTime.UtcNow,
                    CreatedUtc = DateTime.UtcNow,
                    Priority = mainStage.Priority + 1, // Переналадка имеет более высокий приоритет
                    IsCritical = false,
                    PlannedStartTimeUtc = DateTime.UtcNow,
                    MainStageId = mainStage.Id,
                    OperatorId = mainStage.OperatorId,
                    DeviceId = mainStage.DeviceId
                };

                var subBatch = mainStage.SubBatch;
                subBatch.StageExecutions.Add(setupStage);
                await _batchRepo.UpdateSubBatchAsync(subBatch);

                _logger.LogInformation("Создан этап переналадки {SetupStageId} для основного этапа {MainStageId}. " +
                    "С детали {FromDetailId} на деталь {ToDetailId}, время: {SetupTime}ч",
                    setupStage.Id, mainStage.Id, previousDetailId, currentDetailId, setupDuration);

                return setupStage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании этапа переналадки для этапа {StageId}", mainStage.Id);
                throw;
            }
        }


        /// <summary>
        /// Обработка завершения этапа переналадки
        /// </summary>
        private async Task HandleSetupStageCompletion(int mainStageId)
        {
            try
            {
                var mainStage = await _batchRepo.GetStageExecutionByIdAsync(mainStageId);
                if (mainStage != null && mainStage.Status == StageExecutionStatus.Waiting)
                {
                    mainStage.Status = StageExecutionStatus.Pending;
                    mainStage.StatusChangedTimeUtc = DateTime.UtcNow;
                    mainStage.UpdateLastModified();
                    await _batchRepo.UpdateStageExecutionAsync(mainStage);

                    _logger.LogInformation("Основной этап {MainStageId} готов к запуску после завершения переналадки", mainStageId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке завершения переналадки для основного этапа {MainStageId}", mainStageId);
            }
        }

        #endregion

        #region Проверки и валидации
        /// <summary>
        /// Проверка завершения всех предыдущих этапов (ИСПРАВЛЕННАЯ ВЕРСИЯ)
        /// </summary>
        private async Task<bool> CheckAllPreviousStagesCompleted(StageExecution stageExecution)
        {
            try
            {
                // Для этапов переналадки не требуется проверка последовательности
                if (stageExecution.IsSetup) return true;

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
                    _logger.LogDebug("Этап {StageId} является первым в маршруте", stageExecution.Id);
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
                        _logger.LogDebug("Предыдущий этап {PrevStageId} не завершен (статус: {Status}) для этапа {StageId}",
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
        /// Проверка рабочего времени согласно ТЗ
        /// </summary>
        private bool IsWorkingTime(DateTime dateTime)
        {
            var timeOfDay = dateTime.TimeOfDay;
            var dayOfWeek = dateTime.DayOfWeek;

            // Не работаем в выходные (согласно ТЗ)
            if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
                return false;

            // Обеденный перерыв 12:00-13:00 (согласно ТЗ)
            if (timeOfDay >= _lunchStart && timeOfDay < _lunchEnd)
                return false;

            // Перерыв на ужин 21:00-21:30 (согласно ТЗ)
            if (timeOfDay >= _dinnerStart && timeOfDay < _dinnerEnd)
                return false;

            // Рабочее время: 08:00-01:30 следующего дня (согласно ТЗ)
            if (_workEnd < _workStart) // переход через полночь
            {
                return timeOfDay >= _workStart || timeOfDay <= _workEnd;
            }
            else
            {
                return timeOfDay >= _workStart && timeOfDay <= _workEnd;
            }
        }

        /// <summary>
        /// Проверка возможности работы вне рабочего времени согласно ТЗ
        /// </summary>
        private bool CanRunOutsideWorkingHours(StageExecution stage)
        {
            // Этапы переналадки могут продолжаться вне рабочего времени (согласно ТЗ)
            if (stage.IsSetup) return true;

            // Критически важные этапы (согласно ТЗ)
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

        #endregion

        #region Диаграмма Ганта

        /// <summary>
        /// Получение данных для диаграммы Ганта
        /// </summary>
        public async Task<List<GanttStageDto>> GetAllStagesForGanttChart(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var allStages = await _batchRepo.GetAllStageExecutionsAsync();

                // Применяем фильтры по датам если указаны
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

                    var ganttStage = new GanttStageDto
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
                        PlannedDuration = stage.PlannedDuration,
                        ScheduledStartTime = stage.ScheduledStartTimeUtc,
                        ScheduledEndTime = stage.ScheduledStartTimeUtc?.Add(stage.PlannedDuration),
                        QueuePosition = stage.QueuePosition,
                        Priority = stage.Priority,
                        OperatorId = stage.OperatorId,
                        ReasonNote = stage.ReasonNote
                    };

                    result.Add(ganttStage);
                }

                _logger.LogDebug("Получено {Count} этапов для диаграммы Ганта", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении данных для диаграммы Ганта");
                return new List<GanttStageDto>();
            }
        }

        #endregion

        #region Дополнительные методы

        /// <summary>
        /// Получение статистики выполнения этапов
        /// </summary>
        public async Task<StageExecutionStatisticsDto> GetExecutionStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var stages = await _batchRepo.GetStageExecutionHistoryAsync(startDate, endDate);

                var statistics = new StageExecutionStatisticsDto
                {
                    TotalStages = stages.Count,
                    CompletedStages = stages.Count(s => s.Status == StageExecutionStatus.Completed),
                    InProgressStages = stages.Count(s => s.Status == StageExecutionStatus.InProgress),
                    PausedStages = stages.Count(s => s.Status == StageExecutionStatus.Paused),
                    QueuedStages = stages.Count(s => s.Status == StageExecutionStatus.Waiting),
                    SetupStages = stages.Count(s => s.IsSetup),
                    OverdueStages = stages.Count(s => s.IsOverdue),
                    AverageCompletionTime = CalculateAverageCompletionTime(stages),
                    EfficiencyPercentage = CalculateEfficiencyPercentage(stages)
                };

                return statistics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении статистики выполнения этапов");
                throw;
            }
        }

        private double CalculateAverageCompletionTime(List<StageExecution> stages)
        {
            var completedStages = stages.Where(s => s.Status == StageExecutionStatus.Completed &&
                                                  s.ActualWorkingTime.HasValue).ToList();

            if (!completedStages.Any()) return 0;

            return completedStages.Average(s => s.ActualWorkingTime.Value.TotalHours);
        }

        private double CalculateEfficiencyPercentage(List<StageExecution> stages)
        {
            var completedStages = stages.Where(s => s.Status == StageExecutionStatus.Completed &&
                                                  s.ActualWorkingTime.HasValue).ToList();

            if (!completedStages.Any()) return 0;

            var totalActualTime = completedStages.Sum(s => s.ActualWorkingTime.Value.TotalHours);
            var totalPlannedTime = completedStages.Sum(s => s.PlannedDuration.TotalHours);

            if (totalPlannedTime == 0) return 0;

            return Math.Min(100, (totalPlannedTime / totalActualTime) * 100);
        }

        #endregion
    }

    /// <summary>
    /// DTO для статистики выполнения этапов
    /// </summary>
    public class StageExecutionStatisticsDto
    {
        public int TotalStages { get; set; }
        public int CompletedStages { get; set; }
        public int InProgressStages { get; set; }
        public int PausedStages { get; set; }
        public int QueuedStages { get; set; }
        public int SetupStages { get; set; }
        public int OverdueStages { get; set; }
        public double AverageCompletionTime { get; set; }
        public double EfficiencyPercentage { get; set; }
    }
}