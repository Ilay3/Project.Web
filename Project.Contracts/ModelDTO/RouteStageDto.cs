using System;
using System.ComponentModel.DataAnnotations;

namespace Project.Contracts.ModelDTO
{
    /// <summary>
    /// DTO этапа маршрута согласно ТЗ
    /// </summary>
    public class RouteStageDto
    {
        public int Id { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Порядковый номер должен быть больше 0")]
        public int Order { get; set; }

        [Required(ErrorMessage = "Название этапа обязательно")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Выберите тип станка")]
        public int MachineTypeId { get; set; }
        public string MachineTypeName { get; set; } = string.Empty;

        /// <summary>
        /// Нормативное время выполнения операции на 1 деталь (часы)
        /// </summary>
        [Required]
        [Range(0.001, double.MaxValue, ErrorMessage = "Нормативное время должно быть больше 0")]
        public double NormTime { get; set; }

        /// <summary>
        /// Время переналадки станка при смене деталей (часы)
        /// </summary>
        [Range(0, double.MaxValue, ErrorMessage = "Время переналадки не может быть отрицательным")]
        public double SetupTime { get; set; }

        /// <summary>
        /// Тип этапа: "Operation" или "Setup"
        /// </summary>
        public string StageType { get; set; } = "Operation";

        /// <summary>
        /// Описание операции
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Требуемая квалификация оператора
        /// </summary>
        public string? RequiredSkills { get; set; }

        /// <summary>
        /// Необходимые инструменты/оснастка
        /// </summary>
        public string? RequiredTools { get; set; }

        /// <summary>
        /// Контрольные параметры качества
        /// </summary>
        public string? QualityParameters { get; set; }
    }

    public class RouteStageCreateDto
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int Order { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public int MachineTypeId { get; set; }

        [Required]
        [Range(0.001, double.MaxValue)]
        public double NormTime { get; set; }

        [Range(0, double.MaxValue)]
        public double SetupTime { get; set; }

        public string StageType { get; set; } = "Operation";
        public string? Description { get; set; }
        public string? RequiredSkills { get; set; }
        public string? RequiredTools { get; set; }
        public string? QualityParameters { get; set; }
    }

    public class RouteStageEditDto : RouteStageCreateDto
    {
        [Required]
        public int Id { get; set; }
    }
}