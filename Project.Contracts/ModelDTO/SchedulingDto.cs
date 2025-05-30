using Project.Contracts.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Project.Contracts.ModelDTO
{
    /// <summary>
    /// DTO для прогноза расписания согласно ТЗ
    /// </summary>
    public class PredictedScheduleDto
    {
        public int DetailId { get; set; }
        public string DetailName { get; set; } = string.Empty;
        public int Quantity { get; set; }

        /// <summary>
        /// Самое раннее время начала производства
        /// </summary>
        public DateTime EarliestStartTime { get; set; }

        /// <summary>
        /// Самое позднее время окончания производства
        /// </summary>
        public DateTime LatestEndTime { get; set; }

        /// <summary>
        /// Общая продолжительность производства
        /// </summary>
        public TimeSpan TotalDuration { get; set; }

        /// <summary>
        /// Прогноз по каждому этапу
        /// </summary>
        public List<StageForecastDto> StageForecasts { get; set; } = new();

        /// <summary>
        /// Общее количество переналадок
        /// </summary>
        public int TotalSetups { get; set; }

        /// <summary>
        /// Общее время переналадок (часы)
        /// </summary>
        public double TotalSetupTimeHours { get; set; }

        /// <summary>
        /// Уровень загруженности производства (%)
        /// </summary>
        public decimal OverallUtilization { get; set; }

        /// <summary>
        /// Критический путь производства
        /// </summary>
        public List<int> CriticalPath { get; set; } = new();
    }

    public class StageForecastDto
    {
        public int StageOrder { get; set; }
        public string StageName { get; set; } = string.Empty;

        public int MachineTypeId { get; set; }
        public string MachineTypeName { get; set; } = string.Empty;

        public int MachineId { get; set; }
        public string MachineName { get; set; } = string.Empty;

        /// <summary>
        /// Прогнозируемое время начала
        /// </summary>
        public DateTime ExpectedStartTime { get; set; }

        /// <summary>
        /// Прогнозируемое время окончания
        /// </summary>
        public DateTime ExpectedEndTime { get; set; }

        /// <summary>
        /// Нужна ли переналадка
        /// </summary>
        public bool NeedsSetup { get; set; }

        /// <summary>
        /// Время переналадки (часы)
        /// </summary>
        public double SetupTimeHours { get; set; }

        /// <summary>
        /// Время операции (часы)
        /// </summary>
        public double OperationTimeHours { get; set; }

        /// <summary>
        /// Время ожидания в очереди (часы)
        /// </summary>
        public double QueueTimeHours { get; set; }

        /// <summary>
        /// Уровень загруженности станка (%)
        /// </summary>
        public decimal MachineUtilization { get; set; }

        /// <summary>
        /// Альтернативные станки
        /// </summary>
        public List<AlternativeMachineDto> AlternativeMachines { get; set; } = new();
    }

    public class AlternativeMachineDto
    {
        public int MachineId { get; set; }
        public string MachineName { get; set; } = string.Empty;
        public DateTime AvailableFrom { get; set; }
        public DateTime EstimatedCompletion { get; set; }
        public bool RequiresSetup { get; set; }
        public double SetupTimeHours { get; set; }
        public decimal Utilization { get; set; }
        public int Priority { get; set; }
    }

    /// <summary>
    /// DTO для очереди этапов
    /// </summary>
    public class StageQueueDto
    {
        public int StageExecutionId { get; set; }
        public int SubBatchId { get; set; }
        public int BatchId { get; set; }

        public string DetailName { get; set; } = string.Empty;
        public string DetailNumber { get; set; } = string.Empty;
        public string StageName { get; set; } = string.Empty;

        public StageStatus Status { get; set; }
        public Priority Priority { get; set; }
        public bool IsCritical { get; set; }

        /// <summary>
        /// Ожидаемый станок
        /// </summary>
        public int ExpectedMachineId { get; set; }
        public string ExpectedMachineName { get; set; } = string.Empty;

        /// <summary>
        /// Ожидаемое время начала
        /// </summary>
        public DateTime ExpectedStartTime { get; set; }

        /// <summary>
        /// Ожидаемое время окончания
        /// </summary>
        public DateTime ExpectedEndTime { get; set; }

        /// <summary>
        /// Позиция в очереди
        /// </summary>
        public int QueuePosition { get; set; }

        /// <summary>
        /// Время создания задания
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Время ожидания в очереди
        /// </summary>
        public TimeSpan WaitingTime => DateTime.UtcNow - CreatedUtc;

        /// <summary>
        /// Количество деталей
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Нужна ли переналадка
        /// </summary>
        public bool RequiresSetup { get; set; }

        /// <summary>
        /// Время переналадки (часы)
        /// </summary>
        public double SetupTimeHours { get; set; }
    }

    /// <summary>
    /// DTO для оптимизации очереди
    /// </summary>
    public class QueueOptimizationDto
    {
        [Required]
        public int MachineId { get; set; }

        /// <summary>
        /// Новый порядок этапов в очереди
        /// </summary>
        public List<int> NewStageOrder { get; set; } = new();

        /// <summary>
        /// Причина оптимизации
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Оператор, выполняющий оптимизацию
        /// </summary>
        public string? OperatorId { get; set; }
    }

    /// <summary>
    /// DTO для запроса прогноза
    /// </summary>
    public class ScheduleForecastRequestDto
    {
        [Required]
        public int DetailId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        /// <summary>
        /// Желаемое время начала производства
        /// </summary>
        public DateTime? PreferredStartTime { get; set; }

        /// <summary>
        /// Крайний срок завершения
        /// </summary>
        public DateTime? Deadline { get; set; }

        /// <summary>
        /// Приоритет задания
        /// </summary>
        public Priority Priority { get; set; } = Priority.Normal;

        /// <summary>
        /// Разрешить разделение на подпартии
        /// </summary>
        public bool AllowSplitting { get; set; } = true;

        /// <summary>
        /// Максимальное количество подпартий
        /// </summary>
        public int? MaxSubBatches { get; set; }

        /// <summary>
        /// Предпочтительные станки (если не указаны, система выберет оптимальные)
        /// </summary>
        public List<int>? PreferredMachineIds { get; set; }
    }
}