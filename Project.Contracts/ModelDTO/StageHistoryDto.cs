using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Contracts.ModelDTO
{
    public class StageHistoryDto
    {
        public int Id { get; set; }
        public int SubBatchId { get; set; }
        public int BatchId { get; set; }
        public string DetailName { get; set; }
        public string StageName { get; set; }
        public int? MachineId { get; set; }
        public string MachineName { get; set; }
        public string Status { get; set; }
        public DateTime? StartTimeUtc { get; set; }
        public DateTime? EndTimeUtc { get; set; }
        public DateTime? PauseTimeUtc { get; set; }
        public DateTime? ResumeTimeUtc { get; set; }
        public bool IsSetup { get; set; }
        public string OperatorId { get; set; }
        public string ReasonNote { get; set; }
        public double? Duration { get; set; } // Фактическая длительность в часах
        public DateTime? StatusChangedTimeUtc { get; set; }
    }

    public class StageHistoryFilterDto
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? MachineId { get; set; }
        public int? DetailId { get; set; }
        public bool IncludeSetups { get; set; } = true;
        public string StatusFilter { get; set; } = "All";
    }

    public class StageStatisticsDto
    {
        public int TotalStages { get; set; }
        public int CompletedStages { get; set; }
        public int SetupStages { get; set; }
        public double TotalWorkHours { get; set; }
        public double TotalSetupHours { get; set; }
        public double TotalIdleHours { get; set; }
        public double EfficiencyPercentage { get; set; } // % времени работы от общего
    }
}