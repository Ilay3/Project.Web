using Project.Contracts.Enums;
using System;
using System.Collections.Generic;

namespace Project.Contracts.ModelDTO
{
    /// <summary>
    /// DTO для диаграммы Ганта согласно ТЗ
    /// </summary>
    public class GanttDto
    {
        public int Id { get; set; }
        public int BatchId { get; set; }
        public int SubBatchId { get; set; }

        /// <summary>
        /// Информация о детали
        /// </summary>
        public string DetailName { get; set; } = string.Empty;
        public string DetailNumber { get; set; } = string.Empty;

        /// <summary>
        /// Название этапа
        /// </summary>
        public string StageName { get; set; } = string.Empty;

        /// <summary>
        /// Информация о станке
        /// </summary>
        public int? MachineId { get; set; }
        public string? MachineName { get; set; }
        public string? MachineTypeName { get; set; }

        /// <summary>
        /// Временные рамки
        /// </summary>
        public DateTime? PlannedStartTime { get; set; }
        public DateTime? PlannedEndTime { get; set; }
        public DateTime? ActualStartTime { get; set; }
        public DateTime? ActualEndTime { get; set; }

        /// <summary>
        /// Статус этапа
        /// </summary>
        public StageStatus Status { get; set; }

        /// <summary>
        /// Является ли переналадкой
        /// </summary>
        public bool IsSetup { get; set; }

        /// <summary>
        /// Приоритет этапа
        /// </summary>
        public Priority Priority { get; set; }

        /// <summary>
        /// Критически важный этап
        /// </summary>
        public bool IsCritical { get; set; }

        /// <summary>
        /// Плановая продолжительность
        /// </summary>
        public TimeSpan PlannedDuration { get; set; }

        /// <summary>
        /// Фактическая продолжительность
        /// </summary>
        public TimeSpan? ActualDuration { get; set; }

        /// <summary>
        /// Позиция в очереди
        /// </summary>
        public int? QueuePosition { get; set; }

        /// <summary>
        /// Количество деталей
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Процент выполнения
        /// </summary>
        public decimal? CompletionPercentage { get; set; }

        /// <summary>
        /// Оператор
        /// </summary>
        public string? OperatorId { get; set; }

        /// <summary>
        /// Примечание
        /// </summary>
        public string? ReasonNote { get; set; }

        /// <summary>
        /// Просрочен ли этап
        /// </summary>
        public bool IsOverdue { get; set; }

        /// <summary>
        /// Цвет для отображения на диаграмме
        /// </summary>
        public string DisplayColor => GetDisplayColor();

        /// <summary>
        /// Описание для всплывающей подсказки
        /// </summary>
        public string Tooltip => GetTooltip();

        private string GetDisplayColor()
        {
            if (IsOverdue) return "#dc3545"; // красный

            return Status switch
            {
                StageStatus.AwaitingStart => "#6c757d", // серый
                StageStatus.InQueue => "#ffc107", // желтый
                StageStatus.InProgress => IsSetup ? "#17a2b8" : "#28a745", // голубой для переналадки, зеленый для работы
                StageStatus.Paused => "#fd7e14", // оранжевый
                StageStatus.Completed => "#6f42c1", // фиолетовый
                StageStatus.Cancelled => "#dc3545", // красный
                _ => "#6c757d"
            };
        }

        private string GetTooltip()
        {
            var tooltip = $"{DetailName} - {StageName}";
            if (IsSetup) tooltip = $"Переналадка: {tooltip}";
            if (MachineName != null) tooltip += $"\nСтанок: {MachineName}";
            if (Quantity > 1) tooltip += $"\nКоличество: {Quantity} шт";
            if (OperatorId != null) tooltip += $"\nОператор: {OperatorId}";
            return tooltip;
        }
    }

    /// <summary>
    /// DTO для машины в диаграмме Ганта
    /// </summary>
    public class GanttMachineDto
    {
        public int MachineId { get; set; }
        public string MachineName { get; set; } = string.Empty;
        public string MachineTypeName { get; set; } = string.Empty;
        public int Priority { get; set; }
        public MachineStatus Status { get; set; }

        /// <summary>
        /// Этапы, назначенные на данный станок
        /// </summary>
        public List<GanttDto> Stages { get; set; } = new();

        /// <summary>
        /// Загруженность станка (%)
        /// </summary>
        public decimal UtilizationPercentage { get; set; }

        /// <summary>
        /// Количество этапов в очереди
        /// </summary>
        public int QueueLength { get; set; }
    }

    /// <summary>
    /// DTO для фильтрации диаграммы Ганта
    /// </summary>
    public class GanttFilterDto
    {
        /// <summary>
        /// Период отображения
        /// </summary>
        public DateTime StartDate { get; set; } = DateTime.Today;
        public DateTime EndDate { get; set; } = DateTime.Today.AddDays(7);

        /// <summary>
        /// Фильтр по станкам
        /// </summary>
        public List<int>? MachineIds { get; set; }

        /// <summary>
        /// Фильтр по типам станков
        /// </summary>
        public List<int>? MachineTypeIds { get; set; }

        /// <summary>
        /// Фильтр по деталям
        /// </summary>
        public List<int>? DetailIds { get; set; }

        /// <summary>
        /// Фильтр по партиям
        /// </summary>
        public List<int>? BatchIds { get; set; }

        /// <summary>
        /// Фильтр по статусам
        /// </summary>
        public List<StageStatus>? Statuses { get; set; }

        /// <summary>
        /// Показывать только переналадки
        /// </summary>
        public bool? ShowSetupsOnly { get; set; }

        /// <summary>
        /// Показывать только основные операции
        /// </summary>
        public bool? ShowOperationsOnly { get; set; }

        /// <summary>
        /// Показывать только просроченные этапы
        /// </summary>
        public bool? ShowOverdueOnly { get; set; }

        /// <summary>
        /// Минимальный приоритет
        /// </summary>
        public Priority? MinPriority { get; set; }
    }
}