using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Domain.Repositories;
using System;
using System.Threading.Tasks;

namespace Project.Web.Controllers
{
    [Route("api/stages")]
    [ApiController]
    public class StageManagementController : ControllerBase
    {
        private readonly StageExecutionService _stageService;
        private readonly ProductionSchedulerService _schedulerService;
        private readonly IBatchRepository _batchRepo;

        public StageManagementController(
            StageExecutionService stageService,
            ProductionSchedulerService schedulerService,
            IBatchRepository batchRepo)
        {
            _stageService = stageService;
            _schedulerService = schedulerService;
            _batchRepo = batchRepo;
        }

        /// <summary>
        /// Запуск этапа
        /// </summary>
        [HttpPost("{stageId}/start")]
        public async Task<IActionResult> StartStage(int stageId, [FromBody] StageActionRequest request = null)
        {
            try
            {
                await _stageService.StartStageExecution(
                    stageId,
                    request?.OperatorId ?? "MANUAL",
                    request?.DeviceId ?? "WEB");

                return Ok(new { success = true, message = "Этап успешно запущен" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = $"Ошибка при запуске этапа: {ex.Message}" });
            }
        }

        /// <summary>
        /// Приостановка этапа
        /// </summary>
        [HttpPost("{stageId}/pause")]
        public async Task<IActionResult> PauseStage(int stageId, [FromBody] StageActionRequest request)
        {
            try
            {
                await _stageService.PauseStageExecution(
                    stageId,
                    request?.OperatorId ?? "MANUAL",
                    request?.ReasonNote ?? "Приостановлено оператором",
                    request?.DeviceId ?? "WEB");

                return Ok(new { success = true, message = "Этап приостановлен" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = $"Ошибка при приостановке этапа: {ex.Message}" });
            }
        }

        /// <summary>
        /// Возобновление этапа
        /// </summary>
        [HttpPost("{stageId}/resume")]
        public async Task<IActionResult> ResumeStage(int stageId, [FromBody] StageActionRequest request = null)
        {
            try
            {
                await _stageService.ResumeStageExecution(
                    stageId,
                    request?.OperatorId ?? "MANUAL",
                    request?.ReasonNote ?? "Возобновлено оператором",
                    request?.DeviceId ?? "WEB");

                return Ok(new { success = true, message = "Этап возобновлен" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = $"Ошибка при возобновлении этапа: {ex.Message}" });
            }
        }

        /// <summary>
        /// Завершение этапа
        /// </summary>
        [HttpPost("{stageId}/complete")]
        public async Task<IActionResult> CompleteStage(int stageId, [FromBody] StageActionRequest request = null)
        {
            try
            {
                await _stageService.CompleteStageExecution(
                    stageId,
                    request?.OperatorId ?? "MANUAL",
                    request?.ReasonNote ?? "Завершено оператором",
                    request?.DeviceId ?? "WEB");

                // Обрабатываем завершение этапа для запуска следующих
                await _schedulerService.HandleStageCompletionAsync(stageId);

                return Ok(new { success = true, message = "Этап завершен" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = $"Ошибка при завершении этапа: {ex.Message}" });
            }
        }

        /// <summary>
        /// Отмена этапа
        /// </summary>
        [HttpPost("{stageId}/cancel")]
        public async Task<IActionResult> CancelStage(int stageId, [FromBody] StageActionRequest request)
        {
            try
            {
                await _stageService.CancelStageExecution(
                    stageId,
                    request?.ReasonNote ?? "Отменено оператором",
                    request?.OperatorId ?? "MANUAL",
                    request?.DeviceId ?? "WEB");

                return Ok(new { success = true, message = "Этап отменен" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = $"Ошибка при отмене этапа: {ex.Message}" });
            }
        }

        /// <summary>
        /// Назначение этапа на станок
        /// </summary>
        [HttpPost("{stageId}/assign")]
        public async Task<IActionResult> AssignStage(int stageId, [FromBody] AssignStageRequest request)
        {
            try
            {
                await _stageService.AssignStageToMachine(stageId, request.MachineId);
                return Ok(new { success = true, message = "Этап назначен на станок" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = $"Ошибка при назначении этапа: {ex.Message}" });
            }
        }

