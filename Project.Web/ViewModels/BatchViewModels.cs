using Project.Contracts.Enums;
using System.ComponentModel.DataAnnotations;

namespace Project.Web.ViewModels
{
    /// <summary>
    /// ViewModel для списка партий согласно ТЗ
    /// </summary>
    public class BatchesIndexViewModel
    {
        public List<BatchItemViewModel> Batches { get; set; } = new();
        public BatchFilterViewModel Filter { get; set; } = new();
        public PaginationViewModel Pagination { get; set; } = new();
    }

    public class BatchItemViewModel
    {
        public int Id { get; set; }
        public string DetailName { get; set; } = string.Empty;
        public string DetailNumber { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public DateTime CreatedUtc { get; set; }
        public Priority Priority { get; set; }
        public string PriorityDisplayName => GetPriorityDisplayName();
        public string PriorityBadgeClass => GetPriorityBadgeClass();

        // Прогресс
        public decimal CompletionPercentage { get; set; }
        public string CompletionStatusText => GetCompletionStatusText();

        // Статистика этапов
        public int TotalStages { get; set; }
        public int CompletedStages { get; set; }
        public int InProgressStages { get; set; }
        public int QueuedStages { get; set; }

        // Времена
        public double TotalPlannedTimeHours { get; set; }
        public double? TotalActualTimeHours { get; set; }
        public DateTime? EstimatedCompletionTimeUtc { get; set; }

        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }

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

        private string GetCompletionStatusText()
        {
            if (CompletionPercentage >= 100) return "Завершено";
            if (InProgressStages > 0) return "В работе";
            if (QueuedStages > 0) return "В очереди";
            return "Ожидает";
        }
    }

    public class BatchFilterViewModel
    {
        [Display(Name = "Поиск по детали")]
        public string SearchTerm { get; set; } = string.Empty;

        [Display(Name = "Приоритет")]
        public Priority? Priority { get; set; }

        [Display(Name = "Статус")]
        public string? Status { get; set; }

        [Display(Name = "Дата создания с")]
        [DataType(DataType.Date)]
        public DateTime? CreatedFrom { get; set; }

        [Display(Name = "Дата создания по")]
        [DataType(DataType.Date)]
        public DateTime? CreatedTo { get; set; }

        public bool ShowCompleted { get; set; } = true;
        public bool ShowInProgress { get; set; } = true;
        public bool ShowPending { get; set; } = true;
    }

    /// <summary>
    /// ViewModel для создания партии согласно ТЗ
    /// </summary>
    public class BatchCreateViewModel
    {
        [Required(ErrorMessage = "Выберите деталь")]
        [Display(Name = "Деталь")]
        public int DetailId { get; set; }

        [Required(ErrorMessage = "Укажите количество")]
        [Range(1, int.MaxValue, ErrorMessage = "Количество должно быть больше 0")]
        [Display(Name = "Количество")]
        public int Quantity { get; set; }

        [Display(Name = "Приоритет")]
        public Priority Priority { get; set; } = Priority.Normal;

        [Display(Name = "Автоматически запустить планирование")]
        public bool AutoStartPlanning { get; set; } = true;

        [Display(Name = "Разделить на подпартии")]
        public bool SplitIntoBatches { get; set; }

        [Display(Name = "Количество деталей в подпартии")]
        [Range(1, int.MaxValue, ErrorMessage = "Количество должно быть больше 0")]
        public int? SubBatchSize { get; set; }

        // Список доступных деталей
        public List<DetailForBatchOption> AvailableDetails { get; set; } = new();

        // Предварительный расчет
        public BatchPreviewViewModel? Preview { get; set; }
    }

    public class DetailForBatchOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public bool HasRoute { get; set; }
        public int RouteStagesCount { get; set; }
        public double? EstimatedTimePerUnit { get; set; }
    }

    public class BatchPreviewViewModel
    {
        public double EstimatedTotalHours { get; set; }
        public DateTime EstimatedStartTime { get; set; }
        public DateTime EstimatedCompletionTime { get; set; }
        public int SubBatchesCount { get; set; }
        public List<StagePreviewViewModel> Stages { get; set; } = new();
    }

    public class StagePreviewViewModel
    {
        public string StageName { get; set; } = string.Empty;
        public string MachineTypeName { get; set; } = string.Empty;
        public double EstimatedHours { get; set; }
        public DateTime EstimatedStartTime { get; set; }
        public DateTime EstimatedEndTime { get; set; }
    }

    /// <summary>
    /// ViewModel для детальной информации о партии
    /// </summary>
    public class BatchDetailsViewModel
    {
        public int Id { get; set; }
        public string DetailName { get; set; } = string.Empty;
        public string DetailNumber { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public DateTime CreatedUtc { get; set; }
        public Priority Priority { get; set; }
        public decimal CompletionPercentage { get; set; }

        // Подпартии
        public List<SubBatchViewModel> SubBatches { get; set; } = new();

        // Общая статистика
        public BatchStatisticsViewModel Statistics { get; set; } = new();

        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }

    public class SubBatchViewModel
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
        public decimal CompletionPercentage { get; set; }
        public string? CurrentStageName { get; set; }
        public string? NextStageName { get; set; }
        public List<StageExecutionSummaryViewModel> Stages { get; set; } = new();
    }

    public class StageExecutionSummaryViewModel
    {
        public int Id { get; set; }
        public string StageName { get; set; } = string.Empty;
        public string? MachineName { get; set; }
        public StageStatus Status { get; set; }
        public string StatusDisplayName => GetStatusDisplayName();
        public string StatusBadgeClass => GetStatusBadgeClass();
        public bool IsSetup { get; set; }
        public Priority Priority { get; set; }
        public DateTime? StartTimeUtc { get; set; }
        public DateTime? EndTimeUtc { get; set; }
        public double PlannedDurationHours { get; set; }
        public double? ActualDurationHours { get; set; }

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

    public class BatchStatisticsViewModel
    {
        public int TotalStages { get; set; }
        public int CompletedStages { get; set; }
        public int InProgressStages { get; set; }
        public int QueuedStages { get; set; }
        public int PausedStages { get; set; }
        public double TotalPlannedHours { get; set; }
        public double? TotalActualHours { get; set; }
        public double? EfficiencyPercentage { get; set; }
        public DateTime? EstimatedCompletionTime { get; set; }
    }

    /// <summary>
    /// ViewModel для пагинации
    /// </summary>
    public class PaginationViewModel
    {
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }
}