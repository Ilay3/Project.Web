using Project.Contracts.Enums;

namespace Project.Web.ViewModels
{
    public class PlanningIndexViewModel
    {
        public PlanningOverviewViewModel Overview { get; set; } = new();
        public List<BatchSummaryViewModel> UnscheduledBatches { get; set; } = new();
        public List<CriticalStageViewModel> CriticalStages { get; set; } = new();
    }

    public class PlanningOverviewViewModel
    {
        public int TotalStagesInQueue { get; set; }
        public int StagesInProgress { get; set; }
        public int PendingStages { get; set; }
        public int OverdueStages { get; set; }
        public decimal EfficiencyPercentage { get; set; }
        public TimeSpan AverageWaitTime { get; set; }
        public int ConflictsCount { get; set; }
    }

    public class PlanningQueueViewModel
    {
        public List<QueuedStageViewModel> QueuedStages { get; set; } = new();
    }

    public class PlanningForecastViewModel
    {
        public int DetailId { get; set; }
        public int Quantity { get; set; } = 1;
        public List<SelectOptionViewModel> AvailableDetails { get; set; } = new();
        public ForecastResultViewModel? Result { get; set; }
    }

    public class ForecastResultViewModel
    {
        public DateTime EarliestStartTime { get; set; }
        public DateTime LatestEndTime { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public List<StageForecastViewModel> Stages { get; set; } = new();
    }

    public class StageForecastViewModel
    {
        public string StageName { get; set; } = string.Empty;
        public string MachineTypeName { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public DateTime ExpectedStartTime { get; set; }
        public DateTime ExpectedEndTime { get; set; }
        public bool NeedsSetup { get; set; }
        public double SetupTimeHours { get; set; }
        public double OperationTimeHours { get; set; }
        public double QueueTimeHours { get; set; }
    }

    public class BatchSummaryViewModel
    {
        public int Id { get; set; }
        public string DetailName { get; set; } = string.Empty;
        public string DetailNumber { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public Priority Priority { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public class CriticalStageViewModel
    {
        public int Id { get; set; }
        public string DetailName { get; set; } = string.Empty;
        public string StageName { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public Priority Priority { get; set; }
        public TimeSpan WaitingTime { get; set; }
        public bool IsOverdue { get; set; }
    }

}
