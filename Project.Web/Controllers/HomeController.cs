using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Web.ViewModels;
using Project.Contracts.Enums;

namespace Project.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly BatchService _batchService;
        private readonly MachineService _machineService;
        private readonly StageExecutionService _stageService;
        private readonly ProductionSchedulerService _schedulerService;

        public HomeController(
            ILogger<HomeController> logger,
            BatchService batchService,
            MachineService machineService,
            StageExecutionService stageService,
            ProductionSchedulerService schedulerService)
        {
            _logger = logger;
            _batchService = batchService;
            _machineService = machineService;
            _stageService = stageService;
            _schedulerService = schedulerService;
        }

        /// <summary>
        /// Главная панель управления согласно ТЗ
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                var viewModel = await BuildDashboardViewModel();
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке главной панели");
                return View(new DashboardViewModel());
            }
        }

        private async Task<DashboardViewModel> BuildDashboardViewModel()
        {
            // Получаем все необходимые данные
            var allBatches = await _batchService.GetAllAsync();
            var allMachines = await _machineService.GetAllAsync();
            var queueForecast = await _schedulerService.GetQueueForecastAsync();
            var today = DateTime.Today;

            // Строим модель
            var viewModel = new DashboardViewModel
            {
                ProductionOverview = await BuildProductionOverview(allBatches, today),
                MachineOverview = BuildMachineOverview(allMachines),
                ActiveBatches = BuildActiveBatches(allBatches),
                QueuedStages = BuildQueuedStages(queueForecast),
                MachineStatusSummary = BuildMachineStatusSummary(allMachines),
                RecentEvents = await BuildRecentEvents(),
                Alerts = await BuildAlerts(allBatches, allMachines)
            };

            return viewModel;
        }

        private async Task<ProductionOverviewViewModel> BuildProductionOverview(
            List<Project.Contracts.ModelDTO.BatchDto> batches, DateTime today)
        {
            var activeBatches = batches.Where(b => b.CompletionPercentage < 100).ToList();

            // Получаем статистику выполнения этапов за сегодня
            var todayStats = await _stageService.GetExecutionStatisticsAsync(today, today.AddDays(1));

            return new ProductionOverviewViewModel
            {
                ActiveBatches = activeBatches.Count,
                WorkingMachines = await GetWorkingMachinesCount(),
                QueuedStages = todayStats.QueuedStages,
                TodayCompletedParts = await GetTodayCompletedParts(),
                OverallEfficiency = (decimal)todayStats.EfficiencyPercentage,
                ProductionUtilization = await GetProductionUtilization(),
                OverdueStages = todayStats.OverdueStages,
                TodaySetups = todayStats.SetupStages
            };
        }

        private MachineOverviewViewModel BuildMachineOverview(
            List<Project.Contracts.ModelDTO.MachineDto> machines)
        {
            return new MachineOverviewViewModel
            {
                TotalMachines = machines.Count,
                FreeMachines = machines.Count(m => m.Status == MachineStatus.Free),
                BusyMachines = machines.Count(m => m.Status == MachineStatus.Busy),
                SetupMachines = machines.Count(m => m.Status == MachineStatus.Setup),
                BrokenMachines = machines.Count(m => m.Status == MachineStatus.Broken),
                AverageUtilization = machines.Any() ?
                    machines.Where(m => m.TodayUtilizationPercent.HasValue)
                           .Select(m => m.TodayUtilizationPercent!.Value)
                           .DefaultIfEmpty(0)
                           .Average() : 0
            };
        }

        private List<ActiveBatchViewModel> BuildActiveBatches(
            List<Project.Contracts.ModelDTO.BatchDto> batches)
        {
            return batches
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
                    Priority = MapToPriority(b.Priority),
                    CreatedUtc = b.CreatedUtc,
                    EstimatedCompletionTimeUtc = b.EstimatedCompletionTimeUtc,
                    InProgressStages = b.StageStatistics.InProgressStages,
                    QueuedStages = b.StageStatistics.InQueueStages
                })
                .ToList();
        }

        private List<QueuedStageViewModel> BuildQueuedStages(
            List<Project.Contracts.ModelDTO.StageQueueDto> queueForecast)
        {
            return queueForecast
                .Take(15)
                .Select(q => new QueuedStageViewModel
                {
                    Id = q.StageExecutionId,
                    DetailName = q.DetailName,
                    StageName = q.StageName,
                    MachineName = q.ExpectedMachineName,
                    Priority = q.Priority,
                    CreatedUtc = q.CreatedUtc,
                    QueuePosition = q.QueuePosition,
                    RequiresSetup = q.RequiresSetup
                })
                .ToList();
        }

        private List<MachineStatusSummaryViewModel> BuildMachineStatusSummary(
            List<Project.Contracts.ModelDTO.MachineDto> machines)
        {
            var statusGroups = machines
                .GroupBy(m => m.Status)
                .Select(g => new MachineStatusSummaryViewModel
                {
                    Status = g.Key,
                    StatusName = GetStatusDisplayName(g.Key),
                    Count = g.Count(),
                    CssClass = GetStatusCssClass(g.Key),
                    MachineNames = g.Select(m => m.Name).ToList()
                })
                .ToList();

            return statusGroups;
        }

        private async Task<List<RecentEventViewModel>> BuildRecentEvents()
        {
            // Здесь можно получить недавние события из логов или отдельной таблицы
            // Пока что возвращаем заглушку
            return new List<RecentEventViewModel>();
        }

        private async Task<List<AlertViewModel>> BuildAlerts(
            List<Project.Contracts.ModelDTO.BatchDto> batches,
            List<Project.Contracts.ModelDTO.MachineDto> machines)
        {
            var alerts = new List<AlertViewModel>();

            // Проверяем просроченные партии
            var overdueBatches = batches.Where(b =>
                b.StageStatistics.OverdueStages > 0).Count();

            if (overdueBatches > 0)
            {
                alerts.Add(new AlertViewModel
                {
                    Type = "warning",
                    Title = "Просроченные этапы",
                    Message = $"Обнаружено {overdueBatches} партий с просроченными этапами",
                    CreatedAt = DateTime.UtcNow,
                    ActionUrl = "/Batches?status=overdue",
                    ActionText = "Просмотреть"
                });
            }

            // Проверяем неисправные станки
            var brokenMachines = machines.Count(m => m.Status == MachineStatus.Broken);
            if (brokenMachines > 0)
            {
                alerts.Add(new AlertViewModel
                {
                    Type = "danger",
                    Title = "Неисправные станки",
                    Message = $"{brokenMachines} станков требуют обслуживания",
                    CreatedAt = DateTime.UtcNow,
                    ActionUrl = "/Machines?status=broken",
                    ActionText = "Просмотреть"
                });
            }

            return alerts;
        }

        // Вспомогательные методы
        private async Task<int> GetWorkingMachinesCount()
        {
            var machines = await _machineService.GetAllAsync();
            return machines.Count(m => m.Status == MachineStatus.Busy || m.Status == MachineStatus.Setup);
        }

        private async Task<int> GetTodayCompletedParts()
        {
            // Получаем статистику за сегодня
            var stats = await _stageService.GetExecutionStatisticsAsync(DateTime.Today, DateTime.Today.AddDays(1));
            return stats.CompletedStages; // Упрощенно - количество завершенных этапов
        }

        private async Task<decimal> GetProductionUtilization()
        {
            var machines = await _machineService.GetAllAsync();
            var workingMachines = machines.Count(m => m.Status == MachineStatus.Busy || m.Status == MachineStatus.Setup);
            return machines.Count > 0 ? (decimal)(workingMachines * 100.0 / machines.Count) : 0;
        }

        private Priority MapToPriority(Project.Contracts.Enums.Priority priority)
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

        private string GetStatusCssClass(MachineStatus status) => status switch
        {
            MachineStatus.Free => "success",
            MachineStatus.Busy => "primary",
            MachineStatus.Setup => "warning",
            MachineStatus.Broken => "danger",
            _ => "secondary"
        };
    }
}