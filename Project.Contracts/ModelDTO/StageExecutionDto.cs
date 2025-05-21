using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Contracts.ModelDTO
{
    public class StageExecutionDto
    {
        public int Id { get; set; }
        public int RouteStageId { get; set; }
        public string StageName { get; set; }
        public int? MachineId { get; set; }
        public string? MachineName { get; set; }
        public string Status { get; set; }
        public DateTime? StartTimeUtc { get; set; }
        public DateTime? EndTimeUtc { get; set; }
        public bool IsSetup { get; set; }
    }

}
