using System;
using System.Collections.Generic;

namespace Project.Contracts.ModelDTO
{
    /// <summary>
    /// DTO для события этапа
    /// </summary>
    public class StageEventDto
    {
        public int Id { get; set; }
        public int StageExecutionId { get; set; }
        public string EventType { get; set; }
        public string EventTypeName { get; set; }
        public string PreviousStatus { get; set; }
        public string NewStatus { get; set; }
        public DateTime EventTime { get; set; }
        public string OperatorId { get; set; }
        public string OperatorName { get; set; }
        public string DeviceId { get; set; }
        public string Comment { get; set; }
        public bool IsAutomatic { get; set; }
        public int? PreviousMachineId { get; set; }
        public string PreviousMachineName { get; set; }
        public int? NewMachineId { get; set; }
        public string NewMachineName { get; set; }
        public TimeSpan? DurationInPreviousState { get; set; }
        public string StageName { get; set; }
        public string DetailName { get; set; }

        // Вычисляемые поля
        public string DurationFormatted =>
            DurationInPreviousState?.ToString(@"hh\:mm\:ss") ?? "-";

        public string EventDescription => GetEventDescription();

        private string GetEventDescription()
        {
            return EventType switch
            {
                "Created" => "Этап создан в системе",
                "Assigned" => $"Этап назначен на станок {NewMachineName}",
                "Started" => IsAutomatic ? "Этап автоматически запущен" : $"Этап запущен оператором {OperatorName}",
                "Paused" => $"Этап приостановлен: {Comment}",
                "Resumed" => $"Этап возобновлен оператором {OperatorName}",
                "Completed" => IsAutomatic ? "Этап автоматически завершен" : $"Этап завершен оператором {OperatorName}",
                "Cancelled" => $"Этап отменен: {Comment}",
                "Reassigned" => $"Этап переназначен с {PreviousMachineName} на {NewMachineName}",
                "Prioritized" => "Этап получил приоритет",
                _ => EventTypeName ?? EventType
            };
        }
    }

