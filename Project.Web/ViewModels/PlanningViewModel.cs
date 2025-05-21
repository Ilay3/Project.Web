using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Project.Web.ViewModels
{
    // Сводка по производственному плану
    public class PlanSummaryViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalBatches { get; set; }
        public int TotalQuantity { get; set; }
        public int CompletedQuantity { get; set; }
        public double CompletionPercent { get; set; }
        public List<DetailPlanStatsViewModel> DetailStats { get; set; } = new List<DetailPlanStatsViewModel>();
        public List<MachinePlanStatsViewModel> MachineStats { get; set; } = new List<MachinePlanStatsViewModel>();
    }

    public class DetailPlanStatsViewModel
    {
        public int DetailId { get; set; }
        public string DetailName { get; set; }
        public int PlannedQuantity { get; set; }
        public int CompletedQuantity { get; set; }
        public double CompletionPercent { get; set; }
    }

    public class MachinePlanStatsViewModel
    {
        public int MachineId { get; set; }
        public string MachineName { get; set; }
        public double TotalHours { get; set; }
        public double SetupHours { get; set; }
        public int StageCount { get; set; }
        public int CompletedStageCount { get; set; }
        public double EfficiencyPercent { get; set; }
        public double CompletionPercent { get; set; }
    }

    // Создание плана производства
    public class CreatePlanViewModel
    {
        [Required(ErrorMessage = "Введите название плана")]
        [Display(Name = "Название плана")]
        public string PlanName { get; set; }

        [Required(ErrorMessage = "Выберите дату выполнения")]
        [Display(Name = "Дата выполнения")]
        public DateTime TargetDate { get; set; } = DateTime.Today.AddDays(7);

        [Required(ErrorMessage = "Добавьте хотя бы одну позицию в план")]
        public List<PlanItemViewModel> Items { get; set; } = new List<PlanItemViewModel>();

        // Доступные детали для добавления в план
        public List<DetailViewModel> AvailableDetails { get; set; } = new List<DetailViewModel>();
    }

    public class PlanItemViewModel
    {
        [Required(ErrorMessage = "Выберите деталь")]
        public int DetailId { get; set; }

        [Required(ErrorMessage = "Введите количество")]
        [Range(1, int.MaxValue, ErrorMessage = "Количество должно быть больше 0")]
        public int Quantity { get; set; }

        [Display(Name = "Оптимальный размер партии")]
        public int OptimalBatchSize { get; set; }

        [Display(Name = "Размеры подпартий")]
        public List<int> SubBatchSizes { get; set; } = new List<int>();

        // Для отображения в интерфейсе
        public string DetailName { get; set; }
        public string DetailNumber { get; set; }
    }

    // Оптимальный размер партии
    public class OptimalBatchSizeViewModel
    {
        public int DetailId { get; set; }
        public string DetailName { get; set; }
        public int OptimalBatchSize { get; set; }
        public List<BatchSizeOptionViewModel> BatchSizeOptions { get; set; } = new List<BatchSizeOptionViewModel>();
    }

    public class BatchSizeOptionViewModel
    {
        public int BatchSize { get; set; }
        public double TotalTimeForBatch { get; set; }
        public double SetupTime { get; set; }
        public double ProcessingTime { get; set; }
        public double AvgTimePerUnit { get; set; }
    }
}