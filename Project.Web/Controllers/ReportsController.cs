using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Contracts.ModelDTO;
using Project.Web.ViewModels;

namespace Project.Web.Controllers
{
    /// <summary>
    /// Контроллер отчетности и аналитики согласно ТЗ
    /// </summary>
    public class ReportsController : Controller
    {
        private readonly HistoryService _historyService;
        private readonly MachineService _machineService;
        private readonly DetailService _detailService;
        private readonly BatchService _batchService;
        private readonly MachineTypeService _machineTypeService;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(
            HistoryService historyService,
            MachineService machineService,
            DetailService detailService,
            BatchService batchService,
            MachineTypeService machineTypeService,
            ILogger<ReportsController> logger)
        {
            _historyService = historyService;
            _machineService = machineService;
            _detailService = detailService;
            _batchService = batchService;
            _machineTypeService = machineTypeService;
            _logger = logger;
        }

        /// <summary>
        /// Главная страница отчетов согласно ТЗ
        /// </summary>
        public IActionResult Index()
        {
            var viewModel = new ReportsIndexViewModel
            {
                Categories = new List<ReportCategoryViewModel>
                {
                    new()
                    {
                        Name = "Отчеты по станкам",
                        Description = "Календарные отчеты, загрузка, эффективность использования станков",
                        Reports = new List<ReportItemViewModel>
                        {
                            new()
                            {
                                Id = "machine-calendar",
                                Name = "Календарный отчет по станкам",
                                Description = "Время работы, переналадок, простоев по дням",
                                Icon = "fas fa-calendar-alt",
                                Url = Url.Action(nameof(MachineCalendar)) ?? ""
                            },
                            new()
                            {
                                Id = "machine-utilization",
                                Name = "Загрузка станков",
                                Description = "Коэффициент использования, статистика работы",
                                Icon = "fas fa-chart-bar",
                                Url = Url.Action(nameof(MachineUtilization)) ?? ""
                            },
                            new()
                            {
                                Id = "machine-comparison",
                                Name = "Сравнение станков",
                                Description = "Сравнительный анализ эффективности станков",
                                Icon = "fas fa-balance-scale",
                                Url = Url.Action(nameof(MachineComparison)) ?? ""
                            }
                        }
                    },
                    new()
                    {
                        Name = "Отчеты по производительности",
                        Description = "Анализ отклонений, эффективность работы, выполнение планов",
                        Reports = new List<ReportItemViewModel>
                        {
                            new()
                            {
                                Id = "productivity",
                                Name = "Отчет по производительности",
                                Description = "Сравнение планового и фактического времени",
                                Icon = "fas fa-chart-line",
                                Url = Url.Action(nameof(Productivity)) ?? ""
                            },
                            new()
                            {
                                Id = "deviations",
                                Name = "Анализ отклонений",
                                Description = "Отклонения по деталям и операциям",
                                Icon = "fas fa-exclamation-triangle",
                                Url = Url.Action(nameof(Deviations)) ?? ""
                            },
                            new()
                            {
                                Id = "efficiency",
                                Name = "Эффективность работы",
                                Description = "Статистика по операторам и сменам",
                                Icon = "fas fa-users",
                                Url = Url.Action(nameof(Efficiency)) ?? ""
                            }
                        }
                    },
                    new()
                    {
                        Name = "Производственные отчеты",
                        Description = "Отчеты по партиям, деталям, выполнению заданий",
                        Reports = new List<ReportItemViewModel>
                        {
                            new()
                            {
                                Id = "batch-summary",
                                Name = "Сводка по партиям",
                                Description = "Статус выполнения производственных заданий",
                                Icon = "fas fa-boxes",
                                Url = Url.Action(nameof(BatchSummary)) ?? ""
                            },
                            new()
                            {
                                Id = "detail-statistics",
                                Name = "Статистика по деталям",
                                Description = "Время изготовления, количество, качество",
                                Icon = "fas fa-cogs",
                                Url = Url.Action(nameof(DetailStatistics)) ?? ""
                            },
                            new()
                            {
                                Id = "setup-analysis",
                                Name = "Анализ переналадок",
                                Description = "Время переналадок, частота, оптимизация",
                                Icon = "fas fa-tools",
                                Url = Url.Action(nameof(SetupAnalysis)) ?? ""
                            }
                        }
                    }
                }
            };

            return View(viewModel);
        }

