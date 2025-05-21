using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Contracts.ModelDTO;
using System;
using System.Threading.Tasks;

namespace Project.Web.Controllers
{
    [Route("api/gantt")]
    [ApiController]
    public class GanttApiController : ControllerBase
    {
        private readonly StageExecutionService _stageService;
        private readonly ProductionSchedulerService _schedulerService;
        private readonly MachineService _machineService;
        private readonly DetailService _detailService;

        public GanttApiController(
            StageExecutionService stageService,
            ProductionSchedulerService schedulerService,
            MachineService machineService,
            DetailService detailService)
        {
            _stageService = stageService;
            _schedulerService = schedulerService;
            _machineService = machineService;
            _detailService = detailService;
        }

        // GET: api/gantt/stages
        [HttpGet("stages")]
        public async Task<IActionResult> GetGanttStages([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
        {
            var stages = await _stageService.GetAllStagesForGanttChart(startDate, endDate);
            return Ok(stages);
        }

        // GET: api/gantt/machines
        [HttpGet("machines")]
        public async Task<IActionResult> GetMachines()
        {
            var machines = await _machineService.GetAllAsync();
            return Ok(machines);
        }

        // GET: api/gantt/details
        [HttpGet("details")]
        public async Task<IActionResult> GetDetails()
        {
            var details = await _detailService.GetAllAsync();
            return Ok(details);
        }

        // GET: api/gantt/queue
        [HttpGet("queue")]
        public async Task<IActionResult> GetQueueForecast()
        {
            var queueForecast = await _schedulerService.GetQueueForecastAsync();
            return Ok(queueForecast);
        }

        // POST: api/gantt/stages/{id}/start
        [HttpPost("stages/{id}/start")]
        public async Task<IActionResult> StartStage(int id)
        {
            try
            {
                await _stageService.StartStageExecution(id);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // POST: api/gantt/stages/{id}/pause
        [HttpPost("stages/{id}/pause")]
        public async Task<IActionResult> PauseStage(int id)
        {
            try
            {
                await _stageService.PauseStageExecution(id);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // POST: api/gantt/stages/{id}/resume
        [HttpPost("stages/{id}/resume")]
        public async Task<IActionResult> ResumeStage(int id)
        {
            try
            {
                await _stageService.ResumeStageExecution(id);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // POST: api/gantt/stages/{id}/complete
        [HttpPost("stages/{id}/complete")]
        public async Task<IActionResult> CompleteStage(int id)
        {
            try
            {
                await _stageService.CompleteStageExecution(id);
                await _schedulerService.HandleStageCompletionAsync(id);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // POST: api/gantt/stages/{id}/reassign
        [HttpPost("stages/{id}/reassign")]
        public async Task<IActionResult> ReassignStage(int id, [FromBody] ReassignRequest request)
        {
            try
            {
                await _schedulerService.ReassignStageToMachineAsync(id, request.MachineId);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // POST: api/gantt/stages/{id}/prioritize
        [HttpPost("stages/{id}/prioritize")]
        public async Task<IActionResult> PrioritizeStage(int id, [FromBody] PrioritizeRequest request)
        {
            try
            {
                await _schedulerService.ReassignQueueAsync(request.MachineId, id);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // POST: api/gantt/batch/create
        [HttpPost("batch/create")]
        public async Task<IActionResult> CreateBatch([FromBody] BatchCreateDto dto)
        {
            try
            {
                var batchId = await _schedulerService.CreateBatchAsync(dto);
                return Ok(new { success = true, batchId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }
    }

    public class ReassignRequest
    {
        public int MachineId { get; set; }
    }

    public class PrioritizeRequest
    {
        public int MachineId { get; set; }
    }
}