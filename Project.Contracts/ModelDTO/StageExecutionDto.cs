using Project.Contracts.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace Project.Contracts.ModelDTO
{
    /// <summary>
    /// DTO этапа выполнения согласно ТЗ
    /// </summary>
    public class StageExecutionDto
    {
        public int Id { get; set; }
        public int SubBatchId { get; set; }
        public int RouteStageId { get; set; }
        public string StageName { get; set; } = string.Empty;
        public string StageType { get; set; } = string.Empty;

        /// <summary>
        /// ID назначенного станка
        /// </summary>
        public int? MachineId { get; set; }
        public string? MachineName { get; set; }

        /// <summary>
        /// Статус выполнения согласно ТЗ
        /// </summary>
        public StageStatus Status { get; set; }

        /// <summary>
        /// Является ли этап переналадкой
        /// </summary>
        public bool IsSetup { get; set; }

        /// <summary>
        /// Приоритет этапа
        /// </summary>
        public Priority Priority { get; set; } = Priority.Normal;

        /// <summary>
        /// Критически важный этап (не прерывается вне рабочего времени)
        /// </summary>
        public bool IsCritical { get; set; }

        /// <summary>
        /// Плановое время начала
        /// </summary>
        public DateTime? PlannedStartTimeUtc { get; set; }

        /// <summary>
        /// Плановое время окончания
        /// </summary>
        public DateTime? PlannedEndTimeUtc { get; set; }

        /// <summary>
        /// Фактическое время начала
        /// </summary>
        public DateTime? StartTimeUtc { get; set; }

        /// <summary>
        /// Фактическое время окончания
        /// </summary>
        public DateTime? EndTimeUtc { get; set; }

        /// <summary>
        /// Время постановки на паузу
        /// </summary>
        public DateTime? PauseTimeUtc { get; set; }

        /// <summary>
        /// Время возобновления после паузы
        /// </summary>
        public DateTime? ResumeTimeUtc { get; set; }

        /// <summary>
        /// Плановая продолжительность (часы)
        /// </summary>
        public double PlannedDurationHours { get; set; }

        /// <summary>
        /// Фактическая продолжительность (часы)
        /// </summary>
        public double? ActualDurationHours { get; set; }

        /// <summary>
        /// Отклонение от планового времени (часы)
        /// </summary>
        public double? DeviationHours { get; set; }

        /// <summary>
        /// Процент выполнения
        /// </summary>
        public decimal? CompletionPercentage { get; set; }

        /// <summary>
        /// Позиция в очереди (если статус InQueue)
        /// </summary>
        public int? QueuePosition { get; set; }

        /// <summary>
        /// Количество деталей в обработке
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Оператор, выполняющий этап
        /// </summary>
        public string? OperatorId { get; set; }

        /// <summary>
        /// Примечание/причина
        /// </summary>
        public string? ReasonNote { get; set; }

        /// <summary>
        /// Устройство/терминал
        /// </summary>
        public string? DeviceId { get; set; }

        /// <summary>
        /// Время создания этапа
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Время последнего изменения статуса
        /// </summary>
        public DateTime? StatusChangedTimeUtc { get; set; }

        /// <summary>
        /// Просрочен ли этап (более 2 часов сверх нормы согласно ТЗ)
        /// </summary>
        public bool IsOverdue { get; set; }

        /// <summary>
        /// Можно ли запустить этап сейчас
        /// </summary>
        public bool CanStart { get; set; }

        /// <summary>
        /// Связанная информация о партии
        /// </summary>
        public string DetailName { get; set; } = string.Empty;
        public int BatchId { get; set; }

        /// <summary>
        /// ID этапа переналадки (если это основной этап)
        /// </summary>
        public int? SetupStageId { get; set; }

        /// <summary>
        /// ID основного этапа (если это переналадка)
        /// </summary>
        public int? MainStageId { get; set; }
    }

    /// <summary>
    /// DTO для управления этапом выполнения
    /// </summary>
    public class StageExecutionControlDto
    {
        [Required]
        public int StageExecutionId { get; set; }

        public string? OperatorId { get; set; }
        public string? ReasonNote { get; set; }
        public string? DeviceId { get; set; }
    }

    /// <summary>
    /// DTO для назначения этапа на станок
    /// </summary>
    public class StageAssignmentDto
    {
        [Required]
        public int StageExecutionId { get; set; }

        [Required]
        public int MachineId { get; set; }

        public string? OperatorId { get; set; }
        public string? ReasonNote { get; set; }

        /// <summary>
        /// Принудительное назначение (игнорировать проверки совместимости)
        /// </summary>
        public bool ForceAssignment { get; set; }
    }

    /// <summary>
    /// DTO для изменения приоритета этапа
    /// </summary>
    public class StagePriorityUpdateDto
    {
        [Required]
        public int StageExecutionId { get; set; }

        [Required]
        public Priority NewPriority { get; set; }

        public bool IsCritical { get; set; }
        public string? ReasonNote { get; set; }
        public string? OperatorId { get; set; }
    }
}