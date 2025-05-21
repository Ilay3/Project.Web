using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Web.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Web.Controllers
{
    public class GanttController : Controller
    {
        private readonly StageExecutionService _stageService;
        private readonly ProductionSchedulerService _schedulerService;
        private readonly BatchService _batchService;
        private readonly MachineService _machineService;

        public GanttController(
            StageExecutionService stageService,
            ProductionSchedulerService schedulerService,
            BatchService batchService,
            MachineService machineService)
        {
            _stageService = stageService;
            _schedulerService = schedulerService;
            _batchService = batchService;
            _machineService = machineService;
        }

        public async Task<IActionResult> Index()
        {
            // Получаем данные для диаграммы
            var ganttData = await _stageService.GetAllStagesForGanttChart();
            var machines = await _machineService.GetAllAsync();
            var queueForecast = await _schedulerService.GetQueueForecastAsync();

            var viewModel = new GanttViewModel
            {
                Stages = ganttData.Select(s => new GanttStageViewModel
                {
                    Id = s.Id,
                    BatchId = s.BatchId,
                    SubBatchId = s.SubBatchId,
                    DetailName = s.DetailName,
                    StageName = s.StageName,
                    MachineName = s.MachineName,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    Status = s.Status,
                    IsSetup = s.IsSetup,
                    PlannedDuration = s.PlannedDuration
                }).ToList(),
                Machines = machines.Select(m => new MachineViewModel
                {
                    Id = m.Id,
                    Name = m.Name,
                    InventoryNumber = m.InventoryNumber,
                    MachineTypeName = m.MachineTypeName,
                    Priority = m.Priority
                }).ToList(),
                QueueItems = queueForecast.Select(q => new StageQueueViewModel
                {
                    StageExecutionId = q.StageExecutionId,
                    SubBatchId = q.SubBatchId,
                    DetailName = q.DetailName,
                    StageName = q.StageName,
                    Status = q.Status,
                    ExpectedMachineId = q.ExpectedMachineId,
                    ExpectedMachineName = q.ExpectedMachineName,
                    ExpectedStartTime = q.ExpectedStartTime
                }).ToList()
            };

            return View(viewModel);
        }

        // Действия для управления этапами

        [HttpPost]
        public async Task<IActionResult> StartStage(int id)
        {
            try
            {
                await _stageService.StartStageExecution(id);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> PauseStage(int id)
        {
            try
            {
                await _stageService.PauseStageExecution(id);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ResumeStage(int id)
        {
            try
            {
                await _stageService.ResumeStageExecution(id);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CompleteStage(int id)
        {
            try
            {
                await _stageService.CompleteStageExecution(id);
                await _schedulerService.HandleStageCompletionAsync(id);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ReassignStage(int stageId, int machineId)
        {
            try
            {
                await _schedulerService.ReassignStageToMachineAsync(stageId, machineId);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> PrioritizeStage(int stageId, int machineId)
        {
            try
            {
                await _schedulerService.ReassignQueueAsync(machineId, stageId);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // API для обновления данных диаграммы
        [HttpGet]
        public async Task<IActionResult> GetGanttData()
        {
            var ganttData = await _stageService.GetAllStagesForGanttChart();
            return Json(ganttData);
        }

        // API для получения прогноза очереди
        [HttpGet]
        public async Task<IActionResult> GetQueueForecast()
        {
            var queueForecast = await _schedulerService.GetQueueForecastAsync();
            return Json(queueForecast);
        }
    }
}