        /// <summary>
        /// Получение информации об этапе
        /// </summary>
        [HttpGet("{stageId}")]
        public async Task<IActionResult> GetStageInfo(int stageId)
        {
            try
            {
                var stage = await _batchRepo.GetStageExecutionByIdAsync(stageId);
                if (stage == null)
                    return NotFound();

                // Рассчитываем продолжительность
                TimeSpan? duration = null;
                if (stage.StartTimeUtc.HasValue)
                {
                    var endTime = stage.EndTimeUtc ?? DateTime.UtcNow;
                    duration = endTime - stage.StartTimeUtc.Value;
                }

                // Рассчитываем плановую продолжительность
                var plannedDuration = stage.IsSetup
                    ? TimeSpan.FromHours(stage.RouteStage.SetupTime)
                    : TimeSpan.FromHours(stage.RouteStage.NormTime * stage.SubBatch.Quantity);

                var result = new
                {
                    id = stage.Id,
                    subBatchId = stage.SubBatchId,
                    batchId = stage.SubBatch.BatchId,
                    detailName = stage.SubBatch.Batch.Detail.Name,
                    stageName = stage.RouteStage.Name,
                    machineId = stage.MachineId,
                    machineName = stage.Machine?.Name,
                    status = stage.Status.ToString(),
                    statusDisplay = GetStatusDisplayName(stage.Status.ToString()),
                    isSetup = stage.IsSetup,
                    startTime = stage.StartTimeUtc,
                    endTime = stage.EndTimeUtc,
                    pauseTime = stage.PauseTimeUtc,
                    resumeTime = stage.ResumeTimeUtc,
                    duration = duration?.TotalHours,
                    durationFormatted = FormatDuration(duration),
                    plannedDuration = plannedDuration.TotalHours,
                    plannedDurationFormatted = FormatDuration(plannedDuration),
                    isOverdue = duration > plannedDuration.Add(TimeSpan.FromHours(1)), // Просрочен на час
                    operatorId = stage.OperatorId,
                    reasonNote = stage.ReasonNote
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Получение списка доступных действий для этапа
        /// </summary>
        [HttpGet("{stageId}/actions")]
        public async Task<IActionResult> GetAvailableActions(int stageId)
        {
            try
            {
                var stage = await _batchRepo.GetStageExecutionByIdAsync(stageId);
                if (stage == null)
                    return NotFound();

                var actions = new List<string>();

                switch (stage.Status)
                {
                    case Domain.Entities.StageExecutionStatus.Pending:
                        actions.Add("start");
                        actions.Add("assign");
                        actions.Add("cancel");
                        break;

                    case Domain.Entities.StageExecutionStatus.Waiting:
                        actions.Add("assign");
                        actions.Add("cancel");
                        break;

                    case Domain.Entities.StageExecutionStatus.InProgress:
                        actions.Add("pause");
                        actions.Add("complete");
                        break;

                    case Domain.Entities.StageExecutionStatus.Paused:
                        actions.Add("resume");
                        actions.Add("complete");
                        actions.Add("cancel");
                        break;
                }

                return Ok(new { actions });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        private string GetStatusDisplayName(string status)
        {
            return status switch
            {
                "Pending" => "Ожидает запуска",
                "Waiting" => "В очереди",
                "InProgress" => "В работе",
                "Paused" => "На паузе",
                "Completed" => "Завершено",
                "Error" => "Ошибка/Отменено",
                _ => status
            };
        }

        private string FormatDuration(TimeSpan? duration)
        {
            if (!duration.HasValue) return "-";

            var d = duration.Value;
            if (d.TotalDays >= 1)
                return $"{(int)d.TotalDays}д {d.Hours}ч {d.Minutes}м";
            else if (d.TotalHours >= 1)
                return $"{(int)d.TotalHours}ч {d.Minutes}м";
            else
                return $"{d.Minutes}м";
        }
    }

    public class StageActionRequest
    {
        public string OperatorId { get; set; }
        public string ReasonNote { get; set; }
        public string DeviceId { get; set; }
    }

    public class AssignStageRequest
    {
        public int MachineId { get; set; }
        public string OperatorId { get; set; }
        public string ReasonNote { get; set; }
    }
}