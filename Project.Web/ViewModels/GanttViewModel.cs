using System;
using System.Collections.Generic;

namespace Project.Web.ViewModels
{
    public class GanttViewModel
    {
        public List<GanttStageViewModel> Stages { get; set; }
        public List<MachineViewModel> Machines { get; set; }
        public List<StageQueueViewModel> QueueItems { get; set; }
    }

    public class GanttStageViewModel
    {
        public int Id { get; set; }
        public int BatchId { get; set; }
        public int SubBatchId { get; set; }
        public string DetailName { get; set; }
        public string StageName { get; set; }
        public string MachineName { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Status { get; set; }
        public bool IsSetup { get; set; }
        public TimeSpan PlannedDuration { get; set; }
    }

    public class StageQueueViewModel
    {
        public int StageExecutionId { get; set; }
        public int SubBatchId { get; set; }
        public string DetailName { get; set; }
        public string StageName { get; set; }
        public string Status { get; set; }
        public int ExpectedMachineId { get; set; }
        public string ExpectedMachineName { get; set; }
        public DateTime ExpectedStartTime { get; set; }
    }
}