using Microsoft.AspNetCore.Mvc;
using Project.Web.ViewModels;
using Project.Application.Services;
using Project.Contracts.ModelDTO;

namespace Project.Web.Controllers
{
    public class DetailController : Controller
    {
        private readonly DetailService _service;
        public DetailController(DetailService service) => _service = service;

        // Отображение списка (ViewModel)
        public async Task<IActionResult> Index()
        {
            var dtos = await _service.GetAllAsync();
            var list = dtos.Select(d => new DetailViewModel
            {
                Id = d.Id,
                Number = d.Number,
                Name = d.Name
            }).ToList();
            return View(list);
        }

        // POST: Добавление детали (через DTO)
        [HttpPost]
        public async Task<IActionResult> Create(DetailCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            await _service.AddAsync(dto);
            return RedirectToAction(nameof(Index));
        }

        // POST: Редактирование детали (через DTO)
        [HttpPost]
        public async Task<IActionResult> Edit(DetailEditDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            await _service.UpdateAsync(dto);
            return RedirectToAction(nameof(Index));
        }

        // POST: Удаление детали
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
