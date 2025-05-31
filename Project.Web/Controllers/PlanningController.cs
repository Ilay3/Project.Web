using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Contracts.ModelDTO;
using Project.Contracts.Enums;
using Project.Web.ViewModels;

namespace Project.Web.Controllers
{
    /// <summary>
    /// Контроллер планирования производства согласно ТЗ
    /// </summary>
    public class PlanningController : Controller
    {
        private readonly ProductionSchedulerService _schedulerService;
        private readonly BatchService _batchService;
        private readonly MachineService _machineService;
        private readonly DetailService _detailService;
        private readonly StageExecutionService _stageService;
        private readonly ILogger<PlanningController> _logger;

        public PlanningController(
            ProductionSchedulerService schedulerService,
            BatchService batchService,
            MachineService machineService,
            DetailService detailService,
            StageExecutionService stageService,
            ILogger<PlanningController> logger)
        {
            _schedulerService = schedulerService;
            _batchService = batchService;
            _machineService = machineService;
            _detailService = detailService;
            _stageService = stageService;
            _logger = logger;
        }

        /// <summary>
        /// Главная страница планирования согласно ТЗ
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                var viewModel = new PlanningIndexViewModel();

                // Загружаем текущее состояние планирования
                await LoadPlanningOverview(viewModel);

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке страницы планирования");
                TempData["Error"] = "Произошла ошибка при загрузке данных планирования";
                return View(new PlanningIndexViewModel());
            }
        }

        /// <summary>
        /// Очередь планирования
        /// </summary>
        public async Task<IActionResult> Queue()
        {
            try
            {
                var queuedStages = await _schedulerService.GetQueueForecastAsync();

                var viewModel = new PlanningQueueViewModel
                {
                    QueuedStages = queuedStages.Select(q => new QueuedStageViewModel
                    {
                        Id = q.StageExecutionId,
                        DetailName = q.DetailName,
                        StageName = q.StageName,
                        MachineName = q.ExpectedMachineName,
                        Priority = MapFromContractPriority(q.Priority),
                        CreatedUtc = q.CreatedUtc,
                        QueuePosition = q.QueuePosition,
                        RequiresSetup = q.RequiresSetup
                    }).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке очереди планирования");
                TempData["Error"] = "Произошла ошибка при загрузке очереди";
                return View(new PlanningQueueViewModel());
            }
        }

        /// <summary>
        /// Прогноз оптимального расписания
        /// </summary>
        public async Task<IActionResult> Forecast()
        {
            try
            {
                var viewModel = new PlanningForecastViewModel();
                await LoadForecastData(viewModel);

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке прогноза планирования");
                return View(new PlanningForecastViewModel());
            }
        }

        /// <summary>
        /// Получение прогноза для детали
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> GetForecast(int detailId, int quantity)
        {
            try
            {
                if (detailId <= 0 || quantity <= 0)
                {
                    return Json(new { success = false, message = "Некорректные параметры" });
                }

                var forecast = await _schedulerService.PredictOptimalScheduleForDetailAsync(detailId, quantity);

                var result = new
                {
                    success = true,
                    forecast = new
                    {
                        detailId = forecast.DetailId,
                        quantity = forecast.Quantity,
                        earliestStartTime = forecast.EarliestStartTime,
                        latestEndTime = forecast.LatestEndTime,
                        totalDuration = forecast.TotalDuration,
                        stages = forecast.StageForecasts.Select(sf => new
                        {
                            stageOrder = sf.StageOrder,
                            stageName = sf.StageName,
                            machineTypeName = sf.MachineTypeName,
                            machineName = sf.MachineName,
                            expectedStartTime = sf.ExpectedStartTime,
                            expectedEndTime = sf.ExpectedEndTime,
                            needsSetup = sf.NeedsSetup,
                            setupTimeHours = sf.SetupTimeHours,
                            operationTimeHours = sf.OperationTimeHours,
                            queueTimeHours = sf.QueueTimeHours
                        })
                    }
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении прогноза для детали {DetailId}", detailId);
                return Json(new { success = false, message = "Ошибка при расчете прогноза" });
            }
        }

        /// <summary>
        /// Автоматическое планирование партии
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AutoSchedule(int batchId)
        {
            try
            {
                await _schedulerService.ScheduleSubBatchesAsync(batchId);

                TempData["Success"] = "Партия успешно запланирована";
                return RedirectToAction("Details", "Batches", new { id = batchId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при автоматическом планировании партии {BatchId}", batchId);
                TempData["Error"] = ex.Message;
                return RedirectToAction("Details", "Batches", new { id = batchId });
            }
        }

        /// <summary>
        /// Переназначение этапа на другой станок
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReassignStage(int stageExecutionId, int newMachineId)
        {
            try
            {
                await _schedulerService.ReassignStageToMachineAsync(stageExecutionId, newMachineId);

                TempData["Success"] = "Этап успешно переназначен";
                return RedirectToAction(nameof(Queue));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при переназначении этапа {StageId}", stageExecutionId);
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Queue));
            }
        }

        /// <summary>
        /// Изменение приоритета в очереди
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePriority(int machineId, int priorityStageId)
        {
            try
            {
                await _schedulerService.ReassignQueueAsync(machineId, priorityStageId);

                TempData["Success"] = "Приоритет изменен";
                return RedirectToAction(nameof(Queue));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при изменении приоритета");
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Queue));
            }
        }

        /// <summary>
        /// Разрешение конфликтов в расписании
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResolveConflicts()
        {
            try
            {
                await _schedulerService.ResolveScheduleConflictsAsync();

                TempData["Success"] = "Конфликты в расписании разрешены";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при разрешении конфликтов");
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Оптимизация планирования
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OptimizeSchedule()
        {
            try
            {
                await _schedulerService.OptimizeQueueAsync();

                TempData["Success"] = "Планирование оптимизировано";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при оптимизации планирования");
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Массовое планирование нескольких партий
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkSchedule(List<int> batchIds)
        {
            try
            {
                if (!batchIds?.Any() == true)
                {
                    TempData["Error"] = "Не выбраны партии для планирования";
                    return RedirectToAction(nameof(Index));
                }

                var successCount = 0;
                var errorCount = 0;

                foreach (var batchId in batchIds)
                {
                    try
                    {
                        await _schedulerService.ScheduleSubBatchesAsync(batchId);
                        successCount++;
                    }
                    catch (Exception)
                    {
                        errorCount++;
                    }
                }

                TempData["Success"] = $"Успешно запланировано {successCount} партий";
                if (errorCount > 0)
                {
                    TempData["Warning"] = $"Не удалось запланировать {errorCount} партий";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при массовом планировании");
                TempData["Error"] = "Произошла ошибка при планировании";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// API для получения состояния планирования
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPlanningStatus()
        {
            try
            {
                var queuedStages = await _schedulerService.GetQueueForecastAsync();
                var statistics = await _stageService.GetExecutionStatisticsAsync();

                var status = new
                {
                    success = true,
                    queueLength = queuedStages.Count,
                    inProgressStages = statistics.InProgressStages,
                    pendingStages = statistics.TotalStages - statistics.CompletedStages - statistics.InProgressStages,
                    overdueStages = statistics.OverdueStages,
                    efficiency = statistics.EfficiencyPercentage,
                    timestamp = DateTime.Now
                };

                return Json(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении состояния планирования");
                return Json(new { success = false, message = "Ошибка при получении данных" });
            }
        }

        #region Вспомогательные методы

        private async Task LoadPlanningOverview(PlanningIndexViewModel viewModel)
        {
            try
            {
                // Загружаем статистику
                var statistics = await _stageService.GetExecutionStatisticsAsync();

                viewModel.Overview = new PlanningOverviewViewModel
                {
                    TotalStagesInQueue = statistics.QueuedStages,
                    StagesInProgress = statistics.InProgressStages,
                    PendingStages = statistics.TotalStages - statistics.CompletedStages - statistics.InProgressStages,
                    OverdueStages = statistics.OverdueStages,
                    EfficiencyPercentage = (decimal)statistics.EfficiencyPercentage,
                    AverageWaitTime = TimeSpan.FromHours(2), // Заглушка
                    ConflictsCount = 0 // Здесь должен быть подсчет конфликтов
                };

                // Загружаем незапланированные партии
                var batches = await _batchService.GetAllAsync();
                viewModel.UnscheduledBatches = batches
                    .Where(b => b.CompletionPercentage == 0)
                    .Take(10)
                    .Select(b => new BatchSummaryViewModel
                    {
                        Id = b.Id,
                        DetailName = b.DetailName,
                        DetailNumber = b.DetailNumber,
                        Quantity = b.Quantity,
                        Priority = MapFromContractPriority(b.Priority),
                        CreatedUtc = b.CreatedUtc
                    }).ToList();

                // Загружаем критические этапы
                var queuedStages = await _schedulerService.GetQueueForecastAsync();
                viewModel.CriticalStages = queuedStages
                    .Where(q => q.IsCritical || q.Priority == Project.Contracts.Enums.Priority.Critical)
                    .Take(5)
                    .Select(q => new CriticalStageViewModel
                    {
                        Id = q.StageExecutionId,
                        DetailName = q.DetailName,
                        StageName = q.StageName,
                        MachineName = q.ExpectedMachineName,
                        Priority = MapFromContractPriority(q.Priority),
                        WaitingTime = DateTime.UtcNow - q.CreatedUtc,
                        IsOverdue = DateTime.UtcNow > q.ExpectedStartTime.AddHours(1)
                    }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке обзора планирования");
                viewModel.Overview = new PlanningOverviewViewModel();
                viewModel.UnscheduledBatches = new List<BatchSummaryViewModel>();
                viewModel.CriticalStages = new List<CriticalStageViewModel>();
            }
        }

        private async Task LoadForecastData(PlanningForecastViewModel viewModel)
        {
            try
            {
                var details = await _detailService.GetAllAsync();
                viewModel.AvailableDetails = details.Select(d => new SelectOptionViewModel
                {
                    Id = d.Id,
                    Name = $"{d.Number} - {d.Name}"
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке данных для прогноза");
                viewModel.AvailableDetails = new List<SelectOptionViewModel>();
            }
        }

        private Priority MapFromContractPriority(Project.Contracts.Enums.Priority priority)
        {
            return priority switch
            {
                Project.Contracts.Enums.Priority.Low => Priority.Low,
                Project.Contracts.Enums.Priority.Normal => Priority.Normal,
                Project.Contracts.Enums.Priority.High => Priority.High,
                Project.Contracts.Enums.Priority.Critical => Priority.Critical,
                _ => Priority.Normal
            };
        }

        #endregion
    }

    
}