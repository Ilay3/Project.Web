using Microsoft.AspNetCore.Mvc;
using Project.Web.ViewModels;
using Project.Application.Services;
using Project.Contracts.ModelDTO;

namespace Project.Web.Controllers
{
    public class DetailController : Controller
    {
        private readonly DetailService _detailService;
        private readonly RouteService _routeService;

        public DetailController(DetailService detailService, RouteService routeService)
        {
            _detailService = detailService;
            _routeService = routeService;
        }

        // Отображение списка (ViewModel)
        public async Task<IActionResult> Index()
        {
            var dtos = await _detailService.GetAllAsync();
            var list = dtos.Select(d => new DetailViewModel
            {
                Id = d.Id,
                Number = d.Number,
                Name = d.Name
            }).ToList();
            return View(list);
        }

        // GET: Детальная информация о детали
        public async Task<IActionResult> Details(int id)
        {
            var detail = await _detailService.GetByIdAsync(id);
            if (detail == null)
                return NotFound();

            var viewModel = new DetailViewModel
            {
                Id = detail.Id,
                Number = detail.Number,
                Name = detail.Name
            };

            // Получаем маршрут, если есть
            var route = await _routeService.GetByDetailIdAsync(id);
            ViewBag.Route = route;

            return View(viewModel);
        }

        // POST: Добавление детали (через DTO)
        [HttpPost]
        public async Task<IActionResult> Create(DetailCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            await _detailService.AddAsync(dto);
            return RedirectToAction(nameof(Index));
        }

        // POST: Редактирование детали (через DTO)
        [HttpPost]
        public async Task<IActionResult> Edit(DetailEditDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            await _detailService.UpdateAsync(dto);
            return RedirectToAction(nameof(Index));
        }

        // POST: Удаление детали
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            await _detailService.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }

        // GET: API метод для получения списка деталей (для AJAX)
        [HttpGet]
        public async Task<IActionResult> GetDetails()
        {
            var details = await _detailService.GetAllAsync();
            return Json(details);
        }
    }
}