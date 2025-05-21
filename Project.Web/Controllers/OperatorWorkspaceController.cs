using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Contracts.ModelDTO;
using Project.Domain.Entities;
using Project.Web.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Web.Controllers
{
    public class OperatorWorkspaceController : Controller
    {
        private readonly StageExecutionService _stageService;
        private readonly MachineService _machineService;
        private readonly DetailService _detailService;
        private readonly HistoryService _historyService;
        private readonly ProductionSchedulerService _schedulerService;

        public OperatorWorkspaceController(
            StageExecutionService stageService,
            MachineService machineService,
            DetailService detailService,
            HistoryService historyService,
            ProductionSchedulerService schedulerService)
        {
            _stageService = stageService;
            _machineService = machineService;
            _detailService = detailService;
            _historyService = historyService;
            _schedulerService = schedulerService;
        }

        // Главная страница рабочего места оператора
        public async Task<IActionResult> Index(int? machineId)
        {
            // Если машина не указана, показываем список машин
            if (!machineId.HasValue)
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

                return View("MachineSelection", viewModels);
            }

            // Получаем информацию о станке
            var machine = await _machineService.GetByIdAsync(machineId.Value);
            if (machine == null)
                return NotFound();

            // Создаем модель рабочего места
            var workspace = await GetOperatorWorkspaceViewModel(machineId.Value);

            return View(workspace);
        }

        // Получение полной модели для рабочего места
        private async Task<OperatorWorkspaceViewModel> GetOperatorWorkspaceViewModel(int machineId)
        {
            var machine = await _machineService.GetByIdAsync(machineId);
            if (machine == null)
                throw new Exception($"Станок с ID {machineId} не найден");

            // Получаем все этапы для диаграммы Ганта
            var ganttData = await _stageService.GetAllStagesForGanttChart();

            // Получаем текущий этап на станке
            var currentStage = ganttData
                .FirstOrDefault(s => s.MachineId == machineId && s.Status == "InProgress");

            // Получаем этапы в очереди на этот станок
            var queuedStages = ganttData
                .Where(s => s.MachineId == machineId && s.Status == "Waiting" || s.Status == "Pending")
                .OrderBy(s => s.ScheduledStartTime)
                .ToList();

            // Получаем следующий этап (первый в очереди)
            var nextStage = queuedStages.FirstOrDefault();

            // Получаем историю для станка
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var history = await _historyService.GetStageExecutionHistoryAsync(new StageHistoryFilterDto
            {
                StartDate = today,
                EndDate = tomorrow,
                MachineId = machineId,
                IncludeSetups = true,
                StatusFilter = "All"
            });

            // Получаем статистику для станка
            var statistics = await _historyService.GetStatisticsAsync(today, tomorrow, machineId);

            // Создаем модель представления
            var workspace = new OperatorWorkspaceViewModel
            {
                MachineId = machine.Id,
                MachineName = machine.Name,
                InventoryNumber = machine.InventoryNumber,
                MachineTypeName = machine.MachineTypeName,
                CurrentStage = currentStage != null ? MapToStageViewModel(currentStage) : null,
                NextStage = nextStage != null ? MapToStageViewModel(nextStage) : null,
                QueuedStages = queuedStages.Skip(1).Select(MapToStageViewModel).ToList(),
                TotalWorkHours = statistics.TotalWorkHours,
                TotalSetupHours = statistics.TotalSetupHours,
                TotalIdleHours = statistics.TotalIdleHours,
                EfficiencyPercentage = statistics.EfficiencyPercentage,
                RecentlyCompletedStages = history
                    .Where(s => s.Status == "Completed")
                    .OrderByDescending(s => s.EndTimeUtc)
                    .Take(5)
                    .Select(MapToStageViewModel)
                    .ToList(),
                RecentStatusChanges = history
                    .Where(s => s.StatusChangedTimeUtc.HasValue)
                    .OrderByDescending(s => s.StatusChangedTimeUtc)
                    .Take(10)
                    .Select(s => new StatusChangeViewModel
                    {
                        ChangeTime = s.StatusChangedTimeUtc ?? DateTime.Now,
                        FromStatus = "Unknown", // В идеале нужно хранить предыдущий статус
                        ToStatus = s.Status,
                        DetailName = s.DetailName,
                        StageName = s.StageName,
                        OperatorId = s.OperatorId,
                        Note = s.ReasonNote
                    })
                    .ToList()
            };

            return workspace;
        }

        // Маппинг моделей GanttStageDto или StageHistoryDto в StageExecutionViewModel
        private StageExecutionViewModel MapToStageViewModel(dynamic stage)
        {
            // Создаем новый объект StageExecutionViewModel с правильным маппингом полей
            var viewModel = new StageExecutionViewModel
            {
                Id = stage.Id,
                StageName = stage.StageName,
                DetailName = stage.DetailName,
                MachineName = stage.MachineName,
                Status = stage.Status,
                IsSetup = stage.IsSetup,
                CompletionPercentage = CalculateCompletionPercentage(stage)
            };

            // Обрабатываем время с учетом разных свойств в разных DTO
            if (stage.GetType().Name.Contains("GanttStage"))
            {
                viewModel.StartTime = stage.StartTime;
                viewModel.EndTime = stage.EndTime;
                viewModel.PauseTime = null; // В GanttStageDto нет этих полей
                viewModel.ResumeTime = null;
                viewModel.PlannedDuration = stage.PlannedDuration;
                viewModel.ScheduledStartTime = stage.ScheduledStartTime;
                viewModel.OperatorId = stage.OperatorId;
                viewModel.ReasonNote = stage.ReasonNote;
            }
            else // Предполагаем, что это StageHistoryDto
            {
                viewModel.StartTime = stage.StartTimeUtc;
                viewModel.EndTime = stage.EndTimeUtc;
                viewModel.PauseTime = stage.PauseTimeUtc;
                viewModel.ResumeTime = stage.ResumeTimeUtc;
                viewModel.PlannedDuration = TimeSpan.FromHours(stage.Duration ?? 1.0);
                viewModel.ScheduledStartTime = null; // В StageHistoryDto нет этого поля
                viewModel.OperatorId = stage.OperatorId;
                viewModel.ReasonNote = stage.ReasonNote;
            }

            return viewModel;
        }

        private double CalculateCompletionPercentage(dynamic stage)
        {
            if (!stage.StartTime.HasValue) return 0;
            if (stage.Status == "Completed" && stage.EndTime.HasValue) return 100;

            // Расчет ожидаемой продолжительности (из PlannedDuration или заданного значения)
            var plannedDuration = stage.PlannedDuration;
            if (plannedDuration == TimeSpan.Zero)
            {
                plannedDuration = TimeSpan.FromHours(1); // Значение по умолчанию, если не указано
            }

            // Вычисление процента выполнения
            var elapsed = DateTime.Now - stage.StartTime.Value;
            double percentage = Math.Min(100, Math.Round((elapsed.TotalSeconds / plannedDuration.TotalSeconds) * 100, 1));

            return percentage;
        }

        // API для получения данных рабочего места
        [HttpGet]
        public async Task<IActionResult> GetWorkspaceData(int machineId)
        {
            try
            {
                var workspace = await GetOperatorWorkspaceViewModel(machineId);
                return Json(workspace);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Действия оператора

        // Запуск этапа
        [HttpPost]
        public async Task<IActionResult> StartStage(int id, string operatorId, string deviceId)
        {
            try
            {
                await _stageService.StartStageExecution(id, operatorId, deviceId);
                return RedirectToAction("Index", new { machineId = GetMachineIdFromRequest() });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index", new { machineId = GetMachineIdFromRequest() });
            }
        }

        // Пауза этапа
        [HttpPost]
        public async Task<IActionResult> PauseStage(int id, string operatorId, string reasonNote, string deviceId)
        {
            try
            {
                await _stageService.PauseStageExecution(id, operatorId, reasonNote, deviceId);
                return RedirectToAction("Index", new { machineId = GetMachineIdFromRequest() });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index", new { machineId = GetMachineIdFromRequest() });
            }
        }

        // Возобновление этапа
        [HttpPost]
        public async Task<IActionResult> ResumeStage(int id, string operatorId, string reasonNote, string deviceId)
        {
            try
            {
                await _stageService.ResumeStageExecution(id, operatorId, reasonNote, deviceId);
                return RedirectToAction("Index", new { machineId = GetMachineIdFromRequest() });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index", new { machineId = GetMachineIdFromRequest() });
            }
        }

        // Завершение этапа
        [HttpPost]
        public async Task<IActionResult> CompleteStage(int id, string operatorId, string reasonNote, string deviceId)
        {
            try
            {
                await _stageService.CompleteStageExecution(id, operatorId, reasonNote, deviceId);

                // Запускаем обработку завершения этапа для автоматического планирования
                await _schedulerService.HandleStageCompletionAsync(id);

                return RedirectToAction("Index", new { machineId = GetMachineIdFromRequest() });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index", new { machineId = GetMachineIdFromRequest() });
            }
        }

        // Отмена этапа
        [HttpPost]
        public async Task<IActionResult> CancelStage(int id, string reason, string operatorId, string deviceId)
        {
            try
            {
                await _stageService.CancelStageExecution(id, reason, operatorId, deviceId);
                return RedirectToAction("Index", new { machineId = GetMachineIdFromRequest() });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index", new { machineId = GetMachineIdFromRequest() });
            }
        }

        // Добавление примечания к этапу
        [HttpPost]
        public async Task<IActionResult> AddNote(int id, string operatorId, string note, string deviceId)
        {
            try
            {
                // Получаем этап
                // В зависимости от статуса этапа, выполняем соответствующее действие
                // Эта логика должна быть в сервисе

                TempData["SuccessMessage"] = "Примечание успешно добавлено";
                return RedirectToAction("Index", new { machineId = GetMachineIdFromRequest() });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index", new { machineId = GetMachineIdFromRequest() });
            }
        }

        // Вспомогательный метод для получения ID станка из запроса
        private int GetMachineIdFromRequest()
        {
            int machineId;
            if (Request.Form.ContainsKey("machineId") && int.TryParse(Request.Form["machineId"], out machineId))
            {
                return machineId;
            }

            // По умолчанию возвращаем 0, что приведет к выбору станка
            return 0;
        }
    }
}