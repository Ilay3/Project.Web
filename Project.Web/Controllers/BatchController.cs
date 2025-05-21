using Microsoft.AspNetCore.Mvc;
using Project.Web.ViewModels;
using Project.Application.Services;
using Project.Contracts.ModelDTO;

public class BatchController : Controller
{
    private readonly BatchService _service;
    public BatchController(BatchService service) => _service = service;

    // Список партий
    public async Task<IActionResult> Index()
    {
        var dtos = await _service.GetAllAsync();
        var vms = dtos.Select(b => new BatchViewModel
        {
            Id = b.Id,
            DetailName = b.DetailName,
            Quantity = b.Quantity,
            Created = b.CreatedUtc,
            SubBatches = b.SubBatches.Select(sb => new SubBatchViewModel
            {
                Id = sb.Id,
                Quantity = sb.Quantity,
                StageExecutions = sb.StageExecutions.Select(se => new StageExecutionViewModel
                {
                    Id = se.Id,
                    StageName = se.StageName,
                    MachineName = se.MachineName,
                    Status = se.Status,
                    StartTime = se.StartTimeUtc,
                    EndTime = se.EndTimeUtc,
                    IsSetup = se.IsSetup
                }).ToList()
            }).ToList()
        }).ToList();
        return View(vms);
    }

    // Создание партии
    [HttpPost]
    public async Task<IActionResult> Create(BatchCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        await _service.AddAsync(dto);
        return RedirectToAction(nameof(Index));
    }

    // Редактирование партии
    [HttpPost]
    public async Task<IActionResult> Edit(BatchEditDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        await _service.UpdateAsync(dto);
        return RedirectToAction(nameof(Index));
    }

    // Удаление партии
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
