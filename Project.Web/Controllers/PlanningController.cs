using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Contracts.ModelDTO;
using Project.Web.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Web.Controllers
{
    public class PlanningController : Controller
    {
        private readonly PlanningService _planningService;
        private readonly DetailService _detailService;
        private readonly MachineService _machineService;

        public PlanningController(
            PlanningService planningService,
            DetailService detailService,
            MachineService machineService)
        {
            _planningService = planningService;
            _detailService = detailService;
            _machineService = machineService;
        }

        public async Task<IActionResult> Index()
        {
            // По умолчанию показываем план на текущий месяц
            var startDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            return await GetPlanSummary(startDate, endDate);
        }

        [HttpGet]
        public async Task<IActionResult> Summary(DateTime? startDate = null, DateTime? endDate = null)
        {
            if (!startDate.HasValue)
                startDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

            if (!endDate.HasValue)
                endDate = startDate.Value.AddMonths(1).AddDays(-1);

            return await GetPlanSummary(startDate.Value, endDate.Value);
        }

        private async Task<IActionResult> GetPlanSummary(DateTime startDate, DateTime endDate)
        {
            try
            {
                var summary = await _planningService.GetPlanSummaryAsync(startDate, endDate);

                var viewModel = new PlanSummaryViewModel
                {
                    StartDate = summary.StartDate,
                    EndDate = summary.EndDate,
                    TotalBatches = summary.TotalBatches,
                    TotalQuantity = summary.TotalQuantity,
                    CompletedQuantity = summary.CompletedQuantity,
                    CompletionPercent = summary.TotalQuantity > 0 ?
                        Math.Round(((double)summary.CompletedQuantity / summary.TotalQuantity) * 100, 1) : 0,
                    DetailStats = summary.DetailStats.Select(d => new DetailPlanStatsViewModel
                    {
                        DetailId = d.DetailId,
                        DetailName = d.DetailName,
                        PlannedQuantity = d.PlannedQuantity,
                        CompletedQuantity = d.CompletedQuantity,
                        CompletionPercent = d.CompletionPercent
                    }).ToList(),
                    MachineStats = summary.MachineStats.Select(m => new MachinePlanStatsViewModel
                    {
                        MachineId = m.MachineId,
                        MachineName = m.MachineName,
                        TotalHours = m.TotalHours,
                        SetupHours = m.SetupHours,
                        StageCount = m.StageCount,
                        CompletedStageCount = m.CompletedStageCount,
                        EfficiencyPercent = m.EfficiencyPercent,
                        CompletionPercent = m.CompletionPercent
                    }).ToList()
                };

                return View("Summary", viewModel);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при получении сводки: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            // Получаем список всех деталей для выпадающего списка
            var details = await _detailService.GetAllAsync();

            var viewModel = new CreatePlanViewModel
            {
                Items = new List<PlanItemViewModel>(),
                AvailableDetails = details.Select(d => new DetailViewModel
                {
                    Id = d.Id,
                    Name = d.Name,
                    Number = d.Number
                }).ToList()
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreatePlanViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                // Восстанавливаем список деталей
                var details = await _detailService.GetAllAsync();
                viewModel.AvailableDetails = details.Select(d => new DetailViewModel
                {
                    Id = d.Id,
                    Name = d.Name,
                    Number = d.Number
                }).ToList();

                return View(viewModel);
            }

            try
            {
                // Преобразуем ViewModel в DTO
                var planItems = viewModel.Items.Select(item => new PlanItemDto
                {
                    DetailId = item.DetailId,
                    Quantity = item.Quantity,
                    OptimalBatchSize = item.OptimalBatchSize,
                    SubBatchSizes = item.SubBatchSizes
                }).ToList();

                // Создаем план производства
                int batchCount = await _planningService.CreateProductionPlanAsync(
                    planItems,
                    viewModel.PlanName,
                    viewModel.TargetDate);

                TempData["SuccessMessage"] = $"План производства создан успешно. Создано {batchCount} производственных заданий.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при создании плана: {ex.Message}";

                // Восстанавливаем список деталей
                var details = await _detailService.GetAllAsync();
                viewModel.AvailableDetails = details.Select(d => new DetailViewModel
                {
                    Id = d.Id,
                    Name = d.Name,
                    Number = d.Number
                }).ToList();

                return View(viewModel);
            }
        }

        [HttpGet]
        public async Task<IActionResult> OptimalBatchSize(int detailId)
        {
            try
            {
                var result = await _planningService.CalculateOptimalBatchSizeAsync(detailId);

                return Json(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}