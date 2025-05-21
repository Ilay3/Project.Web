using System;
using System.Collections.Generic;

namespace Project.Web.ViewModels
{
    public class HistoryViewModel
    {
        public List<StageHistoryViewModel> Stages { get; set; }
        public HistoryFilterViewModel Filter { get; set; }
        public StatisticsViewModel Statistics { get; set; }
    }

    public class StageHistoryViewModel
    {
        public int Id { get; set; }
        public int SubBatchId { get; set; }
        public int BatchId { get; set; }
        public string DetailName { get; set; }
        public string StageName { get; set; }
        public int? MachineId { get; set; }
        public string MachineName { get; set; }
        public string Status { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public DateTime? PauseTime { get; set; }
        public DateTime? ResumeTime { get; set; }
        public bool IsSetup { get; set; }
        public string OperatorId { get; set; }
        public string ReasonNote { get; set; }
        public double? Duration { get; set; }
    }

    public class HistoryFilterViewModel
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? MachineId { get; set; }
        public string MachineName { get; set; }
        public int? DetailId { get; set; }
        public string DetailName { get; set; }
        public bool IncludeSetups { get; set; } = true;
        public string StatusFilter { get; set; } = "All";
        public List<MachineViewModel> AvailableMachines { get; set; }
        public List<DetailViewModel> AvailableDetails { get; set; }
    }

    public class StatisticsViewModel
    {
        public int TotalStages { get; set; }
        public int CompletedStages { get; set; }
        public int SetupStages { get; set; }
        public double TotalWorkHours { get; set; }
        public double TotalSetupHours { get; set; }
        public double TotalIdleHours { get; set; }
        public double EfficiencyPercentage { get; set; }
    }
}