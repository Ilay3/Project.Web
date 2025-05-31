using Project.Contracts.Enums;
using System.ComponentModel.DataAnnotations;

namespace Project.Web.ViewModels
{
    /// <summary>
    /// ViewModel для отчетов согласно ТЗ
    /// </summary>
    public class ReportsIndexViewModel
    {
        public List<ReportCategoryViewModel> Categories { get; set; } = new();
    }

    public class ReportCategoryViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<ReportItemViewModel> Reports { get; set; } = new();
    }

    public class ReportItemViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }

    /// <summary>
    /// ViewModel для календарного отчета по станкам согласно ТЗ
    /// </summary>
    public class MachineCalendarReportViewModel
    {
        public MachineCalendarFilterViewModel Filter { get; set; } = new();
        public MachineCalendarDataViewModel? Data { get; set; }
    }

    public class MachineCalendarFilterViewModel
    {
        [Required(ErrorMessage = "Выберите станок")]
        [Display(Name = "Станок")]
        public int MachineId { get; set; }

        [Required]
        [Display(Name = "Дата начала")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today.AddDays(-7);

        [Required]
        [Display(Name = "Дата окончания")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; } = DateTime.Today;

        [Display(Name = "Формат экспорта")]
        public string? ExportFormat { get; set; }

        // Список станков
        public List<SelectOptionViewModel> AvailableMachines { get; set; } = new();
    }

    public class MachineCalendarDataViewModel
    {
        public string MachineName { get; set; } = string.Empty;
        public string MachineTypeName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // Данные по дням
        public List<MachineCalendarDayViewModel> Days { get; set; } = new();

        // Общая статистика
        public MachineCalendarSummaryViewModel Summary { get; set; } = new();
    }

    public class MachineCalendarDayViewModel
    {
        public DateTime Date { get; set; }
        public double WorkingHours { get; set; }
        public double SetupHours { get; set; }
        public double IdleHours { get; set; }
        public decimal UtilizationPercentage { get; set; }
        public List<ManufacturedPartViewModel> ManufacturedParts { get; set; } = new();

        public string UtilizationCssClass => UtilizationPercentage switch
        {
            >= 80 => "text-success",
            >= 60 => "text-warning",
            _ => "text-danger"
        };
    }

    public class ManufacturedPartViewModel
    {
        public string DetailName { get; set; } = string.Empty;
        public string DetailNumber { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public double ManufacturingTimeHours { get; set; }
        public int BatchId { get; set; }
    }

    public class MachineCalendarSummaryViewModel
    {
        public double TotalWorkingHours { get; set; }
        public double TotalSetupHours { get; set; }
        public double TotalIdleHours { get; set; }
        public decimal OverallUtilization { get; set; }
        public int TotalPartsManufactured { get; set; }
        public int TotalSetupOperations { get; set; }
        public double AverageSetupTime { get; set; }
    }

    /// <summary>
    /// ViewModel для отчета по производительности согласно ТЗ
    /// </summary>
    public class ProductivityReportViewModel
    {
        public ProductivityFilterViewModel Filter { get; set; } = new();
        public ProductivityDataViewModel? Data { get; set; }
    }

    public class ProductivityFilterViewModel
    {
        [Required]
        [Display(Name = "Дата начала")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today.AddDays(-30);

        [Required]
        [Display(Name = "Дата окончания")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; } = DateTime.Today;

        [Display(Name = "Станки")]
        public List<int> SelectedMachineIds { get; set; } = new();

        [Display(Name = "Типы станков")]
        public List<int> SelectedMachineTypeIds { get; set; } = new();

        [Display(Name = "Детали")]
        public List<int> SelectedDetailIds { get; set; } = new();

        [Display(Name = "Операторы")]
        public List<string> SelectedOperatorIds { get; set; } = new();

        [Display(Name = "Включать переналадки")]
        public bool IncludeSetups { get; set; } = true;

        [Display(Name = "Только просроченные")]
        public bool IncludeOverdueOnly { get; set; } = false;

        [Display(Name = "Группировка")]
        public string GroupBy { get; set; } = "Day"; // Day, Week, Month

        [Display(Name = "Формат экспорта")]
        public string? ExportFormat { get; set; }

        // Списки для фильтров
        public List<SelectOptionViewModel> AvailableMachines { get; set; } = new();
        public List<SelectOptionViewModel> AvailableMachineTypes { get; set; } = new();
        public List<SelectOptionViewModel> AvailableDetails { get; set; } = new();
        public List<SelectOptionViewModel> AvailableOperators { get; set; } = new();
    }

    public class ProductivityDataViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // Сравнение планового и фактического времени по деталям
        public List<DetailPerformanceViewModel> DetailPerformance { get; set; } = new();

        // Анализ отклонений по операциям
        public List<OperationDeviationViewModel> OperationDeviations { get; set; } = new();

        // Статистика по загрузке станков
        public List<MachineUtilizationReportViewModel> MachineUtilization { get; set; } = new();

        // Эффективность работы операторов
        public List<OperatorEfficiencyViewModel> OperatorEfficiency { get; set; } = new();

        // Общие показатели
        public OverallProductivityViewModel OverallProductivity { get; set; } = new();
    }

    public class DetailPerformanceViewModel
    {
        public string DetailName { get; set; } = string.Empty;
        public string DetailNumber { get; set; } = string.Empty;
        public int QuantityManufactured { get; set; }
        public double PlannedTimeHours { get; set; }
        public double ActualTimeHours { get; set; }
        public double DeviationHours { get; set; }
        public decimal EfficiencyPercentage { get; set; }
        public int CompletedBatches { get; set; }
        public double AverageTimePerUnit { get; set; }

        public string EfficiencyCssClass => EfficiencyPercentage switch
        {
            >= 90 => "text-success",
            >= 70 => "text-warning",
            _ => "text-danger"
        };
    }

    public class OperationDeviationViewModel
    {
        public string OperationName { get; set; } = string.Empty;
        public string MachineTypeName { get; set; } = string.Empty;
        public int ExecutionCount { get; set; }
        public double AveragePlannedTime { get; set; }
        public double AverageActualTime { get; set; }
        public double AverageDeviation { get; set; }
        public decimal DeviationPercentage { get; set; }
        public int OverdueCount { get; set; }

        public string DeviationCssClass => DeviationPercentage switch
        {
            <= 10 => "text-success",
            <= 25 => "text-warning",
            _ => "text-danger"
        };
    }

    public class MachineUtilizationReportViewModel
    {
        public string MachineName { get; set; } = string.Empty;
        public string MachineTypeName { get; set; } = string.Empty;
        public double TotalAvailableHours { get; set; }
        public double WorkingHours { get; set; }
        public double SetupHours { get; set; }
        public double IdleHours { get; set; }
        public decimal UtilizationPercentage { get; set; }
        public int CompletedOperations { get; set; }
        public int SetupOperations { get; set; }

        public string UtilizationCssClass => UtilizationPercentage switch
        {
            >= 80 => "text-success",
            >= 60 => "text-warning",
            _ => "text-danger"
        };
    }

    public class OperatorEfficiencyViewModel
    {
        public string OperatorId { get; set; } = string.Empty;
        public string OperatorName { get; set; } = string.Empty;
        public int OperationsCompleted { get; set; }
        public double TotalWorkingHours { get; set; }
        public double AverageOperationTime { get; set; }
        public decimal EfficiencyPercentage { get; set; }
        public int OperationsStarted { get; set; }
        public int OperationsPaused { get; set; }
        public DateTime LastActivity { get; set; }

        public string EfficiencyCssClass => EfficiencyPercentage switch
        {
            >= 90 => "text-success",
            >= 70 => "text-warning",
            _ => "text-danger"
        };
    }

    public class OverallProductivityViewModel
    {
        public int TotalPartsManufactured { get; set; }
        public int TotalBatchesCompleted { get; set; }
        public double TotalManufacturingTime { get; set; }
        public double TotalSetupTime { get; set; }
        public decimal OverallEfficiency { get; set; }
        public double AverageTimePerPart { get; set; }
        public double AverageSetupTime { get; set; }
        public int TotalOverdueStages { get; set; }
        public decimal OnTimeDeliveryRate { get; set; }

        public string EfficiencyCssClass => OverallEfficiency switch
        {
            >= 85 => "text-success",
            >= 70 => "text-warning",
            _ => "text-danger"
        };
    }
}