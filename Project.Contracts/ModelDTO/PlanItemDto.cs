using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Contracts.ModelDTO
{
    // DTO для планирования
    public class PlanItemDto
    {
        public int DetailId { get; set; }
        public int Quantity { get; set; }
        public int OptimalBatchSize { get; set; }
        public List<int> SubBatchSizes { get; set; }
    }

    public class PlanSummaryDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalBatches { get; set; }
        public int TotalQuantity { get; set; }
        public int CompletedQuantity { get; set; }
        public List<DetailPlanStatsDto> DetailStats { get; set; }
        public List<MachinePlanStatsDto> MachineStats { get; set; }
    }

    public class DetailPlanStatsDto
    {
        public int DetailId { get; set; }
        public string DetailName { get; set; }
        public int PlannedQuantity { get; set; }
        public int CompletedQuantity { get; set; }
        public double CompletionPercent => PlannedQuantity > 0 ?
            Math.Round(((double)CompletedQuantity / PlannedQuantity) * 100, 1) : 0;
    }

    public class MachinePlanStatsDto
    {
        public int MachineId { get; set; }
        public string MachineName { get; set; }
        public double TotalHours { get; set; }
        public double SetupHours { get; set; }
        public int StageCount { get; set; }
        public int CompletedStageCount { get; set; }
        public double EfficiencyPercent { get; set; }
        public double CompletionPercent => StageCount > 0 ?
            Math.Round(((double)CompletedStageCount / StageCount) * 100, 1) : 0;
    }

    public class OptimalBatchSizeDto
    {
        public int DetailId { get; set; }
        public string DetailName { get; set; }
        public int OptimalBatchSize { get; set; }
        public List<BatchSizeOptionDto> BatchSizeOptions { get; set; }
    }

    public class BatchSizeOptionDto
    {
        public int BatchSize { get; set; }
        public double TotalTimeForBatch { get; set; }
        public double SetupTime { get; set; }
        public double ProcessingTime { get; set; }
        public double AvgTimePerUnit { get; set; }
    }
}
