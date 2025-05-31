using Project.Contracts.Enums;
using System.ComponentModel.DataAnnotations;

namespace Project.Web.ViewModels
{
    /// <summary>
    /// ViewModel для управления этапами выполнения согласно ТЗ
    /// </summary>
    public class StageExecutionIndexViewModel
    {
        public List<StageExecutionItemViewModel> StageExecutions { get; set; } = new();
        public StageExecutionFilterViewModel Filter { get; set; } = new();
        public PaginationViewModel Pagination { get; set; } = new();
    }

    public class StageExecutionItemViewModel
    {
        public int Id { get; set; }
        public int BatchId { get; set; }
        public int SubBatchId { get; set; }
        public string DetailName { get; set; } = string.Empty;
        public string DetailNumber { get; set; } = string.Empty;
        public string StageName { get; set; } = string.Empty;
        public string? MachineName { get; set; }
        public string? MachineTypeName { get; set; }
        public StageStatus Status { get; set; }
        public string StatusDisplayName => GetStatusDisplayName();
        public string StatusBadgeClass => GetStatusBadgeClass();
        public bool IsSetup { get; set; }
        public Priority Priority { get; set; }
        public string PriorityDisplayName => GetPriorityDisplayName();
        public string PriorityBadgeClass => GetPriorityBadgeClass();
        public bool IsCritical { get; set; }
        public DateTime? PlannedStartTimeUtc { get; set; }
        public DateTime? PlannedEndTimeUtc { get; set; }
        public DateTime? StartTimeUtc { get; set; }
        public DateTime? EndTimeUtc { get; set; }
        public double PlannedDurationHours { get; set; }
        public double? ActualDurationHours { get; set; }
        public double? DeviationHours { get; set; }
        public decimal? CompletionPercentage { get; set; }
        public int? QueuePosition { get; set; }
        public int Quantity { get; set; }
        public string? OperatorId { get; set; }
        public bool IsOverdue { get; set; }
        public bool CanStart { get; set; }
        public DateTime CreatedUtc { get; set; }

        // Действия, доступные для этапа
        public bool CanPause => Status == StageStatus.InProgress;
        public bool CanResume => Status == StageStatus.Paused;
        public bool CanComplete => Status == StageStatus.InProgress || Status == StageStatus.Paused;
        public bool CanCancel => Status != StageStatus.Completed && Status != StageStatus.Cancelled;
        public bool CanReassign => Status == StageStatus.AwaitingStart || Status == StageStatus.InQueue || Status == StageStatus.Paused;

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

        private string GetPriorityDisplayName() => Priority switch
        {
            Priority.Low => "Низкий",
            Priority.Normal => "Обычный",
            Priority.High => "Высокий",
            Priority.Critical => "Критический",
            _ => "Обычный"
        };

        private string GetPriorityBadgeClass() => Priority switch
        {
            Priority.Low => "secondary",
            Priority.Normal => "primary",
            Priority.High => "warning",
            Priority.Critical => "danger",
            _ => "primary"
        };
    }

    public class StageExecutionFilterViewModel
    {
        [Display(Name = "Поиск")]
        public string SearchTerm { get; set; } = string.Empty;

        [Display(Name = "Станок")]
        public int? MachineId { get; set; }

        [Display(Name = "Тип станка")]
        public int? MachineTypeId { get; set; }

        [Display(Name = "Деталь")]
        public int? DetailId { get; set; }

        [Display(Name = "Партия")]
        public int? BatchId { get; set; }

        [Display(Name = "Статусы")]
        public List<StageStatus> SelectedStatuses { get; set; } = new();

        [Display(Name = "Приоритет")]
        public Priority? MinPriority { get; set; }

        [Display(Name = "Показывать только переналадки")]
        public bool ShowSetupsOnly { get; set; }

        [Display(Name = "Показывать только просроченные")]
        public bool ShowOverdueOnly { get; set; }

        [Display(Name = "Показывать только критические")]
        public bool ShowCriticalOnly { get; set; }

