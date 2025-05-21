using Microsoft.AspNetCore.Mvc;
using Project.Application;
using Project.Application.Services;
using Project.Contracts.ModelDTO;
using Project.Domain;
using Project.Domain.Entities;
using Project.Web.ViewModels;

namespace Project.Web.Controllers
{
    public class MachineController : Controller
    {
        private readonly MachineService _service;
        public MachineController(MachineService service) => _service = service;

        public async Task<IActionResult> Index()
        {
            var dtos = await _service.GetAllAsync();
            var vms = dtos.Select(x => new MachineViewModel
            {
                Id = x.Id,
                Name = x.Name,
                InventoryNumber = x.InventoryNumber,
                MachineTypeName = x.MachineTypeName,
                Priority = x.Priority
            }).ToList();
            return View(vms);
        }

        [HttpPost]
        public async Task<IActionResult> Create(MachineCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            await _service.AddAsync(dto);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Edit(MachineEditDto dto)
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

}
