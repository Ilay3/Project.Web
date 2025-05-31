using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Contracts.ModelDTO;
using Project.Contracts.Enums;
using Project.Web.ViewModels;

namespace Project.Web.Controllers
{
    /// <summary>
    /// Контроллер истории выполнения этапов согласно ТЗ
    /// </summary>
    public class HistoryController : Controller
    {
        private readonly HistoryService _historyService;
        private readonly MachineService _machineService;
        private readonly DetailService _detailService;
        private readonly BatchService _batchService;
        private readonly ILogger<HistoryController> _logger;

        public HistoryController(
            HistoryService historyService,
            MachineService machineService,
            DetailService detailService,
            BatchService batchService,
            ILogger<HistoryController> logger)
        {
            _historyService = historyService;
            _machineService = machineService;
            _detailService = detailService;
            _batchService = batchService;
            _logger = logger;
        }

        /// <summary>
        /// История выполнения этапов согласно ТЗ
        /// </summary>
        public async Task<IActionResult> Index(HistoryFilterViewModel? filter, int page = 1, int pageSize = 20)
        {
            try
            {
                filter ??= new HistoryFilterViewModel();

                // Создаем фильтр для сервиса
                var serviceFilter = new StageHistoryFilterDto
                {
                    StartDate = filter.StartDate,
                    EndDate = filter.EndDate,
                    MachineId = filter.MachineId,
                    DetailId = filter.DetailId,
                    BatchId = filter.BatchId,
                    OperatorId = filter.OperatorId,
                    Statuses = filter.SelectedStatuses?.Select(MapToContractStatus).ToList(),
                    IncludeSetups = filter.IncludeSetups,
                    IsOverdueOnly = filter.IsOverdueOnly,
                    MinDurationHours = filter.MinDurationHours,
                    MaxDurationHours = filter.MaxDurationHours,
                    SortBy = filter.SortBy,
                    SortDescending = filter.SortDescending,
                    Page = page,
                    PageSize = pageSize
                };

                // Получаем историю
                var history = await _historyService.GetStageExecutionHistoryAsync(serviceFilter);

                // Получаем статистику
                var statistics = await _historyService.GetStatisticsAsync(
                    filter.StartDate ?? DateTime.Today.AddDays(-7),
                    filter.EndDate ?? DateTime.Today.AddDays(1),
                    filter.MachineId);

                // Загружаем данные для фильтров
                await LoadFilterOptions(filter);

                var viewModel = new HistoryIndexViewModel
                {
                    Filter = filter,
                    Pagination = new PaginationViewModel
                    {
                        CurrentPage = page,
                        PageSize = pageSize,
                        TotalItems = history.Count // Общее количество может отличаться
                    },
                    HistoryItems = history.Select(h => new HistoryItemViewModel
                    {
                        Id = h.Id,
                        SubBatchId = h.SubBatchId,
                        BatchId = h.BatchId,
                        DetailName = h.DetailName,
                        DetailNumber = h.DetailNumber,
                        StageName = h.StageName,
                        MachineId = h.MachineId,
                        MachineName = h.MachineName,
                        MachineTypeName = h.MachineTypeName,
                        Status = h.Status,
                        IsSetup = h.IsSetup,
                        Priority = h.Priority,
                        StartTimeUtc = h.StartTimeUtc,
                        EndTimeUtc = h.EndTimeUtc,
                        PauseTimeUtc = h.PauseTimeUtc,
                        ResumeTimeUtc = h.ResumeTimeUtc,
                        StatusChangedTimeUtc = h.StatusChangedTimeUtc,
                        OperatorId = h.OperatorId,
                        ReasonNote = h.ReasonNote,
                        DeviceId = h.DeviceId,
                        DurationHours = h.DurationHours,
                        PlannedDurationHours = h.PlannedDurationHours,
                        DeviationHours = h.DeviationHours,
                        Quantity = h.Quantity,
                        CompletionPercentage = h.CompletionPercentage,
                        IsOverdue = h.IsOverdue,
                        CreatedUtc = h.CreatedUtc
                    }).ToList(),
                    Statistics = new HistoryStatisticsViewModel
                    {
                        StartDate = statistics.StartDate,
                        EndDate = statistics.EndDate,
                        TotalStages = statistics.TotalStages,
                        CompletedStages = statistics.CompletedStages,
                        SetupStages = statistics.SetupStages,
                        TotalWorkHours = statistics.TotalWorkHours,
                        TotalSetupHours = statistics.TotalSetupHours,
                        TotalIdleHours = statistics.TotalIdleHours,
                        EfficiencyPercentage = statistics.EfficiencyPercentage,
                        AverageStageTime = statistics.AverageStageTime,
                        AverageSetupTime = statistics.AverageSetupTime,
                        OverdueStages = statistics.OverdueStages,
                        OnTimePercentage = statistics.OnTimePercentage
                    }
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении истории выполнения этапов");
                TempData["Error"] = "Произошла ошибка при загрузке истории";
                return View(new HistoryIndexViewModel());
            }
        }

        /// <summary>
        /// Детальная информация о записи истории
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                // Здесь должна быть загрузка детальной информации о записи истории
                var viewModel = new HistoryDetailsViewModel
                {
                    HistoryItem = new HistoryItemViewModel
                    {
                        Id = id,
                        DetailName = "Деталь-001",
                        StageName = "Токарная обработка",
                        Status = StageStatus.Completed,
                        CreatedUtc = DateTime.UtcNow.AddHours(-5)
                    }
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении детальной информации истории {HistoryId}", id);
                return NotFound("Запись истории не найдена");
            }
        }

