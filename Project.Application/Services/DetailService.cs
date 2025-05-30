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
    public class DetailService
    {
        private readonly IDetailRepository _detailRepo;
        private readonly IRouteRepository _routeRepo;
        private readonly IBatchRepository _batchRepo;
        private readonly ILogger<DetailService> _logger;

        public DetailService(
            IDetailRepository detailRepo,
            IRouteRepository routeRepo,
            IBatchRepository batchRepo,
            ILogger<DetailService> logger)
        {
            _detailRepo = detailRepo;
            _routeRepo = routeRepo;
            _batchRepo = batchRepo;
            _logger = logger;
        }

        /// <summary>
        /// Получение всех деталей с расширенной информацией
        /// </summary>
        public async Task<List<DetailDto>> GetAllAsync()
        {
            try
            {
                var details = await _detailRepo.GetAllAsync();
                var result = new List<DetailDto>();

                foreach (var detail in details)
                {
                    var detailDto = await MapToDetailDto(detail);
                    result.Add(detailDto);
                }

                _logger.LogDebug("Получено {Count} деталей", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка деталей");
                throw;
            }
        }

        /// <summary>
        /// Получение детали по ID с полной информацией
        /// </summary>
        public async Task<DetailDto?> GetByIdAsync(int id)
        {
            try
            {
                var detail = await _detailRepo.GetByIdAsync(id);
                if (detail == null)
                {
                    _logger.LogWarning("Деталь с ID {DetailId} не найдена", id);
                    return null;
                }

                return await MapToDetailDto(detail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении детали {DetailId}", id);
                throw;
            }
        }

        /// <summary>
        /// Создание новой детали
        /// </summary>
        public async Task<int> CreateAsync(DetailCreateDto dto)
        {
            try
            {
                // Валидация
                if (string.IsNullOrWhiteSpace(dto.Name))
                    throw new ArgumentException("Название детали обязательно");

                if (string.IsNullOrWhiteSpace(dto.Number))
                    throw new ArgumentException("Номер детали обязателен");

                // Проверяем уникальность номера детали
                var allDetails = await _detailRepo.GetAllAsync();
                if (allDetails.Any(d => d.Number.Equals(dto.Number.Trim(), StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException($"Деталь с номером '{dto.Number}' уже существует");

                var entity = new Detail
                {
                    Name = dto.Name.Trim(),
                    Number = dto.Number.Trim()
                };

                await _detailRepo.AddAsync(entity);

                _logger.LogInformation("Создана деталь: {DetailName} ({DetailNumber})",
                    entity.Name, entity.Number);

                return entity.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании детали {@DetailDto}", dto);
                throw;
            }
        }

        /// <summary>
        /// Обновление детали
        /// </summary>
        public async Task UpdateAsync(DetailEditDto dto)
        {
            try
            {
                var entity = await _detailRepo.GetByIdAsync(dto.Id);
                if (entity == null)
                    throw new ArgumentException($"Деталь с ID {dto.Id} не найдена");

                // Валидация
                if (string.IsNullOrWhiteSpace(dto.Name))
                    throw new ArgumentException("Название детали обязательно");

                if (string.IsNullOrWhiteSpace(dto.Number))
                    throw new ArgumentException("Номер детали обязателен");

                // Проверяем уникальность номера (исключая текущую деталь)
                var allDetails = await _detailRepo.GetAllAsync();
                if (allDetails.Any(d => d.Id != dto.Id &&
                    d.Number.Equals(dto.Number.Trim(), StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException($"Деталь с номером '{dto.Number}' уже существует");

                var oldName = entity.Name;
                var oldNumber = entity.Number;

                entity.Name = dto.Name.Trim();
                entity.Number = dto.Number.Trim();

                await _detailRepo.UpdateAsync(entity);

                _logger.LogInformation("Обновлена деталь {DetailId}: '{OldName}' ({OldNumber}) -> '{NewName}' ({NewNumber})",
                    dto.Id, oldName, oldNumber, entity.Name, entity.Number);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении детали {@DetailDto}", dto);
                throw;
            }
        }

        /// <summary>
        /// Удаление детали
        /// </summary>
        public async Task DeleteAsync(int id)
        {
            try
            {
                var entity = await _detailRepo.GetByIdAsync(id);
                if (entity == null)
                    throw new ArgumentException($"Деталь с ID {id} не найдена");

                // Проверяем, есть ли маршруты для этой детали
                var route = await _routeRepo.GetByDetailIdAsync(id);
                if (route != null)
                    throw new InvalidOperationException($"Нельзя удалить деталь '{entity.Name}' - для неё существует маршрут");

                // Проверяем, есть ли партии для этой детали
                var allBatches = await _batchRepo.GetAllAsync();
                var hasActiveBatches = allBatches.Any(b => b.DetailId == id);
                if (hasActiveBatches)
                    throw new InvalidOperationException($"Нельзя удалить деталь '{entity.Name}' - для неё есть производственные партии");

                await _detailRepo.DeleteAsync(id);

                _logger.LogInformation("Удалена деталь {DetailId}: {DetailName} ({DetailNumber})",
                    id, entity.Name, entity.Number);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении детали {DetailId}", id);
                throw;
            }
        }

        /// <summary>
        /// Получение статистики производства по детали
        /// </summary>
        public async Task<DetailProductionStatisticsDto> GetProductionStatisticsAsync(int detailId, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var detail = await _detailRepo.GetByIdAsync(detailId);
                if (detail == null)
                    throw new ArgumentException($"Деталь с ID {detailId} не найдена");

                startDate ??= DateTime.Today.AddDays(-30);
                endDate ??= DateTime.Today.AddDays(1);

                // Получаем все партии этой детали
                var allBatches = await _batchRepo.GetAllAsync();
                var detailBatches = allBatches.Where(b => b.DetailId == detailId).ToList();

                // Фильтруем по дате
                var filteredBatches = detailBatches.Where(b =>
                    b.CreatedUtc >= startDate && b.CreatedUtc <= endDate).ToList();

                // Получаем все этапы по этим партиям
                var allStages = filteredBatches
                    .SelectMany(b => b.SubBatches)
                    .SelectMany(sb => sb.StageExecutions)
                    .Where(se => !se.IsSetup) // Только основные операции
                    .ToList();

                var completedStages = allStages.Where(s => s.Status == StageExecutionStatus.Completed).ToList();

                // Подсчитываем статистику
                var totalQuantityPlanned = filteredBatches.Sum(b => b.Quantity);
                var totalQuantityCompleted = CalculateCompletedQuantity(filteredBatches);

                var totalPlannedTime = allStages.Sum(s => s.PlannedDuration.TotalHours);
                var totalActualTime = completedStages
                    .Where(s => s.ActualWorkingTime.HasValue)
                    .Sum(s => s.ActualWorkingTime!.Value.TotalHours);

                var averageTimePerUnit = totalQuantityCompleted > 0 ? totalActualTime / totalQuantityCompleted : 0;
                var onTimeDeliveryRate = CalculateOnTimeDeliveryRate(filteredBatches);

                return new DetailProductionStatisticsDto
                {
                    DetailId = detailId,
                    DetailName = detail.Name,
                    DetailNumber = detail.Number,
                    Period = new ProductionPeriodDto
                    {
                        StartDate = startDate.Value,
                        EndDate = endDate.Value
                    },
                    TotalQuantityPlanned = totalQuantityPlanned,
                    TotalQuantityCompleted = totalQuantityCompleted,
                    TotalQuantityInProgress = CalculateInProgressQuantity(filteredBatches),
                    CompletionPercentage = totalQuantityPlanned > 0 ?
                        (decimal)(totalQuantityCompleted * 100.0 / totalQuantityPlanned) : 0,
                    TotalBatches = filteredBatches.Count,
                    CompletedBatches = filteredBatches.Count(b => IsAllStagesCompleted(b)),
                    InProgressBatches = filteredBatches.Count(b => HasInProgressStages(b)),
                    QueuedBatches = filteredBatches.Count(b => HasOnlyPendingStages(b)),
                    TotalPlannedHours = Math.Round(totalPlannedTime, 2),
                    TotalActualHours = Math.Round(totalActualTime, 2),
                    EfficiencyPercentage = totalActualTime > 0 ?
                        (decimal)Math.Round(totalPlannedTime / totalActualTime * 100, 1) : 0,
                    AverageTimePerUnit = Math.Round(averageTimePerUnit, 2),
                    OnTimeDeliveryRate = Math.Round(onTimeDeliveryRate, 1),
                    QualityMetrics = await GetQualityMetrics(detailId, startDate.Value, endDate.Value)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении статистики производства детали {DetailId}", detailId);
                throw;
            }
        }

        /// <summary>
        /// Получение списка деталей для быстрого создания партий
        /// </summary>
        public async Task<List<DetailForBatchDto>> GetDetailsForBatchCreationAsync()
        {
            try
            {
                var details = await _detailRepo.GetAllAsync();
                var result = new List<DetailForBatchDto>();

                foreach (var detail in details)
                {
                    var route = await _routeRepo.GetByDetailIdAsync(detail.Id);
                    if (route != null) // Только детали с маршрутами
                    {
                        // Получаем статистику последних партий
                        var recentBatches = await GetRecentBatchesForDetail(detail.Id, 5);
                        var averageQuantity = recentBatches.Any() ?
                            (int)recentBatches.Average(b => b.Quantity) : 0;

                        result.Add(new DetailForBatchDto
                        {
                            Id = detail.Id,
                            Name = detail.Name,
                            Number = detail.Number,
                            HasRoute = true,
                            RouteStagesCount = route.Stages?.Count ?? 0,
                            LastBatchDate = recentBatches.FirstOrDefault()?.CreatedUtc,
                            AverageQuantity = averageQuantity,
                            EstimatedDuration = CalculateEstimatedDuration(route, averageQuantity)
                        });
                    }
                }

                return result.OrderBy(d => d.Name).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка деталей для создания партий");
                throw;
            }
        }

        #region Приватные методы

        /// <summary>
        /// Маппинг Entity в DTO
        /// </summary>
        private async Task<DetailDto> MapToDetailDto(Detail detail)
        {
            try
            {
                var route = await _routeRepo.GetByDetailIdAsync(detail.Id);
                var statistics = await GetProductionStatisticsAsync(detail.Id,
                    DateTime.Today.AddDays(-30), DateTime.Today.AddDays(1));

                return new DetailDto
                {
                    Id = detail.Id,
                    Name = detail.Name,
                    Number = detail.Number,
                    HasRoute = route != null,
                    RouteId = route?.Id,
                    RouteStagesCount = route?.Stages?.Count ?? 0,
                    TotalQuantityProduced = statistics.TotalQuantityCompleted,
                    TotalBatchesCreated = statistics.TotalBatches,
                    AverageTimePerUnit = statistics.AverageTimePerUnit,
                    LastProductionDate = await GetLastProductionDate(detail.Id),
                    CanDelete = await CanDeleteDetail(detail.Id)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка при маппинге детали {DetailId}, используем базовые данные", detail.Id);

                return new DetailDto
                {
                    Id = detail.Id,
                    Name = detail.Name,
                    Number = detail.Number,
                    HasRoute = false,
                    CanDelete = false
                };
            }
        }

        /// <summary>
        /// Расчет количества завершенных деталей
        /// </summary>
        private int CalculateCompletedQuantity(List<Batch> batches)
        {
            return batches
                .Where(b => IsAllStagesCompleted(b))
                .Sum(b => b.Quantity);
        }

        /// <summary>
        /// Расчет количества деталей в работе
        /// </summary>
        private int CalculateInProgressQuantity(List<Batch> batches)
        {
            return batches
                .Where(b => HasInProgressStages(b))
                .Sum(b => b.Quantity);
        }

        /// <summary>
        /// Проверка, все ли этапы партии завершены
        /// </summary>
        private bool IsAllStagesCompleted(Batch batch)
        {
            var allStages = batch.SubBatches
                .SelectMany(sb => sb.StageExecutions)
                .Where(se => !se.IsSetup)
                .ToList();

            return allStages.Any() && allStages.All(se => se.Status == StageExecutionStatus.Completed);
        }

        /// <summary>
        /// Проверка, есть ли этапы в работе
        /// </summary>
        private bool HasInProgressStages(Batch batch)
        {
            return batch.SubBatches
                .SelectMany(sb => sb.StageExecutions)
                .Any(se => se.Status == StageExecutionStatus.InProgress);
        }

        /// <summary>
        /// Проверка, есть ли только ожидающие этапы
        /// </summary>
        private bool HasOnlyPendingStages(Batch batch)
        {
            var allStages = batch.SubBatches
                .SelectMany(sb => sb.StageExecutions)
                .Where(se => !se.IsSetup)
                .ToList();

            return allStages.Any() &&
                   allStages.All(se => se.Status == StageExecutionStatus.Pending ||
                                      se.Status == StageExecutionStatus.Waiting);
        }

        /// <summary>
        /// Расчет процента поставок в срок
        /// </summary>
        private double CalculateOnTimeDeliveryRate(List<Batch> batches)
        {
            var completedBatches = batches.Where(b => IsAllStagesCompleted(b)).ToList();
            if (!completedBatches.Any()) return 0;

            // Упрощенный расчет - считаем что партии завершены в срок, если нет просроченных этапов
            var onTimeBatches = completedBatches.Where(b =>
                !b.SubBatches.SelectMany(sb => sb.StageExecutions).Any(se => se.IsOverdue)).Count();

            return (double)onTimeBatches / completedBatches.Count * 100;
        }

        /// <summary>
        /// Получение метрик качества
        /// </summary>
        private async Task<QualityMetricsDto> GetQualityMetrics(int detailId, DateTime startDate, DateTime endDate)
        {
            // Заглушка для метрик качества
            return new QualityMetricsDto
            {
                DefectRate = 0,
                ReworkRate = 0,
                ScrapRate = 0,
                FirstPassYield = 100
            };
        }

        /// <summary>
        /// Получение последних партий детали
        /// </summary>
        private async Task<List<Batch>> GetRecentBatchesForDetail(int detailId, int count)
        {
            var allBatches = await _batchRepo.GetAllAsync();
            return allBatches
                .Where(b => b.DetailId == detailId)
                .OrderByDescending(b => b.CreatedUtc)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Расчет предполагаемой длительности производства
        /// </summary>
        private TimeSpan? CalculateEstimatedDuration(Route route, int quantity)
        {
            if (route?.Stages == null || !route.Stages.Any() || quantity <= 0)
                return null;

            var totalHours = route.Stages.Sum(s => s.NormTime * quantity + s.SetupTime);
            return TimeSpan.FromHours(totalHours);
        }

        /// <summary>
        /// Получение даты последнего производства
        /// </summary>
        private async Task<DateTime?> GetLastProductionDate(int detailId)
        {
            try
            {
                var allBatches = await _batchRepo.GetAllAsync();
                var lastBatch = allBatches
                    .Where(b => b.DetailId == detailId)
                    .OrderByDescending(b => b.CreatedUtc)
                    .FirstOrDefault();

                return lastBatch?.CreatedUtc;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Проверка возможности удаления детали
        /// </summary>
        private async Task<bool> CanDeleteDetail(int detailId)
        {
            try
            {
                // Проверяем маршруты
                var route = await _routeRepo.GetByDetailIdAsync(detailId);
                if (route != null) return false;

                // Проверяем партии
                var allBatches = await _batchRepo.GetAllAsync();
                return !allBatches.Any(b => b.DetailId == detailId);
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}