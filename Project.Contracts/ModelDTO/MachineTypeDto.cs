using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Project.Contracts.ModelDTO
{
    /// <summary>
    /// DTO типа станка согласно ТЗ
    /// </summary>
    public class MachineTypeDto
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Название типа станка обязательно")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Описание типа станка
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Категория станков
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Количество станков данного типа
        /// </summary>
        public int MachineCount { get; set; }

        /// <summary>
        /// Количество активных станков
        /// </summary>
        public int ActiveMachineCount { get; set; }

        /// <summary>
        /// Средний приоритет станков данного типа
        /// </summary>
        public decimal AveragePriority { get; set; }

        /// <summary>
        /// Операции, которые можно выполнять на станках данного типа
        /// </summary>
        public List<string> SupportedOperations { get; set; } = new();

        /// <summary>
        /// Статистика использования
        /// </summary>
        public MachineTypeUsageStatisticsDto? UsageStatistics { get; set; }

        /// <summary>
        /// Можно ли удалить тип станка
        /// </summary>
        public bool CanDelete { get; set; }

        /// <summary>
        /// Дата создания
        /// </summary>
        public DateTime CreatedUtc { get; set; }
    }

    public class MachineTypeCreateDto
    {
        [Required(ErrorMessage = "Название типа станка обязательно")]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
        public string? Category { get; set; }
        public List<string>? SupportedOperations { get; set; }
    }

    public class MachineTypeEditDto : MachineTypeCreateDto
    {
        [Required]
        public int Id { get; set; }
    }

    /// <summary>
    /// Статистика использования типа станка
    /// </summary>
    public class MachineTypeUsageStatisticsDto
    {
        /// <summary>
        /// Общее время работы всех станков данного типа (часы)
        /// </summary>
        public double TotalWorkingHours { get; set; }

        /// <summary>
        /// Среднее время загрузки (%)
        /// </summary>
        public decimal AverageUtilization { get; set; }

        /// <summary>
        /// Количество выполненных операций
        /// </summary>
        public int CompletedOperations { get; set; }

        /// <summary>
        /// Среднее время переналадки (часы)
        /// </summary>
        public double AverageSetupTime { get; set; }

        /// <summary>
        /// Количество деталей в очереди
        /// </summary>
        public int QueuedParts { get; set; }
    }
}