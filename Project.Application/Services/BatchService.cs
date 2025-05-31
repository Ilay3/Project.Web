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
        private readonly ILogger<BatchService> _logger;

        public BatchService(
            IBatchRepository batchRepo,
            IDetailRepository detailRepo,
            IRouteRepository routeRepo,
            ILogger<BatchService> logger)
        {
            _batchRepo = batchRepo;
            _detailRepo = detailRepo;
            _routeRepo = routeRepo;
            _logger = logger;
        }

        public async Task<List<BatchDto>> GetAllAsync()
        {
            var batches = await _batchRepo.GetAllAsync();
            return batches.Select(b => MapToBatchDto(b)).ToList();
        }

        public async Task<BatchDto> GetByIdAsync(int id)
        {
            var batch = await _batchRepo.GetByIdAsync(id);
            if (batch == null)
                throw new Exception($"Batch with id {id} not found");

            return MapToBatchDto(batch);
        }

        /// <summary>
        /// Создание партии с валидацией согласно ТЗ
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

                // Создаем подпартии согласно ТЗ
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

                // Сохраняем партию в БД
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

            // Проверяем, можно ли изменять партию согласно ТЗ
            var hasStartedStages = entity.SubBatches.Any(sb =>
                sb.StageExecutions.Any(se =>
                    se.Status == StageExecutionStatus.InProgress ||
                    se.Status == StageExecutionStatus.Completed ||
                    se.Status == StageExecutionStatus.Paused));

            if (hasStartedStages)
                throw new Exception("Нельзя изменить партию, в которой уже начато выполнение этапов");

            entity.Quantity = dto.Quantity;

            // Обновляем подпартии, которые еще не начали выполняться
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

            // Проверяем, что ни один этап партии еще не начал выполняться согласно ТЗ
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
        /// Генерация этапов выполнения для подпартии на основе маршрута согласно ТЗ
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
                        Status = StageExecutionStatus.Pending, // Статус согласно ТЗ: "Ожидает запуска"
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

        /// <summary>
        /// Получение всех этапов выполнения для партии
        /// </summary>
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
                    result.Add(MapToStageExecutionDto(se));
                }
            }

            return result;
        }

        /// <summary>
        /// Получение детальной статистики по партии согласно ТЗ
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

        #region Приватные методы маппинга

        private BatchDto MapToBatchDto(Batch batch)
        {
            return new BatchDto
            {
                Id = batch.Id,
                DetailId = batch.DetailId,
                DetailName = batch.Detail?.Name ?? "",
                DetailNumber = batch.Detail?.Number ?? "",
                Quantity = batch.Quantity,
                CreatedUtc = batch.CreatedUtc,
                SubBatches = batch.SubBatches?.Select(sb => MapToSubBatchDto(sb)).ToList() ?? new List<SubBatchDto>(),
                TotalPlannedTimeHours = CalculateTotalPlannedTime(batch),
                TotalActualTimeHours = CalculateTotalActualTime(batch),
                CompletionPercentage = CalculateCompletionPercentage(batch),
                EstimatedCompletionTimeUtc = null, // Можно добавить расчет
                StageStatistics = CalculateStageStatistics(batch),
                CanEdit = CanEditBatch(batch),
                CanDelete = CanDeleteBatch(batch)
            };
        }

        private SubBatchDto MapToSubBatchDto(SubBatch subBatch)
        {
            return new SubBatchDto
            {
                Id = subBatch.Id,
                BatchId = subBatch.BatchId,
                Quantity = subBatch.Quantity,
                StageExecutions = subBatch.StageExecutions?.Select(se => MapToStageExecutionDto(se)).ToList() ?? new List<StageExecutionDto>(),
                CompletionPercentage = CalculateSubBatchCompletion(subBatch),
                CurrentStageExecutionId = GetCurrentStageId(subBatch),
                CurrentStageName = GetCurrentStageName(subBatch),
                NextStageExecutionId = GetNextStageId(subBatch),
                NextStageName = GetNextStageName(subBatch)
            };
        }

        private StageExecutionDto MapToStageExecutionDto(StageExecution se)
        {
            return new StageExecutionDto
            {
                Id = se.Id,
                SubBatchId = se.SubBatchId,
                RouteStageId = se.RouteStageId,
                StageName = se.IsSetup ? $"Переналадка: {se.RouteStage?.Name}" : se.RouteStage?.Name ?? "",
                StageType = se.IsSetup ? "Setup" : "Operation",
                MachineId = se.MachineId,
                MachineName = se.Machine?.Name,
                Status = MapStageExecutionStatus(se.Status),
                IsSetup = se.IsSetup,
                Priority = MapPriority(se.Priority),
                IsCritical = se.IsCritical,
                PlannedStartTimeUtc = se.PlannedStartTimeUtc,
                PlannedEndTimeUtc = se.PlannedStartTimeUtc?.Add(se.PlannedDuration),
                StartTimeUtc = se.StartTimeUtc,
                EndTimeUtc = se.EndTimeUtc,
                PauseTimeUtc = se.PauseTimeUtc,
                ResumeTimeUtc = se.ResumeTimeUtc,
                PlannedDurationHours = se.PlannedDuration.TotalHours,
                ActualDurationHours = se.ActualWorkingTime?.TotalHours,
                DeviationHours = se.TimeDeviation?.TotalHours,
                CompletionPercentage = se.CompletionPercentage,
                QueuePosition = se.QueuePosition,
                Quantity = se.SubBatch?.Quantity ?? 0,
                OperatorId = se.OperatorId,
                ReasonNote = se.ReasonNote,
                DeviceId = se.DeviceId,
                CreatedUtc = se.CreatedUtc,
                StatusChangedTimeUtc = se.StatusChangedTimeUtc,
                IsOverdue = se.IsOverdue,
                CanStart = se.CanStart,
                DetailName = se.SubBatch?.Batch?.Detail?.Name ?? "",
                BatchId = se.SubBatch?.BatchId ?? 0,
                SetupStageId = se.SetupStageId,
                MainStageId = se.MainStageId
            };
        }

        // Вспомогательные методы расчета статистики
        private double CalculateTotalPlannedTime(Batch batch)
        {
            return batch.SubBatches
                .SelectMany(sb => sb.StageExecutions.Where(se => !se.IsSetup))
                .Sum(se => se.PlannedDuration.TotalHours);
        }

        private double? CalculateTotalActualTime(Batch batch)
        {
            var actualTimes = batch.SubBatches
                .SelectMany(sb => sb.StageExecutions)
                .Where(se => se.ActualWorkingTime.HasValue)
                .Select(se => se.ActualWorkingTime!.Value.TotalHours)
                .ToList();

            return actualTimes.Any() ? actualTimes.Sum() : null;
        }

        private decimal CalculateCompletionPercentage(Batch batch)
        {
            var allStages = batch.SubBatches.SelectMany(sb => sb.StageExecutions.Where(se => !se.IsSetup)).ToList();
            if (!allStages.Any()) return 0;

            var completedStages = allStages.Count(se => se.Status == StageExecutionStatus.Completed);
            return Math.Round((decimal)completedStages / allStages.Count * 100, 1);
        }

        private BatchStageStatisticsDto CalculateStageStatistics(Batch batch)
        {
            var allStages = batch.SubBatches.SelectMany(sb => sb.StageExecutions).ToList();

            return new BatchStageStatisticsDto
            {
                TotalStages = allStages.Count,
                AwaitingStartStages = allStages.Count(s => s.Status == StageExecutionStatus.Pending),
                InQueueStages = allStages.Count(s => s.Status == StageExecutionStatus.Waiting),
                InProgressStages = allStages.Count(s => s.Status == StageExecutionStatus.InProgress),
                PausedStages = allStages.Count(s => s.Status == StageExecutionStatus.Paused),
                CompletedStages = allStages.Count(s => s.Status == StageExecutionStatus.Completed),
                CancelledStages = allStages.Count(s => s.Status == StageExecutionStatus.Error),
                SetupStages = allStages.Count(s => s.IsSetup),
                OverdueStages = allStages.Count(s => s.IsOverdue)
            };
        }

        private bool CanEditBatch(Batch batch)
        {
            return !batch.SubBatches.Any(sb =>
                sb.StageExecutions.Any(se =>
                    se.Status == StageExecutionStatus.InProgress ||
                    se.Status == StageExecutionStatus.Completed));
        }

        private bool CanDeleteBatch(Batch batch)
        {
            return !batch.SubBatches.Any(sb =>
                sb.StageExecutions.Any(se =>
                    se.Status != StageExecutionStatus.Pending &&
                    se.Status != StageExecutionStatus.Error));
        }

        private decimal CalculateSubBatchCompletion(SubBatch subBatch)
        {
            var stages = subBatch.StageExecutions.Where(se => !se.IsSetup).ToList();
            if (!stages.Any()) return 0;

            var completed = stages.Count(se => se.Status == StageExecutionStatus.Completed);
            return Math.Round((decimal)completed / stages.Count * 100, 1);
        }

        private int? GetCurrentStageId(SubBatch subBatch)
        {
            return subBatch.StageExecutions
                .Where(se => se.Status == StageExecutionStatus.InProgress)
                .FirstOrDefault()?.Id;
        }

        private string? GetCurrentStageName(SubBatch subBatch)
        {
            var currentStage = subBatch.StageExecutions
                .Where(se => se.Status == StageExecutionStatus.InProgress)
                .FirstOrDefault();
            return currentStage?.RouteStage?.Name;
        }

        private int? GetNextStageId(SubBatch subBatch)
        {
            return subBatch.StageExecutions
                .Where(se => se.Status == StageExecutionStatus.Pending)
                .OrderBy(se => se.RouteStage.Order)
                .FirstOrDefault()?.Id;
        }

        private string? GetNextStageName(SubBatch subBatch)
        {
            var nextStage = subBatch.StageExecutions
                .Where(se => se.Status == StageExecutionStatus.Pending)
                .OrderBy(se => se.RouteStage.Order)
                .FirstOrDefault();
            return nextStage?.RouteStage?.Name;
        }

        private Project.Contracts.Enums.StageStatus MapStageExecutionStatus(StageExecutionStatus status)
        {
            return status switch
            {
                StageExecutionStatus.Pending => Project.Contracts.Enums.StageStatus.AwaitingStart,
                StageExecutionStatus.Waiting => Project.Contracts.Enums.StageStatus.InQueue,
                StageExecutionStatus.InProgress => Project.Contracts.Enums.StageStatus.InProgress,
                StageExecutionStatus.Paused => Project.Contracts.Enums.StageStatus.Paused,
                StageExecutionStatus.Completed => Project.Contracts.Enums.StageStatus.Completed,
                StageExecutionStatus.Error => Project.Contracts.Enums.StageStatus.Cancelled,
                _ => Project.Contracts.Enums.StageStatus.AwaitingStart
            };
        }

        private Project.Contracts.Enums.Priority MapPriority(int priority)
        {
            return priority switch
            {
                <= 2 => Project.Contracts.Enums.Priority.Low,
                <= 6 => Project.Contracts.Enums.Priority.Normal,
                <= 9 => Project.Contracts.Enums.Priority.High,
                _ => Project.Contracts.Enums.Priority.Critical
            };
        }

        #endregion
    }

    /// <summary>
    /// DTO для статистики партии согласно ТЗ
    /// </summary>
    public class BatchStatisticsDto
    {
        public int BatchId { get; set; }
        public string DetailName { get; set; } = string.Empty;
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