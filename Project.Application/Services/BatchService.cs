using Project.Contracts.ModelDTO;
using Project.Domain.Entities;
using Project.Domain.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class BatchService
    {
        private readonly IBatchRepository _batchRepo;
        private readonly IDetailRepository _detailRepo;
        private readonly IRouteRepository _routeRepo;
        private readonly StageExecutionService _stageService;
        private readonly ILogger<BatchService> _logger;

        public BatchService(
            IBatchRepository batchRepo,
            IDetailRepository detailRepo,
            IRouteRepository routeRepo,
            StageExecutionService stageService,
            ILogger<BatchService> logger)
        {
            _batchRepo = batchRepo;
            _detailRepo = detailRepo;
            _routeRepo = routeRepo;
            _stageService = stageService;
            _logger = logger;
        }

        public async Task<List<BatchDto>> GetAllAsync()
        {
            var batches = await _batchRepo.GetAllAsync();
            return batches.Select(b => new BatchDto
            {
                Id = b.Id,
                DetailId = b.DetailId,
                DetailName = b.Detail?.Name ?? "",
                Quantity = b.Quantity,
                CreatedUtc = b.CreatedUtc,
                SubBatches = b.SubBatches?.Select(sb => new SubBatchDto
                {
                    Id = sb.Id,
                    Quantity = sb.Quantity,
                    StageExecutions = sb.StageExecutions?.Select(se => new StageExecutionDto
                    {
                        Id = se.Id,
                        RouteStageId = se.RouteStageId,
                        StageName = se.RouteStage?.Name ?? "",
                        MachineId = se.MachineId,
                        MachineName = se.Machine?.Name,
                        Status = se.Status.ToString(),
                        StartTimeUtc = se.StartTimeUtc,
                        EndTimeUtc = se.EndTimeUtc,
                        IsSetup = se.IsSetup
                    }).ToList() ?? new List<StageExecutionDto>()
                }).ToList() ?? new List<SubBatchDto>()
            }).ToList();
        }

        public async Task<BatchDto> GetByIdAsync(int id)
        {
            var batch = await _batchRepo.GetByIdAsync(id);
            if (batch == null)
                throw new Exception($"Batch with id {id} not found");

            return new BatchDto
            {
                Id = batch.Id,
                DetailId = batch.DetailId,
                DetailName = batch.Detail?.Name ?? "",
                Quantity = batch.Quantity,
                CreatedUtc = batch.CreatedUtc,
                SubBatches = batch.SubBatches?.Select(sb => new SubBatchDto
                {
                    Id = sb.Id,
                    Quantity = sb.Quantity,
                    StageExecutions = sb.StageExecutions?.Select(se => new StageExecutionDto
                    {
                        Id = se.Id,
                        RouteStageId = se.RouteStageId,
                        StageName = se.RouteStage?.Name ?? "",
                        MachineId = se.MachineId,
                        MachineName = se.Machine?.Name,
                        Status = se.Status.ToString(),
                        StartTimeUtc = se.StartTimeUtc,
                        EndTimeUtc = se.EndTimeUtc,
                        IsSetup = se.IsSetup
                    }).ToList() ?? new List<StageExecutionDto>()
                }).ToList() ?? new List<SubBatchDto>()
            };
        }

        /// <summary>
        /// ИСПРАВЛЕННОЕ создание партии с валидацией
        /// </summary>
        public async Task<int> CreateAsync(BatchCreateDto dto)
        {
            try
            {
                _logger.LogInformation("Создание партии для детали {DetailId}, количество: {Quantity}",
                    dto.DetailId, dto.Quantity);

                // Проверяем, что деталь существует
                var detail = await _detailRepo.GetByIdAsync(dto.DetailId);
                if (detail == null)
                    throw new Exception($"Деталь с ID {dto.DetailId} не найдена");

                // Проверяем, что у детали есть маршрут
                var route = await _routeRepo.GetByDetailIdAsync(dto.DetailId);
                if (route == null)
                    throw new Exception($"Маршрут для детали '{detail.Name}' не найден");

                if (route.Stages == null || !route.Stages.Any())
                    throw new Exception($"Маршрут детали '{detail.Name}' не содержит этапов");

                // Создаем партию
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
                    // Валидация подпартий
                    var totalSubQuantity = dto.SubBatches.Sum(sb => sb.Quantity);
                    if (totalSubQuantity != dto.Quantity)
                        throw new Exception($"Сумма подпартий ({totalSubQuantity}) не равна общему количеству ({dto.Quantity})");

                    if (dto.SubBatches.Any(sb => sb.Quantity <= 0))
                        throw new Exception("Количество в подпартиях должно быть больше 0");

                    foreach (var sbDto in dto.SubBatches)
                    {
                        batch.SubBatches.Add(new SubBatch
                        {
                            Quantity = sbDto.Quantity,
                            StageExecutions = new List<StageExecution>()
                        });
                    }
                }

                await _batchRepo.AddAsync(batch);

                // Генерируем этапы маршрута для каждой подпартии
                foreach (var subBatch in batch.SubBatches)
                {
                    await GenerateStageExecutionsForSubBatch(subBatch.Id, route);
                }

                _logger.LogInformation("Партия {BatchId} успешно создана с {SubBatchCount} подпартиями",
                    batch.Id, batch.SubBatches.Count);

                return batch.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании партии для детали {DetailId}", dto.DetailId);
                throw;
            }
        }

        public async Task UpdateAsync(BatchEditDto dto)
        {
            var entity = await _batchRepo.GetByIdAsync(dto.Id);
            if (entity == null) throw new Exception("Batch not found");

            // Проверяем, можно ли изменять партию
            var hasStartedStages = entity.SubBatches.Any(sb =>
                sb.StageExecutions.Any(se =>
                    se.Status == StageExecutionStatus.InProgress ||
                    se.Status == StageExecutionStatus.Completed ||
                    se.Status == StageExecutionStatus.Paused));

            if (hasStartedStages)
                throw new Exception("Нельзя изменить партию, в которой уже начато выполнение этапов");

            entity.Quantity = dto.Quantity;

            // Для подпартий, которые еще не начали выполняться
            foreach (var sbDto in dto.SubBatches ?? new List<SubBatchEditDto>())
            {
                var subBatch = entity.SubBatches.FirstOrDefault(sb => sb.Id == sbDto.Id);
                if (subBatch != null)
                {
                    var canUpdate = !subBatch.StageExecutions.Any(se =>
                        se.Status == StageExecutionStatus.InProgress ||
                        se.Status == StageExecutionStatus.Completed ||
                        se.Status == StageExecutionStatus.Paused);

                    if (canUpdate)
                    {
                        subBatch.Quantity = sbDto.Quantity;
                    }
                }
            }

            await _batchRepo.UpdateAsync(entity);
        }

        public async Task DeleteAsync(int id)
        {
            var batch = await _batchRepo.GetByIdAsync(id);
            if (batch == null) throw new Exception("Batch not found");

            // Проверяем, что ни один этап партии еще не начал выполняться
            var canDelete = !batch.SubBatches.Any(sb =>
                sb.StageExecutions.Any(se =>
                    se.Status == StageExecutionStatus.InProgress ||
                    se.Status == StageExecutionStatus.Completed ||
                    se.Status == StageExecutionStatus.Paused));

            if (!canDelete)
                throw new Exception("Нельзя удалить партию, в которой уже начато выполнение этапов");

            await _batchRepo.DeleteAsync(id);
        }

        /// <summary>
        /// ИСПРАВЛЕННАЯ генерация этапов выполнения для подпартии на основе маршрута
        /// </summary>
        private async Task GenerateStageExecutionsForSubBatch(int subBatchId, Route route)
        {
            try
            {
                var subBatch = await _batchRepo.GetSubBatchByIdAsync(subBatchId);
                if (subBatch == null) throw new Exception($"SubBatch {subBatchId} not found");

                var currentTime = DateTime.UtcNow;

                _logger.LogDebug("Генерация этапов для подпартии {SubBatchId}, маршрут содержит {StageCount} этапов",
                    subBatchId, route.Stages.Count);

                // Создаем этапы выполнения в той же последовательности, что и в маршруте
                foreach (var routeStage in route.Stages.OrderBy(s => s.Order))
                {
                    var stageExecution = new StageExecution
                    {
                        SubBatchId = subBatchId,
                        RouteStageId = routeStage.Id,
                        Status = StageExecutionStatus.Pending,
                        IsSetup = false, // это основной этап, не переналадка
                        StatusChangedTimeUtc = currentTime,
                        CreatedUtc = currentTime,
                        Priority = 0,
                        IsCritical = false,
                        IsProcessedByScheduler = false,
                        PlannedStartTimeUtc = null, // Будет установлено при планировании
                        LastUpdatedUtc = currentTime
                    };

                    subBatch.StageExecutions.Add(stageExecution);
                }

                await _batchRepo.UpdateSubBatchAsync(subBatch);

                _logger.LogInformation("Создано {Count} этапов для подпартии {SubBatchId}",
                    route.Stages.Count, subBatchId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при генерации этапов для подпартии {SubBatchId}", subBatchId);
                throw;
            }
        }

        // Получение всех этапов выполнения для партии
        public async Task<List<StageExecutionDto>> GetStageExecutionsForBatchAsync(int batchId)
        {
            var batch = await _batchRepo.GetByIdAsync(batchId);
            if (batch == null)
                throw new Exception($"Batch with id {batchId} not found");

            var result = new List<StageExecutionDto>();

            foreach (var subBatch in batch.SubBatches)
            {
                foreach (var se in subBatch.StageExecutions.OrderBy(s => s.RouteStage.Order).ThenBy(s => s.IsSetup ? 0 : 1))
                {
                    result.Add(new StageExecutionDto
                    {
                        Id = se.Id,
                        RouteStageId = se.RouteStageId,
                        StageName = se.IsSetup ? $"Переналадка: {se.RouteStage?.Name}" : se.RouteStage?.Name ?? "",
                        MachineId = se.MachineId,
                        MachineName = se.Machine?.Name,
                        Status = se.Status.ToString(),
                        StartTimeUtc = se.StartTimeUtc,
                        EndTimeUtc = se.EndTimeUtc,
                        IsSetup = se.IsSetup
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Получение детальной статистики по партии
        /// </summary>
        public async Task<BatchStatisticsDto> GetBatchStatisticsAsync(int batchId)
        {
            var batch = await _batchRepo.GetByIdAsync(batchId);
            if (batch == null)
                throw new Exception($"Batch with id {batchId} not found");

            var allStages = batch.SubBatches.SelectMany(sb => sb.StageExecutions).ToList();
            var mainStages = allStages.Where(s => !s.IsSetup).ToList();
            var setupStages = allStages.Where(s => s.IsSetup).ToList();

            var totalWorkTime = mainStages
                .Where(s => s.ActualWorkingTime.HasValue)
                .Sum(s => s.ActualWorkingTime.Value.TotalHours);

            var totalSetupTime = setupStages
                .Where(s => s.ActualWorkingTime.HasValue)
                .Sum(s => s.ActualWorkingTime.Value.TotalHours);

            var plannedTotalTime = mainStages.Sum(s => s.PlannedDuration.TotalHours);

            return new BatchStatisticsDto
            {
                BatchId = batchId,
                DetailName = batch.Detail?.Name ?? "",
                TotalQuantity = batch.Quantity,
                TotalStages = allStages.Count,
                MainStages = mainStages.Count,
                SetupStages = setupStages.Count,
                CompletedStages = allStages.Count(s => s.Status == StageExecutionStatus.Completed),
                InProgressStages = allStages.Count(s => s.Status == StageExecutionStatus.InProgress),
                PendingStages = allStages.Count(s => s.Status == StageExecutionStatus.Pending),
                QueuedStages = allStages.Count(s => s.Status == StageExecutionStatus.Waiting),
                PausedStages = allStages.Count(s => s.Status == StageExecutionStatus.Paused),
                ErrorStages = allStages.Count(s => s.Status == StageExecutionStatus.Error),
                OverdueStages = allStages.Count(s => s.IsOverdue),
                TotalWorkHours = Math.Round(totalWorkTime, 2),
                TotalSetupHours = Math.Round(totalSetupTime, 2),
                PlannedTotalHours = Math.Round(plannedTotalTime, 2),
                EfficiencyPercentage = plannedTotalTime > 0 ? Math.Round((plannedTotalTime / (totalWorkTime + totalSetupTime)) * 100, 1) : 0,
                CompletionPercentage = allStages.Count > 0 ? Math.Round((double)allStages.Count(s => s.Status == StageExecutionStatus.Completed) / allStages.Count * 100, 1) : 0,
                EstimatedCompletionTime = await _batchRepo.GetEstimatedCompletionTimeForBatchAsync(batchId)
            };
        }
    }

    /// <summary>
    /// DTO для статистики партии
    /// </summary>
    public class BatchStatisticsDto
    {
        public int BatchId { get; set; }
        public string DetailName { get; set; }
        public int TotalQuantity { get; set; }
        public int TotalStages { get; set; }
        public int MainStages { get; set; }
        public int SetupStages { get; set; }
        public int CompletedStages { get; set; }
        public int InProgressStages { get; set; }
        public int PendingStages { get; set; }
        public int QueuedStages { get; set; }
        public int PausedStages { get; set; }
        public int ErrorStages { get; set; }
        public int OverdueStages { get; set; }
        public double TotalWorkHours { get; set; }
        public double TotalSetupHours { get; set; }
        public double PlannedTotalHours { get; set; }
        public double EfficiencyPercentage { get; set; }
        public double CompletionPercentage { get; set; }
        public DateTime? EstimatedCompletionTime { get; set; }
    }
}