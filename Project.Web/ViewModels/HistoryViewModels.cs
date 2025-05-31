using Project.Contracts.Enums;
using System.ComponentModel.DataAnnotations;

namespace Project.Web.ViewModels
{
    /// <summary>
    /// ViewModel для истории выполнения этапов согласно ТЗ
    /// </summary>
    public class HistoryIndexViewModel
    {
        public List<HistoryItemViewModel> HistoryItems { get; set; } = new();
        public HistoryFilterViewModel Filter { get; set; } = new();
        public PaginationViewModel Pagination { get; set; } = new();
        public HistoryStatisticsViewModel Statistics { get; set; } = new();
    }

    public class HistoryItemViewModel
    {
        public int Id { get; set; }
        public int SubBatchId { get; set; }
        public int BatchId { get; set; }
        public string DetailName { get; set; } = string.Empty;
        public string DetailNumber { get; set; } = string.Empty;
        public string StageName { get; set; } = string.Empty;
        public int? MachineId { get; set; }
        public string? MachineName { get; set; }
        public string? MachineTypeName { get; set; }
        public StageStatus Status { get; set; }
        public string StatusDisplayName => GetStatusDisplayName();
        public string StatusBadgeClass => GetStatusBadgeClass();
        public bool IsSetup { get; set; }
        public Priority Priority { get; set; }
        public DateTime? StartTimeUtc { get; set; }
        public DateTime? EndTimeUtc { get; set; }
        public DateTime? PauseTimeUtc { get; set; }
        public DateTime? ResumeTimeUtc { get; set; }
        public DateTime StatusChangedTimeUtc { get; set; }
        public string? OperatorId { get; set; }
        public string? ReasonNote { get; set; }
        public string? DeviceId { get; set; }
        public double? DurationHours { get; set; }
        public double PlannedDurationHours { get; set; }
        public double? DeviationHours { get; set; }
        public int Quantity { get; set; }
        public decimal? CompletionPercentage { get; set; }
        public bool IsOverdue { get; set; }
        public DateTime CreatedUtc { get; set; }

        public string DurationDisplayText => DurationHours.HasValue ? $"{DurationHours.Value:F2} ч" : "—";
        public string DeviationDisplayText => DeviationHours.HasValue ?
            $"{(DeviationHours.Value >= 0 ? "+" : "")}{DeviationHours.Value:F2} ч" : "—";
        public string DeviationCssClass => DeviationHours.HasValue ?
            (DeviationHours.Value > 0 ? "text-danger" : "text-success") : "";

        private string GetStatusDisplayName() => Status switch
        {
            StageStatus.AwaitingStart => "Ожидает запуска",
            StageStatus.InQueue => "В очереди",
            StageStatus.InProgress => "Выполняется",
            StageStatus.Paused => "На паузе",
            StageStatus.Completed => "Завершено",
            StageStatus.Cancelled => "Отменено",
            _ => "Неизвестно"
        };

        private string GetStatusBadgeClass() => Status switch
        {
            StageStatus.AwaitingStart => "secondary",
            StageStatus.InQueue => "warning",
            StageStatus.InProgress => "primary",
            StageStatus.Paused => "info",
            StageStatus.Completed => "success",
            StageStatus.Cancelled => "danger",
            _ => "secondary"
        };
    }

    public class HistoryFilterViewModel
    {
        [Display(Name = "Дата начала")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; } = DateTime.Today.AddDays(-7);

        [Display(Name = "Дата окончания")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; } = DateTime.Today.AddDays(1);

        [Display(Name = "Станок")]
        public int? MachineId { get; set; }

        [Display(Name = "Деталь")]
        public int? DetailId { get; set; }

        [Display(Name = "Партия")]
        public int? BatchId { get; set; }

        [Display(Name = "Оператор")]
        public string? OperatorId { get; set; }

        [Display(Name = "Статусы")]
        public List<StageStatus> SelectedStatuses { get; set; } = new();

        [Display(Name = "Включать переналадки")]
        public bool IncludeSetups { get; set; } = true;

        [Display(Name = "Только просроченные")]
        public bool IsOverdueOnly { get; set; }

        [Display(Name = "Минимальная длительность (ч)")]
        [Range(0, double.MaxValue)]
        public double? MinDurationHours { get; set; }

        [Display(Name = "Максимальная длительность (ч)")]
        [Range(0, double.MaxValue)]
        public double? MaxDurationHours { get; set; }

        [Display(Name = "Сортировка")]
        public string SortBy { get; set; } = "StartTime";

        [Display(Name = "По убыванию")]
        public bool SortDescending { get; set; } = true;

        // Списки для фильтров
        public List<SelectOptionViewModel> AvailableMachines { get; set; } = new();
        public List<SelectOptionViewModel> AvailableDetails { get; set; } = new();
        public List<SelectOptionViewModel> AvailableBatches { get; set; } = new();
        public List<SelectOptionViewModel> AvailableOperators { get; set; } = new();
    }

    public class HistoryStatisticsViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalStages { get; set; }
        public int CompletedStages { get; set; }
        public int SetupStages { get; set; }
        public double TotalWorkHours { get; set; }
        public double TotalSetupHours { get; set; }
        public double TotalIdleHours { get; set; }
        public decimal EfficiencyPercentage { get; set; }
        public double AverageStageTime { get; set; }
        public double AverageSetupTime { get; set; }
        public int OverdueStages { get; set; }
        public decimal OnTimePercentage { get; set; }

        public string EfficiencyCssClass => EfficiencyPercentage switch
        {
            >= 85 => "text-success",
            >= 70 => "text-warning",
            _ => "text-danger"
        };

        public string OnTimeCssClass => OnTimePercentage switch
        {
            >= 90 => "text-success",
            >= 75 => "text-warning",
            _ => "text-danger"
        };
    }

    /// <summary>
    /// ViewModel для детального просмотра записи истории
    /// </summary>
    public class HistoryDetailsViewModel
    {
        public HistoryItemViewModel HistoryItem { get; set; } = new();
        public List<HistoryEventViewModel> Events { get; set; } = new();
        public HistoryRelatedInfoViewModel RelatedInfo { get; set; } = new();
    }

    public class HistoryEventViewModel
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? OperatorId { get; set; }
        public string? DeviceId { get; set; }
        public string? Data { get; set; }
    }

    public class HistoryRelatedInfoViewModel
    {
        public BatchItemViewModel? Batch { get; set; }
        public DetailItemViewModel? Detail { get; set; }
        public MachineItemViewModel? Machine { get; set; }
        public List<HistoryItemViewModel> RelatedStages { get; set; } = new();
    }
}