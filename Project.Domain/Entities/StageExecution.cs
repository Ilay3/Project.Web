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
        public bool IsProcessedByScheduler { get; set; }

        // Время последнего изменения статуса
        public DateTime? StatusChangedTimeUtc { get; set; }

        // Количество попыток запуска этапа
        public int StartAttempts { get; set; } = 0;

        // Последняя ошибка, если была
        public string? LastErrorMessage { get; set; }

        // Идентификатор последнего устройства, с которого управляли этапом
        public string? DeviceId { get; set; }

        // Фактическое время работы (с учетом пауз)
        public TimeSpan? ActualWorkingTime => CalculateActualWorkingTime();

        private TimeSpan? CalculateActualWorkingTime()
        {
            if (!StartTimeUtc.HasValue) return null;

            // Если этап завершен, используем время окончания
            if (Status == StageExecutionStatus.Completed && EndTimeUtc.HasValue)
            {
                // Если был паузы, учитываем их
                if (PauseTimeUtc.HasValue && ResumeTimeUtc.HasValue)
                {
                    // Общее время минус время паузы
                    return (EndTimeUtc.Value - StartTimeUtc.Value) - (ResumeTimeUtc.Value - PauseTimeUtc.Value);
                }

                // Если паузы не было, просто разница между началом и концом
                return EndTimeUtc.Value - StartTimeUtc.Value;
            }

            // Если этап в процессе, считаем текущее время
            if (Status == StageExecutionStatus.InProgress)
            {
                // Если был паузы, учитываем их
                if (PauseTimeUtc.HasValue && ResumeTimeUtc.HasValue)
                {
                    // Общее время минус время паузы
                    return (DateTime.UtcNow - StartTimeUtc.Value) - (ResumeTimeUtc.Value - PauseTimeUtc.Value);
                }

                // Если паузы не было, просто разница между началом и текущим временем
                return DateTime.UtcNow - StartTimeUtc.Value;
            }

            // Если этап на паузе, считаем до момента паузы
            if (Status == StageExecutionStatus.Paused && PauseTimeUtc.HasValue)
            {
                return PauseTimeUtc.Value - StartTimeUtc.Value;
            }

            return null;
        }

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