using Project.Contracts.ModelDTO;
using Project.Domain.Entities;
using Project.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class PlanningService
    {
        private readonly IBatchRepository _batchRepo;
        private readonly IDetailRepository _detailRepo;
        private readonly IMachineRepository _machineRepo;
        private readonly IRouteRepository _routeRepo;
        private readonly ProductionSchedulerService _schedulerService;

        public PlanningService(
            IBatchRepository batchRepo,
            IDetailRepository detailRepo,
            IMachineRepository machineRepo,
            IRouteRepository routeRepo,
            ProductionSchedulerService schedulerService)
        {
            _batchRepo = batchRepo;
            _detailRepo = detailRepo;
            _machineRepo = machineRepo;
            _routeRepo = routeRepo;
            _schedulerService = schedulerService;
        }

        // Получение сводки по производственному плану
        public async Task<PlanSummaryDto> GetPlanSummaryAsync(DateTime startDate, DateTime endDate)
        {
            // Получаем все партии
            var batches = await _batchRepo.GetAllAsync();

            // Фильтруем только те, которые были созданы в указанном периоде
            var batchesInPeriod = batches
                .Where(b => b.CreatedUtc >= startDate && b.CreatedUtc <= endDate)
                .ToList();

            // Получаем все этапы выполнения
            var allStages = new List<StageExecution>();
            foreach (var batch in batchesInPeriod)
            {
                foreach (var subBatch in batch.SubBatches)
                {
                    allStages.AddRange(subBatch.StageExecutions);
                }
            }

            // Группируем по деталям
            var detailGroups = batchesInPeriod
                .GroupBy(b => b.DetailId)
                .Select(g => new
                {
                    DetailId = g.Key,
                    DetailName = g.First().Detail?.Name ?? "Неизвестная деталь",
                    Quantity = g.Sum(b => b.Quantity),
                    CompletedQuantity = g.Sum(b =>
                        // Если все этапы подпартии завершены, считаем детали как произведенные
                        b.SubBatches.Sum(sb =>
                            sb.StageExecutions.All(se => se.Status == StageExecutionStatus.Completed)
                            ? sb.Quantity : 0)
                    )
                })
                .ToList();

            // Группируем по станкам
            var machineGroups = allStages
                .Where(s => s.MachineId.HasValue)
                .GroupBy(s => s.MachineId.Value)
                .Select(g => new
                {
                    MachineId = g.Key,
                    MachineName = g.First().Machine?.Name ?? "Неизвестный станок",
                    TotalHours = g.Sum(s =>
                        (s.EndTimeUtc.HasValue && s.StartTimeUtc.HasValue) ?
                        (s.EndTimeUtc.Value - s.StartTimeUtc.Value).TotalHours : 0
                    ),
                    SetupHours = g.Where(s => s.IsSetup).Sum(s =>
                        (s.EndTimeUtc.HasValue && s.StartTimeUtc.HasValue) ?
                        (s.EndTimeUtc.Value - s.StartTimeUtc.Value).TotalHours : 0
                    ),
                    StageCount = g.Count(),
                    CompletedStageCount = g.Count(s => s.Status == StageExecutionStatus.Completed)
                })
                .ToList();

            // Создаем сводку
            var summary = new PlanSummaryDto
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalBatches = batchesInPeriod.Count,
                TotalQuantity = batchesInPeriod.Sum(b => b.Quantity),
                CompletedQuantity = detailGroups.Sum(g => g.CompletedQuantity),
                DetailStats = detailGroups.Select(g => new DetailPlanStatsDto
                {
                    DetailId = g.DetailId,
                    DetailName = g.DetailName,
                    PlannedQuantity = g.Quantity,
                    CompletedQuantity = g.CompletedQuantity
                }).ToList(),
                MachineStats = machineGroups.Select(g => new MachinePlanStatsDto
                {
                    MachineId = g.MachineId,
                    MachineName = g.MachineName,
                    TotalHours = Math.Round(g.TotalHours, 2),
                    SetupHours = Math.Round(g.SetupHours, 2),
                    StageCount = g.StageCount,
                    CompletedStageCount = g.CompletedStageCount,
                    EfficiencyPercent = g.TotalHours > 0 ?
                        Math.Round(((g.TotalHours - g.SetupHours) / g.TotalHours) * 100, 1) : 0
                }).ToList()
            };

            return summary;
        }

        // Создание плана производства на основе деталей и количества
        public async Task<int> CreateProductionPlanAsync(List<PlanItemDto> items, string planName, DateTime targetDate)
        {
            int batchCount = 0;

            foreach (var item in items)
            {
                // Проверяем, что деталь существует
                var detail = await _detailRepo.GetByIdAsync(item.DetailId);
                if (detail == null)
                    throw new Exception($"Деталь с ID {item.DetailId} не найдена");

                // Проверяем, что у детали есть маршрут
                var route = await _routeRepo.GetByDetailIdAsync(item.DetailId);
                if (route == null)
                    throw new Exception($"Маршрут для детали {detail.Name} не найден");

                // Создаем партию
                var batchDto = new BatchCreateDto
                {
                    DetailId = item.DetailId,
                    Quantity = item.Quantity,
                    SubBatches = new List<SubBatchCreateDto>()
                };

                // Если указано деление на подпартии
                if (item.SubBatchSizes != null && item.SubBatchSizes.Any())
                {
                    foreach (var size in item.SubBatchSizes)
                    {
                        batchDto.SubBatches.Add(new SubBatchCreateDto
                        {
                            Quantity = size
                        });
                    }

                    // Проверяем, что сумма подпартий равна общему количеству
                    int totalSubBatchQuantity = item.SubBatchSizes.Sum();
                    if (totalSubBatchQuantity != item.Quantity)
                        throw new Exception($"Сумма количеств в подпартиях ({totalSubBatchQuantity}) не равна общему количеству ({item.Quantity}) для детали {detail.Name}");
                }
                else if (item.OptimalBatchSize > 0)
                {
                    // Автоматически делим на подпартии по оптимальному размеру
                    int remainingQuantity = item.Quantity;
                    while (remainingQuantity > 0)
                    {
                        int subBatchSize = Math.Min(remainingQuantity, item.OptimalBatchSize);
                        batchDto.SubBatches.Add(new SubBatchCreateDto
                        {
                            Quantity = subBatchSize
                        });
                        remainingQuantity -= subBatchSize;
                    }
                }
                else
                {
                    // Одна подпартия на весь объем
                    batchDto.SubBatches.Add(new SubBatchCreateDto
                    {
                        Quantity = item.Quantity
                    });
                }

                // Создаем партию и запускаем планирование
                await _schedulerService.CreateBatchAsync(batchDto);
                batchCount++;
            }

            return batchCount;
        }

        // Расчет оптимального размера партии
        public async Task<OptimalBatchSizeDto> CalculateOptimalBatchSizeAsync(int detailId)
        {
            var detail = await _detailRepo.GetByIdAsync(detailId);
            if (detail == null)
                throw new Exception($"Деталь с ID {detailId} не найдена");

            var route = await _routeRepo.GetByDetailIdAsync(detailId);
            if (route == null)
                throw new Exception($"Маршрут для детали {detail.Name} не найден");

            // Получаем историю производства этой детали
            var history = await _batchRepo.GetStageExecutionHistoryAsync(
                detailId: detailId,
                startDate: DateTime.UtcNow.AddMonths(-3)); // за последние 3 месяца

            // Группируем по размерам подпартий
            var batchSizeGroups = history
                .GroupBy(s => s.SubBatch.Quantity)
                .Select(g => new
                {
                    BatchSize = g.Key,
                    Count = g.Select(s => s.SubBatchId).Distinct().Count(),
                    AvgDuration = g.Where(s => s.EndTimeUtc.HasValue && s.StartTimeUtc.HasValue)
                        .Average(s => (s.EndTimeUtc.Value - s.StartTimeUtc.Value).TotalHours),
                    SetupTime = g.Where(s => s.IsSetup && s.EndTimeUtc.HasValue && s.StartTimeUtc.HasValue)
                        .Average(s => (s.EndTimeUtc.Value - s.StartTimeUtc.Value).TotalHours)
                })
                .OrderByDescending(g => g.Count)
                .ToList();

            int optimalSize = 0;
            double minTotalTime = double.MaxValue;

            // Если есть история, определяем оптимальный размер
            if (batchSizeGroups.Any())
            {
                optimalSize = batchSizeGroups.First().BatchSize;
            }
            else
            {
                // Если истории нет, рассчитываем на основе маршрута

                // Получаем норму времени и время переналадки
                double totalNormTime = route.Stages.Sum(s => s.NormTime);
                double totalSetupTime = route.Stages.Sum(s => s.SetupTime);

                // Вычисляем оптимальный размер партии по формуле экономического размера заказа
                // Предполагаем, что стоимость хранения и стоимость переналадки известны
                const double holdingCost = 0.1; // условный коэффициент стоимости хранения
                double setupCost = totalSetupTime * 100; // условная стоимость переналадки
                double annualDemand = 1000; // годовая потребность (предположение)

                optimalSize = (int)Math.Sqrt((2 * annualDemand * setupCost) / holdingCost);

                // Округляем до ближайшего нормального размера (кратно 5 или 10)
                optimalSize = (int)Math.Ceiling(optimalSize / 10.0) * 10;

                // Обеспечиваем минимальный размер
                optimalSize = Math.Max(10, optimalSize);
            }

            // Рассчитываем общее время изготовления для разных размеров партий
            var batchSizeOptions = new List<BatchSizeOptionDto>();
            int[] sizes = { 10, 20, 50, 100, optimalSize };

            foreach (var size in sizes.Distinct().OrderBy(s => s))
            {
                // Расчет времени на единицу продукции
                double normTimePerUnit = route.Stages.Sum(s => s.NormTime);
                double setupTimePerBatch = route.Stages.Sum(s => s.SetupTime);

                // Время на партию и среднее время на единицу
                double totalTimeForBatch = (normTimePerUnit * size) + setupTimePerBatch;
                double avgTimePerUnit = totalTimeForBatch / size;

                batchSizeOptions.Add(new BatchSizeOptionDto
                {
                    BatchSize = size,
                    TotalTimeForBatch = Math.Round(totalTimeForBatch, 2),
                    SetupTime = Math.Round(setupTimePerBatch, 2),
                    ProcessingTime = Math.Round(normTimePerUnit * size, 2),
                    AvgTimePerUnit = Math.Round(avgTimePerUnit, 4)
                });
            }

            return new OptimalBatchSizeDto
            {
                DetailId = detailId,
                DetailName = detail.Name,
                OptimalBatchSize = optimalSize,
                BatchSizeOptions = batchSizeOptions
            };
        }
    }

    
}