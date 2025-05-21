using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Contracts.ModelDTO
{
    public class GanttStageDto
    {
        public int Id { get; set; }
        public int BatchId { get; set; }
        public int SubBatchId { get; set; }
        public string DetailName { get; set; }
        public string StageName { get; set; }
        public int? MachineId { get; set; }
        public string MachineName { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Status { get; set; }
        public bool IsSetup { get; set; }
        public TimeSpan PlannedDuration { get; set; }
    }
}
