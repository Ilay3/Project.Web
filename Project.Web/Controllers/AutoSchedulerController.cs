using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Contracts.ModelDTO;
using Project.Web.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AutoSchedulerController : ControllerBase
    {
        private readonly ProductionSchedulerService _schedulerService;
        private readonly BatchService _batchService;
        private readonly StageExecutionService _stageService;

        public AutoSchedulerController(
            ProductionSchedulerService schedulerService,
            BatchService batchService,
            StageExecutionService stageService)
        {
            _schedulerService = schedulerService;
            _batchService = batchService;
            _stageService = stageService;
        }

        /// <summary>
        /// Запускает планирование для указанной партии
        /// </summary>
        [HttpPost("batches/{batchId}/schedule")]
        public async Task<IActionResult> ScheduleBatch(int batchId)
        {
            try
            {
                await _schedulerService.ScheduleSubBatchesAsync(batchId);
                return Ok(new { success = true, message = "Планирование успешно запущено" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = $"Ошибка при планировании: {ex.Message}" });
            }
        }

        /// <summary>
        /// Оптимизирует очередь этапов
        /// </summary>
        [HttpPost("optimize")]
        public async Task<IActionResult> OptimizeQueue()
        {
            try
            {
                await _schedulerService.OptimizeQueueAsync();
                return Ok(new { success = true, message = "Очередь успешно оптимизирована" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = $"Ошибка при оптимизации: {ex.Message}" });
            }
        }

        /// <summary>
        /// Планирует и запускает указанный этап
        /// </summary>
        [HttpPost("stages/{stageId}/schedule")]
        public async Task<IActionResult> ScheduleStage(int stageId)
        {
            try
            {
                await _schedulerService.ScheduleStageExecutionAsync(stageId);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Запускает указанный этап
        /// </summary>
        [HttpPost("stages/{stageId}/start")]
        public async Task<IActionResult> StartStage(int stageId)
        {
            try
            {
                var result = await _schedulerService.StartPendingStageAsync(stageId);
                return Ok(new { success = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Автоматически назначает станок для этапа
        /// </summary>
        [HttpPost("stages/{stageId}/auto-assign")]
        public async Task<IActionResult> AutoAssignMachine(int stageId)
        {
            try
            {
                var result = await _schedulerService.AutoAssignMachineToStageAsync(stageId);
                return Ok(new { success = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Разрешает конфликты в расписании
        /// </summary>
        [HttpPost("resolve-conflicts")]
        public async Task<IActionResult> ResolveConflicts()
        {
            try
            {
                await _schedulerService.ResolveScheduleConflictsAsync();
                return Ok(new { success = true, message = "Конфликты успешно разрешены" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Получает прогноз для производства детали
        /// </summary>
        [HttpGet("predict")]
        public async Task<IActionResult> PredictSchedule(int detailId, int quantity)
        {
            try
            {
                var prediction = await _schedulerService.PredictOptimalScheduleForDetailAsync(detailId, quantity);
                return Ok(prediction);
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Отменяет этап
        /// </summary>
        [HttpPost("stages/{stageId}/cancel")]
        public async Task<IActionResult> CancelStage(int stageId, [FromBody] CancelStageRequest request)
        {
            try
            {
                await _stageService.CancelStageExecution(stageId, request.Reason, request.OperatorId, request.DeviceId);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }

    public class CancelStageRequest
    {
        public string Reason { get; set; }
        public string OperatorId { get; set; }
        public string DeviceId { get; set; }
    }
}