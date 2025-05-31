using Project.Contracts.Enums;
using System.ComponentModel.DataAnnotations;

namespace Project.Web.ViewModels
{
    /// <summary>
    /// ViewModel для списка станков согласно ТЗ
    /// </summary>
    public class MachinesIndexViewModel
    {
        public List<MachineItemViewModel> Machines { get; set; } = new();
        public List<MachineTypeFilterViewModel> MachineTypes { get; set; } = new();
        public int? SelectedMachineTypeId { get; set; }
        public MachineStatus? StatusFilter { get; set; }
        public string SearchTerm { get; set; } = string.Empty;
    }

    public class MachineItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string InventoryNumber { get; set; } = string.Empty;
        public string MachineTypeName { get; set; } = string.Empty;
        public int Priority { get; set; }
        public MachineStatus Status { get; set; }
        public string StatusDisplayName => GetStatusDisplayName();
        public string StatusCssClass => GetStatusCssClass();

        // Текущая работа
        public string? CurrentStageDescription { get; set; }
        public TimeSpan? TimeToFree { get; set; }
        public int QueueLength { get; set; }

        // Статистика за сегодня
        public decimal? TodayUtilizationPercent { get; set; }

        private string GetStatusDisplayName() => Status switch
        {
            MachineStatus.Free => "Свободен",
            MachineStatus.Busy => "Занят",
            MachineStatus.Setup => "Переналадка",
            MachineStatus.Broken => "Неисправен",
            _ => "Неизвестно"
        };

        private string GetStatusCssClass() => Status switch
        {
            MachineStatus.Free => "success",
            MachineStatus.Busy => "primary",
            MachineStatus.Setup => "warning",
            MachineStatus.Broken => "danger",
            _ => "secondary"
        };
    }

    public class MachineTypeFilterViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int MachineCount { get; set; }
    }

    /// <summary>
    /// ViewModel для создания/редактирования станка
    /// </summary>
    public class MachineFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Название станка обязательно")]
        [Display(Name = "Название станка")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Инвентарный номер обязателен")]
        [Display(Name = "Инвентарный номер")]
        public string InventoryNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Тип станка обязателен")]
        [Display(Name = "Тип станка")]
        public int MachineTypeId { get; set; }

        [Range(0, 10, ErrorMessage = "Приоритет должен быть от 0 до 10")]
        [Display(Name = "Приоритет (0-10)")]
        public int Priority { get; set; } = 5;

        // Для выпадающего списка типов станков
        public List<MachineTypeOption> AvailableMachineTypes { get; set; } = new();

        public bool IsEdit => Id > 0;
    }

    public class MachineTypeOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}