using Project.Contracts.Enums;
using System.ComponentModel.DataAnnotations;

namespace Project.Web.ViewModels
{
    /// <summary>
    /// ViewModel для списка деталей
    /// </summary>
    public class DetailsIndexViewModel
    {
        public List<DetailItemViewModel> Details { get; set; } = new();
        public string SearchTerm { get; set; } = string.Empty;
        public bool ShowOnlyWithRoutes { get; set; }
        public bool ShowOnlyWithoutRoutes { get; set; }
    }

    public class DetailItemViewModel
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool HasRoute { get; set; }
        public int? RouteStageCount { get; set; }
        public double? TotalManufacturingTimeHours { get; set; }
        public int TotalManufactured { get; set; }
        public int ActiveBatches { get; set; }
        public DateTime? LastManufacturedDate { get; set; }
        public bool CanDelete { get; set; }
    }

    /// <summary>
    /// ViewModel для создания/редактирования детали
    /// </summary>
    public class DetailFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Номер детали обязателен")]
        [Display(Name = "Номер детали")]
        public string Number { get; set; } = string.Empty;

        [Required(ErrorMessage = "Наименование детали обязательно")]
        [Display(Name = "Наименование")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Описание")]
        public string? Description { get; set; }

        [Display(Name = "Материал")]
        public string? Material { get; set; }

        [Display(Name = "Единица измерения")]
        public string Unit { get; set; } = "шт";

        [Display(Name = "Вес (кг)")]
        [Range(0, double.MaxValue, ErrorMessage = "Вес не может быть отрицательным")]
        public decimal? Weight { get; set; }

        [Display(Name = "Категория")]
        public string? Category { get; set; }

        public bool IsEdit => Id > 0;
    }

    /// <summary>
    /// ViewModel для детальной информации о детали
    /// </summary>
    public class DetailDetailsViewModel
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Material { get; set; }
        public string Unit { get; set; } = "шт";
        public decimal? Weight { get; set; }
        public string? Category { get; set; }
        public bool HasRoute { get; set; }
        public double? TotalManufacturingTimeHours { get; set; }
        public int? RouteStageCount { get; set; }
        public bool CanDelete { get; set; }

        // Статистика использования
        public int TotalManufactured { get; set; }
        public int ActiveBatches { get; set; }
        public DateTime? LastManufacturedDate { get; set; }
        public double? AverageManufacturingTime { get; set; }

        // Список последних партий
        public List<RecentBatchViewModel> RecentBatches { get; set; } = new();
    }

    public class RecentBatchViewModel
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
        public DateTime CreatedUtc { get; set; }
        public decimal CompletionPercentage { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}