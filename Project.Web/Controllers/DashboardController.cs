using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Contracts.Enums;
using Project.Web.ViewModels;

namespace Project.Web.Controllers
{
    /// <summary>
    /// Контроллер главной панели управления согласно ТЗ
    /// </summary>
    public class DashboardController : Controller
    {
        private readonly BatchService _batchService;
        private readonly MachineService _machineService;
        private readonly StageExecutionService _stageService;
        private readonly HistoryService _historyService;
        private readonly ProductionSchedulerService _schedulerService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(
            BatchService batchService,
            MachineService machineService,
            StageExecutionService stageService,
            HistoryService historyService,
            ProductionSchedulerService schedulerService,
            ILogger<DashboardController> logger)
        {
            _batchService = batchService;
            _machineService = machineService;
            _stageService = stageService;
            _historyService = historyService;
            _schedulerService = schedulerService;
            _logger = logger;
        }

        /// <summary>
        /// Главная панель управления согласно ТЗ
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                var viewModel = new DashboardViewModel();

                // Загружаем данные параллельно для лучшей производительности
                var tasks = new List<Task>
                {
                    LoadProductionOverviewAsync(viewModel),
                    LoadMachineOverviewAsync(viewModel),
                    LoadActiveBatchesAsync(viewModel),
                    LoadQueuedStagesAsync(viewModel),
                    LoadMachineStatusSummaryAsync(viewModel),
                    LoadRecentEventsAsync(viewModel),
                    LoadAlertsAsync(viewModel)
                };

