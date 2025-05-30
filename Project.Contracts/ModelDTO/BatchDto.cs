using Project.Contracts.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Project.Contracts.ModelDTO
{
    /// <summary>
    /// DTO партии согласно ТЗ
    /// </summary>
    public class BatchDto
    {
        public int Id { get; set; }

        [Required]
        public int DetailId { get; set; }
        public string DetailName { get; set; } = string.Empty;
        public string DetailNumber { get; set; } = string.Empty;

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Количество должно быть больше 0")]
        public int Quantity { get; set; }

        /// <summary>
        /// Дата создания задания
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Приоритет партии
        /// </summary>
        public Priority Priority { get; set; } = Priority.Normal;

        /// <summary>
        /// Подпартии (возможность деления на подпартии согласно ТЗ)
        /// </summary>
        public List<SubBatchDto> SubBatches { get; set; } = new();

        /// <summary>
        /// Общее нормативное время изготовления (часы)
        /// </summary>
        public double TotalPlannedTimeHours { get; set; }

        /// <summary>
        /// Фактическое время изготовления (часы)
        /// </summary>
        public double? TotalActualTimeHours { get; set; }

        /// <summary>
        /// Процент завершения партии
        /// </summary>
        public decimal CompletionPercentage { get; set; }

        /// <summary>
        /// Прогнозируемое время завершения
        /// </summary>
        public DateTime? EstimatedCompletionTimeUtc { get; set; }

        /// <summary>
        /// Статистика по этапам
        /// </summary>
        public BatchStageStatisticsDto StageStatistics { get; set; } = new();

        /// <summary>
        /// Можно ли редактировать партию
        /// </summary>
        public bool CanEdit { get; set; }

        /// <summary>
        /// Можно ли удалить партию
        /// </summary>
        public bool CanDelete { get; set; }
    }

    public class BatchCreateDto
    {
        [Required(ErrorMessage = "Выберите деталь")]
        public int DetailId { get; set; }

        [Required(ErrorMessage = "Укажите количество")]
        [Range(1, int.MaxValue, ErrorMessage = "Количество должно быть больше 0")]
        public int Quantity { get; set; }

        /// <summary>
        /// Приоритет партии
        /// </summary>
        public Priority Priority { get; set; } = Priority.Normal;

        /// <summary>
        /// Подпартии (если не указаны, создается одна подпартия на весь объем)
        /// </summary>
        public List<SubBatchCreateDto>? SubBatches { get; set; }

        /// <summary>
        /// Автоматически запустить планирование после создания
        /// </summary>
        public bool AutoStartPlanning { get; set; } = true;
    }

    public class BatchEditDto
    {
        [Required]
        public int Id { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Количество должно быть больше 0")]
        public int Quantity { get; set; }

        public Priority Priority { get; set; }

        public List<SubBatchEditDto>? SubBatches { get; set; }
    }

    /// <summary>
    /// DTO для быстрого создания партии согласно ТЗ
    /// </summary>
    public class QuickBatchCreateDto
    {
        [Required]
        public int DetailId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        public Priority Priority { get; set; } = Priority.Normal;

        /// <summary>
        /// Автоматически запустить в производство
        /// </summary>
        public bool AutoStart { get; set; } = true;

        /// <summary>
        /// Разделить на подпартии по количеству штук
        /// </summary>
        public int? SplitByQuantity { get; set; }
    }

    /// <summary>
    /// Статистика по этапам партии
    /// </summary>
    public class BatchStageStatisticsDto
    {
        /// <summary>
        /// Общее количество этапов
        /// </summary>
        public int TotalStages { get; set; }

        /// <summary>
        /// Этапы ожидают запуска
        /// </summary>
        public int AwaitingStartStages { get; set; }

        /// <summary>
        /// Этапы в очереди
        /// </summary>
        public int InQueueStages { get; set; }

        /// <summary>
        /// Этапы в работе
        /// </summary>
        public int InProgressStages { get; set; }

        /// <summary>
        /// Этапы на паузе
        /// </summary>
        public int PausedStages { get; set; }

        /// <summary>
        /// Завершенные этапы
        /// </summary>
        public int CompletedStages { get; set; }

        /// <summary>
        /// Отмененные этапы
        /// </summary>
        public int CancelledStages { get; set; }

        /// <summary>
        /// Этапы переналадки
        /// </summary>
        public int SetupStages { get; set; }

        /// <summary>
        /// Просроченные этапы (более 2 часов сверх нормы)
        /// </summary>
        public int OverdueStages { get; set; }
    }
}