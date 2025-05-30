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

        // === ПЛАНИРОВАНИЕ ===
        public int? QueuePosition { get; set; }
        public DateTime? ScheduledStartTimeUtc { get; set; }
        public int Priority { get; set; } = 0;

        // === ОПЕРАЦИОННЫЕ ДАННЫЕ ===
        public string? OperatorId { get; set; }
        public string? ReasonNote { get; set; }
        public string? DeviceId { get; set; }

        // === СИСТЕМНЫЕ ПОЛЯ ===
        public bool IsProcessedByScheduler { get; set; } = false;
        public DateTime? StatusChangedTimeUtc { get; set; }
        public int? StartAttempts { get; set; } = 0;
        public string? LastErrorMessage { get; set; }

        // === ДОПОЛНИТЕЛЬНЫЕ ПОЛЯ ИЗ ТЗ ===
        // Время создания этапа
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        // Плановое время начала (для соблюдения последовательности)
        public DateTime? PlannedStartTimeUtc { get; set; }

        // Время последнего обновления
        public DateTime? LastUpdatedUtc { get; set; }

        // ID связанного этапа переналадки (если это основной этап)
        public int? SetupStageId { get; set; }

        // ID основного этапа (если это переналадка)
        public int? MainStageId { get; set; }

        // Признак критичности этапа (не прерывать вне рабочего времени)
        public bool IsCritical { get; set; } = false;

        // Процент выполнения (для этапов в работе)
        public decimal? CompletionPercentage { get; set; }

        // === ВЫЧИСЛЯЕМЫЕ СВОЙСТВА ===

        /// <summary>
        /// Фактическое время работы (с учетом пауз)
        /// </summary>
        public TimeSpan? ActualWorkingTime => CalculateActualWorkingTime();

        /// <summary>
        /// Плановая продолжительность этапа
        /// </summary>
        public TimeSpan PlannedDuration
        {
            get
            {
                if (RouteStage == null) return TimeSpan.Zero;

                return IsSetup
                    ? TimeSpan.FromHours(RouteStage.SetupTime)
                    : TimeSpan.FromHours(RouteStage.NormTime * (SubBatch?.Quantity ?? 1));
            }
        }

        /// <summary>
        /// Отклонение от планового времени
        /// </summary>
        public TimeSpan? TimeDeviation
        {
            get
            {
                var actualTime = ActualWorkingTime;
                if (!actualTime.HasValue) return null;

                return actualTime.Value - PlannedDuration;
            }
        }

        /// <summary>
        /// Процент выполнения относительно планового времени
        /// </summary>
        public decimal? ProgressPercentage
        {
            get
            {
                var actualTime = ActualWorkingTime;
                if (!actualTime.HasValue) return null;

                var plannedHours = PlannedDuration.TotalHours;
                if (plannedHours == 0) return 100;

                return (decimal)Math.Min(100, (actualTime.Value.TotalHours / plannedHours) * 100);
            }
        }

        /// <summary>
        /// Просрочен ли этап
        /// </summary>
        public bool IsOverdue
        {
            get
            {
                if (!StartTimeUtc.HasValue || Status == StageExecutionStatus.Completed)
                    return false;

                var elapsedTime = DateTime.UtcNow - StartTimeUtc.Value;
                return elapsedTime > PlannedDuration.Add(TimeSpan.FromHours(2)); // Просрочка больше 2 часов как в ТЗ
            }
        }

        /// <summary>
        /// Можно ли запустить этап сейчас
        /// </summary>
        public bool CanStart => Status == StageExecutionStatus.Pending && MachineId.HasValue;

        /// <summary>
        /// Время до планового начала
        /// </summary>
        public TimeSpan? TimeToStart
        {
            get
            {
                if (!PlannedStartTimeUtc.HasValue) return null;
                var timeToStart = PlannedStartTimeUtc.Value - DateTime.UtcNow;
                return timeToStart.TotalSeconds > 0 ? timeToStart : TimeSpan.Zero;
            }
        }

        private TimeSpan? CalculateActualWorkingTime()
        {
            if (!StartTimeUtc.HasValue) return null;

            DateTime endTime = EndTimeUtc ?? DateTime.UtcNow;

            // Базовое время работы
            var totalTime = endTime - StartTimeUtc.Value;

            // Вычитаем время пауз
            if (PauseTimeUtc.HasValue && ResumeTimeUtc.HasValue)
            {
                var pauseDuration = ResumeTimeUtc.Value - PauseTimeUtc.Value;
                totalTime -= pauseDuration;
            }
            else if (PauseTimeUtc.HasValue && Status == StageExecutionStatus.Paused)
            {
                // Этап сейчас на паузе
                var pauseDuration = DateTime.UtcNow - PauseTimeUtc.Value;
                totalTime -= pauseDuration;
            }

            return totalTime.TotalSeconds > 0 ? totalTime : TimeSpan.Zero;
        }

        /// <summary>
        /// Обновляет время последнего изменения
        /// </summary>
        public void UpdateLastModified()
        {
            LastUpdatedUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Проверяет возможность перехода в новый статус
        /// </summary>
        public bool CanTransitionTo(StageExecutionStatus newStatus)
        {
            var allowedTransitions = new Dictionary<StageExecutionStatus, List<StageExecutionStatus>>
            {
                [StageExecutionStatus.Pending] = new() {
                    StageExecutionStatus.InProgress,
                    StageExecutionStatus.Waiting,
                    StageExecutionStatus.Error
                },
                [StageExecutionStatus.Waiting] = new() {
                    StageExecutionStatus.Pending,
                    StageExecutionStatus.Error
                },
                [StageExecutionStatus.InProgress] = new() {
                    StageExecutionStatus.Paused,
                    StageExecutionStatus.Completed,
                    StageExecutionStatus.Error
                },
                [StageExecutionStatus.Paused] = new() {
                    StageExecutionStatus.InProgress,
                    StageExecutionStatus.Completed,
                    StageExecutionStatus.Error
                },
                [StageExecutionStatus.Completed] = new() { }, // Завершенный этап нельзя изменить
                [StageExecutionStatus.Error] = new() {
                    StageExecutionStatus.Pending
                } // Можно перезапустить
            };

            return allowedTransitions.ContainsKey(Status) &&
                   allowedTransitions[Status].Contains(newStatus);
        }
    }

    public enum StageExecutionStatus
    {
        Pending,    // Ожидание (готов к запуску)
        InProgress, // Выполняется
        Paused,     // На паузе
        Completed,  // Завершено
        Waiting,    // В очереди
        Error       // Ошибка/отменено
    }
}