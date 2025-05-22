using System;
using System.ComponentModel.DataAnnotations;

namespace Project.Domain.Entities
{
    /// <summary>
    /// События, происходящие с этапами выполнения
    /// </summary>
    public class StageEvent
    {
        public int Id { get; set; }

        public int StageExecutionId { get; set; }
        public StageExecution StageExecution { get; set; }

        /// <summary>
        /// Тип события
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string EventType { get; set; } // Created, Started, Paused, Resumed, Completed, Cancelled, Reassigned

        /// <summary>
        /// Предыдущий статус этапа
        /// </summary>
        [MaxLength(50)]
        public string PreviousStatus { get; set; }

        /// <summary>
        /// Новый статус этапа
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string NewStatus { get; set; }

        /// <summary>
        /// Время события (UTC)
        /// </summary>
        public DateTime EventTimeUtc { get; set; }

        /// <summary>
        /// ID оператора, инициировавшего событие
        /// </summary>
        [MaxLength(100)]
        public string OperatorId { get; set; }

        /// <summary>
        /// Имя оператора
        /// </summary>
        [MaxLength(200)]
        public string OperatorName { get; set; }

        /// <summary>
        /// ID устройства, с которого произошло событие
        /// </summary>
        [MaxLength(100)]
        public string DeviceId { get; set; }

        /// <summary>
        /// Комментарий к событию
        /// </summary>
        [MaxLength(1000)]
        public string Comment { get; set; }

        /// <summary>
        /// Дополнительные данные в JSON формате
        /// </summary>
        public string AdditionalData { get; set; }

        /// <summary>
        /// Автоматическое событие или инициированное пользователем
        /// </summary>
        public bool IsAutomatic { get; set; }

        /// <summary>
        /// ID предыдущего станка (для событий переназначения)
        /// </summary>
        public int? PreviousMachineId { get; set; }
        public Machine PreviousMachine { get; set; }

        /// <summary>
        /// ID нового станка (для событий переназначения)
        /// </summary>
        public int? NewMachineId { get; set; }
        public Machine NewMachine { get; set; }

        /// <summary>
        /// Длительность в предыдущем состоянии (для расчета статистики)
        /// </summary>
        public TimeSpan? DurationInPreviousState { get; set; }

        /// <summary>
        /// IP адрес инициатора события
        /// </summary>
        [MaxLength(45)]
        public string IpAddress { get; set; }

        /// <summary>
        /// User Agent браузера/приложения
        /// </summary>
        [MaxLength(500)]
        public string UserAgent { get; set; }
    }

    /// <summary>
    /// Типы событий этапов
    /// </summary>
    public static class StageEventTypes
    {
        public const string Created = "Created";
        public const string Assigned = "Assigned";
        public const string Started = "Started";
        public const string Paused = "Paused";
        public const string Resumed = "Resumed";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";
        public const string Reassigned = "Reassigned";
        public const string Failed = "Failed";
        public const string Prioritized = "Prioritized";
        public const string QueuePositionChanged = "QueuePositionChanged";
        public const string SetupRequired = "SetupRequired";
        public const string SetupCompleted = "SetupCompleted";
        public const string CommentAdded = "CommentAdded";
        public const string DurationEstimated = "DurationEstimated";
        public const string DelayReported = "DelayReported";
    }
}