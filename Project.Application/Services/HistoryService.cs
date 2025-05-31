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
    public class HistoryService
    {
        private readonly IBatchRepository _batchRepo;
        private readonly IMachineRepository _machineRepo;
        private readonly IDetailRepository _detailRepo;
        private readonly ILogger<HistoryService> _logger;

        public HistoryService(
            IBatchRepository batchRepo,
            IMachineRepository machineRepo,
            IDetailRepository detailRepo,
            ILogger<HistoryService> logger)
        {
            _batchRepo = batchRepo;
            _machineRepo = machineRepo;
            _detailRepo = detailRepo;
            _logger = logger;
        }

        /// <summary>
        /// Получение истории выполнения этапов согласно ТЗ
        /// </summary>
        public async Task<List<HistoryDto>> GetStageExecutionHistoryAsync(StageHistoryFilterDto filter)
        {
            try
            {
                _logger.LogDebug("Получение истории этапов с фильтрами: {@Filter}", filter);

                var stageExecutions = await _batchRepo.GetStageExecutionHistoryAsync(
                    filter.StartDate,
                    filter.EndDate,
                    filter.MachineId,
                    filter.DetailId);

                // Применяем дополнительные фильтры
                var filtered = stageExecutions.AsEnumerable();

                // Фильтр по этапам переналадки
                if (!filter.IncludeSetups)
                {
                    filtered = filtered.Where(se => !se.IsSetup);
                }

                // Фильтр по статусам
                if (filter.Statuses != null && filter.Statuses.Any())
                {
                    var statusFilters = filter.Statuses.Select(s => MapFromContractStatus(s)).ToList();
                    filtered = filtered.Where(se => statusFilters.Contains(se.Status));
                }

                // Фильтр по оператору
                if (!string.IsNullOrEmpty(filter.OperatorId))
                {
                    filtered = filtered.Where(se => se.OperatorId != null &&
                                                   se.OperatorId.Contains(filter.OperatorId, StringComparison.OrdinalIgnoreCase));
                }

                // Фильтр по партии
                if (filter.BatchId.HasValue)
                {
                    filtered = filtered.Where(se => se.SubBatch.BatchId == filter.BatchId.Value);
                }

                // Фильтр только просроченные
                if (filter.IsOverdueOnly == true)
                {
                    filtered = filtered.Where(se => se.IsOverdue);
                }

                // Фильтр по длительности
                if (filter.MinDurationHours.HasValue)
                {
                    filtered = filtered.Where(se => se.ActualWorkingTime.HasValue &&
                                                   se.ActualWorkingTime.Value.TotalHours >= filter.MinDurationHours.Value);
                }

                if (filter.MaxDurationHours.HasValue)
                {
                    filtered = filtered.Where(se => se.ActualWorkingTime.HasValue &&
                                                   se.ActualWorkingTime.Value.TotalHours <= filter.MaxDurationHours.Value);
                }

                // Применяем сортировку
                filtered = ApplySorting(filtered, filter.SortBy, filter.SortDescending);

                // Применяем пагинацию
                var totalCount = filtered.Count();
                var paginatedResults = filtered
                    .Skip((filter.Page - 1) * filter.PageSize)
                    .Take(filter.PageSize)
                    .ToList();

                // Маппим в DTO
                var result = new List<HistoryDto>();
                foreach (var stage in paginatedResults)
                {
                    result.Add(MapToHistoryDto(stage));
                }

                _logger.LogDebug("Получено {Count} записей истории из {Total} общих", result.Count, totalCount);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении истории выполнения этапов");
                throw;
            }
        }

        /// <summary>
        /// Статистика за период согласно ТЗ
        /// </summary>
        public async Task<StageStatisticsDto> GetStatisticsAsync(
            DateTime startDate,
            DateTime endDate,
            int? machineId = null)
        {
            try
            {
                _logger.LogDebug("Получение статистики за период {StartDate} - {EndDate}, станок: {MachineId}",
                    startDate, endDate, machineId?.ToString() ?? "все");

                var stageExecutions = await _batchRepo.GetStageExecutionHistoryAsync(
                    startDate,
                    endDate,
                    machineId,
                    null);

                // Вычисляем общую статистику
                double totalWorkHours = 0;
                double totalSetupHours = 0;
                double totalIdleHours = 0;

                var completedStages = stageExecutions.Where(se => se.Status == StageExecutionStatus.Completed).ToList();
                var setupStages = completedStages.Where(se => se.IsSetup).ToList();
                var workStages = completedStages.Where(se => !se.IsSetup).ToList();

                // Расчет рабочего времени и времени переналадки
                totalWorkHours = workStages
                    .Where(s => s.ActualWorkingTime.HasValue)
                    .Sum(s => s.ActualWorkingTime!.Value.TotalHours);

                totalSetupHours = setupStages
                    .Where(s => s.ActualWorkingTime.HasValue)
                    .Sum(s => s.ActualWorkingTime!.Value.TotalHours);

                // Расчет времени простоя (только если указан конкретный станок)
                if (machineId.HasValue)
                {
                    totalIdleHours = await CalculateIdleTimeAsync(machineId.Value, startDate, endDate, stageExecutions);
                }

                // Расчет эффективности
                double totalHours = totalWorkHours + totalSetupHours + totalIdleHours;
                decimal efficiencyPercentage = totalHours > 0
                    ? (decimal)Math.Round((totalWorkHours / totalHours) * 100, 2)
                    : 0;

                // Среднее время выполнения этапа
                double averageStageTime = workStages.Any() ?
                    workStages.Where(s => s.ActualWorkingTime.HasValue)
                             .Average(s => s.ActualWorkingTime!.Value.TotalHours) : 0;

                // Среднее время переналадки
                double averageSetupTime = setupStages.Any() ?
                    setupStages.Where(s => s.ActualWorkingTime.HasValue)
                              .Average(s => s.ActualWorkingTime!.Value.TotalHours) : 0;

                // Просроченные этапы
                var overdueStages = stageExecutions.Count(se => se.IsOverdue);

                // Процент выполнения в срок
                decimal onTimePercentage = completedStages.Any() ?
                    (decimal)Math.Round(((double)(completedStages.Count - overdueStages) / completedStages.Count) * 100, 2) : 100;

                // Статистика по станкам
                var machineStatistics = await GetMachineStatisticsAsync(stageExecutions, startDate, endDate);

                // Статистика по операторам
                var operatorStatistics = await GetOperatorStatisticsAsync(stageExecutions, startDate, endDate);

                return new StageStatisticsDto
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    TotalStages = stageExecutions.Count,
                    CompletedStages = completedStages.Count,
                    SetupStages = setupStages.Count,
                    TotalWorkHours = Math.Round(totalWorkHours, 2),
                    TotalSetupHours = Math.Round(totalSetupHours, 2),
                    TotalIdleHours = Math.Round(totalIdleHours, 2),
                    EfficiencyPercentage = efficiencyPercentage,
                    AverageStageTime = Math.Round(averageStageTime, 2),
                    AverageSetupTime = Math.Round(averageSetupTime, 2),
                    OverdueStages = overdueStages,
                    OnTimePercentage = onTimePercentage,
                    MachineStatistics = machineStatistics,
                    OperatorStatistics = operatorStatistics
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении статистики за период");
                throw;
            }
        }

        /// <summary>
        /// Получение календарного отчета по станкам согласно ТЗ
        /// </summary>
        public async Task<MachineCalendarReportDto> GetMachineCalendarReportAsync(int machineId, DateTime startDate, DateTime endDate)
        {
            try
            {
                var machine = await _machineRepo.GetByIdAsync(machineId);
                if (machine == null)
                    throw new ArgumentException($"Станок с ID {machineId} не найден");

                var stageHistory = await _batchRepo.GetStageExecutionHistoryAsync(startDate, endDate, machineId);

                var report = new MachineCalendarReportDto
                {
                    MachineId = machineId,
                    MachineName = machine.Name,
                    MachineTypeName = machine.MachineType?.Name ?? "",
                    StartDate = startDate,
                    EndDate = endDate,
                    DailyWorkingHours = new Dictionary<DateTime, double>(),
                    DailySetupHours = new Dictionary<DateTime, double>(),
                    DailyIdleHours = new Dictionary<DateTime, double>(),
                    DailyManufacturedParts = new Dictionary<DateTime, List<ManufacturedPartDto>>(),
                    DailyUtilization = new Dictionary<DateTime, decimal>()
                };

                // Группируем по дням
                var stagesByDay = stageHistory
                    .Where(s => s.StartTimeUtc.HasValue)
                    .GroupBy(s => s.StartTimeUtc!.Value.Date)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Заполняем все дни в периоде
                for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
                {
                    var dayStages = stagesByDay.ContainsKey(date) ? stagesByDay[date] : new List<StageExecution>();

                    // Рабочее время
                    var workingHours = dayStages
                        .Where(s => !s.IsSetup && s.ActualWorkingTime.HasValue)
                        .Sum(s => s.ActualWorkingTime!.Value.TotalHours);

                    // Время переналадок
                    var setupHours = dayStages
                        .Where(s => s.IsSetup && s.ActualWorkingTime.HasValue)
                        .Sum(s => s.ActualWorkingTime!.Value.TotalHours);

                    // Время простоев (упрощенный расчет)
                    var idleHours = Math.Max(0, 24 - workingHours - setupHours);

                    // Изготовленные детали
                    var manufacturedParts = dayStages
                        .Where(s => !s.IsSetup && s.Status == StageExecutionStatus.Completed)
                        .Select(s => new ManufacturedPartDto
                        {
                            DetailName = s.SubBatch.Batch.Detail.Name,
                            DetailNumber = s.SubBatch.Batch.Detail.Number,
                            Quantity = s.SubBatch.Quantity,
                            ManufacturingTimeHours = s.ActualWorkingTime?.TotalHours ?? 0,
                            CompletedUtc = s.EndTimeUtc ?? DateTime.UtcNow,
                            BatchId = s.SubBatch.BatchId
                        }).ToList();

                    // Коэффициент использования
                    var totalActiveHours = workingHours + setupHours;
                    var utilization = totalActiveHours > 0 && workingHours > 0 ?
                        (decimal)Math.Round((workingHours / totalActiveHours) * 100, 1) : 0;

                    report.DailyWorkingHours[date] = Math.Round(workingHours, 2);
                    report.DailySetupHours[date] = Math.Round(setupHours, 2);
                    report.DailyIdleHours[date] = Math.Round(idleHours, 2);
                    report.DailyManufacturedParts[date] = manufacturedParts;
                    report.DailyUtilization[date] = utilization;
                }

                // Общая статистика
                report.TotalStatistics = new MachineStatisticsDto
                {
                    MachineId = machineId,
                    MachineName = machine.Name,
                    MachineTypeName = machine.MachineType?.Name ?? "",
                    WorkingHours = report.DailyWorkingHours.Values.Sum(),
                    SetupHours = report.DailySetupHours.Values.Sum(),
                    IdleHours = report.DailyIdleHours.Values.Sum(),
                    UtilizationPercentage = report.DailyWorkingHours.Values.Sum() > 0 ?
                        (decimal)Math.Round((report.DailyWorkingHours.Values.Sum() /
                                          (report.DailyWorkingHours.Values.Sum() + report.DailySetupHours.Values.Sum())) * 100, 2) : 0,
                    PartsMade = stageHistory.Count(s => !s.IsSetup && s.Status == StageExecutionStatus.Completed),
                    SetupCount = stageHistory.Count(s => s.IsSetup && s.Status == StageExecutionStatus.Completed),
                    AverageSetupTime = stageHistory.Where(s => s.IsSetup && s.ActualWorkingTime.HasValue).Any() ?
                        stageHistory.Where(s => s.IsSetup && s.ActualWorkingTime.HasValue).Average(s => s.ActualWorkingTime!.Value.TotalHours) : 0
                };

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении календарного отчета для станка {MachineId}", machineId);
                throw;
            }
        }

        /// <summary>
        /// Экспорт истории в различные форматы
        /// </summary>
        public async Task<byte[]> ExportHistoryAsync(StageHistoryFilterDto filter, string format)
        {
            try
            {
                var history = await GetStageExecutionHistoryAsync(filter);

                switch (format.ToUpper())
                {
                    case "CSV":
                        return ExportToCsv(history);
                    case "EXCEL":
                        return ExportToExcel(history);
                    case "JSON":
                        return ExportToJson(history);
                    default:
                        throw new ArgumentException($"Неподдерживаемый формат экспорта: {format}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при экспорте истории в формат {Format}", format);
                throw;
            }
        }

        #region Приватные методы

        /// <summary>
        /// Маппинг этапа выполнения в DTO истории
        /// </summary>
        private HistoryDto MapToHistoryDto(StageExecution stageExecution)
        {
            var duration = CalculateDuration(stageExecution);
            var plannedDuration = stageExecution.PlannedDuration.TotalHours;
            var deviation = duration.HasValue ? (double?)(duration.Value - plannedDuration) : null;

            return new HistoryDto
            {
                Id = stageExecution.Id,
                SubBatchId = stageExecution.SubBatchId,
                BatchId = stageExecution.SubBatch.BatchId,
                DetailName = stageExecution.SubBatch.Batch.Detail.Name,
                DetailNumber = stageExecution.SubBatch.Batch.Detail.Number,
                StageName = stageExecution.IsSetup ?
                    $"Переналадка: {stageExecution.RouteStage.Name}" :
                    stageExecution.RouteStage.Name,
                MachineId = stageExecution.MachineId,
                MachineName = stageExecution.Machine?.Name,
                MachineTypeName = stageExecution.Machine?.MachineType?.Name,
                Status = MapToContractStatus(stageExecution.Status),
                IsSetup = stageExecution.IsSetup,
                Priority = MapToPriority(stageExecution.Priority),
                StartTimeUtc = stageExecution.StartTimeUtc,
                EndTimeUtc = stageExecution.EndTimeUtc,
                PauseTimeUtc = stageExecution.PauseTimeUtc,
                ResumeTimeUtc = stageExecution.ResumeTimeUtc,
                StatusChangedTimeUtc = stageExecution.StatusChangedTimeUtc ?? stageExecution.CreatedUtc,
                OperatorId = stageExecution.OperatorId,
                ReasonNote = stageExecution.ReasonNote,
                DeviceId = stageExecution.DeviceId,
                DurationHours = duration,
                PlannedDurationHours = plannedDuration,
                DeviationHours = deviation,
                Quantity = stageExecution.SubBatch.Quantity,
                CompletionPercentage = stageExecution.CompletionPercentage,
                IsOverdue = stageExecution.IsOverdue,
                CreatedUtc = stageExecution.CreatedUtc
            };
        }

        /// <summary>
        /// Расчет фактической длительности этапа
        /// </summary>
        private double? CalculateDuration(StageExecution stage)
        {
            return stage.ActualWorkingTime?.TotalHours;
        }

        /// <summary>
        /// Применение сортировки к результатам
        /// </summary>
        private IEnumerable<StageExecution> ApplySorting(IEnumerable<StageExecution> stages, string sortBy, bool descending)
        {
            return sortBy.ToLower() switch
            {
                "starttime" => descending ?
                    stages.OrderByDescending(s => s.StartTimeUtc) :
                    stages.OrderBy(s => s.StartTimeUtc),
                "duration" => descending ?
                    stages.OrderByDescending(s => s.ActualWorkingTime?.TotalHours ?? 0) :
                    stages.OrderBy(s => s.ActualWorkingTime?.TotalHours ?? 0),
                "status" => descending ?
                    stages.OrderByDescending(s => s.Status) :
                    stages.OrderBy(s => s.Status),
                _ => descending ?
                    stages.OrderByDescending(s => s.StartTimeUtc ?? s.CreatedUtc) :
                    stages.OrderBy(s => s.StartTimeUtc ?? s.CreatedUtc)
            };
        }

        /// <summary>
        /// Расчет времени простоя станка
        /// </summary>
        private async Task<double> CalculateIdleTimeAsync(int machineId, DateTime startDate, DateTime endDate, List<StageExecution> stageExecutions)
        {
            try
            {
                // Получаем все этапы выполнения на указанном станке
                var machineStages = stageExecutions
                    .Where(se => se.MachineId == machineId)
                    .OrderBy(se => se.StartTimeUtc)
                    .ToList();

                double totalIdleHours = 0;
                DateTime? lastEndTime = null;

                foreach (var stage in machineStages)
                {
                    if (stage.StartTimeUtc.HasValue && lastEndTime.HasValue)
                    {
                        // Если есть разрыв между окончанием предыдущего этапа и началом текущего - это простой
                        if (stage.StartTimeUtc.Value > lastEndTime.Value)
                        {
                            var idleTime = stage.StartTimeUtc.Value - lastEndTime.Value;
                            totalIdleHours += idleTime.TotalHours;
                        }
                    }

                    if (stage.EndTimeUtc.HasValue)
                    {
                        lastEndTime = stage.EndTimeUtc.Value;
                    }
                }

                return totalIdleHours;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при расчете времени простоя станка {MachineId}", machineId);
                return 0;
            }
        }

        /// <summary>
        /// Получение статистики по станкам
        /// </summary>
        private async Task<List<MachineStatisticsDto>> GetMachineStatisticsAsync(List<StageExecution> stageExecutions, DateTime startDate, DateTime endDate)
        {
            try
            {
                var machineGroups = stageExecutions
                    .Where(s => s.MachineId.HasValue)
                    .GroupBy(s => s.MachineId!.Value);

                var result = new List<MachineStatisticsDto>();

                foreach (var group in machineGroups)
                {
                    var machineId = group.Key;
                    var machineStages = group.ToList();

                    var machine = await _machineRepo.GetByIdAsync(machineId);
                    if (machine == null) continue;

                    var workingHours = machineStages
                        .Where(s => !s.IsSetup && s.ActualWorkingTime.HasValue)
                        .Sum(s => s.ActualWorkingTime!.Value.TotalHours);

                    var setupHours = machineStages
                        .Where(s => s.IsSetup && s.ActualWorkingTime.HasValue)
                        .Sum(s => s.ActualWorkingTime!.Value.TotalHours);

                    result.Add(new MachineStatisticsDto
                    {
                        MachineId = machineId,
                        MachineName = machine.Name,
                        MachineTypeName = machine.MachineType?.Name ?? "",
                        WorkingHours = workingHours,
                        SetupHours = setupHours,
                        IdleHours = 0, // Упрощенно
                        UtilizationPercentage = (workingHours + setupHours) > 0 ?
                            (decimal)Math.Round((workingHours / (workingHours + setupHours)) * 100, 2) : 0,
                        PartsMade = machineStages.Count(s => !s.IsSetup && s.Status == StageExecutionStatus.Completed),
                        SetupCount = machineStages.Count(s => s.IsSetup && s.Status == StageExecutionStatus.Completed),
                        AverageSetupTime = machineStages.Where(s => s.IsSetup && s.ActualWorkingTime.HasValue).Any() ?
                            machineStages.Where(s => s.IsSetup && s.ActualWorkingTime.HasValue).Average(s => s.ActualWorkingTime!.Value.TotalHours) : 0
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении статистики по станкам");
                return new List<MachineStatisticsDto>();
            }
        }

        /// <summary>
        /// Получение статистики по операторам
        /// </summary>
        private async Task<List<OperatorStatisticsDto>> GetOperatorStatisticsAsync(List<StageExecution> stageExecutions, DateTime startDate, DateTime endDate)
        {
            try
            {
                var operatorGroups = stageExecutions
                    .Where(s => !string.IsNullOrEmpty(s.OperatorId))
                    .GroupBy(s => s.OperatorId!);

                var result = new List<OperatorStatisticsDto>();

                foreach (var group in operatorGroups)
                {
                    var operatorId = group.Key;
                    var operatorStages = group.ToList();

                    var completedStages = operatorStages.Where(s => s.Status == StageExecutionStatus.Completed).ToList();
                    var totalWorkingHours = completedStages
                        .Where(s => s.ActualWorkingTime.HasValue)
                        .Sum(s => s.ActualWorkingTime!.Value.TotalHours);

                    result.Add(new OperatorStatisticsDto
                    {
                        OperatorId = operatorId,
                        OperatorName = operatorId, // Можно расширить получением имени из справочника
                        StagesCompleted = completedStages.Count,
                        TotalWorkingHours = totalWorkingHours,
                        EfficiencyPercentage = 100, // Упрощенно
                        StagesStarted = operatorStages.Count(s => s.Status == StageExecutionStatus.InProgress),
                        StagesPaused = operatorStages.Count(s => s.Status == StageExecutionStatus.Paused),
                        OverdueStages = operatorStages.Count(s => s.IsOverdue),
                        LastActivity = operatorStages.Max(s => s.StatusChangedTimeUtc),
                        AverageStageTime = completedStages.Any() ?
                            completedStages.Where(s => s.ActualWorkingTime.HasValue)
                                          .Average(s => s.ActualWorkingTime!.Value.TotalHours) : 0
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении статистики по операторам");
                return new List<OperatorStatisticsDto>();
            }
        }

        /// <summary>
        /// Маппинг статуса из Domain в Contract
        /// </summary>
        private Project.Contracts.Enums.StageStatus MapToContractStatus(StageExecutionStatus status)
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

        /// <summary>
        /// Маппинг статуса из Contract в Domain
        /// </summary>
        private StageExecutionStatus MapFromContractStatus(Project.Contracts.Enums.StageStatus status)
        {
            return status switch
            {
                Project.Contracts.Enums.StageStatus.AwaitingStart => StageExecutionStatus.Pending,
                Project.Contracts.Enums.StageStatus.InQueue => StageExecutionStatus.Waiting,
                Project.Contracts.Enums.StageStatus.InProgress => StageExecutionStatus.InProgress,
                Project.Contracts.Enums.StageStatus.Paused => StageExecutionStatus.Paused,
                Project.Contracts.Enums.StageStatus.Completed => StageExecutionStatus.Completed,
                Project.Contracts.Enums.StageStatus.Cancelled => StageExecutionStatus.Error,
                _ => StageExecutionStatus.Pending
            };
        }

        /// <summary>
        /// Маппинг приоритета
        /// </summary>
        private Project.Contracts.Enums.Priority MapToPriority(int priority)
        {
            return priority switch
            {
                <= 2 => Project.Contracts.Enums.Priority.Low,
                <= 6 => Project.Contracts.Enums.Priority.Normal,
                <= 9 => Project.Contracts.Enums.Priority.High,
                _ => Project.Contracts.Enums.Priority.Critical
            };
        }

        /// <summary>
        /// Экспорт в CSV
        /// </summary>
        private byte[] ExportToCsv(List<HistoryDto> history)
        {
            // Упрощенная реализация экспорта в CSV
            var csv = "Id,DetailName,StageName,MachineName,Status,StartTime,EndTime,Duration\n";
            foreach (var record in history)
            {
                csv += $"{record.Id},{record.DetailName},{record.StageName},{record.MachineName}," +
                       $"{record.Status},{record.StartTimeUtc},{record.EndTimeUtc},{record.DurationHours}\n";
            }
            return System.Text.Encoding.UTF8.GetBytes(csv);
        }

        /// <summary>
        /// Экспорт в Excel (упрощенная версия)
        /// </summary>
        private byte[] ExportToExcel(List<HistoryDto> history)
        {
            // Здесь должна быть реализация экспорта в Excel
            // Для примера возвращаем CSV
            return ExportToCsv(history);
        }

        /// <summary>
        /// Экспорт в JSON
        /// </summary>
        private byte[] ExportToJson(List<HistoryDto> history)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(history, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        #endregion
    }
}