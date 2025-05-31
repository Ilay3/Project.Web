using System.ComponentModel.DataAnnotations;

namespace Project.Web.ViewModels
{
    /// <summary>
    /// ViewModel для списка маршрутов согласно ТЗ
    /// </summary>
    public class RoutesIndexViewModel
    {
        public List<RouteItemViewModel> Routes { get; set; } = new();
        public string SearchTerm { get; set; } = string.Empty;
        public bool ShowOnlyEditable { get; set; }
    }

    public class RouteItemViewModel
    {
        public int Id { get; set; }
        public string DetailName { get; set; } = string.Empty;
        public string DetailNumber { get; set; } = string.Empty;
        public int StageCount { get; set; }
        public double TotalNormTimeHours { get; set; }
        public double TotalSetupTimeHours { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public DateTime? LastModifiedUtc { get; set; }
    }

    /// <summary>
    /// ViewModel для создания/редактирования маршрута
    /// </summary>
    public class RouteFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Выберите деталь")]
        [Display(Name = "Деталь")]
        public int DetailId { get; set; }

        public string DetailName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Добавьте хотя бы один этап")]
        [MinLength(1, ErrorMessage = "Маршрут должен содержать хотя бы один этап")]
        public List<RouteStageFormViewModel> Stages { get; set; } = new();

        // Для выбора детали
        public List<DetailOptionViewModel> AvailableDetails { get; set; } = new();

        // Для выбора типов станков
        public List<MachineTypeOptionViewModel> AvailableMachineTypes { get; set; } = new();

        public bool IsEdit => Id > 0;
        public bool CanEdit { get; set; } = true;

        // Итоговые значения
        public double TotalNormTimeHours => Stages.Sum(s => s.NormTime);
        public double TotalSetupTimeHours => Stages.Sum(s => s.SetupTime);
    }

    public class RouteStageFormViewModel
    {
        public int Id { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Порядковый номер должен быть больше 0")]
        [Display(Name = "Порядок")]
        public int Order { get; set; }

        [Required(ErrorMessage = "Название этапа обязательно")]
        [Display(Name = "Название этапа")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Выберите тип станка")]
        [Display(Name = "Тип станка")]
        public int MachineTypeId { get; set; }

        public string MachineTypeName { get; set; } = string.Empty;

        [Required]
        [Range(0.001, double.MaxValue, ErrorMessage = "Нормативное время должно быть больше 0")]
        [Display(Name = "Нормативное время (часы)")]
        public double NormTime { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Время переналадки не может быть отрицательным")]
        [Display(Name = "Время переналадки (часы)")]
        public double SetupTime { get; set; }

        [Display(Name = "Описание операции")]
        public string? Description { get; set; }

        [Display(Name = "Требуемая квалификация")]
        public string? RequiredSkills { get; set; }

        [Display(Name = "Необходимые инструменты")]
        public string? RequiredTools { get; set; }

        [Display(Name = "Контрольные параметры")]
        public string? QualityParameters { get; set; }

        public string StageType { get; set; } = "Operation";

        // Для удаления
        public bool IsDeleted { get; set; }
    }

    public class DetailOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public bool HasRoute { get; set; }
    }

    public class MachineTypeOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int MachineCount { get; set; }
    }

    /// <summary>
    /// ViewModel для детального просмотра маршрута
    /// </summary>
    public class RouteDetailsViewModel
    {
        public int Id { get; set; }
        public string DetailName { get; set; } = string.Empty;
        public string DetailNumber { get; set; } = string.Empty;
        public int StageCount { get; set; }
        public double TotalNormTimeHours { get; set; }
        public double TotalSetupTimeHours { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public DateTime? LastModifiedUtc { get; set; }

        public List<RouteStageDetailsViewModel> Stages { get; set; } = new();

        // Статистика использования
        public RouteUsageStatisticsViewModel UsageStatistics { get; set; } = new();
    }

    public class RouteStageDetailsViewModel
    {
        public int Id { get; set; }
        public int Order { get; set; }
        public string Name { get; set; } = string.Empty;
        public string MachineTypeName { get; set; } = string.Empty;
        public double NormTime { get; set; }
        public double SetupTime { get; set; }
        public string? Description { get; set; }
        public string? RequiredSkills { get; set; }
        public string? RequiredTools { get; set; }
        public string? QualityParameters { get; set; }

        // Статистика по этапу
        public int CompletedExecutions { get; set; }
        public double? AverageActualTime { get; set; }
        public double? EfficiencyPercentage { get; set; }
    }

    public class RouteUsageStatisticsViewModel
    {
        public int TotalBatchesProduced { get; set; }
        public int TotalPartsProduced { get; set; }
        public DateTime? LastUsedDate { get; set; }
        public double? AverageProductionTime { get; set; }
        public double? OverallEfficiency { get; set; }
    }

    /// <summary>
    /// ViewModel для копирования маршрута
    /// </summary>
    public class RouteCopyViewModel
    {
        [Required]
        public int SourceRouteId { get; set; }

        public string SourceRouteName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Выберите целевую деталь")]
        [Display(Name = "Целевая деталь")]
        public int TargetDetailId { get; set; }

        [Display(Name = "Копировать времена переналадки")]
        public bool CopySetupTimes { get; set; } = true;

        // Список доступных деталей (без маршрутов)
        public List<DetailOptionViewModel> AvailableTargetDetails { get; set; } = new();
    }
}