using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Project.Contracts.ModelDTO
{
    public class BatchDto
    {
        public int Id { get; set; }
        public int DetailId { get; set; }
        public string DetailName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public DateTime CreatedUtc { get; set; }
        public List<SubBatchDto> SubBatches { get; set; } = new List<SubBatchDto>();
    }

    public class BatchCreateDto
    {
        [Required]
        public int DetailId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Количество должно быть больше 0")]
        public int Quantity { get; set; }

        public List<SubBatchCreateDto>? SubBatches { get; set; }
    }

    public class BatchEditDto
    {
        [Required]
        public int Id { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Количество должно быть больше 0")]
        public int Quantity { get; set; }

        public List<SubBatchEditDto>? SubBatches { get; set; }
    }
}