using Microsoft.AspNetCore.Mvc;
using Project.Web.ViewModels;
using Project.Application.Services;
using Project.Contracts.ModelDTO;

public class MachineTypeController : Controller
{
    private readonly MachineTypeService _service;
    public MachineTypeController(MachineTypeService service) => _service = service;

    public async Task<IActionResult> Index()
    {
        var dtos = await _service.GetAllAsync();
        var vms = dtos.Select(x => new MachineTypeViewModel { Id = x.Id, Name = x.Name }).ToList();
        return View(vms);
    }

    [HttpPost]
    public async Task<IActionResult> Create(MachineTypeCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        await _service.AddAsync(dto);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(MachineTypeEditDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        await _service.UpdateAsync(dto);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
