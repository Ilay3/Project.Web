using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class StageExecution
    {
        public int Id { get; set; }
        public int SubBatchId { get; set; }
        public SubBatch SubBatch { get; set; }
        public int RouteStageId { get; set; }
        public RouteStage RouteStage { get; set; }

        public int? MachineId { get; set; }
        public Machine? Machine { get; set; }

        public StageExecutionStatus Status { get; set; }
        public DateTime? StartTimeUtc { get; set; }
        public DateTime? EndTimeUtc { get; set; }
        public DateTime? PauseTimeUtc { get; set; }
        public DateTime? ResumeTimeUtc { get; set; }

        // true если это переналадка, иначе операция
        public bool IsSetup { get; set; }

        // Позиция в очереди, если этап ожидает
        public int? QueuePosition { get; set; }

        // Запланированное время начала (для этапов в очереди)
        public DateTime? ScheduledStartTimeUtc { get; set; }

        // Приоритет этапа (для управления очередью)
        public int Priority { get; set; } = 0; // 0 - обычный, больше - выше приоритет

        // Идентификатор оператора, выполняющего этап
        public string? OperatorId { get; set; }

        // Причина паузы или отмены
        public string? ReasonNote { get; set; }
    }

    public enum StageExecutionStatus
    {
        Pending,    // Ожидание (готов к запуску)
        InProgress, // Выполняется
        Paused,     // На паузе
        Completed,  // Завершено
        Waiting,    // В очереди
        Error       // Ошибка
    }
}