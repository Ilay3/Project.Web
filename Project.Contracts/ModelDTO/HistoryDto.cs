using Project.Contracts.Enums;
using System;
using System.Collections.Generic;

namespace Project.Contracts.ModelDTO
{
    /// <summary>
    /// DTO истории выполнения этапов согласно ТЗ
    /// </summary>
    public class HistoryDto
    {
        public int Id { get; set; }
        public int SubBatchId { get; set; }
        public int BatchId { get; set; }

        public string DetailName { get; set; } = string.Empty;
        public string DetailNumber { get; set; } = string.Empty;
        public string StageName { get; set; } = string.Empty;

        public int? MachineId { get; set; }
        public string? MachineName { get; set; }
        public string? MachineTypeName { get; set; }

        public StageStatus Status { get; set; }
        public bool IsSetup { get; set; }
        public Priority Priority { get; set; }

        /// <summary>
        /// Временные метки
        /// </summary>
        public DateTime? StartTimeUtc { get; set; }
        public DateTime? EndTimeUtc { get; set; }
        public DateTime? PauseTimeUtc { get; set; }
        public DateTime? ResumeTimeUtc { get; set; }
        public DateTime StatusChangedTimeUtc { get; set; }

        /// <summary>
        /// Операционная информация
        /// </summary>
        public string? OperatorId { get; set; }
        public string? ReasonNote { get; set; }
        public string? DeviceId { get; set; }

        /// <summary>
        /// Фактическая длительность (часы)
        /// </summary>
        public double? DurationHours { get; set; }

        /// <summary>
        /// Плановая длительность (часы)
        /// </summary>
        public double PlannedDurationHours { get; set; }

        /// <summary>
        /// Отклонение от плана (часы)
        /// </summary>
        public double? DeviationHours { get; set; }

        /// <summary>
        /// Количество деталей
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Процент выполнения
        /// </summary>
        public decimal? CompletionPercentage { get; set; }

        /// <summary>
        /// Просрочен ли этап
        /// </summary>
        public bool IsOverdue { get; set; }

        /// <summary>
        /// Время создания этапа
        /// </summary>
        public DateTime CreatedUtc { get; set; }
    }

    /// <summary>
    /// DTO для фильтрации истории
    /// </summary>
    public class StageHistoryFilterDto
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public int? MachineId { get; set; }
        public int? DetailId { get; set; }
        public int? BatchId { get; set; }
        public string? OperatorId { get; set; }

        public List<StageStatus>? Statuses { get; set; }

        public bool IncludeSetups { get; set; } = true;
        public bool? IsOverdueOnly { get; set; }

        /// <summary>
        /// Минимальная длительность (часы)
        /// </summary>
        public double? MinDurationHours { get; set; }

        /// <summary>
        /// Максимальная длительность (часы)
        /// </summary>
        public double? MaxDurationHours { get; set; }

        /// <summary>
        /// Сортировка
        /// </summary>
        public string SortBy { get; set; } = "StartTime"; // StartTime, Duration, Status
        public bool SortDescending { get; set; } = true;

        /// <summary>
        /// Пагинация
        /// </summary>
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    /// <summary>
    /// DTO статистики за период
    /// </summary>
    public class StageStatisticsDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Общее количество этапов
        /// </summary>
        public int TotalStages { get; set; }

        /// <summary>
        /// Завершенные этапы
        /// </summary>
        public int CompletedStages { get; set; }

        /// <summary>
        /// Этапы переналадки
        /// </summary>
        public int SetupStages { get; set; }

        /// <summary>
        /// Общее время работы (часы)
        /// </summary>
        public double TotalWorkHours { get; set; }

        /// <summary>
        /// Общее время переналадок (часы)
        /// </summary>
        public double TotalSetupHours { get; set; }

        /// <summary>
        /// Общее время простоев (часы)
        /// </summary>
        public double TotalIdleHours { get; set; }

        /// <summary>
        /// Процент эффективности (% времени работы от общего)
        /// </summary>
        public decimal EfficiencyPercentage { get; set; }

        /// <summary>
        /// Среднее время выполнения этапа (часы)
        /// </summary>
        public double AverageStageTime { get; set; }

        /// <summary>
        /// Среднее время переналадки (часы)
        /// </summary>
        public double AverageSetupTime { get; set; }

        /// <summary>
        /// Количество просроченных этапов
        /// </summary>
        public int OverdueStages { get; set; }

        /// <summary>
        /// Процент выполнения в срок
        /// </summary>
        public decimal OnTimePercentage { get; set; }

        /// <summary>
        /// Статистика по станкам
        /// </summary>
        public List<MachineStatisticsDto> MachineStatistics { get; set; } = new();

        /// <summary>
        /// Статистика по операторам
        /// </summary>
        public List<OperatorStatisticsDto> OperatorStatistics { get; set; } = new();
    }

    public class OperatorStatisticsDto
    {
        public string OperatorId { get; set; } = string.Empty;
        public string OperatorName { get; set; } = string.Empty;

        public int StagesCompleted { get; set; }
        public double TotalWorkingHours { get; set; }
        public decimal EfficiencyPercentage { get; set; }

        public int StagesStarted { get; set; }
        public int StagesPaused { get; set; }
        public int OverdueStages { get; set; }

        public DateTime? LastActivity { get; set; }
        public double AverageStageTime { get; set; }
    }
}