        /// <summary>
        /// Календарный отчет по станкам согласно ТЗ
        /// </summary>
        public async Task<IActionResult> MachineCalendar(MachineCalendarFilterViewModel? filter)
        {
            try
            {
                var viewModel = new MachineCalendarReportViewModel
                {
                    Filter = filter ?? new MachineCalendarFilterViewModel()
                };

                // Загружаем список станков для фильтра
                await LoadMachineCalendarFilterData(viewModel.Filter);

                // Если выбран станок и период, загружаем данные
                if (viewModel.Filter.MachineId > 0)
                {
                    var calendarReport = await _historyService.GetMachineCalendarReportAsync(
                        viewModel.Filter.MachineId,
                        viewModel.Filter.StartDate,
                        viewModel.Filter.EndDate);

                    viewModel.Data = new MachineCalendarDataViewModel
                    {
                        MachineName = calendarReport.MachineName,
                        MachineTypeName = calendarReport.MachineTypeName,
                        StartDate = calendarReport.StartDate,
                        EndDate = calendarReport.EndDate,
                        Days = calendarReport.DailyWorkingHours.Select(kvp => new MachineCalendarDayViewModel
                        {
                            Date = kvp.Key,
                            WorkingHours = kvp.Value,
                            SetupHours = calendarReport.DailySetupHours.GetValueOrDefault(kvp.Key, 0),
                            IdleHours = calendarReport.DailyIdleHours.GetValueOrDefault(kvp.Key, 0),
                            UtilizationPercentage = calendarReport.DailyUtilization.GetValueOrDefault(kvp.Key, 0),
                            ManufacturedParts = calendarReport.DailyManufacturedParts
                                .GetValueOrDefault(kvp.Key, new List<ManufacturedPartDto>())
                                .Select(mp => new ManufacturedPartViewModel
                                {
                                    DetailName = mp.DetailName,
                                    DetailNumber = mp.DetailNumber,
                                    Quantity = mp.Quantity,
                                    ManufacturingTimeHours = mp.ManufacturingTimeHours,
                                    BatchId = mp.BatchId
                                }).ToList()
                        }).ToList(),
                        Summary = new MachineCalendarSummaryViewModel
                        {
                            TotalWorkingHours = calendarReport.TotalStatistics.WorkingHours,
                            TotalSetupHours = calendarReport.TotalStatistics.SetupHours,
                            TotalIdleHours = calendarReport.TotalStatistics.IdleHours,
                            OverallUtilization = calendarReport.TotalStatistics.UtilizationPercentage,
                            TotalPartsManufactured = calendarReport.TotalStatistics.PartsMade,
                            TotalSetupOperations = calendarReport.TotalStatistics.SetupCount,
                            AverageSetupTime = calendarReport.TotalStatistics.AverageSetupTime
                        }
                    };
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при формировании календарного отчета по станкам");
                TempData["Error"] = "Произошла ошибка при формировании отчета";
                return View(new MachineCalendarReportViewModel());
            }
        }

        /// <summary>
        /// Отчет по производительности согласно ТЗ
        /// </summary>
        public async Task<IActionResult> Productivity(ProductivityFilterViewModel? filter)
        {
            try
            {
                var viewModel = new ProductivityReportViewModel
                {
                    Filter = filter ?? new ProductivityFilterViewModel()
                };

                // Загружаем данные для фильтров
                await LoadProductivityFilterData(viewModel.Filter);

                // Если заданы параметры, формируем отчет
                if (viewModel.Filter.StartDate < viewModel.Filter.EndDate)
                {
                    var statistics = await _historyService.GetStatisticsAsync(
                        viewModel.Filter.StartDate,
                        viewModel.Filter.EndDate);

                    viewModel.Data = new ProductivityDataViewModel
                    {
                        StartDate = viewModel.Filter.StartDate,
                        EndDate = viewModel.Filter.EndDate,
                        OverallProductivity = new OverallProductivityViewModel
                        {
                            TotalPartsManufactured = statistics.CompletedStages,
                            TotalManufacturingTime = statistics.TotalWorkHours,
                            TotalSetupTime = statistics.TotalSetupHours,
                            OverallEfficiency = statistics.EfficiencyPercentage,
                            AverageTimePerPart = statistics.AverageStageTime,
                            AverageSetupTime = statistics.AverageSetupTime,
                            TotalOverdueStages = statistics.OverdueStages,
                            OnTimeDeliveryRate = statistics.OnTimePercentage
                        }
                    };

                    // Добавляем детализацию по деталям, операциям, станкам, операторам
                    viewModel.Data.DetailPerformance = await GetDetailPerformanceAsync(viewModel.Filter);
                    viewModel.Data.OperationDeviations = await GetOperationDeviationsAsync(viewModel.Filter);
                    viewModel.Data.MachineUtilization = await GetMachineUtilizationReportAsync(viewModel.Filter);
                    viewModel.Data.OperatorEfficiency = await GetOperatorEfficiencyAsync(viewModel.Filter);
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при формировании отчета по производительности");
                TempData["Error"] = "Произошла ошибка при формировании отчета";
                return View(new ProductivityReportViewModel());
            }
        }

        /// <summary>
        /// Загрузка станков
        /// </summary>
        public async Task<IActionResult> MachineUtilization(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                startDate ??= DateTime.Today.AddDays(-7);
                endDate ??= DateTime.Today.AddDays(1);

                var machines = await _machineService.GetAllAsync();
                var utilizationData = new List<MachineUtilizationReportViewModel>();

                foreach (var machine in machines)
                {
                    var utilization = await _machineService.GetMachineUtilizationAsync(
                        machine.Id, startDate, endDate);

                    utilizationData.Add(new MachineUtilizationReportViewModel
                    {
                        MachineName = machine.Name,
                        MachineTypeName = machine.MachineTypeName,
                        TotalAvailableHours = utilization.TotalAvailableHours,
                        WorkingHours = utilization.WorkingHours,
                        SetupHours = utilization.SetupHours,
                        IdleHours = utilization.IdleHours,
                        UtilizationPercentage = utilization.UtilizationPercentage,
                        CompletedOperations = utilization.CompletedOperations,
                        SetupOperations = utilization.SetupOperations
                    });
                }

                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;

                return View(utilizationData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при формировании отчета по загрузке станков");
                TempData["Error"] = "Произошла ошибка при формировании отчета";
                return View(new List<MachineUtilizationReportViewModel>());
            }
        }

        /// <summary>
        /// Сравнение станков
        /// </summary>
        public async Task<IActionResult> MachineComparison(List<int>? machineIds, DateTime? startDate, DateTime? endDate)
        {
            try
            {
                startDate ??= DateTime.Today.AddDays(-7);
                endDate ??= DateTime.Today.AddDays(1);

                var machines = await _machineService.GetAllAsync();
                ViewBag.AvailableMachines = machines;
                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;

                var comparisonData = new List<MachineUtilizationReportViewModel>();

                if (machineIds?.Any() == true)
                {
                    foreach (var machineId in machineIds.Take(5)) // Ограничиваем количество
                    {
                        var machine = machines.FirstOrDefault(m => m.Id == machineId);
                        if (machine != null)
                        {
                            var utilization = await _machineService.GetMachineUtilizationAsync(
                                machineId, startDate, endDate);

                            comparisonData.Add(new MachineUtilizationReportViewModel
                            {
                                MachineName = machine.Name,
                                MachineTypeName = machine.MachineTypeName,
                                WorkingHours = utilization.WorkingHours,
                                SetupHours = utilization.SetupHours,
                                IdleHours = utilization.IdleHours,
                                UtilizationPercentage = utilization.UtilizationPercentage,
                                CompletedOperations = utilization.CompletedOperations,
                                SetupOperations = utilization.SetupOperations
                            });
                        }
                    }
                }

                return View(comparisonData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сравнении станков");
                TempData["Error"] = "Произошла ошибка при формировании отчета";
                return View(new List<MachineUtilizationReportViewModel>());
            }
        }

        /// <summary>
        /// Анализ отклонений
        /// </summary>
        public async Task<IActionResult> Deviations(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                startDate ??= DateTime.Today.AddDays(-30);
                endDate ??= DateTime.Today.AddDays(1);

                var statistics = await _historyService.GetStatisticsAsync(startDate.Value, endDate.Value);

                var deviations = new List<OperationDeviationViewModel>();

                // Здесь должна быть логика получения отклонений по операциям
                // Пока используем заглушку на основе общей статистики

                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;
                ViewBag.Statistics = statistics;

                return View(deviations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при анализе отклонений");
                TempData["Error"] = "Произошла ошибка при формировании отчета";
                return View(new List<OperationDeviationViewModel>());
            }
        }

        /// <summary>
        /// Эффективность работы
        /// </summary>
        public async Task<IActionResult> Efficiency(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                startDate ??= DateTime.Today.AddDays(-7);
                endDate ??= DateTime.Today.AddDays(1);

                var efficiency = new List<OperatorEfficiencyViewModel>();

                // Здесь должна быть логика получения эффективности операторов
                // Пока используем заглушку

                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;

                return View(efficiency);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при анализе эффективности");
                TempData["Error"] = "Произошла ошибка при формировании отчета";
                return View(new List<OperatorEfficiencyViewModel>());
            }
        }

        /// <summary>
        /// Сводка по партиям
        /// </summary>
        public async Task<IActionResult> BatchSummary(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                startDate ??= DateTime.Today.AddDays(-30);
                endDate ??= DateTime.Today.AddDays(1);

                var batches = await _batchService.GetAllAsync();
                var filteredBatches = batches.Where(b =>
                    b.CreatedUtc >= startDate && b.CreatedUtc <= endDate).ToList();

                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;

                return View(filteredBatches);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при формировании сводки по партиям");
                TempData["Error"] = "Произошла ошибка при формировании отчета";
                return View(new List<Project.Contracts.ModelDTO.BatchDto>());
            }
        }

        /// <summary>
        /// Статистика по деталям
        /// </summary>
        public async Task<IActionResult> DetailStatistics(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                startDate ??= DateTime.Today.AddDays(-30);
                endDate ??= DateTime.Today.AddDays(1);

                var details = await _detailService.GetAllAsync();

                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;

                return View(details);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при формировании статистики по деталям");
                TempData["Error"] = "Произошла ошибка при формировании отчета";
                return View(new List<Project.Contracts.ModelDTO.DetailDto>());
            }
        }

