using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Contracts.ModelDTO
{
    public class BatchDto
    {
        public int Id { get; set; }
        public int DetailId { get; set; }
        public string DetailName { get; set; }
        public int Quantity { get; set; }
        public DateTime CreatedUtc { get; set; }
        public List<SubBatchDto> SubBatches { get; set; }
    }

    public class BatchCreateDto
    {
        public int DetailId { get; set; }
        public int Quantity { get; set; }
        public List<SubBatchCreateDto> SubBatches { get; set; }
    }

    public class BatchEditDto
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
        public List<SubBatchEditDto> SubBatches { get; set; }
    }

}
