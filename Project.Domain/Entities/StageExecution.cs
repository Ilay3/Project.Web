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
    }
    public enum StageExecutionStatus
    {
        Pending,    // Ожидание
        InProgress, // Выполняется
        Paused,     // На паузе
        Completed,  // Завершено
        Waiting,    // В очереди
        Error       // Ошибка
    }

}