        /// <summary>
        /// Экспорт истории в различные форматы
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Export(HistoryFilterViewModel filter, string format = "CSV")
        {
            try
            {
                // Создаем фильтр для сервиса без пагинации
                var serviceFilter = new StageHistoryFilterDto
                {
                    StartDate = filter.StartDate,
                    EndDate = filter.EndDate,
                    MachineId = filter.MachineId,
                    DetailId = filter.DetailId,
                    BatchId = filter.BatchId,
                    OperatorId = filter.OperatorId,
                    Statuses = filter.SelectedStatuses?.Select(MapToContractStatus).ToList(),
                    IncludeSetups = filter.IncludeSetups,
                    IsOverdueOnly = filter.IsOverdueOnly,
                    MinDurationHours = filter.MinDurationHours,
                    MaxDurationHours = filter.MaxDurationHours,
                    SortBy = filter.SortBy,
                    SortDescending = filter.SortDescending,
                    Page = 1,
                    PageSize = 10000 // Большое количество для экспорта
                };

                var exportData = await _historyService.ExportHistoryAsync(serviceFilter, format);

                var contentType = format.ToUpper() switch
                {
                    "CSV" => "text/csv",
                    "EXCEL" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "JSON" => "application/json",
                    _ => "application/octet-stream"
                };

                var fileName = $"history_{DateTime.Now:yyyyMMdd_HHmmss}.{format.ToLower()}";

                return File(exportData, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при экспорте истории в формат {Format}", format);
                TempData["Error"] = "Произошла ошибка при экспорте данных";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Получение статистики за период
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetStatistics(DateTime? startDate, DateTime? endDate, int? machineId)
        {
            try
            {
                startDate ??= DateTime.Today.AddDays(-7);
                endDate ??= DateTime.Today.AddDays(1);

                var statistics = await _historyService.GetStatisticsAsync(startDate.Value, endDate.Value, machineId);

                return Json(new
                {
                    success = true,
                    statistics = new
                    {
                        totalStages = statistics.TotalStages,
                        completedStages = statistics.CompletedStages,
                        totalWorkHours = statistics.TotalWorkHours,
                        totalSetupHours = statistics.TotalSetupHours,
                        efficiencyPercentage = statistics.EfficiencyPercentage,
                        averageStageTime = statistics.AverageStageTime,
                        overdueStages = statistics.OverdueStages,
                        onTimePercentage = statistics.OnTimePercentage
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении статистики");
                return Json(new { success = false, message = "Ошибка при получении статистики" });
            }
        }

        /// <summary>
        /// Календарный отчет по станку
        /// </summary>
        public async Task<IActionResult> MachineCalendar(int machineId, DateTime? startDate, DateTime? endDate)
        {
            try
            {
                startDate ??= DateTime.Today.AddDays(-7);
                endDate ??= DateTime.Today;

                var calendarReport = await _historyService.GetMachineCalendarReportAsync(
                    machineId, startDate.Value, endDate.Value);

                return View(calendarReport);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении календарного отчета для станка {MachineId}", machineId);
                return NotFound("Станок не найден");
            }
        }

        /// <summary>
        /// API для получения данных истории для диаграмм
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetChartData(DateTime? startDate, DateTime? endDate, string groupBy = "day")
        {
            try
            {
                startDate ??= DateTime.Today.AddDays(-7);
                endDate ??= DateTime.Today.AddDays(1);

                // Получаем статистику по дням/часам для построения графиков
                var statistics = await _historyService.GetStatisticsAsync(startDate.Value, endDate.Value);

                // Формируем данные для графика эффективности
                var chartData = new
                {
                    efficiency = new
                    {
                        labels = GetPeriodLabels(startDate.Value, endDate.Value, groupBy),
                        datasets = new[]
                        {
                            new
                            {
                                label = "Эффективность (%)",
                                data = new[] { statistics.EfficiencyPercentage },
                                backgroundColor = "rgba(54, 162, 235, 0.2)",
                                borderColor = "rgba(54, 162, 235, 1)"
                            }
                        }
                    },
                    workHours = new
                    {
                        labels = new[] { "Рабочее время", "Переналадки", "Простои" },
                        datasets = new[]
                        {
                            new
                            {
                                data = new[] { statistics.TotalWorkHours, statistics.TotalSetupHours, statistics.TotalIdleHours },
                                backgroundColor = new[] { "#28a745", "#ffc107", "#dc3545" }
                            }
                        }
                    }
                };

                return Json(chartData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении данных для диаграмм");
                return Json(new { success = false });
            }
        }

        /// <summary>
        /// Сравнение производительности по станкам
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CompareMachines(List<int> machineIds, DateTime? startDate, DateTime? endDate)
        {
            try
            {
                startDate ??= DateTime.Today.AddDays(-7);
                endDate ??= DateTime.Today.AddDays(1);

                var comparisons = new List<object>();

                foreach (var machineId in machineIds.Take(5)) // Ограничиваем количество для производительности
                {
                    var statistics = await _historyService.GetStatisticsAsync(startDate.Value, endDate.Value, machineId);
                    var machine = await _machineService.GetByIdAsync(machineId);

                    if (machine != null)
                    {
                        comparisons.Add(new
                        {
                            machineName = machine.Name,
                            workingHours = statistics.TotalWorkHours,
                            setupHours = statistics.TotalSetupHours,
                            efficiency = statistics.EfficiencyPercentage,
                            completedStages = statistics.CompletedStages,
                            overdueStages = statistics.OverdueStages
                        });
                    }
                }

                return Json(comparisons);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сравнении производительности станков");
                return Json(new List<object>());
            }
        }

        #region Вспомогательные методы

        private async Task LoadFilterOptions(HistoryFilterViewModel filter)
        {
            try
            {
                var machines = await _machineService.GetAllAsync();
                filter.AvailableMachines = machines.Select(m => new SelectOptionViewModel
                {
                    Id = m.Id,
                    Name = $"{m.Name} ({m.InventoryNumber})"
                }).ToList();

                var details = await _detailService.GetAllAsync();
                filter.AvailableDetails = details.Select(d => new SelectOptionViewModel
                {
                    Id = d.Id,
                    Name = $"{d.Number} - {d.Name}"
                }).ToList();

                var batches = await _batchService.GetAllAsync();
                filter.AvailableBatches = batches.Take(50).Select(b => new SelectOptionViewModel
                {
                    Id = b.Id,
                    Name = $"Партия #{b.Id} - {b.DetailName}"
                }).ToList();

                // Здесь можно добавить список операторов
                filter.AvailableOperators = new List<SelectOptionViewModel>
                {
                    new() { Id = 1, Name = "Оператор 1" },
                    new() { Id = 2, Name = "Оператор 2" }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке опций фильтра");
                filter.AvailableMachines = new List<SelectOptionViewModel>();
                filter.AvailableDetails = new List<SelectOptionViewModel>();
                filter.AvailableBatches = new List<SelectOptionViewModel>();
                filter.AvailableOperators = new List<SelectOptionViewModel>();
            }
        }

        private Project.Contracts.Enums.StageStatus MapToContractStatus(StageStatus status)
        {
            return status switch
            {
                StageStatus.AwaitingStart => Project.Contracts.Enums.StageStatus.AwaitingStart,
                StageStatus.InQueue => Project.Contracts.Enums.StageStatus.InQueue,
                StageStatus.InProgress => Project.Contracts.Enums.StageStatus.InProgress,
                StageStatus.Paused => Project.Contracts.Enums.StageStatus.Paused,
                StageStatus.Completed => Project.Contracts.Enums.StageStatus.Completed,
                StageStatus.Cancelled => Project.Contracts.Enums.StageStatus.Cancelled,
                _ => Project.Contracts.Enums.StageStatus.AwaitingStart
            };
        }

        private List<string> GetPeriodLabels(DateTime startDate, DateTime endDate, string groupBy)
        {
            var labels = new List<string>();
            var current = startDate.Date;

            while (current <= endDate.Date)
            {
                labels.Add(groupBy.ToLower() switch
                {
                    "hour" => current.ToString("dd.MM HH:mm"),
                    "day" => current.ToString("dd.MM"),
                    "week" => $"Неделя {GetWeekOfYear(current)}",
                    "month" => current.ToString("MM.yyyy"),
                    _ => current.ToString("dd.MM")
                });

                current = groupBy.ToLower() switch
                {
                    "hour" => current.AddHours(1),
                    "day" => current.AddDays(1),
                    "week" => current.AddDays(7),
                    "month" => current.AddMonths(1),
                    _ => current.AddDays(1)
                };
            }

            return labels;
        }

        private int GetWeekOfYear(DateTime date)
        {
            var calendar = System.Globalization.CultureInfo.CurrentCulture.Calendar;
            return calendar.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday);
        }

        #endregion
    }
}