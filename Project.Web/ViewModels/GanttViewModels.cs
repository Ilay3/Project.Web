using Project.Contracts.Enums;
using System.ComponentModel.DataAnnotations;

namespace Project.Web.ViewModels
{
    /// <summary>
    /// ViewModel для диаграммы Ганта согласно ТЗ
    /// </summary>
    public class GanttViewModel
    {
        public List<GanttMachineRowViewModel> MachineRows { get; set; } = new();
        public GanttFilterViewModel Filter { get; set; } = new();
        public GanttTimelineViewModel Timeline { get; set; } = new();
        public List<GanttTaskViewModel> Tasks { get; set; } = new();
    }

    public class GanttMachineRowViewModel
    {
        public int MachineId { get; set; }
        public string MachineName { get; set; } = string.Empty;
        public string MachineTypeName { get; set; } = string.Empty;
        public MachineStatus Status { get; set; }
        public string StatusDisplayName => GetStatusDisplayName();
        public string StatusCssClass => GetStatusCssClass();
        public decimal UtilizationPercentage { get; set; }
        public int QueueLength { get; set; }

        private string GetStatusDisplayName() => Status switch
        {
            MachineStatus.Free => "Свободен",
            MachineStatus.Busy => "Занят",
            MachineStatus.Setup => "Переналадка",
            MachineStatus.Broken => "Неисправен",
            _ => "Неизвестно"
        };

        private string GetStatusCssClass() => Status switch
        {
            MachineStatus.Free => "success",
            MachineStatus.Busy => "primary",
            MachineStatus.Setup => "warning",
            MachineStatus.Broken => "danger",
            _ => "secondary"
        };
    }

    public class GanttFilterViewModel
    {
        [Display(Name = "Дата начала")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Display(Name = "Дата окончания")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; } = DateTime.Today.AddDays(7);

        [Display(Name = "Станки")]
        public List<int> SelectedMachineIds { get; set; } = new();

        [Display(Name = "Типы станков")]
        public List<int> SelectedMachineTypeIds { get; set; } = new();

        [Display(Name = "Детали")]
        public List<int> SelectedDetailIds { get; set; } = new();

        [Display(Name = "Статусы")]
        public List<StageStatus> SelectedStatuses { get; set; } = new();

        [Display(Name = "Показывать только переналадки")]
        public bool ShowSetupsOnly { get; set; }

        [Display(Name = "Показывать только основные операции")]
        public bool ShowOperationsOnly { get; set; }

        [Display(Name = "Показывать только просроченные")]
        public bool ShowOverdueOnly { get; set; }

        [Display(Name = "Минимальный приоритет")]
        public Priority? MinPriority { get; set; }

        // Списки для выпадающих списков
        public List<SelectOptionViewModel> AvailableMachines { get; set; } = new();
        public List<SelectOptionViewModel> AvailableMachineTypes { get; set; } = new();
        public List<SelectOptionViewModel> AvailableDetails { get; set; } = new();
    }

    public class SelectOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class GanttTimelineViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public TimeSpan ViewDuration => EndDate - StartDate;
        public List<GanttTimeMarkViewModel> TimeMarks { get; set; } = new();
    }

    public class GanttTimeMarkViewModel
    {
        public DateTime Time { get; set; }
        public string DisplayText { get; set; } = string.Empty;
        public bool IsMajor { get; set; }
        public double PositionPercent { get; set; }
    }

    /// <summary>
    /// ViewModel для задачи на диаграмме Ганта
    /// </summary>
    public class GanttTaskViewModel
    {
        public int Id { get; set; }
        public int MachineId { get; set; }
        public int BatchId { get; set; }
        public int SubBatchId { get; set; }

        // Информация о задаче
        public string DetailName { get; set; } = string.Empty;
        public string DetailNumber { get; set; } = string.Empty;
        public string StageName { get; set; } = string.Empty;
        public string TaskTitle => IsSetup ? $"Переналадка: {DetailName}" : $"{DetailName} - {StageName}";

        // Временные рамки
        public DateTime? PlannedStartTime { get; set; }
        public DateTime? PlannedEndTime { get; set; }
        public DateTime? ActualStartTime { get; set; }
        public DateTime? ActualEndTime { get; set; }

        // Отображение времени
        public DateTime DisplayStartTime => ActualStartTime ?? PlannedStartTime ?? DateTime.Now;
        public DateTime DisplayEndTime => ActualEndTime ?? PlannedEndTime ?? DisplayStartTime.AddHours(1);
        public TimeSpan DisplayDuration => DisplayEndTime - DisplayStartTime;

        // Статус и внешний вид
        public StageStatus Status { get; set; }
        public bool IsSetup { get; set; }
        public Priority Priority { get; set; }
        public bool IsCritical { get; set; }
        public bool IsOverdue { get; set; }

        public string CssClass => GetCssClass();
        public string BackgroundColor => GetBackgroundColor();
        public string TooltipText => GetTooltipText();

        // Позиционирование на диаграмме
        public double LeftPositionPercent { get; set; }
        public double WidthPercent { get; set; }

        // Дополнительная информация
        public int Quantity { get; set; }
        public string? OperatorId { get; set; }
        public decimal? CompletionPercentage { get; set; }

        private string GetCssClass()
        {
            var classes = new List<string> { "gantt-task" };

            if (IsOverdue) classes.Add("gantt-task-overdue");
            if (IsSetup) classes.Add("gantt-task-setup");
            if (IsCritical) classes.Add("gantt-task-critical");

            classes.Add($"gantt-task-{Status.ToString().ToLower()}");
            classes.Add($"gantt-task-priority-{Priority.ToString().ToLower()}");

            return string.Join(" ", classes);
        }

        private string GetBackgroundColor()
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

        private string GetTooltipText()
        {
            var tooltip = TaskTitle;
            if (Quantity > 1) tooltip += $" ({Quantity} шт)";
            tooltip += $"\nСтатус: {GetStatusDisplayName()}";
            if (OperatorId != null) tooltip += $"\nОператор: {OperatorId}";
            if (PlannedStartTime.HasValue) tooltip += $"\nПлан: {PlannedStartTime:dd.MM HH:mm} - {PlannedEndTime:dd.MM HH:mm}";
            if (ActualStartTime.HasValue) tooltip += $"\nФакт: {ActualStartTime:dd.MM HH:mm}" + (ActualEndTime.HasValue ? $" - {ActualEndTime:dd.MM HH:mm}" : " - в работе");
            return tooltip;
        }

        private string GetStatusDisplayName() => Status switch
        {
            StageStatus.AwaitingStart => "Ожидает запуска",
            StageStatus.InQueue => "В очереди",
            StageStatus.InProgress => "Выполняется",
            StageStatus.Paused => "На паузе",
            StageStatus.Completed => "Завершено",
            StageStatus.Cancelled => "Отменено",
            _ => "Неизвестно"
        };
    }

    /// <summary>
    /// ViewModel для управления задачей в Ганте
    /// </summary>
    public class GanttTaskControlViewModel
    {
        public int StageExecutionId { get; set; }
        public string StageName { get; set; } = string.Empty;
        public string DetailName { get; set; } = string.Empty;
        public StageStatus Status { get; set; }
        public bool CanStart { get; set; }
        public bool CanPause { get; set; }
        public bool CanResume { get; set; }
        public bool CanComplete { get; set; }
        public bool CanCancel { get; set; }
        public bool CanReassign { get; set; }

        // Для переназначения
        public List<SelectOptionViewModel> AvailableMachines { get; set; } = new();
    }
}