    /// <summary>
    /// DTO для системного события
    /// </summary>
    public class SystemEventDto
    {
        public int Id { get; set; }
        public string Category { get; set; }
        public string EventType { get; set; }
        public string Severity { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime EventTime { get; set; }
        public string Source { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string IpAddress { get; set; }
        public string RelatedEntityId { get; set; }
        public string RelatedEntityType { get; set; }
        public bool IsProcessed { get; set; }
        public DateTime? ProcessedTime { get; set; }
        public string ProcessedBy { get; set; }

        // Вычисляемые поля
        public string SeverityDisplayName => GetSeverityDisplayName();
        public string CategoryDisplayName => GetCategoryDisplayName();
        public string SeverityBadgeClass => GetSeverityBadgeClass();

        private string GetSeverityDisplayName()
        {
            return Severity switch
            {
                "Critical" => "Критическая",
                "Error" => "Ошибка",
                "Warning" => "Предупреждение",
                "Info" => "Информация",
                "Debug" => "Отладка",
                _ => Severity
            };
        }

        private string GetCategoryDisplayName()
        {
            return Category switch
            {
                "System" => "Система",
                "Security" => "Безопасность",
                "Configuration" => "Конфигурация",
                "Performance" => "Производительность",
                "Integration" => "Интеграция",
                "Maintenance" => "Обслуживание",
                "Backup" => "Резервное копирование",
                "UserActivity" => "Действия пользователей",
                _ => Category
            };
        }

        private string GetSeverityBadgeClass()
        {
            return Severity switch
            {
                "Critical" => "bg-danger",
                "Error" => "bg-warning",
                "Warning" => "bg-warning text-dark",
                "Info" => "bg-info",
                "Debug" => "bg-secondary",
                _ => "bg-light text-dark"
            };
        }
    }

    /// <summary>
    /// DTO для фильтрации событий
    /// </summary>
    public class EventFilterDto
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? StageExecutionId { get; set; }
        public int? MachineId { get; set; }
        public int? BatchId { get; set; }
        public string EventType { get; set; }
        public string OperatorId { get; set; }
        public bool? IsAutomatic { get; set; }
        public string Category { get; set; }
        public string Severity { get; set; }
        public string Source { get; set; }
        public string UserId { get; set; }
        public bool? IsProcessed { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    /// <summary>
    /// DTO для статистики событий
    /// </summary>
    public class EventStatisticsDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int? MachineId { get; set; }
        public Dictionary<string, int> StageEventStatistics { get; set; } = new();
        public Dictionary<string, Dictionary<string, int>> SystemEventStatistics { get; set; } = new();
        public List<OperatorActivityDto> OperatorActivity { get; set; } = new();
        public Dictionary<string, double> AverageTimeInStatus { get; set; } = new();

        // Вычисляемые поля
        public int TotalStageEvents => StageEventStatistics.Values.Sum();
        public int TotalSystemEvents => SystemEventStatistics.Values.Sum(d => d.Values.Sum());
        public int TotalEvents => TotalStageEvents + TotalSystemEvents;
        public int ActiveOperators => OperatorActivity.Count;
    }

    /// <summary>
    /// DTO для активности оператора
    /// </summary>
    public class OperatorActivityDto
    {
        public string OperatorId { get; set; }
        public string OperatorName { get; set; }
        public int EventCount { get; set; }
        public DateTime LastActivity { get; set; }

        // Вычисляемые поля
        public string LastActivityFormatted => LastActivity.ToString("dd.MM.yyyy HH:mm");
        public bool IsActiveToday => LastActivity.Date == DateTime.Today;
        public TimeSpan TimeSinceLastActivity => DateTime.Now - LastActivity;
    }

    /// <summary>
    /// DTO для временной линии этапа
    /// </summary>
    public class StageTimelineDto
    {
        public int StageExecutionId { get; set; }
        public string StageName { get; set; }
        public string DetailName { get; set; }
        public string MachineName { get; set; }
        public List<StageEventDto> Events { get; set; } = new();
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan? TotalDuration { get; set; }
        public Dictionary<string, TimeSpan> TimeInEachStatus { get; set; } = new();

        // Вычисляемые поля
        public bool IsCompleted => Events.Any(e => e.EventType == "Completed");
        public bool IsInProgress => Events.Any(e => e.EventType == "Started") && !IsCompleted;
        public string CurrentStatus => Events.OrderByDescending(e => e.EventTime).FirstOrDefault()?.NewStatus ?? "Unknown";
    }

    /// <summary>
    /// DTO для пагинированного результата событий
    /// </summary>
    public class PagedEventsDto<T>
    {
        public List<T> Events { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasNext => Page < TotalPages;
        public bool HasPrevious => Page > 1;
    }

    /// <summary>
    /// DTO для экспорта событий
    /// </summary>
    public class EventExportDto
    {
        public DateTime EventTime { get; set; }
        public string EventType { get; set; }
        public string Category { get; set; }
        public string Severity { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string OperatorName { get; set; }
        public string MachineName { get; set; }
        public string DetailName { get; set; }
        public string StageName { get; set; }
        public string Source { get; set; }
        public bool IsAutomatic { get; set; }
        public string Comment { get; set; }
        public string Duration { get; set; }
    }

    /// <summary>
    /// DTO для запроса статистики переназначений
    /// </summary>
    public class ReassignmentStatisticsDto
    {
        public int FromMachineId { get; set; }
        public string FromMachineName { get; set; }
        public int ToMachineId { get; set; }
        public string ToMachineName { get; set; }
        public int Count { get; set; }
        public double Percentage { get; set; }

        // Причины переназначений (топ-3)
        public List<string> TopReasons { get; set; } = new();
    }

    /// <summary>
    /// DTO для dashboard событий
    /// </summary>
    public class EventDashboardDto
    {
        public int TodayStageEvents { get; set; }
        public int TodaySystemEvents { get; set; }
        public int TodayErrors { get; set; }
        public int TodayWarnings { get; set; }
        public int UnprocessedCriticalEvents { get; set; }
        public int ActiveOperators { get; set; }
        public List<StageEventDto> RecentStageEvents { get; set; } = new();
        public List<SystemEventDto> RecentSystemEvents { get; set; } = new();
        public List<SystemEventDto> CriticalEvents { get; set; } = new();
        public Dictionary<string, int> EventsByHour { get; set; } = new();
        public List<OperatorActivityDto> TopOperators { get; set; } = new();
    }
}