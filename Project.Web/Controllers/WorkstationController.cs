using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Web.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Web.Controllers
{
    public class WorkstationController : Controller
    {
        private readonly MachineService _machineService;
        private readonly StageExecutionService _stageService;
        private readonly DetailService _detailService;

        public WorkstationController(
            MachineService machineService,
            StageExecutionService stageService,
            DetailService detailService)
        {
            _machineService = machineService;
            _stageService = stageService;
            _detailService = detailService;
        }

        // Отображение списка станций
        public async Task<IActionResult> Index()
        {
            var machines = await _machineService.GetAllAsync();
            var viewModels = machines.Select(m => new MachineViewModel
            {
                Id = m.Id,
                Name = m.Name,
                InventoryNumber = m.InventoryNumber,
                MachineTypeName = m.MachineTypeName,
                Priority = m.Priority
            }).ToList();

            return View(viewModels);
        }

        // Детальная страница для станка с текущими операциями
        public async Task<IActionResult> Details(int id)
        {
            var machine = await _machineService.GetByIdAsync(id);
            if (machine == null)
                return NotFound();

            // Получаем текущий этап на станке
            var ganttData = await _stageService.GetAllStagesForGanttChart();
            var currentStage = ganttData
                .FirstOrDefault(s => s.MachineId == id && s.Status == "InProgress");

            // Получаем этапы в очереди на этот станок
            var queuedStages = ganttData
                .Where(s => s.MachineId == id && s.Status == "Waiting")
                .OrderBy(s => s.ScheduledStartTime) // Предполагаем, что у GanttStageDto есть свойство ScheduledStartTime
                .ToList();

            var viewModel = new WorkstationViewModel
            {
                MachineId = machine.Id,
                MachineName = machine.Name,
                InventoryNumber = machine.InventoryNumber,
                MachineTypeName = machine.MachineTypeName,
                CurrentStage = currentStage != null ? new StageExecutionViewModel
                {
                    Id = currentStage.Id,
                    StageName = currentStage.StageName,
                    MachineName = currentStage.MachineName,
                    Status = currentStage.Status,
                    StartTime = currentStage.StartTime,
                    EndTime = currentStage.EndTime,
                    IsSetup = currentStage.IsSetup
                } : null,
                QueuedStages = queuedStages.Select(s => new StageExecutionViewModel
                {
                    Id = s.Id,
                    StageName = s.StageName,
                    MachineName = s.MachineName,
                    Status = s.Status,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    IsSetup = s.IsSetup
                }).ToList()
            };

            return View(viewModel);
        }

        // Интерфейс оператора для конкретного станка
        public async Task<IActionResult> Operator(int id)
        {
            var machine = await _machineService.GetByIdAsync(id);
            if (machine == null)
                return NotFound();

            // Получаем текущий этап на станке
            var ganttData = await _stageService.GetAllStagesForGanttChart();
            var currentStage = ganttData
                .FirstOrDefault(s => s.MachineId == id && s.Status == "InProgress");

            // Получаем этапы в очереди на этот станок
            var queuedStages = ganttData
                .Where(s => s.MachineId == id && s.Status == "Waiting")
                .OrderBy(s => s.ScheduledStartTime)
                .ToList();

            var viewModel = new WorkstationViewModel
            {
                MachineId = machine.Id,
                MachineName = machine.Name,
                InventoryNumber = machine.InventoryNumber,
                MachineTypeName = machine.MachineTypeName,
                CurrentStage = currentStage != null ? new StageExecutionViewModel
                {
                    Id = currentStage.Id,
                    StageName = currentStage.StageName,
                    MachineName = currentStage.MachineName,
                    Status = currentStage.Status,
                    StartTime = currentStage.StartTime,
                    EndTime = currentStage.EndTime,
                    IsSetup = currentStage.IsSetup
                } : null,
                QueuedStages = queuedStages.Select(s => new StageExecutionViewModel
                {
                    Id = s.Id,
                    StageName = s.StageName,
                    MachineName = s.MachineName,
                    Status = s.Status,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    IsSetup = s.IsSetup
                }).ToList()
            };

            // Данные для начала следующего этапа, если текущий не выполняется
            if (currentStage == null && queuedStages.Any())
            {
                var nextStage = queuedStages.First();
                viewModel.NextStage = new StageExecutionViewModel
                {
                    Id = nextStage.Id,
                    StageName = nextStage.StageName,
                    MachineName = nextStage.MachineName,
                    Status = nextStage.Status,
                    StartTime = nextStage.StartTime,
                    EndTime = nextStage.EndTime,
                    IsSetup = nextStage.IsSetup
                };
            }

            return View(viewModel);
        }

        // Действия оператора на станке

        [HttpPost]
        public async Task<IActionResult> StartStage(int id)
        {
            try
            {
                await _stageService.StartStageExecution(id);
                // Получаем machineId для редиректа
                var ganttData = await _stageService.GetAllStagesForGanttChart();
                var stage = ganttData.FirstOrDefault(s => s.Id == id);
                int machineId = stage?.MachineId ?? 0;

                return RedirectToAction("Operator", new { id = machineId });
            }
            catch (Exception ex)
            {
                // Сохраняем сообщение об ошибке во временные данные
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public async Task<IActionResult> PauseStage(int id)
        {
            try
            {
                await _stageService.PauseStageExecution(id);

                // Получаем machineId для редиректа
                var ganttData = await _stageService.GetAllStagesForGanttChart();
                var stage = ganttData.FirstOrDefault(s => s.Id == id);
                int machineId = stage?.MachineId ?? 0;

                return RedirectToAction("Operator", new { id = machineId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ResumeStage(int id)
        {
            try
            {
                await _stageService.ResumeStageExecution(id);

                // Получаем machineId для редиректа
                var ganttData = await _stageService.GetAllStagesForGanttChart();
                var stage = ganttData.FirstOrDefault(s => s.Id == id);
                int machineId = stage?.MachineId ?? 0;

                return RedirectToAction("Operator", new { id = machineId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public async Task<IActionResult> CompleteStage(int id, string operatorId = null, string note = null)
        {
            try
            {
                await _stageService.CompleteStageExecution(id, operatorId, note);

                // Получаем machineId для редиректа
                var ganttData = await _stageService.GetAllStagesForGanttChart();
                var stage = ganttData.FirstOrDefault(s => s.Id == id);
                int machineId = stage?.MachineId ?? 0;

                return RedirectToAction("Operator", new { id = machineId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        // Метод для добавления примечаний к этапу
        [HttpPost]
        public async Task<IActionResult> AddNote(int id, string operatorId = null, string note = null)
        {
            try
            {
                // Получаем этап
                var ganttData = await _stageService.GetAllStagesForGanttChart();
                var stage = ganttData.FirstOrDefault(s => s.Id == id);

                if (stage == null)
                    throw new Exception("Этап не найден");

                int machineId = stage.MachineId ?? 0;

                // В зависимости от статуса этапа, выполняем соответствующее действие
                if (stage.Status == "InProgress")
                {
                    // Для работающего этапа - просто добавляем примечание без изменения статуса
                    // Примечание будет сохранено при следующем изменении статуса
                    TempData["OperatorId"] = operatorId;
                    TempData["Note"] = note;
                    TempData["SuccessMessage"] = "Примечание сохранено. Оно будет привязано к этапу при следующем изменении статуса.";
                }
                else if (stage.Status == "Paused")
                {
                    // Для приостановленного этапа - возобновляем с примечанием
                    await _stageService.ResumeStageExecution(id, operatorId, note);
                    TempData["SuccessMessage"] = "Этап возобновлен с примечанием.";
                }

                return RedirectToAction("Operator", new { id = machineId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index");
            }
        }
    }
}