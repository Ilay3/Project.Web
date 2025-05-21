using System;
using System.Collections.Generic;

namespace Project.Web.ViewModels
{
    public class QueueViewModel
    {
        public List<QueueItemViewModel> QueueItems { get; set; } = new List<QueueItemViewModel>();
        public List<MachineViewModel> AvailableMachines { get; set; } = new List<MachineViewModel>();
    }

    public class QueueItemViewModel
    {
        public int StageExecutionId { get; set; }
        public string DetailName { get; set; }
        public string StageName { get; set; }
        public string Status { get; set; }
        public int ExpectedMachineId { get; set; }
        public string ExpectedMachineName { get; set; }
        public DateTime ExpectedStartTime { get; set; }
        public int Priority { get; set; }
    }
}