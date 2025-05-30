using Project.Contracts.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace Project.Contracts.ModelDTO
{
    /// <summary>
    /// DTO станка согласно ТЗ
    /// </summary>
    public class MachineDto
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Название станка обязательно")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Инвентарный номер обязателен")]
        public string InventoryNumber { get; set; } = string.Empty;

        public int MachineTypeId { get; set; }
        public string MachineTypeName { get; set; } = string.Empty;

        /// <summary>
        /// Приоритет использования станка (согласно ТЗ)
        /// </summary>
        [Range(0, 10, ErrorMessage = "Приоритет должен быть от 0 до 10")]
        public int Priority { get; set; }

        /// <summary>
        /// Текущий статус станка (согласно ТЗ)
        /// </summary>
        public MachineStatus Status { get; set; } = MachineStatus.Free;

        /// <summary>
        /// Текущий этап, выполняемый на станке
        /// </summary>
        public int? CurrentStageExecutionId { get; set; }
        public string? CurrentStageDescription { get; set; }

        /// <summary>
        /// Последняя деталь, обработанная на станке (для переналадки)
        /// </summary>
        public int? LastDetailId { get; set; }
        public string? LastDetailName { get; set; }

        /// <summary>
        /// Время последнего обновления статуса
        /// </summary>
        public DateTime LastStatusUpdateUtc { get; set; }

        /// <summary>
        /// Загруженность станка (процент времени в работе за текущий день)
        /// </summary>
        public decimal? TodayUtilizationPercent { get; set; }

        /// <summary>
        /// Время до освобождения (если занят)
        /// </summary>
        public TimeSpan? TimeToFree { get; set; }

        /// <summary>
        /// Количество этапов в очереди на данный станок
        /// </summary>
        public int QueueLength { get; set; }
    }

    public class MachineCreateDto
    {
        [Required(ErrorMessage = "Название станка обязательно")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Инвентарный номер обязателен")]
        public string InventoryNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Тип станка обязателен")]
        public int MachineTypeId { get; set; }

        [Range(0, 10, ErrorMessage = "Приоритет должен быть от 0 до 10")]
        public int Priority { get; set; } = 5;
    }

    public class MachineEditDto : MachineCreateDto
    {
        [Required]
        public int Id { get; set; }
    }

    /// <summary>
    /// DTO для обновления статуса станка
    /// </summary>
    public class MachineStatusUpdateDto
    {
        [Required]
        public int MachineId { get; set; }

        [Required]
        public MachineStatus NewStatus { get; set; }

        public string? ReasonNote { get; set; }
        public string? OperatorId { get; set; }
    }

    /// <summary>
    /// DTO для статистики станка
    /// </summary>
    public class MachineStatisticsDto
    {
        public int MachineId { get; set; }
        public string MachineName { get; set; } = string.Empty;
        public string MachineTypeName { get; set; } = string.Empty;

        /// <summary>
        /// Время работы за период (часы)
        /// </summary>
        public double WorkingHours { get; set; }

        /// <summary>
        /// Время переналадок за период (часы)
        /// </summary>
        public double SetupHours { get; set; }

        /// <summary>
        /// Время простоев за период (часы)
        /// </summary>
        public double IdleHours { get; set; }

        /// <summary>
        /// Коэффициент использования станка (%)
        /// </summary>
        public decimal UtilizationPercentage { get; set; }

        /// <summary>
        /// Количество изготовленных деталей
        /// </summary>
        public int PartsMade { get; set; }

        /// <summary>
        /// Список изготовленных деталей
        /// </summary>
        public List<string> PartsManufactured { get; set; } = new();

        /// <summary>
        /// Количество переналадок
        /// </summary>
        public int SetupCount { get; set; }

        /// <summary>
        /// Среднее время переналадки (часы)
        /// </summary>
        public double AverageSetupTime { get; set; }
    }
}