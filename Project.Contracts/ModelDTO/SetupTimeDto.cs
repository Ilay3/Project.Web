using System;
using System.ComponentModel.DataAnnotations;

namespace Project.Contracts.ModelDTO
{
    /// <summary>
    /// DTO времени переналадки согласно ТЗ
    /// </summary>
    public class SetupTimeDto
    {
        public int Id { get; set; }

        [Required]
        public int MachineId { get; set; }
        public string MachineName { get; set; } = string.Empty;

        [Required]
        public int FromDetailId { get; set; }
        public string FromDetailName { get; set; } = string.Empty;
        public string FromDetailNumber { get; set; } = string.Empty;

        [Required]
        public int ToDetailId { get; set; }
        public string ToDetailName { get; set; } = string.Empty;
        public string ToDetailNumber { get; set; } = string.Empty;

        /// <summary>
        /// Время переналадки в часах
        /// </summary>
        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Время переналадки не может быть отрицательным")]
        public double Time { get; set; }

        /// <summary>
        /// Описание операций переналадки
        /// </summary>
        public string? SetupDescription { get; set; }

        /// <summary>
        /// Требуемая квалификация для переналадки
        /// </summary>
        public string? RequiredSkills { get; set; }

        /// <summary>
        /// Необходимые инструменты
        /// </summary>
        public string? RequiredTools { get; set; }

        /// <summary>
        /// Дата создания записи
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Дата последнего использования (фактической переналадки)
        /// </summary>
        public DateTime? LastUsedUtc { get; set; }

        /// <summary>
        /// Количество использований
        /// </summary>
        public int UsageCount { get; set; }

        /// <summary>
        /// Среднее фактическое время переналадки (часы)
        /// </summary>
        public double? AverageActualTime { get; set; }
    }

    public class SetupTimeCreateDto
    {
        [Required(ErrorMessage = "Выберите станок")]
        public int MachineId { get; set; }

        [Required(ErrorMessage = "Выберите деталь 'откуда'")]
        public int FromDetailId { get; set; }

        [Required(ErrorMessage = "Выберите деталь 'куда'")]
        public int ToDetailId { get; set; }

        [Required(ErrorMessage = "Укажите время переналадки")]
        [Range(0, double.MaxValue, ErrorMessage = "Время переналадки не может быть отрицательным")]
        public double Time { get; set; }

        public string? SetupDescription { get; set; }
        public string? RequiredSkills { get; set; }
        public string? RequiredTools { get; set; }
    }

    public class SetupTimeEditDto : SetupTimeCreateDto
    {
        [Required]
        public int Id { get; set; }
    }

    /// <summary>
    /// DTO для информации о необходимости переналадки
    /// </summary>
    public class SetupInfoDto
    {
        /// <summary>
        /// Нужна ли переналадка
        /// </summary>
        public bool SetupNeeded { get; set; }

        /// <summary>
        /// С какой детали переналадка
        /// </summary>
        public int? FromDetailId { get; set; }
        public string? FromDetailName { get; set; }
        public string? FromDetailNumber { get; set; }

        /// <summary>
        /// На какую деталь переналадка
        /// </summary>
        public int ToDetailId { get; set; }
        public string ToDetailName { get; set; } = string.Empty;
        public string ToDetailNumber { get; set; } = string.Empty;

        /// <summary>
        /// Время переналадки (часы)
        /// </summary>
        public double SetupTime { get; set; }

        /// <summary>
        /// Описание операций переналадки
        /// </summary>
        public string? SetupDescription { get; set; }

        /// <summary>
        /// Требуемые навыки
        /// </summary>
        public string? RequiredSkills { get; set; }

        /// <summary>
        /// Необходимые инструменты
        /// </summary>
        public string? RequiredTools { get; set; }
    }

    /// <summary>
    /// DTO для массового импорта времен переналадки
    /// </summary>
    public class BulkSetupTimeImportDto
    {
        public List<SetupTimeCreateDto> SetupTimes { get; set; } = new();

        /// <summary>
        /// Перезаписать существующие записи
        /// </summary>
        public bool OverwriteExisting { get; set; } = false;
    }

    /// <summary>
    /// Результат массового импорта
    /// </summary>
    public class BulkSetupTimeResultDto
    {
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}