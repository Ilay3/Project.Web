using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Project.Contracts.ModelDTO
{
    public class SubBatchDto
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
        public List<StageExecutionDto> StageExecutions { get; set; } = new List<StageExecutionDto>();
    }

    public class SubBatchCreateDto
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Количество должно быть больше 0")]
        public int Quantity { get; set; }
    }

    public class SubBatchEditDto : SubBatchCreateDto
    {
        [Required]
        public int Id { get; set; }
    }
}