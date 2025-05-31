using System.ComponentModel.DataAnnotations;

namespace Project.Web.ViewModels
{
    /// <summary>
    /// ViewModel для управления временами переналадки согласно ТЗ
    /// </summary>
    public class SetupTimesIndexViewModel
    {
        public List<SetupTimeItemViewModel> SetupTimes { get; set; } = new();
        public SetupTimeFilterViewModel Filter { get; set; } = new();
        public PaginationViewModel Pagination { get; set; } = new();
    }

    public class SetupTimeItemViewModel
    {
        public int Id { get; set; }
        public string MachineName { get; set; } = string.Empty;
        public string FromDetailName { get; set; } = string.Empty;
        public string FromDetailNumber { get; set; } = string.Empty;
        public string ToDetailName { get; set; } = string.Empty;
        public string ToDetailNumber { get; set; } = string.Empty;
        public double Time { get; set; }
        public string TimeDisplayText => $"{Time:F2} ч";
        public DateTime? LastUsedUtc { get; set; }
        public int UsageCount { get; set; }
        public double? AverageActualTime { get; set; }
    }

    public class SetupTimeFilterViewModel
    {
        [Display(Name = "Станок")]
        public int? MachineId { get; set; }

        [Display(Name = "Деталь 'откуда'")]
        public int? FromDetailId { get; set; }

        [Display(Name = "Деталь 'куда'")]
        public int? ToDetailId { get; set; }

        [Display(Name = "Минимальное время (ч)")]
        [Range(0, double.MaxValue)]
        public double? MinTime { get; set; }

        [Display(Name = "Максимальное время (ч)")]
        [Range(0, double.MaxValue)]
        public double? MaxTime { get; set; }

        [Display(Name = "Показывать только используемые")]
        public bool ShowOnlyUsed { get; set; }

        // Списки для фильтров
        public List<SelectOptionViewModel> AvailableMachines { get; set; } = new();
        public List<SelectOptionViewModel> AvailableDetails { get; set; } = new();
    }

    /// <summary>
    /// ViewModel для создания/редактирования времени переналадки
    /// </summary>
    public class SetupTimeFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Выберите станок")]
        [Display(Name = "Станок")]
        public int MachineId { get; set; }

        [Required(ErrorMessage = "Выберите деталь 'откуда'")]
        [Display(Name = "Деталь 'откуда'")]
        public int FromDetailId { get; set; }

        [Required(ErrorMessage = "Выберите деталь 'куда'")]
        [Display(Name = "Деталь 'куда'")]
        public int ToDetailId { get; set; }

        [Required(ErrorMessage = "Укажите время переналадки")]
        [Range(0, double.MaxValue, ErrorMessage = "Время переналадки не может быть отрицательным")]
        [Display(Name = "Время переналадки (часы)")]
        public double Time { get; set; }

        [Display(Name = "Описание операций переналадки")]
        public string? SetupDescription { get; set; }

        [Display(Name = "Требуемая квалификация")]
        public string? RequiredSkills { get; set; }

        [Display(Name = "Необходимые инструменты")]
        public string? RequiredTools { get; set; }

        // Списки для выпадающих списков
        public List<SelectOptionViewModel> AvailableMachines { get; set; } = new();
        public List<SelectOptionViewModel> AvailableDetails { get; set; } = new();

        public bool IsEdit => Id > 0;

        // Для проверки корректности
        public bool IsValid => MachineId > 0 && FromDetailId > 0 && ToDetailId > 0 &&
                              FromDetailId != ToDetailId && Time >= 0;
    }

    /// <summary>
    /// ViewModel для массового импорта времен переналадки
    /// </summary>
    public class SetupTimeBulkImportViewModel
    {
        [Display(Name = "Перезаписать существующие записи")]
        public bool OverwriteExisting { get; set; } = false;

        [Display(Name = "CSV файл")]
        public IFormFile? CsvFile { get; set; }

        [Display(Name = "Данные в формате CSV")]
        [DataType(DataType.MultilineText)]
        public string? CsvData { get; set; }

        // Результаты импорта
        public SetupTimeBulkImportResultViewModel? ImportResult { get; set; }

        // Пример формата
        public string ExampleCsvFormat =>
            "Станок,Деталь_Откуда,Деталь_Куда,Время_Часы,Описание\n" +
            "Станок-1,Деталь-А,Деталь-Б,0.5,Смена оснастки\n" +
            "Станок-1,Деталь-Б,Деталь-В,1.0,Полная переналадка";
    }

    public class SetupTimeBulkImportResultViewModel
    {
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public bool HasErrors => FailureCount > 0 || Errors.Any();
        public bool HasWarnings => SkippedCount > 0 || Warnings.Any();
    }

    /// <summary>
    /// ViewModel для проверки необходимости переналадки
    /// </summary>
    public class SetupCheckViewModel
    {
        [Required(ErrorMessage = "Выберите станок")]
        [Display(Name = "Станок")]
        public int MachineId { get; set; }

        [Required(ErrorMessage = "Выберите деталь")]
        [Display(Name = "Деталь")]
        public int DetailId { get; set; }

        // Списки для выбора
        public List<SelectOptionViewModel> AvailableMachines { get; set; } = new();
        public List<SelectOptionViewModel> AvailableDetails { get; set; } = new();

        // Результат проверки
        public SetupInfoResultViewModel? Result { get; set; }
    }

    public class SetupInfoResultViewModel
    {
        public bool SetupNeeded { get; set; }
        public string? FromDetailName { get; set; }
        public string? FromDetailNumber { get; set; }
        public string ToDetailName { get; set; } = string.Empty;
        public string ToDetailNumber { get; set; } = string.Empty;
        public double SetupTime { get; set; }
        public string? SetupDescription { get; set; }
        public string? RequiredSkills { get; set; }
        public string? RequiredTools { get; set; }

        public string SetupTimeDisplayText => $"{SetupTime:F2} ч";
        public string StatusText => SetupNeeded ? "Требуется переналадка" : "Переналадка не требуется";
        public string StatusCssClass => SetupNeeded ? "text-warning" : "text-success";
    }
}