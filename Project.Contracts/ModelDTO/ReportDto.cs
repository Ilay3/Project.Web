using Project.Contracts.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Project.Contracts.ModelDTO
{
    /// <summary>
    /// DTO календарного отчета по станкам согласно ТЗ
    /// </summary>
    public class MachineCalendarReportDto
    {
        public int MachineId { get; set; }
        public string MachineName { get; set; } = string.Empty;
        public string MachineTypeName { get; set; } = string.Empty;

        /// <summary>
        /// Период отчета
        /// </summary>
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Время работы станка по дням (часы)
        /// </summary>
        public Dictionary<DateTime, double> DailyWorkingHours { get; set; } = new();

        /// <summary>
        /// Время переналадок по дням (часы)
        /// </summary>
        public Dictionary<DateTime, double> DailySetupHours { get; set; } = new();

        /// <summary>
        /// Время простоев по дням (часы)
        /// </summary>
        public Dictionary<DateTime, double> DailyIdleHours { get; set; } = new();

        /// <summary>
        /// Список изготовленных деталей по дням
        /// </summary>
        public Dictionary<DateTime, List<ManufacturedPartDto>> DailyManufacturedParts { get; set; } = new();

        /// <summary>
        /// Коэффициент использования станка по дням (%)
        /// </summary>
        public Dictionary<DateTime, decimal> DailyUtilization { get; set; } = new();

        /// <summary>
        /// Общие показатели за период
        /// </summary>
        public MachineStatisticsDto TotalStatistics { get; set; } = new();
    }

    /// <summary>
    /// DTO изготовленной детали
    /// </summary>
    public class ManufacturedPartDto
    {
        public string DetailName { get; set; } = string.Empty;
        public string DetailNumber { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public double ManufacturingTimeHours { get; set; }
        public DateTime CompletedUtc { get; set; }
        public int BatchId { get; set; }
    }

    /// <summary>
    /// DTO отчета по производительности согласно ТЗ
    /// </summary>
    public class ProductivityReportDto
    {
        /// <summary>
        /// Период отчета
        /// </summary>
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Сравнение планового и фактического времени по деталям
        /// </summary>
        public List<DetailPerformanceDto> DetailPerformance { get; set; } = new();

        /// <summary>
        /// Анализ отклонений по операциям
        /// </summary>
        public List<OperationDeviationDto> OperationDeviations { get; set; } = new();

        /// <summary>
        /// Статистика по загрузке станков
        /// </summary>
        public List<MachineUtilizationDto> MachineUtilization { get; set; } = new();

        /// <summary>
        /// Эффективность работы операторов
        /// </summary>
        public List<OperatorEfficiencyDto> OperatorEfficiency { get; set; } = new();

        /// <summary>
        /// Общие показатели производительности
        /// </summary>
        public OverallProductivityDto OverallProductivity { get; set; } = new();
    }

    public class DetailPerformanceDto
    {
        public int DetailId { get; set; }
        public string DetailName { get; set; } = string.Empty;
        public string DetailNumber { get; set; } = string.Empty;

        public int QuantityManufactured { get; set; }
        public double PlannedTimeHours { get; set; }
        public double ActualTimeHours { get; set; }
        public double DeviationHours { get; set; }
        public decimal EfficiencyPercentage { get; set; }

        public int CompletedBatches { get; set; }
        public double AverageTimePerUnit { get; set; }
    }

    public class OperationDeviationDto
    {
        public int RouteStageId { get; set; }
        public string OperationName { get; set; } = string.Empty;
        public string MachineTypeName { get; set; } = string.Empty;

        public int ExecutionCount { get; set; }
        public double AveragePlannedTime { get; set; }
        public double AverageActualTime { get; set; }
        public double AverageDeviation { get; set; }
        public decimal DeviationPercentage { get; set; }

        public int OverdueCount { get; set; }
        public double MaxDeviation { get; set; }
        public double MinDeviation { get; set; }
    }

    public class MachineUtilizationDto
    {
        public int MachineId { get; set; }
        public string MachineName { get; set; } = string.Empty;
        public string MachineTypeName { get; set; } = string.Empty;

        public double TotalAvailableHours { get; set; }
        public double WorkingHours { get; set; }
        public double SetupHours { get; set; }
        public double IdleHours { get; set; }
        public decimal UtilizationPercentage { get; set; }

        public int CompletedOperations { get; set; }
        public int SetupOperations { get; set; }
    }

    public class OperatorEfficiencyDto
    {
        public string OperatorId { get; set; } = string.Empty;
        public string OperatorName { get; set; } = string.Empty;

        public int OperationsCompleted { get; set; }
        public double TotalWorkingHours { get; set; }
        public double AverageOperationTime { get; set; }
        public decimal EfficiencyPercentage { get; set; }

        public int OperationsStarted { get; set; }
        public int OperationsPaused { get; set; }
        public DateTime LastActivity { get; set; }
    }

    public class OverallProductivityDto
    {
        public int TotalPartsManufactured { get; set; }
        public int TotalBatchesCompleted { get; set; }
        public double TotalManufacturingTime { get; set; }
        public double TotalSetupTime { get; set; }
        public decimal OverallEfficiency { get; set; }

        public double AverageTimePerPart { get; set; }
        public double AverageSetupTime { get; set; }
        public int TotalOverdueStages { get; set; }
        public decimal OnTimeDeliveryRate { get; set; }
    }

    /// <summary>
    /// DTO для фильтрации отчетов
    /// </summary>
    public class ReportFilterDto
    {
        [Required]
        public DateTime StartDate { get; set; } = DateTime.Today.AddDays(-30);

        [Required]
        public DateTime EndDate { get; set; } = DateTime.Today;

        public List<int>? MachineIds { get; set; }
        public List<int>? MachineTypeIds { get; set; }
        public List<int>? DetailIds { get; set; }
        public List<string>? OperatorIds { get; set; }

        public bool IncludeSetups { get; set; } = true;
        public bool IncludeOverdueOnly { get; set; } = false;

        /// <summary>
        /// Группировка данных
        /// </summary>
        public string GroupBy { get; set; } = "Day"; // Day, Week, Month

        /// <summary>
        /// Формат экспорта
        /// </summary>
        public string? ExportFormat { get; set; } // Excel, PDF, CSV
    }
}