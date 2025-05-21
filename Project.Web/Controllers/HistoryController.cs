using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Contracts.ModelDTO;
using Project.Web.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Web.Controllers
{
    public class HistoryController : Controller
    {
        private readonly HistoryService _historyService;
        private readonly MachineService _machineService;
        private readonly DetailService _detailService;

        public HistoryController(
            HistoryService historyService,
            MachineService machineService,
            DetailService detailService)
        {
            _historyService = historyService;
            _machineService = machineService;
            _detailService = detailService;
        }

        public async Task<IActionResult> Index(
            DateTime? startDate = null,
            DateTime? endDate = null,
            int? machineId = null,
            int? detailId = null,
            bool includeSetups = true,
            string statusFilter = "All")
        {
            // Значения по умолчанию
            if (!startDate.HasValue)
                startDate = DateTime.Today.AddDays(-7);

            if (!endDate.HasValue)
                endDate = DateTime.Today.AddDays(1);

            // Получение списков для фильтров
            var machines = await _machineService.GetAllAsync();
            var details = await _detailService.GetAllAsync();

            // Создание фильтра
            var filter = new StageHistoryFilterDto
            {
                StartDate = startDate,
                EndDate = endDate,
                MachineId = machineId,
                DetailId = detailId,
                IncludeSetups = includeSetups,
                StatusFilter = statusFilter
            };

            // Получение данных истории
            var stageHistory = await _historyService.GetStageExecutionHistoryAsync(filter);

            // Получение статистики
            var statistics = await _historyService.GetStatisticsAsync(
                startDate.Value,
                endDate.Value,
                machineId);

            // Подготовка ViewModel
            var viewModel = new HistoryViewModel
            {
                Stages = stageHistory.Select(s => new StageHistoryViewModel
                {
                    Id = s.Id,
                    SubBatchId = s.SubBatchId,
                    BatchId = s.BatchId,
                    DetailName = s.DetailName,
                    StageName = s.StageName,
                    MachineId = s.MachineId,
                    MachineName = s.MachineName,
                    Status = s.Status,
                    StartTime = s.StartTimeUtc,
                    EndTime = s.EndTimeUtc,
                    PauseTime = s.PauseTimeUtc,
                    ResumeTime = s.ResumeTimeUtc,
                    IsSetup = s.IsSetup,
                    OperatorId = s.OperatorId,
                    ReasonNote = s.ReasonNote,
                    Duration = s.Duration
                }).ToList(),
                Filter = new HistoryFilterViewModel
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    MachineId = machineId,
                    MachineName = machines.FirstOrDefault(m => m.Id == machineId)?.Name,
                    DetailId = detailId,
                    DetailName = details.FirstOrDefault(d => d.Id == detailId)?.Name,
                    IncludeSetups = includeSetups,
                    StatusFilter = statusFilter,
                    AvailableMachines = machines.Select(m => new MachineViewModel
                    {
                        Id = m.Id,
                        Name = m.Name,
                        InventoryNumber = m.InventoryNumber,
                        MachineTypeName = m.MachineTypeName,
                        Priority = m.Priority
                    }).ToList(),
                    AvailableDetails = details.Select(d => new DetailViewModel
                    {
                        Id = d.Id,
                        Name = d.Name,
                        Number = d.Number
                    }).ToList()
                },
                Statistics = new StatisticsViewModel
                {
                    TotalStages = statistics.TotalStages,
                    CompletedStages = statistics.CompletedStages,
                    SetupStages = statistics.SetupStages,
                    TotalWorkHours = Math.Round(statistics.TotalWorkHours, 2),
                    TotalSetupHours = Math.Round(statistics.TotalSetupHours, 2),
                    TotalIdleHours = Math.Round(statistics.TotalIdleHours, 2),
                    EfficiencyPercentage = statistics.EfficiencyPercentage
                }
            };

            return View(viewModel);
        }

        // API для получения данных истории (для AJAX-обновления)
        [HttpGet]
        public async Task<IActionResult> GetHistoryData(
            DateTime startDate,
            DateTime endDate,
            int? machineId = null,
            int? detailId = null,
            bool includeSetups = true,
            string statusFilter = "All")
        {
            var filter = new StageHistoryFilterDto
            {
                StartDate = startDate,
                EndDate = endDate,
                MachineId = machineId,
                DetailId = detailId,
                IncludeSetups = includeSetups,
                StatusFilter = statusFilter
            };

            var stageHistory = await _historyService.GetStageExecutionHistoryAsync(filter);
            var statistics = await _historyService.GetStatisticsAsync(startDate, endDate, machineId);

            return Json(new
            {
                stages = stageHistory,
                statistics = statistics
            });
        }
    }
}