        /// <summary>
        /// Анализ переналадок
        /// </summary>
        public async Task<IActionResult> SetupAnalysis(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                startDate ??= DateTime.Today.AddDays(-30);
                endDate ??= DateTime.Today.AddDays(1);

                var statistics = await _historyService.GetStatisticsAsync(startDate.Value, endDate.Value);

                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;
                ViewBag.Statistics = statistics;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при анализе переналадок");
                TempData["Error"] = "Произошла ошибка при формировании отчета";
                return View();
            }
        }

        /// <summary>
        /// Экспорт отчета
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ExportReport(string reportType, string format, object parameters)
        {
            try
            {
                // Здесь должна быть логика экспорта различных отчетов
                // Пока возвращаем заглушку

                var fileName = $"{reportType}_{DateTime.Now:yyyyMMdd_HHmmss}.{format.ToLower()}";
                var contentType = format.ToUpper() switch
                {
                    "PDF" => "application/pdf",
                    "EXCEL" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "CSV" => "text/csv",
                    _ => "application/octet-stream"
                };

                var data = System.Text.Encoding.UTF8.GetBytes("Экспорт отчета - заглушка");
                return File(data, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при экспорте отчета {ReportType}", reportType);
                TempData["Error"] = "Произошла ошибка при экспорте отчета";
                return RedirectToAction(nameof(Index));
            }
        }

        #region Вспомогательные методы

        private async Task LoadMachineCalendarFilterData(MachineCalendarFilterViewModel filter)
        {
            try
            {
                var machines = await _machineService.GetAllAsync();
                filter.AvailableMachines = machines.Select(m => new SelectOptionViewModel
                {
                    Id = m.Id,
                    Name = $"{m.Name} ({m.InventoryNumber})"
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке данных фильтра календарного отчета");
                filter.AvailableMachines = new List<SelectOptionViewModel>();
            }
        }

        private async Task LoadProductivityFilterData(ProductivityFilterViewModel filter)
        {
            try
            {
                var machines = await _machineService.GetAllAsync();
                filter.AvailableMachines = machines.Select(m => new SelectOptionViewModel
                {
                    Id = m.Id,
                    Name = $"{m.Name} ({m.InventoryNumber})"
                }).ToList();

                var machineTypes = await _machineTypeService.GetAllAsync();
                filter.AvailableMachineTypes = machineTypes.Select(mt => new SelectOptionViewModel
                {
                    Id = mt.Id,
                    Name = mt.Name
                }).ToList();

                var details = await _detailService.GetAllAsync();
                filter.AvailableDetails = details.Select(d => new SelectOptionViewModel
                {
                    Id = d.Id,
                    Name = $"{d.Number} - {d.Name}"
                }).ToList();

                // Заглушка для операторов
                filter.AvailableOperators = new List<SelectOptionViewModel>
                {
                    new() { Id = 1, Name = "Оператор 1" },
                    new() { Id = 2, Name = "Оператор 2" }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке данных фильтра отчета по производительности");
                filter.AvailableMachines = new List<SelectOptionViewModel>();
                filter.AvailableMachineTypes = new List<SelectOptionViewModel>();
                filter.AvailableDetails = new List<SelectOptionViewModel>();
                filter.AvailableOperators = new List<SelectOptionViewModel>();
            }
        }

        private async Task<List<DetailPerformanceViewModel>> GetDetailPerformanceAsync(ProductivityFilterViewModel filter)
        {
            // Заглушка - здесь должна быть реальная логика получения производительности по деталям
            return new List<DetailPerformanceViewModel>();
        }

        private async Task<List<OperationDeviationViewModel>> GetOperationDeviationsAsync(ProductivityFilterViewModel filter)
        {
            // Заглушка - здесь должна быть реальная логика получения отклонений по операциям
            return new List<OperationDeviationViewModel>();
        }

        private async Task<List<MachineUtilizationReportViewModel>> GetMachineUtilizationReportAsync(ProductivityFilterViewModel filter)
        {
            // Заглушка - здесь должна быть реальная логика получения загрузки станков
            return new List<MachineUtilizationReportViewModel>();
        }

        private async Task<List<OperatorEfficiencyViewModel>> GetOperatorEfficiencyAsync(ProductivityFilterViewModel filter)
        {
            // Заглушка - здесь должна быть реальная логика получения эффективности операторов
            return new List<OperatorEfficiencyViewModel>();
        }

        #endregion
    }
}