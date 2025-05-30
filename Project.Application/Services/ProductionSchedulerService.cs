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
    /// Основной сервис планирования производства согласно ТЗ
    /// </summary>
    public class ProductionSchedulerService
    {
        private readonly IBatchRepository _batchRepo;
        private readonly IMachineRepository _machineRepo;
        private readonly IRouteRepository _routeRepo;
        private readonly ISetupTimeRepository _setupTimeRepo;
        private readonly StageExecutionService _stageService;
        private readonly ILogger<ProductionSchedulerService> _logger;

        public ProductionSchedulerService(
            IBatchRepository batchRepo,
            IMachineRepository machineRepo,
            IRouteRepository routeRepo,
            ISetupTimeRepository setupTimeRepo,
            StageExecutionService stageService,
            ILogger<ProductionSchedulerService> logger)
        {
            _batchRepo = batchRepo;
            _machineRepo = machineRepo;
            _routeRepo = routeRepo;
            _setupTimeRepo = setupTimeRepo;
            _stageService = stageService;
            _logger = logger;
        }

        #region Создание и планирование партий

        /// <summary>
        /// Создание производственного задания (партии) согласно ТЗ
        /// </summary>
        public async Task<int> CreateBatchAsync(BatchCreateDto dto)
        {
            try
            {
                _logger.LogInformation("Создание партии для детали {DetailId}, количество: {Quantity}",
                    dto.DetailId, dto.Quantity);

                // Получаем деталь и её маршрут
                var route = await _routeRepo.GetByDetailIdAsync(dto.DetailId);
                if (route == null)
                    throw new Exception($"Маршрут для детали с ID {dto.DetailId} не найден");

                // Создаем основную партию
                var batch = new Batch
                {
                    DetailId = dto.DetailId,
                    Quantity = dto.Quantity,
                    CreatedUtc = DateTime.UtcNow,
                    SubBatches = new List<SubBatch>()
                };

                // Создаем подпартии
                if (dto.SubBatches == null || !dto.SubBatches.Any())
                {
                    // Если подпартии не указаны, создаем одну на весь объем
                    batch.SubBatches.Add(new SubBatch
                    {
                        Quantity = dto.Quantity,
                        StageExecutions = new List<StageExecution>()
                    });
                }
                else
                {
                    // Проверяем корректность подпартий
                    var totalSubQuantity = dto.SubBatches.Sum(sb => sb.Quantity);
                    if (totalSubQuantity != dto.Quantity)
                        throw new Exception($"Сумма подпартий ({totalSubQuantity}) не равна общему количеству ({dto.Quantity})");

                    foreach (var subBatchDto in dto.SubBatches)
                    {
                        batch.SubBatches.Add(new SubBatch
                        {
                            Quantity = subBatchDto.Quantity,
                            StageExecutions = new List<StageExecution>()
                        });
                    }
                }

                // Сохраняем партию в БД
                await _batchRepo.AddAsync(batch);

                // Для каждой подпартии генерируем этапы маршрута
                foreach (var subBatch in batch.SubBatches)
                {
                    await GenerateStageExecutionsForSubBatchAsync(subBatch.Id, route);
                }

                // Запускаем автоматическое планирование для новой партии
                await ScheduleSubBatchesAsync(batch.Id);

                _logger.LogInformation("Партия {BatchId} успешно создана и запланирована", batch.Id);
                return batch.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании партии");
                throw;
            }
        }

        /// <summary>
        /// Генерация этапов выполнения для подпартии на основе маршрута
        /// </summary>
        private async Task GenerateStageExecutionsForSubBatchAsync(int subBatchId, Route route)
        {
            try
            {
                var subBatch = await _batchRepo.GetSubBatchByIdAsync(subBatchId);
                if (subBatch == null)
                    throw new Exception($"Подпартия {subBatchId} не найдена");

                var currentTime = DateTime.UtcNow;

                // Создаем этапы выполнения в последовательности маршрута
                foreach (var routeStage in route.Stages.OrderBy(s => s.Order))
                {
                    var stageExecution = new StageExecution
                    {
                        SubBatchId = subBatchId,
                        RouteStageId = routeStage.Id,
                        Status = StageExecutionStatus.Pending,
                        IsSetup = false,
                        StatusChangedTimeUtc = currentTime,
                        CreatedUtc = currentTime,
                        Priority = 0,
                        IsCritical = false,
                        IsProcessedByScheduler = false
                    };

                    subBatch.StageExecutions.Add(stageExecution);
                }

                await _batchRepo.UpdateSubBatchAsync(subBatch);

                _logger.LogDebug("Создано {Count} этапов для подпартии {SubBatchId}",
                    route.Stages.Count, subBatchId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при генерации этапов для подпартии {SubBatchId}", subBatchId);
                throw;
            }
        }

        /// <summary>
        /// Автоматическое планирование для партии
        /// </summary>
        public async Task ScheduleSubBatchesAsync(int batchId)
        {
            try
            {
                var batch = await _batchRepo.GetByIdAsync(batchId);
                if (batch == null)
                    throw new Exception($"Партия {batchId} не найдена");

                _logger.LogInformation("Планирование партии {BatchId}", batchId);

                foreach (var subBatch in batch.SubBatches)
                {
                    // Планируем первый этап каждой подпартии
                    var firstStageExecution = subBatch.StageExecutions
                        .Where(se => !se.IsSetup)
                        .OrderBy(se => se.RouteStage.Order)
                        .FirstOrDefault();

                    if (firstStageExecution != null)
                    {
                        await ScheduleStageExecutionAsync(firstStageExecution.Id);
                    }
                }

                _logger.LogInformation("Партия {BatchId} запланирована", batchId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при планировании партии {BatchId}", batchId);
                throw;
            }
        }

        #endregion

        #region Планирование этапов

        /// <summary>
        /// Планирование конкретного этапа (основная логика согласно ТЗ)
        /// </summary>
        public async Task ScheduleStageExecutionAsync(int stageExecutionId)
        {
            try
            {
                var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
                if (stageExecution == null)
                    throw new Exception($"Этап выполнения {stageExecutionId} не найден");

                _logger.LogDebug("Планирование этапа {StageId}: {StageName}",
                    stageExecutionId, stageExecution.RouteStage.Name);

                // Если этап уже назначен на станок или уже выполняется - не планируем
                if (stageExecution.MachineId.HasValue ||
                    stageExecution.Status != StageExecutionStatus.Pending)
                {
                    _logger.LogDebug("Этап {StageId} уже назначен или не готов к планированию. Статус: {Status}",
                        stageExecutionId, stageExecution.Status);
                    return;
                }

                // Проверяем зависимости (последовательность выполнения)
                if (!await CheckStageSequenceDependencies(stageExecution))
                {
                    _logger.LogDebug("Этап {StageId} не может быть запланирован - не выполнены предыдущие этапы",
                        stageExecutionId);
                    return;
                }

                // Получаем доступные станки для этапа
                var availableMachines = await GetAvailableMachinesForStageAsync(stageExecution);

                if (!availableMachines.Any())
                {
                    // Нет доступных станков, ставим в очередь ожидания
                    await SetStageToWaitingQueue(stageExecution);
                    return;
                }

                // Выбираем оптимальный станок
                var selectedMachine = await SelectOptimalMachineAsync(stageExecution, availableMachines);

                // Назначаем этап на выбранный станок
                await AssignStageToMachineAsync(stageExecution, selectedMachine);

                _logger.LogInformation("Этап {StageId} назначен на станок {MachineName} ({MachineId})",
                    stageExecutionId, selectedMachine.Name, selectedMachine.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при планировании этапа {StageId}", stageExecutionId);
                throw;
            }
        }

        /// <summary>
        /// Проверка зависимостей последовательности этапов
        /// </summary>
        private async Task<bool> CheckStageSequenceDependencies(StageExecution stageExecution)
        {
            try
            {
                // Для этапов переналадки не требуется проверка последовательности
                if (stageExecution.IsSetup) return true;

                var subBatch = stageExecution.SubBatch;
                var currentStage = stageExecution.RouteStage;

                // Получаем маршрут
                var route = await _routeRepo.GetByIdAsync(currentStage.RouteId);
                if (route == null) return false;

                // Если это первый этап маршрута
                var isFirstStage = !route.Stages.Any(s => s.Order < currentStage.Order);
                if (isFirstStage) return true;

                // Проверяем, что все предыдущие этапы завершены
                var previousStages = route.Stages
                    .Where(s => s.Order < currentStage.Order)
                    .OrderBy(s => s.Order)
                    .ToList();

                foreach (var prevStage in previousStages)
                {
                    var prevStageExecution = subBatch.StageExecutions
                        .FirstOrDefault(se => !se.IsSetup && se.RouteStageId == prevStage.Id);

                    if (prevStageExecution == null ||
                        prevStageExecution.Status != StageExecutionStatus.Completed)
                    {
                        _logger.LogDebug("Предыдущий этап {PrevStageOrder} не завершен для этапа {StageId}",
                            prevStage.Order, stageExecution.Id);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке зависимостей этапа {StageId}", stageExecution.Id);
                return false;
            }
        }

        /// <summary>
        /// Получение доступных станков для этапа
        /// </summary>
        private async Task<List<Machine>> GetAvailableMachinesForStageAsync(StageExecution stageExecution)
        {
            try
            {
                var machineTypeId = stageExecution.RouteStage.MachineTypeId;

                // Получаем все станки подходящего типа
                var machinesOfType = await _machineRepo.GetMachinesByTypeAsync(machineTypeId);

                // Получаем занятые станки
                var busyMachineIds = await _batchRepo.GetAllStageExecutionsAsync();
                var occupiedMachineIds = busyMachineIds
                    .Where(se => se.Status == StageExecutionStatus.InProgress && se.MachineId.HasValue)
                    .Select(se => se.MachineId.Value)
                    .Distinct()
                    .ToList();

                // Фильтруем доступные станки
                var availableMachines = machinesOfType
                    .Where(m => !occupiedMachineIds.Contains(m.Id))
                    .ToList();

                _logger.LogDebug("Найдено {Available} из {Total} доступных станков типа {MachineType} для этапа {StageId}",
                    availableMachines.Count, machinesOfType.Count,
                    stageExecution.RouteStage.MachineType?.Name, stageExecution.Id);

                return availableMachines;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении доступных станков для этапа {StageId}", stageExecution.Id);
                return new List<Machine>();
            }
        }

        /// <summary>
        /// Выбор оптимального станка согласно ТЗ (приоритет использования, минимизация переналадок)
        /// </summary>
        private async Task<Machine> SelectOptimalMachineAsync(StageExecution stageExecution, List<Machine> availableMachines)
        {
            try
            {
                var detailId = stageExecution.SubBatch.Batch.DetailId;
                var machineScores = new Dictionary<Machine, double>();

                foreach (var machine in availableMachines)
                {
                    double score = 0;

                    // 1. Приоритет станка (согласно ТЗ)
                    score += machine.Priority * 10;

                    // 2. Минимизация переналадок (согласно ТЗ)
                    var lastDetailOnMachine = await _setupTimeRepo.GetLastDetailOnMachineAsync(machine.Id);
                    if (lastDetailOnMachine?.Id == detailId)
                    {
                        score += 50; // Бонус за отсутствие переналадки
                    }
                    else if (lastDetailOnMachine != null)
                    {
                        // Учитываем время переналадки
                        var setupTime = await _setupTimeRepo.GetSetupTimeAsync(machine.Id, lastDetailOnMachine.Id, detailId);
                        if (setupTime != null)
                        {
                            score -= setupTime.Time * 5; // Штраф за время переналадки
                        }
                    }

                    // 3. Загруженность станка (проверяем очередь)
                    var queuedStages = await _batchRepo.GetQueuedStagesForMachineAsync(machine.Id);
                    score -= queuedStages.Count * 2; // Штраф за этапы в очереди

                    // 4. Время до освобождения
                    var releaseTime = await CalculateMachineReleaseTimeAsync(machine.Id);
                    var hoursUntilAvailable = (releaseTime - DateTime.UtcNow).TotalHours;
                    if (hoursUntilAvailable > 0)
                    {
                        score -= hoursUntilAvailable * 3; // Штраф за ожидание
                    }

                    machineScores[machine] = score;
                }

                var selectedMachine = machineScores.OrderByDescending(kvp => kvp.Value).First().Key;

                _logger.LogDebug("Выбран станок {MachineName} (счет: {Score}) для этапа {StageId}",
                    selectedMachine.Name, machineScores[selectedMachine], stageExecution.Id);

                return selectedMachine;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при выборе оптимального станка для этапа {StageId}", stageExecution.Id);
                // Возвращаем первый доступный станок
                return availableMachines.First();
            }
        }

        /// <summary>
        /// Назначение этапа на станок с созданием переналадки при необходимости
        /// </summary>
        private async Task AssignStageToMachineAsync(StageExecution stageExecution, Machine machine)
        {
            try
            {
                var detailId = stageExecution.SubBatch.Batch.DetailId;

                // Проверяем необходимость переналадки
                var setupStage = await CheckAndCreateSetupStageAsync(stageExecution, machine, detailId);

                // Назначаем основной этап на станок
                stageExecution.MachineId = machine.Id;
                stageExecution.Status = setupStage != null ?
                    StageExecutionStatus.Waiting : // Ждем завершения переналадки
                    StageExecutionStatus.Pending;  // Готов к запуску
                stageExecution.StatusChangedTimeUtc = DateTime.UtcNow;
                stageExecution.PlannedStartTimeUtc = setupStage?.PlannedStartTimeUtc?.Add(TimeSpan.FromHours(setupStage.RouteStage.SetupTime))
                    ?? DateTime.UtcNow;

                await _batchRepo.UpdateStageExecutionAsync(stageExecution);

                _logger.LogDebug("Этап {StageId} назначен на станок {MachineId}. Переналадка: {SetupRequired}",
                    stageExecution.Id, machine.Id, setupStage != null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при назначении этапа {StageId} на станок {MachineId}",
                    stageExecution.Id, machine.Id);
                throw;
            }
        }

        /// <summary>
        /// Проверка и создание этапа переналадки при необходимости
        /// </summary>
        private async Task<StageExecution?> CheckAndCreateSetupStageAsync(StageExecution mainStage, Machine machine, int detailId)
        {
            try
            {
                var lastDetailOnMachine = await _setupTimeRepo.GetLastDetailOnMachineAsync(machine.Id);

                // Если это первая деталь на станке или та же деталь - переналадка не нужна
                if (lastDetailOnMachine == null || lastDetailOnMachine.Id == detailId)
                    return null;

                var previousDetailId = lastDetailOnMachine.Id;

                // Получаем время переналадки
                var setupTimeRecord = await _setupTimeRepo.GetSetupTimeAsync(machine.Id, previousDetailId, detailId);

                double setupTime;
                if (setupTimeRecord != null)
                {
                    setupTime = setupTimeRecord.Time;
                }
                else
                {
                    // Используем стандартное время переналадки из маршрута
                    setupTime = mainStage.RouteStage.SetupTime;

                    // Создаем запись времени переналадки для будущего использования
                    var newSetupTimeRecord = new SetupTime
                    {
                        MachineId = machine.Id,
                        FromDetailId = previousDetailId,
                        ToDetailId = detailId,
                        Time = setupTime
                    };
                    await _setupTimeRepo.AddAsync(newSetupTimeRecord);
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
                    Priority = mainStage.Priority + 1, // Переналадка имеет чуть выше приоритет
                    PlannedStartTimeUtc = DateTime.UtcNow,
                    MainStageId = mainStage.Id
                };

                // Связываем основной этап с переналадкой
                mainStage.SetupStageId = setupStage.Id;

                var subBatch = mainStage.SubBatch;
                subBatch.StageExecutions.Add(setupStage);
                await _batchRepo.UpdateSubBatchAsync(subBatch);

                _logger.LogDebug("Создан этап переналадки {SetupStageId} для основного этапа {MainStageId}. Время: {SetupTime}ч",
                    setupStage.Id, mainStage.Id, setupTime);

                return setupStage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании этапа переналадки для этапа {StageId}", mainStage.Id);
                throw;
            }
        }

        /// <summary>
        /// Постановка этапа в очередь ожидания
        /// </summary>
        private async Task SetStageToWaitingQueue(StageExecution stageExecution)
        {
            try
            {
                stageExecution.Status = StageExecutionStatus.Waiting;
                stageExecution.StatusChangedTimeUtc = DateTime.UtcNow;

                // Определяем позицию в очереди
                var queuePosition = await CalculateQueuePositionAsync(stageExecution);
                stageExecution.QueuePosition = queuePosition;

                await _batchRepo.UpdateStageExecutionAsync(stageExecution);

                _logger.LogInformation("Этап {StageId} поставлен в очередь ожидания на позицию {Position}",
                    stageExecution.Id, queuePosition);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при постановке этапа {StageId} в очередь", stageExecution.Id);
                throw;
            }
        }

        /// <summary>
        /// Расчет позиции в очереди
        /// </summary>
        private async Task<int> CalculateQueuePositionAsync(StageExecution stageExecution)
        {
            try
            {
                var machineTypeId = stageExecution.RouteStage.MachineTypeId;

                // Получаем все этапы в очереди для того же типа станка
                var queuedStages = await _batchRepo.GetAllStagesInQueueAsync();
                var relevantQueuedStages = queuedStages
                    .Where(s => s.RouteStage.MachineTypeId == machineTypeId)
                    .OrderBy(s => s.Priority)
                    .ThenBy(s => s.SubBatch.Batch.CreatedUtc)
                    .ThenBy(s => s.CreatedUtc)
                    .ToList();

                return relevantQueuedStages.Count + 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при расчете позиции в очереди для этапа {StageId}", stageExecution.Id);
                return 1;
            }
        }

        #endregion

        #region Проверка и запуск этапов

        /// <summary>
        /// Проверка возможности запуска этапа
        /// </summary>
        public async Task<bool> CanStartStageAsync(int stageExecutionId)
        {
            try
            {
                var stage = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
                if (stage == null) return false;

                // Проверяем статус этапа
                if (stage.Status != StageExecutionStatus.Pending) return false;

                // Проверяем назначение на станок
                if (!stage.MachineId.HasValue) return false;

                // Для этапов переналадки не требуется дополнительных проверок
                if (stage.IsSetup) return true;

                // Проверяем, что этап переналадки завершен (если есть)
                if (stage.SetupStageId.HasValue)
                {
                    var setupStage = await _batchRepo.GetStageExecutionByIdAsync(stage.SetupStageId.Value);
                    if (setupStage != null && setupStage.Status != StageExecutionStatus.Completed)
                    {
                        return false;
                    }
                }

                // Проверяем зависимости последовательности
                return await CheckStageSequenceDependencies(stage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке возможности запуска этапа {StageId}", stageExecutionId);
                return false;
            }
        }

        /// <summary>
        /// Запуск этапа, который находится в статусе Pending
        /// </summary>
        public async Task<bool> StartPendingStageAsync(int stageExecutionId)
        {
            try
            {
                var stage = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
                if (stage == null) return false;

                // Проверяем возможность запуска
                if (!await CanStartStageAsync(stageExecutionId)) return false;

                // Проверяем занятость станка
                if (stage.MachineId.HasValue)
                {
                    var currentStageOnMachine = await _batchRepo.GetCurrentStageOnMachineAsync(stage.MachineId.Value);
                    if (currentStageOnMachine != null && currentStageOnMachine.Id != stageExecutionId)
                    {
                        _logger.LogDebug("Станок {MachineId} занят этапом {CurrentStageId}, этап {StageId} отложен",
                            stage.MachineId.Value, currentStageOnMachine.Id, stageExecutionId);
                        return false;
                    }
                }

                // Запускаем этап
                await _stageService.StartStageExecution(stageExecutionId, operatorId: "SYSTEM", deviceId: "AUTO_SCHEDULER");

                _logger.LogInformation("Этап {StageId} автоматически запущен", stageExecutionId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при автоматическом запуске этапа {StageId}", stageExecutionId);
                return false;
            }
        }

        #endregion

        #region Обработка завершенных этапов

        /// <summary>
        /// Обработка события завершения этапа
        /// </summary>
        public async Task HandleStageCompletionAsync(int stageExecutionId)
        {
            try
            {
                var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
                if (stageExecution == null)
                    throw new Exception($"Этап выполнения {stageExecutionId} не найден");

                _logger.LogDebug("Обработка завершения этапа {StageId}", stageExecutionId);

                // Отмечаем этап как обработанный планировщиком
                await _batchRepo.MarkStageAsProcessedAsync(stageExecutionId);

                // Если это этап переналадки, активируем основной этап
                if (stageExecution.IsSetup && stageExecution.MainStageId.HasValue)
                {
                    await HandleSetupStageCompletionAsync(stageExecution.MainStageId.Value);
                }
                // Если это основной этап, планируем следующий этап подпартии
                else if (!stageExecution.IsSetup)
                {
                    await HandleMainStageCompletionAsync(stageExecution);
                }

                // Проверяем очередь на освободившемся станке
                if (stageExecution.MachineId.HasValue)
                {
                    await ProcessMachineQueueAsync(stageExecution.MachineId.Value);
                }

                _logger.LogDebug("Завершение этапа {StageId} обработано", stageExecutionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке завершения этапа {StageId}", stageExecutionId);
                throw;
            }
        }

        /// <summary>
        /// Обработка завершения этапа переналадки
        /// </summary>
        private async Task HandleSetupStageCompletionAsync(int mainStageId)
        {
            try
            {
                var mainStage = await _batchRepo.GetStageExecutionByIdAsync(mainStageId);
                if (mainStage != null && mainStage.Status == StageExecutionStatus.Waiting)
                {
                    mainStage.Status = StageExecutionStatus.Pending;
                    mainStage.StatusChangedTimeUtc = DateTime.UtcNow;
                    await _batchRepo.UpdateStageExecutionAsync(mainStage);

                    _logger.LogInformation("Основной этап {MainStageId} готов к запуску после переналадки", mainStageId);

                    // Пытаемся автоматически запустить основной этап
                    await StartPendingStageAsync(mainStageId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке завершения переналадки для основного этапа {MainStageId}", mainStageId);
            }
        }

        /// <summary>
        /// Обработка завершения основного этапа
        /// </summary>
        private async Task HandleMainStageCompletionAsync(StageExecution completedStage)
        {
            try
            {
                var subBatch = completedStage.SubBatch;
                var currentRouteStage = completedStage.RouteStage;

                // Получаем маршрут
                var route = await _routeRepo.GetByIdAsync(currentRouteStage.RouteId);
                if (route == null) return;

                // Находим следующий этап маршрута
                var nextRouteStage = route.Stages
                    .Where(s => s.Order > currentRouteStage.Order)
                    .OrderBy(s => s.Order)
                    .FirstOrDefault();

                if (nextRouteStage != null)
                {
                    // Находим соответствующий этап выполнения в подпартии
                    var nextStageExecution = subBatch.StageExecutions
                        .FirstOrDefault(se => !se.IsSetup && se.RouteStageId == nextRouteStage.Id);

                    if (nextStageExecution != null)
                    {
                        _logger.LogDebug("Планирование следующего этапа {NextStageId} после завершения {CompletedStageId}",
                            nextStageExecution.Id, completedStage.Id);

                        // Планируем следующий этап
                        await ScheduleStageExecutionAsync(nextStageExecution.Id);
                    }
                }
                else
                {
                    _logger.LogInformation("Все этапы подпартии {SubBatchId} завершены", subBatch.Id);
                    await CheckBatchCompletionAsync(subBatch.BatchId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке завершения основного этапа {StageId}", completedStage.Id);
            }
        }

        /// <summary>
        /// Проверка завершения всей партии
        /// </summary>
        private async Task CheckBatchCompletionAsync(int batchId)
        {
            try
            {
                var batch = await _batchRepo.GetByIdAsync(batchId);
                if (batch == null) return;

                var allStages = batch.SubBatches.SelectMany(sb => sb.StageExecutions.Where(se => !se.IsSetup)).ToList();
                var completedStages = allStages.Where(se => se.Status == StageExecutionStatus.Completed).ToList();

                if (allStages.Count == completedStages.Count)
                {
                    _logger.LogInformation("Партия {BatchId} полностью завершена. Изготовлено {Quantity} деталей {DetailName}",
                        batchId, batch.Quantity, batch.Detail?.Name);

                    // Здесь можно добавить дополнительную логику завершения партии
                    // Например, отправка уведомлений, обновление статистики и т.д.
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке завершения партии {BatchId}", batchId);
            }
        }

        /// <summary>
        /// Обработка очереди на освободившемся станке
        /// </summary>
        private async Task ProcessMachineQueueAsync(int machineId)
        {
            try
            {
                var nextInQueue = await _batchRepo.GetNextStageInQueueForMachineAsync(machineId);
                if (nextInQueue != null)
                {
                    _logger.LogDebug("Обработка очереди на станке {MachineId}. Следующий этап: {StageId}",
                        machineId, nextInQueue.Id);

                    // Переводим этап из "Waiting" в "Pending"
                    nextInQueue.Status = StageExecutionStatus.Pending;
                    nextInQueue.StatusChangedTimeUtc = DateTime.UtcNow;
                    await _batchRepo.UpdateStageExecutionAsync(nextInQueue);

                    // Планируем этап из очереди
                    await ScheduleStageExecutionAsync(nextInQueue.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке очереди станка {MachineId}", machineId);
            }
        }

        #endregion

        #region Вспомогательные методы

        /// <summary>
        /// Расчет времени, когда станок освободится
        /// </summary>
        public async Task<DateTime> CalculateMachineReleaseTimeAsync(int machineId)
        {
            try
            {
                // Получаем текущий этап, выполняемый на станке
                var currentStage = await _batchRepo.GetCurrentStageOnMachineAsync(machineId);
                if (currentStage == null)
                    return DateTime.UtcNow; // станок уже свободен

                // Получаем все этапы, назначенные на станок и ожидающие выполнения
                var queuedStages = await _batchRepo.GetQueuedStagesForMachineAsync(machineId);

                // Рассчитываем время завершения текущего этапа
                DateTime estimatedEndTime;
                if (currentStage.EndTimeUtc.HasValue)
                    return DateTime.UtcNow; // этап уже завершен

                if (currentStage.StartTimeUtc.HasValue)
                {
                    // Этап выполняется - рассчитываем оставшееся время
                    var plannedDuration = currentStage.PlannedDuration;
                    var elapsedTime = DateTime.UtcNow - currentStage.StartTimeUtc.Value;
                    var remainingTime = plannedDuration - elapsedTime;

                    estimatedEndTime = DateTime.UtcNow.Add(remainingTime > TimeSpan.Zero ? remainingTime : TimeSpan.FromMinutes(5));
                }
                else
                {
                    // Этап еще не начался
                    estimatedEndTime = DateTime.UtcNow.Add(currentStage.PlannedDuration);
                }

                // Добавляем время выполнения всех этапов в очереди
                foreach (var stage in queuedStages)
                {
                    estimatedEndTime = estimatedEndTime.Add(stage.PlannedDuration);
                }

                return estimatedEndTime;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при расчете времени освобождения станка {MachineId}", machineId);
                return DateTime.UtcNow.AddHours(1); // Предполагаем час по умолчанию
            }
        }

        #endregion

        // Остальные методы (ReassignQueueAsync, ReassignStageToMachineAsync, OptimizeQueueAsync и т.д.) 
        // оставляю без изменений, они корректны

        #region Управление приоритетами и переназначение

        /// <summary>
        /// Переназначение очереди этапов в случае приоритета или паузы
        /// </summary>
        public async Task ReassignQueueAsync(int machineId, int priorityStageExecutionId)
        {
            try
            {
                // Получаем текущий этап, который выполняется на станке
                var currentStage = await _batchRepo.GetCurrentStageOnMachineAsync(machineId);
                if (currentStage == null)
                {
                    // Станок свободен, просто планируем новый этап
                    await ScheduleStageExecutionAsync(priorityStageExecutionId);
                    return;
                }

                // Ставим текущий этап на паузу
                await _stageService.PauseStageExecution(currentStage.Id, operatorId: "SYSTEM",
                    reasonNote: "Приостановлен для приоритетного этапа", deviceId: "AUTO_SCHEDULER");

                // Получаем приоритетный этап
                var priorityStage = await _batchRepo.GetStageExecutionByIdAsync(priorityStageExecutionId);
                if (priorityStage == null)
                    throw new Exception($"Приоритетный этап {priorityStageExecutionId} не найден");

                // Назначаем приоритетный этап на освободившийся станок
                await _stageService.AssignStageToMachine(priorityStageExecutionId, machineId);

                // Автоматически стартуем приоритетный этап
                if (await CanStartStageAsync(priorityStageExecutionId))
                {
                    await _stageService.StartStageExecution(priorityStageExecutionId, operatorId: "SYSTEM", deviceId: "AUTO_SCHEDULER");
                }

                _logger.LogInformation("Этап {PriorityStageId} приоритизирован на станке {MachineId}, текущий этап {CurrentStageId} приостановлен",
                    priorityStageExecutionId, machineId, currentStage.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при переназначении очереди для станка {MachineId}", machineId);
                throw;
            }
        }

        /// <summary>
        /// Переназначение этапа на другой станок
        /// </summary>
        public async Task ReassignStageToMachineAsync(int stageExecutionId, int newMachineId)
        {
            try
            {
                var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
                if (stageExecution == null)
                    throw new Exception($"Этап выполнения {stageExecutionId} не найден");

                var newMachine = await _machineRepo.GetByIdAsync(newMachineId);
                if (newMachine == null)
                    throw new Exception($"Станок {newMachineId} не найден");

                // Проверяем совместимость типа станка
                if (newMachine.MachineTypeId != stageExecution.RouteStage.MachineTypeId)
                    throw new Exception($"Станок {newMachine.Name} не подходит для этапа {stageExecution.RouteStage.Name}");

                var oldMachineId = stageExecution.MachineId;
                var currentStatus = stageExecution.Status;

                // Если этап выполняется, сначала ставим на паузу
                if (stageExecution.Status == StageExecutionStatus.InProgress)
                {
                    await _stageService.PauseStageExecution(stageExecutionId, operatorId: "SYSTEM",
                        reasonNote: "Пауза для переназначения", deviceId: "AUTO_SCHEDULER");
                }

                // Переназначаем на новый станок
                await AssignStageToMachineAsync(stageExecution, newMachine);

                // Если этап был в процессе, возобновляем на новом станке
                if (currentStatus == StageExecutionStatus.InProgress)
                {
                    await _stageService.StartStageExecution(stageExecutionId, operatorId: "SYSTEM", deviceId: "AUTO_SCHEDULER");
                }

                _logger.LogInformation("Этап {StageId} переназначен со станка {OldMachineId} на станок {NewMachineId}",
                    stageExecutionId, oldMachineId, newMachineId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при переназначении этапа {StageId} на станок {MachineId}",
                    stageExecutionId, newMachineId);
                throw;
            }
        }

        #endregion

        #region Оптимизация и аналитика

        /// <summary>
        /// Оптимизация очереди этапов для максимальной загрузки станков
        /// </summary>
        public async Task OptimizeQueueAsync()
        {
            try
            {
                _logger.LogDebug("Начало оптимизации очереди");

                // Получаем все этапы в очереди
                var stagesInQueue = await _batchRepo.GetAllStagesInQueueAsync();
                if (!stagesInQueue.Any())
                {
                    _logger.LogDebug("Очередь пуста, оптимизация не требуется");
                    return;
                }

                var optimizedCount = 0;

                // Группируем этапы по типу станка
                var stagesByMachineType = stagesInQueue
                    .GroupBy(s => s.RouteStage.MachineTypeId)
                    .ToList();

                foreach (var machineTypeGroup in stagesByMachineType)
                {
                    var machineTypeId = machineTypeGroup.Key;
                    var stages = machineTypeGroup.ToList();

                    // Получаем станки этого типа
                    var machines = await _machineRepo.GetMachinesByTypeAsync(machineTypeId);

                    foreach (var stage in stages)
                    {
                        try
                        {
                            var availableMachines = await GetAvailableMachinesForStageAsync(stage);
                            if (availableMachines.Any())
                            {
                                var optimalMachine = await SelectOptimalMachineAsync(stage, availableMachines);

                                // Если найден лучший станок, переназначаем
                                if (stage.MachineId != optimalMachine.Id)
                                {
                                    await AssignStageToMachineAsync(stage, optimalMachine);
                                    optimizedCount++;

                                    _logger.LogDebug("Этап {StageId} оптимизирован и переназначен на станок {MachineId}",
                                        stage.Id, optimalMachine.Id);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Ошибка при оптимизации этапа {StageId}", stage.Id);
                        }
                    }
                }

                _logger.LogInformation("Оптимизация очереди завершена. Оптимизировано этапов: {Count}", optimizedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при оптимизации очереди");
                throw;
            }
        }

        /// <summary>
        /// Получение прогноза времени для этапов в очереди
        /// </summary>
        public async Task<List<StageQueueDto>> GetQueueForecastAsync()
        {
            try
            {
                var stagesInQueue = await _batchRepo.GetAllStagesInQueueAsync();
                var result = new List<StageQueueDto>();

                foreach (var stage in stagesInQueue)
                {
                    var machineTypeId = stage.RouteStage.MachineTypeId;
                    var machines = await _machineRepo.GetMachinesByTypeAsync(machineTypeId);

                    var forecastTimes = new Dictionary<int, DateTime>();
                    foreach (var machine in machines)
                    {
                        var releaseTime = await CalculateMachineReleaseTimeAsync(machine.Id);
                        forecastTimes.Add(machine.Id, releaseTime);
                    }

                    var bestMachine = forecastTimes
                        .OrderBy(kvp => kvp.Value)
                        .ThenByDescending(kvp => machines.First(m => m.Id == kvp.Key).Priority)
                        .FirstOrDefault();

                    if (bestMachine.Key != 0)
                    {
                        var machine = machines.First(m => m.Id == bestMachine.Key);
                        result.Add(new StageQueueDto
                        {
                            StageExecutionId = stage.Id,
                            SubBatchId = stage.SubBatchId,
                            DetailName = stage.SubBatch.Batch.Detail.Name,
                            StageName = stage.RouteStage.Name,
                            Status = stage.Status.ToString(),
                            ExpectedMachineId = bestMachine.Key,
                            ExpectedMachineName = machine.Name,
                            ExpectedStartTime = bestMachine.Value
                        });
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении прогноза очереди");
                return new List<StageQueueDto>();
            }
        }

        /// <summary>
        /// Автоматически выбирает и назначает оптимальный станок для этапа
        /// </summary>
        public async Task<bool> AutoAssignMachineToStageAsync(int stageExecutionId)
        {
            try
            {
                var stage = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
                if (stage == null) return false;

                // Если этап уже назначен на станок, ничего не делаем
                if (stage.MachineId.HasValue) return true;

                // Получаем список доступных станков для этапа
                var availableMachines = await GetAvailableMachinesForStageAsync(stage);
                if (!availableMachines.Any()) return false;

                // Выбираем оптимальный станок
                var bestMachine = await SelectOptimalMachineAsync(stage, availableMachines);

                // Назначаем этап на выбранный станок
                await AssignStageToMachineAsync(stage, bestMachine);

                _logger.LogInformation("Этап {StageId} автоматически назначен на станок {MachineName}",
                    stageExecutionId, bestMachine.Name);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при автоматическом назначении станка для этапа {StageId}", stageExecutionId);
                return false;
            }
        }

        /// <summary>
        /// Предсказывает оптимальное время для запуска производства новой детали
        /// </summary>
        public async Task<PredictedScheduleDto> PredictOptimalScheduleForDetailAsync(int detailId, int quantity)
        {
            try
            {
                var route = await _routeRepo.GetByDetailIdAsync(detailId);
                if (route == null)
                    throw new Exception($"Маршрут для детали {detailId} не найден");

                var startDate = DateTime.UtcNow;
                var stageForecasts = new List<StageForecastDto>();
                DateTime earliestStartTime = startDate;
                DateTime latestEndTime = startDate;

                foreach (var routeStage in route.Stages.OrderBy(s => s.Order))
                {
                    var machineTypeId = routeStage.MachineTypeId;
                    var machines = await _machineRepo.GetMachinesByTypeAsync(machineTypeId);

                    var bestMachineId = 0;
                    var bestStartTime = DateTime.MaxValue;
                    var bestEndTime = DateTime.MaxValue;
                    var needsSetup = false;

                    foreach (var machine in machines)
                    {
                        var machineReleaseTime = await CalculateMachineReleaseTimeAsync(machine.Id);
                        var lastDetailOnMachine = await _setupTimeRepo.GetLastDetailOnMachineAsync(machine.Id);
                        var setupNeeded = lastDetailOnMachine != null && lastDetailOnMachine.Id != detailId;

                        var setupDuration = TimeSpan.Zero;
                        if (setupNeeded)
                        {
                            var setupTimeRecord = await _setupTimeRepo.GetSetupTimeAsync(machine.Id, lastDetailOnMachine.Id, detailId);
                            setupDuration = TimeSpan.FromHours(setupTimeRecord?.Time ?? routeStage.SetupTime);
                        }

                        var operationDuration = TimeSpan.FromHours(routeStage.NormTime * quantity);
                        var stageStartTime = machineReleaseTime > earliestStartTime ? machineReleaseTime : earliestStartTime;
                        var stageEndTime = stageStartTime.Add(setupDuration).Add(operationDuration);

                        if (stageEndTime < bestEndTime)
                        {
                            bestMachineId = machine.Id;
                            bestStartTime = stageStartTime;
                            bestEndTime = stageEndTime;
                            needsSetup = setupNeeded;
                        }
                    }

                    stageForecasts.Add(new StageForecastDto
                    {
                        StageOrder = routeStage.Order,
                        StageName = routeStage.Name,
                        MachineTypeId = routeStage.MachineTypeId,
                        MachineTypeName = routeStage.MachineType?.Name,
                        MachineId = bestMachineId,
                        MachineName = machines.FirstOrDefault(m => m.Id == bestMachineId)?.Name,
                        ExpectedStartTime = bestStartTime,
                        ExpectedEndTime = bestEndTime,
                        NeedsSetup = needsSetup
                    });

                    earliestStartTime = bestEndTime;
                    if (bestEndTime > latestEndTime)
                        latestEndTime = bestEndTime;
                }

                return new PredictedScheduleDto
                {
                    DetailId = detailId,
                    Quantity = quantity,
                    EarliestStartTime = startDate,
                    LatestEndTime = latestEndTime,
                    TotalDuration = latestEndTime - startDate,
                    StageForecasts = stageForecasts
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при предсказании расписания для детали {DetailId}", detailId);
                throw;
            }
        }

        #endregion
    }
}