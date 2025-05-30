using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Project.Contracts.ModelDTO
{
    /// <summary>
    /// DTO маршрута изготовления согласно ТЗ
    /// </summary>
    public class RouteDto
    {
        public int Id { get; set; }

        [Required]
        public int DetailId { get; set; }
        public string DetailName { get; set; } = string.Empty;
        public string DetailNumber { get; set; } = string.Empty;

        /// <summary>
        /// Последовательность операций для детали
        /// </summary>
        public List<RouteStageDto> Stages { get; set; } = new();

        /// <summary>
        /// Общее нормативное время изготовления одной детали (часы)
        /// </summary>
        public double TotalNormTimeHours { get; set; }

        /// <summary>
        /// Общее время переналадок при смене деталей (часы)
        /// </summary>
        public double TotalSetupTimeHours { get; set; }

        /// <summary>
        /// Количество операций в маршруте
        /// </summary>
        public int StageCount { get; set; }

        /// <summary>
        /// Можно ли редактировать маршрут (нет активных производственных заданий)
        /// </summary>
        public bool CanEdit { get; set; }

        /// <summary>
        /// Можно ли удалить маршрут
        /// </summary>
        public bool CanDelete { get; set; }

        /// <summary>
        /// Дата создания маршрута
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Дата последнего изменения
        /// </summary>
        public DateTime? LastModifiedUtc { get; set; }
    }

    public class RouteCreateDto
    {
        [Required(ErrorMessage = "Выберите деталь")]
        public int DetailId { get; set; }

        [Required(ErrorMessage = "Добавьте хотя бы один этап")]
        [MinLength(1, ErrorMessage = "Маршрут должен содержать хотя бы один этап")]
        public List<RouteStageCreateDto> Stages { get; set; } = new();
    }

    public class RouteEditDto
    {
        [Required]
        public int Id { get; set; }

        [Required]
        public int DetailId { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "Маршрут должен содержать хотя бы один этап")]
        public List<RouteStageEditDto> Stages { get; set; } = new();
    }

    /// <summary>
    /// DTO для копирования маршрута
    /// </summary>
    public class RouteCopyDto
    {
        [Required]
        public int SourceRouteId { get; set; }

        [Required]
        public int TargetDetailId { get; set; }

        /// <summary>
        /// Копировать времена переналадки
        /// </summary>
        public bool CopySetupTimes { get; set; } = true;
    }
}