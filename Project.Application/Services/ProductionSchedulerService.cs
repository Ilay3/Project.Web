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

        // Создание производственного задания (партии)
        public async Task<int> CreateBatchAsync(BatchCreateDto dto)
        {
            // Создаем основную партию
            var batch = new Batch
            {
                DetailId = dto.DetailId,
                Quantity = dto.Quantity,
                CreatedUtc = DateTime.UtcNow,
                SubBatches = new List<SubBatch>()
            };

            // Если нет подпартий, создаем одну на весь объем
            if (dto.SubBatches == null || !dto.SubBatches.Any())
            {
                batch.SubBatches.Add(new SubBatch
                {
                    Quantity = dto.Quantity,
                    StageExecutions = new List<StageExecution>()
                });
            }
            else
            {
                // Иначе создаем указанные подпартии
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
                await _stageService.GenerateStageExecutionsForSubBatch(subBatch.Id);
            }

            // Запускаем автоматическое планирование для новой партии
            await ScheduleSubBatchesAsync(batch.Id);

            return batch.Id;
        }

        // Автоматическое планирование для партии
        public async Task ScheduleSubBatchesAsync(int batchId)
        {
            var batch = await _batchRepo.GetByIdAsync(batchId);
            if (batch == null) throw new Exception("Batch not found");

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
        }

        // *** ИСПРАВЛЕНИЕ: Планирование конкретного этапа ***
        public async Task ScheduleStageExecutionAsync(int stageExecutionId)
        {
            var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
            if (stageExecution == null) throw new Exception("Stage execution not found");

            // Если этап уже назначен на станок или уже выполняется - не планируем
            if (stageExecution.MachineId.HasValue ||
                stageExecution.Status != StageExecutionStatus.Pending)
                return;

            // *** ИСПРАВЛЕНИЕ: Используем правильный метод для получения доступных станков ***
            var availableMachines = await _batchRepo.GetAvailableMachinesForStageAsync(stageExecutionId);

            // Если нет доступных станков, ставим в очередь ожидания
            if (!availableMachines.Any())
            {
                stageExecution.Status = StageExecutionStatus.Waiting;
                stageExecution.StatusChangedTimeUtc = DateTime.UtcNow;
                await _batchRepo.UpdateStageExecutionAsync(stageExecution);

                _logger.LogInformation("Этап {StageId} поставлен в очередь ожидания - нет доступных станков", stageExecutionId);
                return;
            }

            // Выбираем станок с наивысшим приоритетом
            var bestMachine = availableMachines
                .OrderByDescending(m => m.Priority)
                .FirstOrDefault();

            if (bestMachine != null)
            {
                // Назначаем этап на выбранный станок
                await _stageService.AssignStageToMachine(stageExecutionId, bestMachine.Id);

                _logger.LogInformation("Этап {StageId} назначен на станок {MachineId}", stageExecutionId, bestMachine.Id);

                // *** ИСПРАВЛЕНИЕ: Проверяем возможность автоматического запуска ***
                if (await CanStartStageAsync(stageExecutionId))
                {
                    await StartPendingStageAsync(stageExecutionId);
                }
            }
        }

        // *** ИСПРАВЛЕНИЕ: Проверка возможности запуска этапа ***
        public async Task<bool> CanStartStageAsync(int stageExecutionId)
        {
            var stage = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
            if (stage == null) return false;

            // Если этап уже не в статусе Pending, его нельзя запустить
            if (stage.Status != StageExecutionStatus.Pending) return false;

            // Проверяем, что этап назначен на станок
            if (!stage.MachineId.HasValue) return false;

            // Для этапа переналадки не требуется проверка предыдущих этапов
            if (stage.IsSetup) return true;

            // *** ИСПРАВЛЕНИЕ: Проверяем, что этап переналадки завершен ***
            var setupStage = await _batchRepo.GetSetupStageForMainStageAsync(stageExecutionId);
            if (setupStage != null && setupStage.Status != StageExecutionStatus.Completed)
            {
                return false; // Ждем завершения переналадки
            }

            // Проверяем, все ли предыдущие этапы завершены
            return await CheckAllPreviousStagesCompletedAsync(stage);
        }

        // *** ИСПРАВЛЕНИЕ: Запуск этапа, который находится в статусе Pending ***
        public async Task<bool> StartPendingStageAsync(int stageExecutionId)
        {
            try
            {
                var stage = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
                if (stage == null) return false;

                // Проверяем, можно ли запустить этап
                if (!await CanStartStageAsync(stageExecutionId)) return false;

                // *** ИСПРАВЛЕНИЕ: Проверяем занятость станка ***
                if (stage.MachineId.HasValue)
                {
                    var currentStageOnMachine = await _batchRepo.GetCurrentStageOnMachineAsync(stage.MachineId.Value);
                    if (currentStageOnMachine != null && currentStageOnMachine.Id != stageExecutionId)
                    {
                        // Станок занят другим этапом
                        _logger.LogInformation("Станок {MachineId} занят этапом {CurrentStageId}, этап {StageId} отложен",
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

        // Проверка, что все предыдущие этапы завершены
        private async Task<bool> CheckAllPreviousStagesCompletedAsync(StageExecution stageExecution)
        {
            // Если это этап переналадки, предыдущие не проверяем
            if (stageExecution.IsSetup)
                return true;

            var subBatch = stageExecution.SubBatch;
            var currentStage = stageExecution.RouteStage;

            // Получаем все этапы маршрута для этой детали
            var route = await _routeRepo.GetByIdAsync(currentStage.RouteId);
            if (route == null) return false;

            // Находим все предыдущие этапы маршрута
            var previousStageOrders = route.Stages
                .Where(s => s.Order < currentStage.Order)
                .Select(s => s.Order)
                .ToList();

            if (!previousStageOrders.Any()) return true; // нет предыдущих этапов

            // Проверяем, что для каждого предыдущего этапа маршрута
            // есть завершенное выполнение в подпартии
            foreach (var stageExecutionInSubBatch in subBatch.StageExecutions
                .Where(se => !se.IsSetup && previousStageOrders.Contains(se.RouteStage.Order)))
            {
                if (stageExecutionInSubBatch.Status != StageExecutionStatus.Completed)
                    return false;
            }

            return true;
        }

        // Переназначение очереди этапов в случае приоритета или паузы
        public async Task ReassignQueueAsync(int machineId, int priorityStageExecutionId)
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
            if (priorityStage == null) throw new Exception("Priority stage not found");

            // Назначаем приоритетный этап на освободившийся станок
            await _stageService.AssignStageToMachine(priorityStageExecutionId, machineId);

            // Автоматически стартуем приоритетный этап
            if (await CanStartStageAsync(priorityStageExecutionId))
            {
                await _stageService.StartStageExecution(priorityStageExecutionId, operatorId: "SYSTEM", deviceId: "AUTO_SCHEDULER");
            }
        }

        // Переназначение этапа на другой станок
        public async Task ReassignStageToMachineAsync(int stageExecutionId, int newMachineId)
        {
            var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
            if (stageExecution == null) throw new Exception("Stage execution not found");

            // Если этап выполняется, сначала ставим на паузу
            if (stageExecution.Status == StageExecutionStatus.InProgress)
            {
                await _stageService.PauseStageExecution(stageExecutionId, operatorId: "SYSTEM",
                    reasonNote: "Пауза для переназначения", deviceId: "AUTO_SCHEDULER");
            }

            // Сохраняем текущий статус перед переназначением
            var currentStatus = stageExecution.Status;

            // Переназначаем на новый станок
            await _stageService.AssignStageToMachine(stageExecutionId, newMachineId);

            // Если этап был в процессе, возобновляем на новом станке
            if (currentStatus == StageExecutionStatus.InProgress)
            {
                await _stageService.StartStageExecution(stageExecutionId, operatorId: "SYSTEM", deviceId: "AUTO_SCHEDULER");
            }
        }

        // *** ИСПРАВЛЕНИЕ: Обработка события завершения этапа ***
        public async Task HandleStageCompletionAsync(int stageExecutionId)
        {
            var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
            if (stageExecution == null) throw new Exception("Stage execution not found");

            // Отмечаем этап как обработанный планировщиком
            await _batchRepo.MarkStageAsProcessedAsync(stageExecutionId);

            // Если это не этап переналадки, планируем следующий этап подпартии
            if (!stageExecution.IsSetup)
            {
                var subBatch = stageExecution.SubBatch;
                var currentStage = stageExecution.RouteStage;

                // Получаем следующий этап маршрута
                var route = await _routeRepo.GetByIdAsync(currentStage.RouteId);
                if (route == null) return;

                var nextRouteStage = route.Stages
                    .Where(s => s.Order > currentStage.Order)
                    .OrderBy(s => s.Order)
                    .FirstOrDefault();

                if (nextRouteStage != null)
                {
                    // Находим соответствующий этап выполнения в подпартии
                    var nextStageExecution = subBatch.StageExecutions
                        .FirstOrDefault(se => !se.IsSetup && se.RouteStageId == nextRouteStage.Id);

                    if (nextStageExecution != null)
                    {
                        // Планируем следующий этап
                        await ScheduleStageExecutionAsync(nextStageExecution.Id);
                    }
                }
            }

            // *** ИСПРАВЛЕНИЕ: Проверяем очередь на освободившемся станке ***
            if (stageExecution.MachineId.HasValue)
            {
                var nextInQueue = await _batchRepo.GetNextStageInQueueForMachineAsync(stageExecution.MachineId.Value);
                if (nextInQueue != null)
                {
                    // Переводим этап из "Waiting" в "Pending"
                    nextInQueue.Status = StageExecutionStatus.Pending;
                    nextInQueue.StatusChangedTimeUtc = DateTime.UtcNow;
                    await _batchRepo.UpdateStageExecutionAsync(nextInQueue);

                    // Планируем этап из очереди
                    await ScheduleStageExecutionAsync(nextInQueue.Id);
                }
            }
        }

        // Получение прогноза времени для этапов в очереди
        public async Task<List<StageQueueDto>> GetQueueForecastAsync()
        {
            // Получаем все этапы в очереди
            var stagesInQueue = await _batchRepo.GetAllStagesInQueueAsync();
            var result = new List<StageQueueDto>();

            foreach (var stage in stagesInQueue)
            {
                // Получаем требуемый тип станка
                var machineTypeId = stage.RouteStage.MachineTypeId;

                // Находим все станки этого типа
                var machines = await _machineRepo.GetMachinesByTypeAsync(machineTypeId);

                // Для каждого станка рассчитываем предполагаемое время освобождения
                var forecastTimes = new Dictionary<int, DateTime>();
                foreach (var machine in machines)
                {
                    var releaseTime = await CalculateMachineReleaseTimeAsync(machine.Id);
                    forecastTimes.Add(machine.Id, releaseTime);
                }

                // Выбираем станок, который освободится раньше всех
                var bestMachine = forecastTimes
                    .OrderBy(kvp => kvp.Value)
                    .ThenByDescending(kvp => machines.First(m => m.Id == kvp.Key).Priority)
                    .FirstOrDefault();

                if (bestMachine.Key != 0)  // если найден подходящий станок
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

        // Расчет времени, когда станок освободится
        private async Task<DateTime> CalculateMachineReleaseTimeAsync(int machineId)
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
                double totalHours = currentStage.IsSetup ?
                    currentStage.RouteStage.SetupTime :
                    currentStage.RouteStage.NormTime * currentStage.SubBatch.Quantity;

                // Рассчитываем, сколько времени уже прошло
                var elapsedTime = DateTime.UtcNow - currentStage.StartTimeUtc.Value;
                var totalTime = TimeSpan.FromHours(totalHours);

                // Если этап должен был уже завершиться по нормативу, но не завершен
                if (elapsedTime >= totalTime)
                    estimatedEndTime = DateTime.UtcNow.AddMinutes(30); // предполагаем, что скоро закончится
                else
                    estimatedEndTime = currentStage.StartTimeUtc.Value.Add(totalTime);
            }
            else
            {
                // Этап еще не начался - предполагаем, что начнется скоро
                estimatedEndTime = DateTime.UtcNow.AddHours(1);
            }

            // Добавляем время выполнения всех этапов в очереди
            foreach (var stage in queuedStages)
            {
                double stageHours = stage.IsSetup ?
                    stage.RouteStage.SetupTime :
                    stage.RouteStage.NormTime * stage.SubBatch.Quantity;

                estimatedEndTime = estimatedEndTime.AddHours(stageHours);
            }

            return estimatedEndTime;
        }

        // *** ИСПРАВЛЕНИЕ: Автоматически выбирает и назначает оптимальный станок для этапа ***
        public async Task<bool> AutoAssignMachineToStageAsync(int stageExecutionId)
        {
            try
            {
                var stage = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
                if (stage == null) return false;

                // Если этап уже назначен на станок, ничего не делаем
                if (stage.MachineId.HasValue) return true;

                // Получаем список доступных станков для этапа
                var availableMachines = await _batchRepo.GetAvailableMachinesForStageAsync(stageExecutionId);
                if (!availableMachines.Any()) return false;

                // Выбираем станок с наивысшим приоритетом
                var bestMachine = availableMachines.OrderByDescending(m => m.Priority).First();

                // Назначаем этап на выбранный станок
                await _stageService.AssignStageToMachine(stageExecutionId, bestMachine.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при автоматическом назначении станка для этапа {StageId}", stageExecutionId);
                return false;
            }
        }

        // *** ИСПРАВЛЕНИЕ: Оптимизирует очередь этапов для максимальной загрузки станков ***
        public async Task OptimizeQueueAsync()
        {
            try
            {
                // Получаем все этапы в очереди
                var stagesInQueue = await _batchRepo.GetAllStagesInQueueAsync();
                if (!stagesInQueue.Any()) return;

                // Получаем текущее расписание станков
                var startDate = DateTime.UtcNow;
                var endDate = startDate.AddDays(7); // Рассматриваем неделю вперед
                var machineSchedule = await _batchRepo.GetMachineScheduleAsync(startDate, endDate);

                // Для каждого этапа в очереди пытаемся найти оптимальное расписание
                foreach (var stage in stagesInQueue)
                {
                    // Получаем требуемый тип станка
                    var machineTypeId = stage.RouteStage.MachineTypeId;

                    // Получаем все станки этого типа
                    var machines = await _machineRepo.GetMachinesByTypeAsync(machineTypeId);

                    // Оцениваем возможность назначения на каждый станок
                    var machineRatings = new Dictionary<int, double>();
                    foreach (var machine in machines)
                    {
                        double rating = 0;

                        // Чем выше приоритет станка, тем лучше
                        rating += machine.Priority * 10;

                        // Если станок свободен сейчас, это хорошо
                        if (!machineSchedule.ContainsKey(machine.Id) ||
                            !machineSchedule[machine.Id].Any(s =>
                                s.Status == StageExecutionStatus.InProgress ||
                                s.Status == StageExecutionStatus.Pending))
                        {
                            rating += 50;
                        }

                        // Чем раньше станок освободится, тем лучше
                        var earliestAvailableTime = await CalculateMachineReleaseTimeAsync(machine.Id);
                        var hoursUntilAvailable = (earliestAvailableTime - DateTime.UtcNow).TotalHours;
                        rating -= hoursUntilAvailable * 5; // Штраф за ожидание

                        // Если это тот же станок, что и для предыдущего этапа той же детали, уменьшаем переналадку
                        var subBatch = stage.SubBatch;
                        var previousStages = subBatch.StageExecutions
                            .Where(s => s.Status == StageExecutionStatus.Completed)
                            .OrderByDescending(s => s.EndTimeUtc)
                            .ToList();

                        if (previousStages.Any() && previousStages.First().MachineId == machine.Id)
                        {
                            rating += 20; // Бонус за использование того же станка
                        }

                        machineRatings[machine.Id] = rating;
                    }

                    // Выбираем станок с наилучшим рейтингом
                    if (machineRatings.Any())
                    {
                        var bestMachineId = machineRatings.OrderByDescending(r => r.Value).First().Key;

                        // Если этап уже назначен на станок, но есть лучший вариант, переназначаем
                        if (!stage.MachineId.HasValue || stage.MachineId.Value != bestMachineId)
                        {
                            await _stageService.AssignStageToMachine(stage.Id, bestMachineId);
                            _logger.LogInformation("Этап {StageId} оптимизирован и назначен на станок {MachineId}", stage.Id, bestMachineId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при оптимизации очереди");
            }
        }

        // *** ИСПРАВЛЕНИЕ: Проверяет конфликты в текущем расписании и пытается их разрешить ***
        public async Task ResolveScheduleConflictsAsync()
        {
            try
            {
                // Получаем список конфликтов
                var conflicts = await _batchRepo.GetScheduleConflictsAsync();
                if (!conflicts.Any()) return;

                foreach (var conflict in conflicts)
                {
                    _logger.LogWarning("Обнаружен конфликт расписания на станке {MachineName}: {ConflictType}",
                        conflict.MachineName, conflict.ConflictType);

                    // Пытаемся переназначить этапы с наименьшим приоритетом
                    var stageToReassign = conflict.ConflictingStages
                        .OrderBy(s => s.Priority)
                        .FirstOrDefault();

                    if (stageToReassign != null)
                    {
                        // Ищем альтернативный станок
                        var availableMachines = await _batchRepo.GetAvailableMachinesForStageAsync(stageToReassign.Id);
                        var alternativeMachine = availableMachines
                            .Where(m => m.Id != conflict.MachineId)
                            .OrderByDescending(m => m.Priority)
                            .FirstOrDefault();

                        if (alternativeMachine != null)
                        {
                            // Переназначаем этап на другой станок
                            await ReassignStageToMachineAsync(stageToReassign.Id, alternativeMachine.Id);
                            _logger.LogInformation("Этап {StageId} переназначен с {OldMachine} на {NewMachine} для разрешения конфликта",
                                stageToReassign.Id, conflict.MachineName, alternativeMachine.Name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при разрешении конфликтов расписания");
            }
        }

        // *** ИСПРАВЛЕНИЕ: Предсказывает оптимальное время для запуска производства новой детали ***
        public async Task<PredictedScheduleDto> PredictOptimalScheduleForDetailAsync(int detailId, int quantity)
        {
            try
            {
                // Получаем маршрут для детали
                var route = await _routeRepo.GetByDetailIdAsync(detailId);
                if (route == null) throw new Exception($"Маршрут для детали {detailId} не найден");

                // Получаем текущее расписание станков
                var startDate = DateTime.UtcNow;
                var endDate = startDate.AddDays(14); // Рассматриваем две недели вперед
                var machineSchedule = await _batchRepo.GetMachineScheduleAsync(startDate, endDate);

                // Рассчитываем прогноз для каждого этапа маршрута
                var stageForecasts = new List<StageForecastDto>();
                DateTime? earliestStartTime = startDate;
                DateTime? latestEndTime = startDate;

                foreach (var routeStage in route.Stages.OrderBy(s => s.Order))
                {
                    // Получаем подходящие станки для этапа
                    var machineTypeId = routeStage.MachineTypeId;
                    var machines = await _machineRepo.GetMachinesByTypeAsync(machineTypeId);

                    // Находим оптимальный станок и время для этапа
                    var bestMachineId = 0;
                    var bestStartTime = DateTime.MaxValue;
                    var bestEndTime = DateTime.MaxValue;
                    var needsSetup = false;

                    foreach (var machine in machines)
                    {
                        // Оцениваем, когда станок будет доступен
                        var machineReleaseTime = await CalculateMachineReleaseTimeAsync(machine.Id);

                        // Проверяем, потребуется ли переналадка
                        var lastDetailOnMachine = await _setupTimeRepo.GetLastDetailOnMachineAsync(machine.Id);
                        var setupNeeded = lastDetailOnMachine != null && lastDetailOnMachine.Id != detailId;
                        var setupTime = setupNeeded ? await _setupTimeRepo.GetSetupTimeAsync(machine.Id, lastDetailOnMachine?.Id ?? 0, detailId) : null;

                        // Рассчитываем время этапа
                        var setupDuration = setupNeeded ? TimeSpan.FromHours(setupTime?.Time ?? routeStage.SetupTime) : TimeSpan.Zero;
                        var operationDuration = TimeSpan.FromHours(routeStage.NormTime * quantity);

                        // Начало этапа - максимум из времени освобождения станка и времени завершения предыдущего этапа
                        var stageStartTime = machineReleaseTime > earliestStartTime ? machineReleaseTime : earliestStartTime.Value;

                        // Окончание этапа - начало + время переналадки + время операции
                        var stageEndTime = stageStartTime.Add(setupDuration).Add(operationDuration);

                        // Если это лучший вариант, запоминаем его
                        if (stageEndTime < bestEndTime)
                        {
                            bestMachineId = machine.Id;
                            bestStartTime = stageStartTime;
                            bestEndTime = stageEndTime;
                            needsSetup = setupNeeded;
                        }
                    }

                    // Добавляем прогноз для этапа
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

                    // Обновляем время начала следующего этапа
                    earliestStartTime = bestEndTime;

                    // Обновляем время завершения всего процесса
                    if (bestEndTime > latestEndTime)
                        latestEndTime = bestEndTime;
                }

                // Возвращаем прогноз
                return new PredictedScheduleDto
                {
                    DetailId = detailId,
                    Quantity = quantity,
                    EarliestStartTime = startDate,
                    LatestEndTime = latestEndTime.Value,
                    TotalDuration = latestEndTime.Value - startDate,
                    StageForecasts = stageForecasts
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при предсказании расписания для детали {DetailId}", detailId);
                throw;
            }
        }
    }
}