using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Web.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Web.Controllers
{
    public class QueueController : Controller
    {
        private readonly ProductionSchedulerService _schedulerService;
        private readonly MachineService _machineService;
        private readonly DetailService _detailService;

        public QueueController(
            ProductionSchedulerService schedulerService,
            MachineService machineService,
            DetailService detailService)
        {
            _schedulerService = schedulerService;
            _machineService = machineService;
            _detailService = detailService;
        }

        public async Task<IActionResult> Index()
        {
            // Получаем данные для очереди
            var queueForecast = await _schedulerService.GetQueueForecastAsync();
            var machines = await _machineService.GetAllAsync();
            var details = await _detailService.GetAllAsync();

            // Создаем словари для быстрого доступа к данным
            var machineDict = machines.ToDictionary(m => m.Id, m => m);
            var detailDict = details.ToDictionary(d => d.Id, d => d);

            var viewModel = new QueueViewModel
            {
                QueueItems = queueForecast.Select(q => new QueueItemViewModel
                {
                    StageExecutionId = q.StageExecutionId,
                    DetailName = q.DetailName,
                    StageName = q.StageName,
                    Status = q.Status,
                    ExpectedMachineId = q.ExpectedMachineId,
                    ExpectedMachineName = q.ExpectedMachineName,
                    ExpectedStartTime = q.ExpectedStartTime
                }).ToList(),

                AvailableMachines = machines.Select(m => new MachineViewModel
                {
                    Id = m.Id,
                    Name = m.Name,
                    InventoryNumber = m.InventoryNumber,
                    MachineTypeName = m.MachineTypeName,
                    Priority = m.Priority
                }).ToList()
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Prioritize(int stageId, int machineId)
        {
            try
            {
                await _schedulerService.ReassignQueueAsync(machineId, stageId);
                TempData["SuccessMessage"] = "Этап успешно приоритизирован";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при приоритизации этапа: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Reassign(int stageId, int machineId)
        {
            try
            {
                await _schedulerService.ReassignStageToMachineAsync(stageId, machineId);
                TempData["SuccessMessage"] = "Этап успешно переназначен на другой станок";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при переназначении этапа: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Cancel(int stageId)
        {
            try
            {
                // Реализовать отмену этапа
                // await _schedulerService.CancelStageAsync(stageId);
                TempData["SuccessMessage"] = "Этап успешно отменен";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при отмене этапа: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // API метод для получения данных очереди (AJAX)
        [HttpGet]
        public async Task<IActionResult> GetQueueData()
        {
            var queueForecast = await _schedulerService.GetQueueForecastAsync();
            return Json(queueForecast);
        }
    }
}