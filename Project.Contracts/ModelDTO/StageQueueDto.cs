using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Contracts.ModelDTO
{
    // DTO для очереди этапов
    public class StageQueueDto
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
