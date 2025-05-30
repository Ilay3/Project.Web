using System;
using System.ComponentModel.DataAnnotations;

namespace Project.Contracts.ModelDTO
{
    /// <summary>
    /// DTO детали согласно ТЗ
    /// </summary>
    public class DetailDto
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Номер детали обязателен")]
        public string Number { get; set; } = string.Empty;

        [Required(ErrorMessage = "Наименование детали обязательно")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Описание детали
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Материал
        /// </summary>
        public string? Material { get; set; }

        /// <summary>
        /// Единица измерения
        /// </summary>
        public string Unit { get; set; } = "шт";

        /// <summary>
        /// Вес детали (кг)
        /// </summary>
        public decimal? Weight { get; set; }

        /// <summary>
        /// Категория детали
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Есть ли маршрут изготовления
        /// </summary>
        public bool HasRoute { get; set; }

        /// <summary>
        /// Общее нормативное время изготовления (из маршрута)
        /// </summary>
        public double? TotalManufacturingTimeHours { get; set; }

        /// <summary>
        /// Количество операций в маршруте
        /// </summary>
        public int? RouteStageCount { get; set; }

        /// <summary>
        /// Дата создания
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Можно ли удалить деталь (нет активных заданий)
        /// </summary>
        public bool CanDelete { get; set; }

        /// <summary>
        /// Статистика использования
        /// </summary>
        public DetailUsageStatisticsDto? UsageStatistics { get; set; }
    }

    public class DetailCreateDto
    {
        [Required(ErrorMessage = "Номер детали обязателен")]
        public string Number { get; set; } = string.Empty;

        [Required(ErrorMessage = "Наименование детали обязательно")]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
        public string? Material { get; set; }
        public string Unit { get; set; } = "шт";

        [Range(0, double.MaxValue, ErrorMessage = "Вес не может быть отрицательным")]
        public decimal? Weight { get; set; }

        public string? Category { get; set; }
    }

    public class DetailEditDto : DetailCreateDto
    {
        [Required]
        public int Id { get; set; }
    }

    /// <summary>
    /// Статистика использования детали
    /// </summary>
    public class DetailUsageStatisticsDto
    {
        /// <summary>
        /// Общее количество изготовленных деталей
        /// </summary>
        public int TotalManufactured { get; set; }

        /// <summary>
        /// Количество активных партий
        /// </summary>
        public int ActiveBatches { get; set; }

        /// <summary>
        /// Дата последнего производства
        /// </summary>
        public DateTime? LastManufacturedDate { get; set; }

        /// <summary>
        /// Среднее время изготовления (часы)
        /// </summary>
        public double? AverageManufacturingTime { get; set; }

        /// <summary>
        /// Эффективность производства (%)
        /// </summary>
        public decimal? EfficiencyPercentage { get; set; }
    }
}