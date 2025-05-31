using Project.Contracts.Enums;

namespace Project.Web.ViewModels
{
    /// <summary>
    /// ViewModel для главной панели управления согласно ТЗ
    /// </summary>
    public class DashboardViewModel
    {
        // Общая статистика по производству
        public ProductionOverviewViewModel ProductionOverview { get; set; } = new();

        // Статистика по станкам
        public MachineOverviewViewModel MachineOverview { get; set; } = new();

        // Активные партии
        public List<ActiveBatchViewModel> ActiveBatches { get; set; } = new();

        // Этапы в очереди
        public List<QueuedStageViewModel> QueuedStages { get; set; } = new();

        // Станки по статусам
        public List<MachineStatusSummaryViewModel> MachineStatusSummary { get; set; } = new();

        // Недавние события
        public List<RecentEventViewModel> RecentEvents { get; set; } = new();

        // Алерты и уведомления
        public List<AlertViewModel> Alerts { get; set; } = new();
    }

    public class ProductionOverviewViewModel
    {
        /// <summary>
        /// Количество активных партий
        /// </summary>
        public int ActiveBatches { get; set; }

        /// <summary>
        /// Количество работающих станков
        /// </summary>
        public int WorkingMachines { get; set; }

        /// <summary>
        /// Количество этапов в очереди
        /// </summary>
        public int QueuedStages { get; set; }

        /// <summary>
        /// Завершенных деталей за сегодня
        /// </summary>
        public int TodayCompletedParts { get; set; }

        /// <summary>
        /// Общая эффективность производства (%)
        /// </summary>
        public decimal OverallEfficiency { get; set; }

        /// <summary>
        /// Загруженность производства (%)
        /// </summary>
        public decimal ProductionUtilization { get; set; }

        /// <summary>
        /// Просроченных этапов
        /// </summary>
        public int OverdueStages { get; set; }

        /// <summary>
        /// Количество переналадок за сегодня
        /// </summary>
        public int TodaySetups { get; set; }
    }

    public class MachineOverviewViewModel
    {
        public int TotalMachines { get; set; }
        public int FreeMachines { get; set; }
        public int BusyMachines { get; set; }
        public int SetupMachines { get; set; }
        public int BrokenMachines { get; set; }

        public decimal AverageUtilization { get; set; }

        public int FreePercentage => TotalMachines > 0 ? (FreeMachines * 100 / TotalMachines) : 0;
        public int BusyPercentage => TotalMachines > 0 ? (BusyMachines * 100 / TotalMachines) : 0;
        public int SetupPercentage => TotalMachines > 0 ? (SetupMachines * 100 / TotalMachines) : 0;
        public int BrokenPercentage => TotalMachines > 0 ? (BrokenMachines * 100 / TotalMachines) : 0;
    }

    public class ActiveBatchViewModel
    {
        public int Id { get; set; }
        public string DetailName { get; set; } = string.Empty;
        public string DetailNumber { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal CompletionPercentage { get; set; }
        public Priority Priority { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? EstimatedCompletionTimeUtc { get; set; }
        public int InProgressStages { get; set; }
        public int QueuedStages { get; set; }

        public string PriorityBadgeClass => Priority switch
        {
            Priority.Low => "secondary",
            Priority.Normal => "primary",
            Priority.High => "warning",
            Priority.Critical => "danger",
            _ => "primary"
        };

        public string StatusText
        {
            get
            {
                if (CompletionPercentage >= 100) return "Завершено";
                if (InProgressStages > 0) return "В работе";
                if (QueuedStages > 0) return "В очереди";
                return "Ожидает";
            }
        }
    }

    public class QueuedStageViewModel
    {
        public int Id { get; set; }
        public string DetailName { get; set; } = string.Empty;
        public string StageName { get; set; } = string.Empty;
        public string? MachineName { get; set; }
        public Priority Priority { get; set; }
        public DateTime CreatedUtc { get; set; }
        public TimeSpan WaitingTime => DateTime.UtcNow - CreatedUtc;
        public int QueuePosition { get; set; }
        public bool RequiresSetup { get; set; }

        public string PriorityBadgeClass => Priority switch
        {
            Priority.Low => "secondary",
            Priority.Normal => "primary",
            Priority.High => "warning",
            Priority.Critical => "danger",
            _ => "primary"
        };

        public string WaitingTimeText
        {
            get
            {
                if (WaitingTime.TotalDays >= 1)
                    return $"{(int)WaitingTime.TotalDays} дн";
                if (WaitingTime.TotalHours >= 1)
                    return $"{(int)WaitingTime.TotalHours} ч";
                return $"{(int)WaitingTime.TotalMinutes} мин";
            }
        }
    }

    public class MachineStatusSummaryViewModel
    {
        public MachineStatus Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public int Count { get; set; }
        public string CssClass { get; set; } = string.Empty;
        public List<string> MachineNames { get; set; } = new();
    }

    public class RecentEventViewModel
    {
        public string EventType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Icon { get; set; } = string.Empty;
        public string CssClass { get; set; } = string.Empty;
        public string? RelatedUrl { get; set; }

        public string TimeAgoText
        {
            get
            {
                var timeAgo = DateTime.UtcNow - Timestamp;
                if (timeAgo.TotalDays >= 1)
                    return $"{(int)timeAgo.TotalDays} дн назад";
                if (timeAgo.TotalHours >= 1)
                    return $"{(int)timeAgo.TotalHours} ч назад";
                return $"{(int)timeAgo.TotalMinutes} мин назад";
            }
        }
    }

    public class AlertViewModel
    {
        public string Type { get; set; } = string.Empty; // success, warning, danger, info
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? ActionUrl { get; set; }
        public string? ActionText { get; set; }
        public bool IsDismissible { get; set; } = true;

        public string AlertCssClass => $"alert-{Type}";
        public string IconClass => Type switch
        {
            "success" => "fas fa-check-circle",
            "warning" => "fas fa-exclamation-triangle",
            "danger" => "fas fa-exclamation-circle",
            "info" => "fas fa-info-circle",
            _ => "fas fa-bell"
        };
    }
}