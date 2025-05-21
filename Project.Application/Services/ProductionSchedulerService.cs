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

        public ProductionSchedulerService(
            IBatchRepository batchRepo,
            IMachineRepository machineRepo,
            IRouteRepository routeRepo,
            ISetupTimeRepository setupTimeRepo,
            StageExecutionService stageService)
        {
            _batchRepo = batchRepo;
            _machineRepo = machineRepo;
            _routeRepo = routeRepo;
            _setupTimeRepo = setupTimeRepo;
            _stageService = stageService;
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

        // Планирование конкретного этапа
        public async Task ScheduleStageExecutionAsync(int stageExecutionId)
        {
            var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
            if (stageExecution == null) throw new Exception("Stage execution not found");

            // Если этап уже назначен на станок или уже выполняется - не планируем
            if (stageExecution.MachineId.HasValue ||
                stageExecution.Status != StageExecutionStatus.Pending)
                return;

            // Получаем подходящий тип станка для этого этапа
            var requiredMachineTypeId = stageExecution.RouteStage.MachineTypeId;

            // Получаем список доступных станков нужного типа
            var availableMachines = await _machineRepo.GetAvailableMachinesAsync(requiredMachineTypeId);

            // Если нет доступных станков, ставим в очередь ожидания
            if (!availableMachines.Any())
            {
                stageExecution.Status = StageExecutionStatus.Waiting;
                await _batchRepo.UpdateStageExecutionAsync(stageExecution);
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

                // Проверяем, все ли предыдущие этапы завершены
                var allPreviousCompleted = await CheckAllPreviousStagesCompletedAsync(stageExecution);

                // Автоматически стартуем этап, если все предыдущие завершены
                if (allPreviousCompleted && stageExecution.Status == StageExecutionStatus.Pending)
                {
                    await _stageService.StartStageExecution(stageExecutionId);
                }
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
            if (currentStage == null) return; // станок свободен, просто планируем новый этап

            // Ставим текущий этап на паузу
            await _stageService.PauseStageExecution(currentStage.Id);

            // Получаем приоритетный этап
            var priorityStage = await _batchRepo.GetStageExecutionByIdAsync(priorityStageExecutionId);
            if (priorityStage == null) throw new Exception("Priority stage not found");

            // Назначаем приоритетный этап на освободившийся станок
            await _stageService.AssignStageToMachine(priorityStageExecutionId, machineId);

            // Автоматически стартуем приоритетный этап
            await _stageService.StartStageExecution(priorityStageExecutionId);
        }

        // Переназначение этапа на другой станок
        public async Task ReassignStageToMachineAsync(int stageExecutionId, int newMachineId)
        {
            var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
            if (stageExecution == null) throw new Exception("Stage execution not found");

            // Если этап выполняется, сначала ставим на паузу
            if (stageExecution.Status == StageExecutionStatus.InProgress)
            {
                await _stageService.PauseStageExecution(stageExecutionId);
            }

            // Сохраняем текущий статус перед переназначением
            var currentStatus = stageExecution.Status;

            // Переназначаем на новый станок
            await _stageService.AssignStageToMachine(stageExecutionId, newMachineId);

            // Если этап был в процессе, возобновляем на новом станке
            if (currentStatus == StageExecutionStatus.InProgress)
            {
                await _stageService.StartStageExecution(stageExecutionId);
            }
        }

        // Обработка события завершения этапа - планирование следующего
        public async Task HandleStageCompletionAsync(int stageExecutionId)
        {
            var stageExecution = await _batchRepo.GetStageExecutionByIdAsync(stageExecutionId);
            if (stageExecution == null) throw new Exception("Stage execution not found");

            // Отмечаем этап как завершенный
            await _stageService.CompleteStageExecution(stageExecutionId);

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

            // Проверяем, есть ли этапы в очереди на этот станок
            if (stageExecution.MachineId.HasValue)
            {
                var nextInQueue = await _batchRepo.GetNextStageInQueueForMachineAsync(stageExecution.MachineId.Value);
                if (nextInQueue != null)
                {
                    // Переводим этап из "Waiting" в "Pending"
                    nextInQueue.Status = StageExecutionStatus.Pending;
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
    }

    
}