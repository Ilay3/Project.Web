using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Contracts.ModelDTO
{
    public class SubBatchDto
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
        public List<StageExecutionDto> StageExecutions { get; set; }
    }

    public class SubBatchCreateDto
    {
        public int Quantity { get; set; }
    }

    public class SubBatchEditDto : SubBatchCreateDto
    {
        public int Id { get; set; }
    }

}
