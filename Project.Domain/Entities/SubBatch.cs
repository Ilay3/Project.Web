using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class SubBatch
    {
        public int Id { get; set; }
        public int BatchId { get; set; }
        public Batch Batch { get; set; }
        public int Quantity { get; set; }
        public ICollection<StageExecution> StageExecutions { get; set; }
    }

}