        [Display(Name = "Дата начала")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [Display(Name = "Дата окончания")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        // Списки для фильтров
        public List<SelectOptionViewModel> AvailableMachines { get; set; } = new();
        public List<SelectOptionViewModel> AvailableMachineTypes { get; set; } = new();
        public List<SelectOptionViewModel> AvailableDetails { get; set; } = new();
        public List<SelectOptionViewModel> AvailableBatches { get; set; } = new();
    }

    /// <summary>
    /// ViewModel для управления этапом
    /// </summary>
    public class StageExecutionControlViewModel
    {
        [Required]
        public int Id { get; set; }

        public string DetailName { get; set; } = string.Empty;
        public string StageName { get; set; } = string.Empty;
        public string? MachineName { get; set; }
        public StageStatus Status { get; set; }
        public Priority Priority { get; set; }
        public bool IsSetup { get; set; }
        public bool IsCritical { get; set; }

        [Display(Name = "Причина/Примечание")]
        public string? ReasonNote { get; set; }

        [Display(Name = "Оператор")]
        public string? OperatorId { get; set; }

        [Display(Name = "Новый станок")]
        public int? NewMachineId { get; set; }

        [Display(Name = "Новый приоритет")]
        public Priority? NewPriority { get; set; }

        // Доступные действия
        public bool CanStart { get; set; }
        public bool CanPause { get; set; }
        public bool CanResume { get; set; }
        public bool CanComplete { get; set; }
        public bool CanCancel { get; set; }
        public bool CanReassign { get; set; }
        public bool CanChangePriority { get; set; }

        // Списки для выбора
        public List<SelectOptionViewModel> AvailableMachines { get; set; } = new();
        public List<SelectOptionViewModel> AvailableOperators { get; set; } = new();
    }

    /// <summary>
    /// ViewModel для детального просмотра этапа
    /// </summary>
    public class StageExecutionDetailsViewModel
    {
        public int Id { get; set; }
        public int BatchId { get; set; }
        public int SubBatchId { get; set; }
        public string DetailName { get; set; } = string.Empty;
        public string DetailNumber { get; set; } = string.Empty;
        public string StageName { get; set; } = string.Empty;
        public string StageType { get; set; } = string.Empty;
        public int? MachineId { get; set; }
        public string? MachineName { get; set; }
        public string? MachineTypeName { get; set; }
        public StageStatus Status { get; set; }
        public bool IsSetup { get; set; }
        public Priority Priority { get; set; }
        public bool IsCritical { get; set; }

        // Временные рамки
        public DateTime? PlannedStartTimeUtc { get; set; }
        public DateTime? PlannedEndTimeUtc { get; set; }
        public DateTime? StartTimeUtc { get; set; }
        public DateTime? EndTimeUtc { get; set; }
        public DateTime? PauseTimeUtc { get; set; }
        public DateTime? ResumeTimeUtc { get; set; }

        // Времена и отклонения
        public double PlannedDurationHours { get; set; }
        public double? ActualDurationHours { get; set; }
        public double? DeviationHours { get; set; }
        public decimal? CompletionPercentage { get; set; }

        // Дополнительная информация
        public int? QueuePosition { get; set; }
        public int Quantity { get; set; }
        public string? OperatorId { get; set; }
        public string? ReasonNote { get; set; }
        public string? DeviceId { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? StatusChangedTimeUtc { get; set; }
        public bool IsOverdue { get; set; }

        // Связанные этапы
        public int? SetupStageId { get; set; }
        public int? MainStageId { get; set; }

        // История изменений статуса
        public List<StageStatusHistoryViewModel> StatusHistory { get; set; } = new();
    }

    public class StageStatusHistoryViewModel
    {
        public StageStatus Status { get; set; }
        public DateTime ChangedUtc { get; set; }
        public string? OperatorId { get; set; }
        public string? ReasonNote { get; set; }
        public string? DeviceId { get; set; }
    }
}