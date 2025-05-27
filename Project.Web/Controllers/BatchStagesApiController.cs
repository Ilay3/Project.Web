// Добавить в BatchController или создать отдельный API контроллер

using Microsoft.AspNetCore.Mvc;
using Project.Application.Services;
using Project.Domain.Entities;
using Project.Domain.Repositories;

[Route("api/batches")]
[ApiController]
public class BatchStagesApiController : ControllerBase
{
    private readonly BatchService _batchService;
    private readonly IBatchRepository _batchRepo;

    public BatchStagesApiController(
        BatchService batchService,
        IBatchRepository batchRepo)
    {
        _batchService = batchService;
        _batchRepo = batchRepo;
    }

    /// <summary>
    /// Получение детальной информации об этапах партии
    /// </summary>
    [HttpGet("{batchId}/stages-detailed")]
    public async Task<IActionResult> GetBatchStagesDetailed(int batchId)
    {
        try
        {
            var batch = await _batchRepo.GetByIdAsync(batchId);
            if (batch == null)
                return NotFound();

            var stages = new List<object>();

            foreach (var subBatch in batch.SubBatches)
            {
                foreach (var stage in subBatch.StageExecutions)
                {
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
                        : TimeSpan.FromHours(stage.RouteStage.NormTime * subBatch.Quantity);

                    // Проверяем, просрочен ли этап
                    bool isOverdue = false;
                    if (duration.HasValue && duration.Value > plannedDuration.Add(TimeSpan.FromHours(1)))
                    {
                        isOverdue = true;
                    }

                    stages.Add(new
                    {
                        id = stage.Id,
                        subBatchId = subBatch.Id,
                        subBatchQuantity = subBatch.Quantity,
                        batchId = batch.Id,
                        detailName = batch.Detail.Name,
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
                        isOverdue = isOverdue,
                        operatorId = stage.OperatorId,
                        reasonNote = stage.ReasonNote,
                        routeStageOrder = stage.RouteStage.Order,
                        // Процент выполнения для этапа в работе
                        completionPercent = CalculateStageCompletion(stage, duration, plannedDuration)
                    });
                }
            }

            // Сортируем по подпартиям и порядку этапов
            var sortedStages = stages.OrderBy(s => ((dynamic)s).subBatchId)
                                   .ThenBy(s => ((dynamic)s).routeStageOrder)
                                   .ThenBy(s => ((dynamic)s).isSetup ? 0 : 1); // Переналадка перед основным этапом

            return Ok(sortedStages);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Получение сводной статистики по партии
    /// </summary>
    [HttpGet("{batchId}/statistics")]
    public async Task<IActionResult> GetBatchStatistics(int batchId)
    {
        try
        {
            var batch = await _batchRepo.GetByIdAsync(batchId);
            if (batch == null)
                return NotFound();

            var allStages = batch.SubBatches.SelectMany(sb => sb.StageExecutions).ToList();

            var statistics = new
            {
                totalStages = allStages.Count,
                completedStages = allStages.Count(s => s.Status == StageExecutionStatus.Completed),
                inProgressStages = allStages.Count(s => s.Status == StageExecutionStatus.InProgress),
                pausedStages = allStages.Count(s => s.Status == StageExecutionStatus.Paused),
                pendingStages = allStages.Count(s => s.Status == StageExecutionStatus.Pending),
                waitingStages = allStages.Count(s => s.Status == StageExecutionStatus.Waiting),
                errorStages = allStages.Count(s => s.Status == StageExecutionStatus.Error),
                setupStages = allStages.Count(s => s.IsSetup),

                // Временные показатели
                totalWorkingTime = allStages
                    .Where(s => !s.IsSetup && s.StartTimeUtc.HasValue && s.EndTimeUtc.HasValue)
                    .Sum(s => (s.EndTimeUtc.Value - s.StartTimeUtc.Value).TotalHours),

                totalSetupTime = allStages
                    .Where(s => s.IsSetup && s.StartTimeUtc.HasValue && s.EndTimeUtc.HasValue)
                    .Sum(s => (s.EndTimeUtc.Value - s.StartTimeUtc.Value).TotalHours),

                avgStageTime = allStages
                    .Where(s => s.StartTimeUtc.HasValue && s.EndTimeUtc.HasValue)
                    .Select(s => (s.EndTimeUtc.Value - s.StartTimeUtc.Value).TotalHours)
                    .DefaultIfEmpty(0)
                    .Average(),

                // Прогресс
                completionPercent = allStages.Count > 0
                    ? Math.Round((double)allStages.Count(s => s.Status == StageExecutionStatus.Completed) / allStages.Count * 100, 1)
                    : 0,

                // Статус партии
                batchStatus = GetBatchStatus(allStages),

                // Машины в работе
                activeMachines = allStages
                    .Where(s => s.Status == StageExecutionStatus.InProgress && s.MachineId.HasValue)
                    .Select(s => new {
                        machineId = s.MachineId.Value,
                        machineName = s.Machine?.Name
                    })
                    .Distinct()
                    .ToList(),

                // Просроченные этапы
                overdueStages = allStages
                    .Where(s => IsStageOverdue(s))
                    .Count()
            };

            return Ok(statistics);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Быстрые действия над всеми этапами партии
    /// </summary>
    [HttpPost("{batchId}/bulk-actions")]
    public async Task<IActionResult> BulkActions(int batchId, [FromBody] BulkActionRequest request)
    {
        try
        {
            var batch = await _batchRepo.GetByIdAsync(batchId);
            if (batch == null)
                return NotFound();

            var stageService = HttpContext.RequestServices.GetRequiredService<StageExecutionService>();
            var schedulerService = HttpContext.RequestServices.GetRequiredService<ProductionSchedulerService>();

            int affectedStages = 0;
            var errors = new List<string>();

            foreach (var subBatch in batch.SubBatches)
            {
                foreach (var stage in subBatch.StageExecutions)
                {
                    try
                    {
                        bool actionPerformed = false;

                        switch (request.Action.ToLower())
                        {
                            case "start":
                                if (stage.Status == StageExecutionStatus.Pending && stage.MachineId.HasValue)
                                {
                                    await stageService.StartStageExecution(stage.Id, "BULK_ACTION", "WEB");
                                    actionPerformed = true;
                                }
                                break;

                            case "pause":
                                if (stage.Status == StageExecutionStatus.InProgress)
                                {
                                    await stageService.PauseStageExecution(stage.Id, "BULK_ACTION",
                                        request.Reason ?? "Массовая приостановка", "WEB");
                                    actionPerformed = true;
                                }
                                break;

                            case "resume":
                                if (stage.Status == StageExecutionStatus.Paused)
                                {
                                    await stageService.ResumeStageExecution(stage.Id, "BULK_ACTION",
                                        request.Reason ?? "Массовое возобновление", "WEB");
                                    actionPerformed = true;
                                }
                                break;

                            case "complete":
                                if (stage.Status == StageExecutionStatus.InProgress ||
                                    stage.Status == StageExecutionStatus.Paused)
                                {
                                    await stageService.CompleteStageExecution(stage.Id, "BULK_ACTION",
                                        request.Reason ?? "Массовое завершение", "WEB");
                                    actionPerformed = true;
                                }
                                break;

                            case "auto-assign":
                                if (stage.Status == StageExecutionStatus.Pending && !stage.MachineId.HasValue)
                                {
                                    var assigned = await schedulerService.AutoAssignMachineToStageAsync(stage.Id);
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
            }

            return Ok(new
            {
                success = true,
                affectedStages = affectedStages,
                errors = errors,
                message = $"Действие выполнено для {affectedStages} этапов"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
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

    private double CalculateStageCompletion(StageExecution stage, TimeSpan? actualDuration, TimeSpan plannedDuration)
    {
        if (stage.Status == StageExecutionStatus.Completed)
            return 100.0;

        if (!actualDuration.HasValue || stage.Status != StageExecutionStatus.InProgress)
            return 0.0;

        var percent = (actualDuration.Value.TotalHours / plannedDuration.TotalHours) * 100;
        return Math.Min(Math.Round(percent, 1), 100.0);
    }

    private string GetBatchStatus(List<StageExecution> stages)
    {
        if (!stages.Any()) return "Пустая";

        var completed = stages.Count(s => s.Status == StageExecutionStatus.Completed);
        var inProgress = stages.Count(s => s.Status == StageExecutionStatus.InProgress);
        var total = stages.Count;

        if (completed == total) return "Завершено";
        if (inProgress > 0) return "В производстве";
        if (completed > 0) return "Частично выполнено";
        return "Не начато";
    }

    private bool IsStageOverdue(StageExecution stage)
    {
        if (!stage.StartTimeUtc.HasValue || stage.Status == StageExecutionStatus.Completed)
            return false;

        var plannedDuration = stage.IsSetup
            ? TimeSpan.FromHours(stage.RouteStage.SetupTime)
            : TimeSpan.FromHours(stage.RouteStage.NormTime * stage.SubBatch.Quantity);

        var actualDuration = DateTime.UtcNow - stage.StartTimeUtc.Value;
        return actualDuration > plannedDuration.Add(TimeSpan.FromHours(1)); // Просрочка больше часа
    }

    public class BulkActionRequest
    {
        public string Action { get; set; } // start, pause, resume, complete, auto-assign
        public string Reason { get; set; }
    }
}