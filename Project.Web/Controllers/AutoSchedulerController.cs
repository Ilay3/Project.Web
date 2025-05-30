using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Contracts.ModelDTO;
using Project.Infrastructure.Repositories;
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
        private readonly BatchRepository _batchRepo;

        public AutoSchedulerController(
            ProductionSchedulerService schedulerService,
            BatchService batchService,
            StageExecutionService stageService,
            BatchRepository batchRepository)
        {
            _schedulerService = schedulerService;
            _batchService = batchService;
            _stageService = stageService;
            _batchRepo = batchRepository;
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
                return BadRequest(new { success = false, message = $"Ошибка при разрешении конфликтов: {ex.Message}" });
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




        /// <summary>
        /// Получает статистику планировщика
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetSchedulerStatus()
        {
            try
            {
                var statistics = await _stageService.GetExecutionStatisticsAsync();
                return Ok(new { success = true, data = statistics });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Массовые операции над этапами
        /// </summary>
        [HttpPost("bulk-actions")]
        public async Task<IActionResult> BulkActions([FromBody] BulkActionRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Action))
                {
                    return BadRequest(new { success = false, message = "Не указано действие" });
                }

                var result = await ProcessBulkActionAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        private async Task<object> ProcessBulkActionAsync(BulkActionRequest request)
        {
            var affectedStages = 0;
            var errors = new List<string>();

            // Получаем этапы для обработки (можно расширить логику выбора)
            var stages = await _batchRepo.GetAllStageExecutionsAsync();
            var targetStages = stages.Where(s => request.StageIds == null ||
                                                request.StageIds.Contains(s.Id)).ToList();

            foreach (var stage in targetStages)
            {
                try
                {
                    bool actionPerformed = false;

                    switch (request.Action.ToLower())
                    {
                        case "start":
                            if (stage.Status == Domain.Entities.StageExecutionStatus.Pending && stage.MachineId.HasValue)
                            {
                                await _stageService.StartStageExecution(stage.Id, "BULK_ACTION", "WEB");
                                actionPerformed = true;
                            }
                            break;

                        case "pause":
                            if (stage.Status == Domain.Entities.StageExecutionStatus.InProgress)
                            {
                                await _stageService.PauseStageExecution(stage.Id, "BULK_ACTION",
                                    request.Reason ?? "Массовая приостановка", "WEB");
                                actionPerformed = true;
                            }
                            break;

                        case "resume":
                            if (stage.Status == Domain.Entities.StageExecutionStatus.Paused)
                            {
                                await _stageService.ResumeStageExecution(stage.Id, "BULK_ACTION",
                                    request.Reason ?? "Массовое возобновление", "WEB");
                                actionPerformed = true;
                            }
                            break;

                        case "auto-assign":
                            if (stage.Status == Domain.Entities.StageExecutionStatus.Pending && !stage.MachineId.HasValue)
                            {
                                var assigned = await _schedulerService.AutoAssignMachineToStageAsync(stage.Id);
                                if (assigned) actionPerformed = true;
                            }
                            break;
                    }

                    if (actionPerformed)
                    {
                        affectedStages++;
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Этап {stage.Id}: {ex.Message}");
                }
            }

            return new
            {
                success = true,
                affectedStages = affectedStages,
                errors = errors,
                message = $"Действие выполнено для {affectedStages} этапов"
            };
        }


        public class BulkActionRequest
        {
            public string Action { get; set; } // start, pause, resume, auto-assign
            public string Reason { get; set; }
            public List<int> StageIds { get; set; } // Если null, то применяется ко всем подходящим этапам
        }


        public class CancelStageRequest
        {
            public string Reason { get; set; }
            public string OperatorId { get; set; }
            public string DeviceId { get; set; }
        }
    }
}