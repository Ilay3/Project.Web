using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Contracts.ModelDTO
{
    public class PredictedScheduleDto
    {
        public int DetailId { get; set; }
        public int Quantity { get; set; }
        public DateTime EarliestStartTime { get; set; }
        public DateTime LatestEndTime { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public List<StageForecastDto> StageForecasts { get; set; } = new List<StageForecastDto>();
    }

    public class StageForecastDto
    {
        public int StageOrder { get; set; }
        public string StageName { get; set; }
        public int MachineTypeId { get; set; }
        public string MachineTypeName { get; set; }
        public int MachineId { get; set; }
        public string MachineName { get; set; }
        public DateTime ExpectedStartTime { get; set; }
        public DateTime ExpectedEndTime { get; set; }
        public bool NeedsSetup { get; set; }
    }

}
