using System.ComponentModel.DataAnnotations;

namespace Project.Web.ViewModels
{
    /// <summary>
    /// ViewModel для списка типов станков согласно ТЗ
    /// </summary>
    public class MachineTypesIndexViewModel
    {
        public List<MachineTypeItemViewModel> MachineTypes { get; set; } = new();
        public string SearchTerm { get; set; } = string.Empty;
        public bool ShowOnlyWithMachines { get; set; }
    }

    public class MachineTypeItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Category { get; set; }
        public int MachineCount { get; set; }
        public int ActiveMachineCount { get; set; }
        public decimal AveragePriority { get; set; }
        public List<string> SupportedOperations { get; set; } = new();
        public bool CanDelete { get; set; }
        public DateTime CreatedUtc { get; set; }

        // Статистика использования
        public double TotalWorkingHours { get; set; }
        public decimal AverageUtilization { get; set; }
        public int CompletedOperations { get; set; }
        public double AverageSetupTime { get; set; }
        public int QueuedParts { get; set; }
    }

    /// <summary>
    /// ViewModel для создания/редактирования типа станка
    /// </summary>
    public class MachineTypeFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Название типа станка обязательно")]
        [Display(Name = "Название типа станка")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Описание")]
        public string? Description { get; set; }

        [Display(Name = "Категория")]
        public string? Category { get; set; }

        [Display(Name = "Поддерживаемые операции")]
        public List<string> SupportedOperations { get; set; } = new();

        [Display(Name = "Новая операция")]
        public string? NewOperation { get; set; }

        public bool IsEdit => Id > 0;
    }

    /// <summary>
    /// ViewModel для детального просмотра типа станка
    /// </summary>
    public class MachineTypeDetailsViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Category { get; set; }
        public int MachineCount { get; set; }
        public int ActiveMachineCount { get; set; }
        public decimal AveragePriority { get; set; }
        public List<string> SupportedOperations { get; set; } = new();
        public bool CanDelete { get; set; }
        public DateTime CreatedUtc { get; set; }

        // Статистика использования
        public MachineTypeUsageStatsViewModel UsageStatistics { get; set; } = new();

        // Список станков данного типа
        public List<MachineItemViewModel> Machines { get; set; } = new();
    }

    public class MachineTypeUsageStatsViewModel
    {
        public double TotalWorkingHours { get; set; }
        public decimal AverageUtilization { get; set; }
        public int CompletedOperations { get; set; }
        public double AverageSetupTime { get; set; }
        public int QueuedParts { get; set; }

        public string UtilizationCssClass => AverageUtilization switch
        {
            >= 80 => "text-success",
            >= 60 => "text-warning",
            _ => "text-danger"
        };
    }
}