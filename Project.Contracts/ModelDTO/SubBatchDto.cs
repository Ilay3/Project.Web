using Project.Contracts.Enums;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Project.Contracts.ModelDTO
{
    /// <summary>
    /// DTO подпартии согласно ТЗ
    /// </summary>
    public class SubBatchDto
    {
        public int Id { get; set; }
        public int BatchId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Количество должно быть больше 0")]
        public int Quantity { get; set; }

        /// <summary>
        /// Этапы выполнения для данной подпартии
        /// </summary>
        public List<StageExecutionDto> StageExecutions { get; set; } = new();

        /// <summary>
        /// Процент завершения подпартии
        /// </summary>
        public decimal CompletionPercentage { get; set; }

        /// <summary>
        /// Текущий этап в работе
        /// </summary>
        public int? CurrentStageExecutionId { get; set; }
        public string? CurrentStageName { get; set; }

        /// <summary>
        /// Следующий этап к выполнению
        /// </summary>
        public int? NextStageExecutionId { get; set; }
        public string? NextStageName { get; set; }

        /// <summary>
        /// Плановое время завершения подпартии
        /// </summary>
        public DateTime? PlannedCompletionTimeUtc { get; set; }

        /// <summary>
        /// Прогнозируемое время завершения
        /// </summary>
        public DateTime? EstimatedCompletionTimeUtc { get; set; }

        /// <summary>
        /// Фактическое время завершения
        /// </summary>
        public DateTime? ActualCompletionTimeUtc { get; set; }

        /// <summary>
        /// Общее время изготовления (часы)
        /// </summary>
        public double? TotalManufacturingTimeHours { get; set; }
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