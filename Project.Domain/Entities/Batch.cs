using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class Batch
    {
        public int Id { get; set; }
        public int DetailId { get; set; }
        public Detail Detail { get; set; }
        public int Quantity { get; set; }
        public DateTime CreatedUtc { get; set; } // Всегда UTC!
        public ICollection<SubBatch> SubBatches { get; set; }
    }

}
