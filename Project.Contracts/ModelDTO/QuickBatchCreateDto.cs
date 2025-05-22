using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Contracts.ModelDTO
{
    public class QuickBatchCreateDto
    {
        public int DetailId { get; set; }
        public int Quantity { get; set; }
        public bool AutoStart { get; set; } = true;
        public List<SubBatchCreateDto> SubBatches { get; set; } = new List<SubBatchCreateDto>();
    }

}