                await Task.WhenAll(tasks);

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке главной панели управления");
                return View(new DashboardViewModel());
            }
        }

        /// <summary>
        /// API для получения текущих данных дашборда
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCurrentData()
        {
            try
            {
                var viewModel = new DashboardViewModel();
                await LoadProductionOverviewAsync(viewModel);
                await LoadMachineOverviewAsync(viewModel);

                return Json(new
                {
                    success = true,
                    productionOverview = viewModel.ProductionOverview,
                    machineOverview = viewModel.MachineOverview,
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении текущих данных дашборда");
                return Json(new { success = false, message = "Ошибка при загрузке данных" });
            }
        }

        /// <summary>
        /// API для получения очереди этапов
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetQueueData()
        {
            try
            {
                var queuedStages = await _schedulerService.GetQueueForecastAsync();

                var result = queuedStages.Take(10).Select(q => new
                {
                    id = q.StageExecutionId,
                    detailName = q.DetailName,
                    stageName = q.StageName,
                    machineName = q.ExpectedMachineName,
                    priority = q.Priority.ToString(),
                    waitingTime = (DateTime.UtcNow - q.CreatedUtc).TotalMinutes,
                    queuePosition = q.QueuePosition
                });

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении данных очереди");
                return Json(new List<object>());
            }
        }

        #region Приватные методы загрузки данных

        private async Task LoadProductionOverviewAsync(DashboardViewModel viewModel)
        {
            try
            {
                var batches = await _batchService.GetAllAsync();
                var machines = await _machineService.GetAllAsync();
                var statistics = await _stageService.GetExecutionStatisticsAsync(DateTime.Today, DateTime.Today.AddDays(1));

                // Получаем статистику за сегодня
                var todayStats = await _historyService.GetStatisticsAsync(DateTime.Today, DateTime.Today.AddDays(1));

                viewModel.ProductionOverview = new ProductionOverviewViewModel
                {
                    ActiveBatches = batches.Count(b => b.CompletionPercentage < 100),
                    WorkingMachines = machines.Count(m => m.Status == MachineStatus.Busy || m.Status == MachineStatus.Setup),
                    QueuedStages = statistics.QueuedStages,
                    TodayCompletedParts = (int)(todayStats?.CompletedStages ?? 0),
                    OverallEfficiency = (decimal)(statistics.EfficiencyPercentage),
                    ProductionUtilization = CalculateProductionUtilization(machines),
                    OverdueStages = statistics.OverdueStages,
                    TodaySetups = statistics.SetupStages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке обзора производства");
                viewModel.ProductionOverview = new ProductionOverviewViewModel();
            }
        }

        private async Task LoadMachineOverviewAsync(DashboardViewModel viewModel)
        {
            try
            {
                var machines = await _machineService.GetAllAsync();

                viewModel.MachineOverview = new MachineOverviewViewModel
                {
                    TotalMachines = machines.Count,
                    FreeMachines = machines.Count(m => m.Status == MachineStatus.Free),
                    BusyMachines = machines.Count(m => m.Status == MachineStatus.Busy),
                    SetupMachines = machines.Count(m => m.Status == MachineStatus.Setup),
                    BrokenMachines = machines.Count(m => m.Status == MachineStatus.Broken),
                    AverageUtilization = machines.Where(m => m.TodayUtilizationPercent.HasValue)
                                               .Average(m => m.TodayUtilizationPercent ?? 0)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке обзора станков");
                viewModel.MachineOverview = new MachineOverviewViewModel();
            }
        }

        private async Task LoadActiveBatchesAsync(DashboardViewModel viewModel)
        {
            try
            {
                var batches = await _batchService.GetAllAsync();

                viewModel.ActiveBatches = batches
                    .Where(b => b.CompletionPercentage < 100)
                    .OrderByDescending(b => b.Priority)
                    .ThenBy(b => b.CreatedUtc)
                    .Take(10)
                    .Select(b => new ActiveBatchViewModel
                    {
                        Id = b.Id,
                        DetailName = b.DetailName,
                        DetailNumber = b.DetailNumber,
                        Quantity = b.Quantity,
                        CompletionPercentage = b.CompletionPercentage,
                        Priority = MapFromContractPriority(b.Priority),
                        CreatedUtc = b.CreatedUtc,
                        EstimatedCompletionTimeUtc = b.EstimatedCompletionTimeUtc,
                        InProgressStages = b.StageStatistics?.InProgressStages ?? 0,
                        QueuedStages = b.StageStatistics?.InQueueStages ?? 0
                    }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке активных партий");
                viewModel.ActiveBatches = new List<ActiveBatchViewModel>();
            }
        }

        private async Task LoadQueuedStagesAsync(DashboardViewModel viewModel)
        {
            try
            {
                var queuedStages = await _schedulerService.GetQueueForecastAsync();

                viewModel.QueuedStages = queuedStages
                    .Take(15)
                    .Select(q => new QueuedStageViewModel
                    {
                        Id = q.StageExecutionId,
                        DetailName = q.DetailName,
                        StageName = q.StageName,
                        MachineName = q.ExpectedMachineName,
                        Priority = MapFromContractPriority(q.Priority),
                        CreatedUtc = q.CreatedUtc,
                        QueuePosition = q.QueuePosition,
                        RequiresSetup = q.RequiresSetup
                    }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке очереди этапов");
                viewModel.QueuedStages = new List<QueuedStageViewModel>();
            }
        }

        private async Task LoadMachineStatusSummaryAsync(DashboardViewModel viewModel)
        {
            try
            {
                var machines = await _machineService.GetAllAsync();

                var statusGroups = machines.GroupBy(m => m.Status);

                viewModel.MachineStatusSummary = statusGroups.Select(g => new MachineStatusSummaryViewModel
                {
                    Status = g.Key,
                    StatusName = GetStatusDisplayName(g.Key),
                    Count = g.Count(),
                    CssClass = GetStatusCssClass(g.Key),
                    MachineNames = g.Select(m => m.Name).ToList()
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке сводки по статусам станков");
                viewModel.MachineStatusSummary = new List<MachineStatusSummaryViewModel>();
            }
        }

        private async Task LoadRecentEventsAsync(DashboardViewModel viewModel)
        {
            try
            {
                // Получаем недавние события из истории
                var recentHistory = await _historyService.GetStageExecutionHistoryAsync(
                    DateTime.Today, DateTime.Now.AddDays(1));

                viewModel.RecentEvents = recentHistory
                    .OrderByDescending(h => h.StatusChangedTimeUtc)
                    .Take(10)
                    .Select(h => new RecentEventViewModel
                    {
                        EventType = GetEventType(h.Status),
                        Message = $"{h.DetailName} - {h.StageName} {GetStatusDisplayName(h.Status)}",
                        Timestamp = h.StatusChangedTimeUtc,
                        Icon = GetEventIcon(h.Status),
                        CssClass = GetEventCssClass(h.Status),
                        RelatedUrl = Url.Action("Details", "Batches", new { id = h.BatchId })
                    }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке недавних событий");
                viewModel.RecentEvents = new List<RecentEventViewModel>();
            }
        }

        private async Task LoadAlertsAsync(DashboardViewModel viewModel)
        {
            try
            {
                var alerts = new List<AlertViewModel>();

                // Получаем статистику для алертов
                var statistics = await _stageService.GetExecutionStatisticsAsync();

                // Алерт о просроченных этапах
                if (statistics.OverdueStages > 0)
                {
                    alerts.Add(new AlertViewModel
                    {
                        Type = "warning",
                        Title = "Просроченные этапы",
                        Message = $"Найдено {statistics.OverdueStages} просроченных этапов",
                        CreatedAt = DateTime.Now,
                        ActionUrl = Url.Action("Index", "History", new { IsOverdueOnly = true }),
                        ActionText = "Просмотреть"
                    });
                }

                // Алерт о низкой эффективности
                if (statistics.EfficiencyPercentage < 70)
                {
                    alerts.Add(new AlertViewModel
                    {
                        Type = "danger",
                        Title = "Низкая эффективность",
                        Message = $"Эффективность производства: {statistics.EfficiencyPercentage:F1}%",
                        CreatedAt = DateTime.Now,
                        ActionUrl = Url.Action("Index", "Reports"),
                        ActionText = "Отчеты"
                    });
                }

                // Алерт о большой очереди
                if (statistics.QueuedStages > 20)
                {
                    alerts.Add(new AlertViewModel
                    {
                        Type = "info",
                        Title = "Большая очередь",
                        Message = $"В очереди {statistics.QueuedStages} этапов",
                        CreatedAt = DateTime.Now,
                        ActionUrl = Url.Action("Index", "Gantt"),
                        ActionText = "Диаграмма Ганта"
                    });
                }

                viewModel.Alerts = alerts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке алертов");
                viewModel.Alerts = new List<AlertViewModel>();
            }
        }

        #endregion

        #region Вспомогательные методы

        private decimal CalculateProductionUtilization(List<Project.Contracts.ModelDTO.MachineDto> machines)
        {
            if (!machines.Any()) return 0;

            var utilizationSum = machines
                .Where(m => m.TodayUtilizationPercent.HasValue)
                .Sum(m => m.TodayUtilizationPercent ?? 0);

            return utilizationSum / machines.Count;
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

        private string GetStatusDisplayName(MachineStatus status) => status switch
        {
            MachineStatus.Free => "Свободен",
            MachineStatus.Busy => "Занят",
            MachineStatus.Setup => "Переналадка",
            MachineStatus.Broken => "Неисправен",
            _ => "Неизвестно"
        };

        private string GetStatusDisplayName(Project.Domain.Entities.StageExecutionStatus status) => status switch
        {
            Project.Domain.Entities.StageExecutionStatus.Completed => "завершен",
            Project.Domain.Entities.StageExecutionStatus.InProgress => "запущен",
            Project.Domain.Entities.StageExecutionStatus.Paused => "приостановлен",
            Project.Domain.Entities.StageExecutionStatus.Error => "отменен",
            _ => "изменен"
        };

        private string GetStatusCssClass(MachineStatus status) => status switch
        {
            MachineStatus.Free => "success",
            MachineStatus.Busy => "primary",
            MachineStatus.Setup => "warning",
            MachineStatus.Broken => "danger",
            _ => "secondary"
        };

        private string GetEventType(Project.Domain.Entities.StageExecutionStatus status) => status switch
        {
            Project.Domain.Entities.StageExecutionStatus.Completed => "completion",
            Project.Domain.Entities.StageExecutionStatus.InProgress => "start",
            Project.Domain.Entities.StageExecutionStatus.Paused => "pause",
            Project.Domain.Entities.StageExecutionStatus.Error => "error",
            _ => "status_change"
        };

        private string GetEventIcon(Project.Domain.Entities.StageExecutionStatus status) => status switch
        {
            Project.Domain.Entities.StageExecutionStatus.Completed => "fas fa-check-circle",
            Project.Domain.Entities.StageExecutionStatus.InProgress => "fas fa-play-circle",
            Project.Domain.Entities.StageExecutionStatus.Paused => "fas fa-pause-circle",
            Project.Domain.Entities.StageExecutionStatus.Error => "fas fa-times-circle",
            _ => "fas fa-info-circle"
        };

        private string GetEventCssClass(Project.Domain.Entities.StageExecutionStatus status) => status switch
        {
            Project.Domain.Entities.StageExecutionStatus.Completed => "text-success",
            Project.Domain.Entities.StageExecutionStatus.InProgress => "text-primary",
            Project.Domain.Entities.StageExecutionStatus.Paused => "text-warning",
            Project.Domain.Entities.StageExecutionStatus.Error => "text-danger",
            _ => "text-info"
        };

        #endregion
    }
}