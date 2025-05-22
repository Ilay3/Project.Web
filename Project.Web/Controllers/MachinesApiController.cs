// Создать файл Project.Web/Controllers/MachinesApiController.cs

using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Domain.Repositories;
using Project.Contracts.ModelDTO;

namespace Project.Web.Controllers
{
    [Route("api/gantt/machines")]
    [ApiController]
    public class MachinesApiController : ControllerBase
    {
        private readonly MachineService _machineService;
        private readonly StageExecutionService _stageService;
        private readonly IBatchRepository _batchRepo;

        public MachinesApiController(
            MachineService machineService,
            StageExecutionService stageService,
            IBatchRepository batchRepo)
        {
            _machineService = machineService;
            _stageService = stageService;
            _batchRepo = batchRepo;
        }

        /// <summary>
        /// Получение доступных станков для этапа
        /// </summary>
        [HttpGet("available/{stageId}")]
        public async Task<IActionResult> GetAvailableMachinesForStage(int stageId)
        {
            try
            {
                var availableMachines = await _batchRepo.GetAvailableMachinesForStageAsync(stageId);

                var result = availableMachines.Select(m => new {
                    id = m.Id,
                    name = m.Name,
                    inventoryNumber = m.InventoryNumber,
                    machineTypeName = m.MachineType?.Name,
                    priority = m.Priority
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Получение текущего статуса всех станков
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetMachinesStatus()
        {
            try
            {
                var machines = await _machineService.GetAllAsync();
                var ganttData = await _stageService.GetAllStagesForGanttChart();

                var result = machines.Select(machine => {
                    var currentStage = ganttData.FirstOrDefault(s =>
                        s.MachineId == machine.Id && s.Status == "InProgress");

                    var queuedCount = ganttData.Count(s =>
                        s.MachineId == machine.Id &&
                        (s.Status == "Waiting" || s.Status == "Pending"));

                    return new
                    {
                        id = machine.Id,
                        name = machine.Name,
                        inventoryNumber = machine.InventoryNumber,
                        machineTypeName = machine.MachineTypeName,
                        priority = machine.Priority,
                        status = currentStage != null ?
                            (currentStage.IsSetup ? "Переналадка" : "В работе") :
                            "Свободен",
                        statusCode = currentStage != null ?
                            (currentStage.IsSetup ? "setup" : "working") :
                            "free",
                        currentStage = currentStage != null ? new
                        {
                            id = currentStage.Id,
                            stageName = currentStage.StageName,
                            detailName = currentStage.DetailName,
                            isSetup = currentStage.IsSetup,
                            startTime = currentStage.StartTime,
                            plannedDuration = currentStage.PlannedDuration
                        } : null,
                        queuedStagesCount = queuedCount,
                        efficiency = CalculateMachineEfficiency(machine.Id, ganttData),
                        lastActivity = GetLastActivity(machine.Id, ganttData)
                    };
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Получение детальной информации о станке
        /// </summary>
        [HttpGet("{machineId}/details")]
        public async Task<IActionResult> GetMachineDetails(int machineId)
        {
            try
            {
                var machine = await _machineService.GetByIdAsync(machineId);
                if (machine == null)
                    return NotFound();

                var ganttData = await _stageService.GetAllStagesForGanttChart();
                var machineStages = ganttData.Where(s => s.MachineId == machineId).ToList();

                var currentStage = machineStages.FirstOrDefault(s => s.Status == "InProgress");
                var queuedStages = machineStages.Where(s => s.Status == "Waiting" || s.Status == "Pending")
                                                .OrderBy(s => s.ScheduledStartTime)
                                                .ToList();

                // Статистика за сегодня
                var today = DateTime.Today;
                var todayStages = machineStages.Where(s =>
                    s.StartTime.HasValue && s.StartTime.Value.Date == today).ToList();

                var workingTime = todayStages.Where(s => !s.IsSetup && s.EndTime.HasValue)
                    .Sum(s => (s.EndTime.Value - s.StartTime.Value).TotalHours);

                var setupTime = todayStages.Where(s => s.IsSetup && s.EndTime.HasValue)
                    .Sum(s => (s.EndTime.Value - s.StartTime.Value).TotalHours);

                var totalTime = workingTime + setupTime;
                var efficiency = totalTime > 0 ? Math.Round((workingTime / totalTime) * 100, 1) : 0;

                return Ok(new
                {
                    machine = new
                    {
                        id = machine.Id,
                        name = machine.Name,
                        inventoryNumber = machine.InventoryNumber,
                        machineTypeName = machine.MachineTypeName,
                        priority = machine.Priority
                    },
                    currentStage = currentStage,
                    queuedStages = queuedStages,
                    statistics = new
                    {
                        workingHours = Math.Round(workingTime, 2),
                        setupHours = Math.Round(setupTime, 2),
                        totalHours = Math.Round(totalTime, 2),
                        efficiency = efficiency,
                        completedStages = todayStages.Count(s => s.Status == "Completed")
                    },
                    recentActivity = GetRecentActivity(machineId, ganttData)
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        private double CalculateMachineEfficiency(int machineId, List<GanttStageDto> stages)
        {
            var today = DateTime.Today;
            var todayStages = stages.Where(s =>
                s.MachineId == machineId &&
                s.StartTime.HasValue &&
                s.StartTime.Value.Date == today).ToList();

            if (!todayStages.Any())
                return 0;

            var workingTime = todayStages.Where(s => !s.IsSetup && s.EndTime.HasValue)
                .Sum(s => (s.EndTime.Value - s.StartTime.Value).TotalHours);

            var setupTime = todayStages.Where(s => s.IsSetup && s.EndTime.HasValue)
                .Sum(s => (s.EndTime.Value - s.StartTime.Value).TotalHours);

            var totalTime = workingTime + setupTime;
            return totalTime > 0 ? Math.Round((workingTime / totalTime) * 100, 1) : 0;
        }

        private DateTime? GetLastActivity(int machineId, List<GanttStageDto> stages)
        {
            return stages.Where(s => s.MachineId == machineId && s.EndTime.HasValue)
                        .OrderByDescending(s => s.EndTime)
                        .FirstOrDefault()?.EndTime;
        }

        private object GetRecentActivity(int machineId, List<GanttStageDto> stages)
        {
            var recentStages = stages.Where(s => s.MachineId == machineId && s.EndTime.HasValue)
                                   .OrderByDescending(s => s.EndTime)
                                   .Take(5)
                                   .Select(s => new {
                                       id = s.Id,
                                       stageName = s.StageName,
                                       detailName = s.DetailName,
                                       isSetup = s.IsSetup,
                                       startTime = s.StartTime,
                                       endTime = s.EndTime,
                                       status = s.Status,
                                       duration = s.EndTime.HasValue && s.StartTime.HasValue ?
                                           Math.Round((s.EndTime.Value - s.StartTime.Value).TotalHours, 2) : 0
                                   })
                                   .ToList();

            return recentStages;
        }
    }
}