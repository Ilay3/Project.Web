using System;
using System.ComponentModel.DataAnnotations;

namespace Project.Domain.Entities
{
    /// <summary>
    /// Системные события (не связанные с конкретными этапами)
    /// </summary>
    public class SystemEvent
    {
        public int Id { get; set; }

        /// <summary>
        /// Категория события
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Category { get; set; } // System, Security, Configuration, Performance, Integration

        /// <summary>
        /// Тип события
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string EventType { get; set; }

        /// <summary>
        /// Уровень важности
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Severity { get; set; } // Critical, Error, Warning, Info, Debug

        /// <summary>
        /// Заголовок события
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string Title { get; set; }

        /// <summary>
        /// Описание события
        /// </summary>
        [MaxLength(2000)]
        public string Description { get; set; }

        /// <summary>
        /// Время события (UTC)
        /// </summary>
        public DateTime EventTimeUtc { get; set; }

        /// <summary>
        /// Источник события
        /// </summary>
        [MaxLength(200)]
        public string Source { get; set; } // BackgroundService, API, UI, External

        /// <summary>
        /// ID пользователя, если применимо
        /// </summary>
        [MaxLength(100)]
        public string UserId { get; set; }

        /// <summary>
        /// Имя пользователя
        /// </summary>
        [MaxLength(200)]
        public string UserName { get; set; }

        /// <summary>
        /// IP адрес
        /// </summary>
        [MaxLength(45)]
        public string IpAddress { get; set; }

        /// <summary>
        /// ID связанной сущности (если применимо)
        /// </summary>
        [MaxLength(100)]
        public string RelatedEntityId { get; set; }

        /// <summary>
        /// Тип связанной сущности
        /// </summary>
        [MaxLength(100)]
        public string RelatedEntityType { get; set; } // Batch, Machine, Detail, Route, etc.

        /// <summary>
        /// Дополнительные данные в JSON формате
        /// </summary>
        public string AdditionalData { get; set; }

        /// <summary>
        /// Стек вызовов для ошибок
        /// </summary>
        public string StackTrace { get; set; }

        /// <summary>
        /// Внутреннее сообщение об ошибке
        /// </summary>
        [MaxLength(1000)]
        public string InnerException { get; set; }

        /// <summary>
        /// Событие обработано/просмотрено
        /// </summary>
        public bool IsProcessed { get; set; }

        /// <summary>
        /// Время обработки события
        /// </summary>
        public DateTime? ProcessedTimeUtc { get; set; }

        /// <summary>
        /// Кто обработал событие
        /// </summary>
        [MaxLength(200)]
        public string ProcessedBy { get; set; }

        /// <summary>
        /// Количество попыток обработки (для критических событий)
        /// </summary>
        public int ProcessingAttempts { get; set; }

        /// <summary>
        /// Время следующей попытки обработки
        /// </summary>
        public DateTime? NextProcessingAttempt { get; set; }
    }

    /// <summary>
    /// Категории системных событий
    /// </summary>
    public static class SystemEventCategories
    {
        public const string System = "System";
        public const string Security = "Security";
        public const string Configuration = "Configuration";
        public const string Performance = "Performance";
        public const string Integration = "Integration";
        public const string Maintenance = "Maintenance";
        public const string Backup = "Backup";
        public const string UserActivity = "UserActivity";
    }

    /// <summary>
    /// Уровни важности событий
    /// </summary>
    public static class EventSeverity
    {
        public const string Critical = "Critical";
        public const string Error = "Error";
        public const string Warning = "Warning";
        public const string Info = "Info";
        public const string Debug = "Debug";
    }

    /// <summary>
    /// Типы системных событий
    /// </summary>
    public static class SystemEventTypes
    {
        // Системные
        public const string ApplicationStarted = "ApplicationStarted";
        public const string ApplicationStopped = "ApplicationStopped";
        public const string ServiceStarted = "ServiceStarted";
        public const string ServiceStopped = "ServiceStopped";
        public const string ConfigurationChanged = "ConfigurationChanged";

        // Безопасность
        public const string UserLogin = "UserLogin";
        public const string UserLogout = "UserLogout";
        public const string FailedLogin = "FailedLogin";
        public const string UnauthorizedAccess = "UnauthorizedAccess";

        // Производительность
        public const string HighCpuUsage = "HighCpuUsage";
        public const string HighMemoryUsage = "HighMemoryUsage";
        public const string SlowQuery = "SlowQuery";
        public const string DatabaseConnectionIssue = "DatabaseConnectionIssue";

        // Интеграция
        public const string ExternalServiceError = "ExternalServiceError";
        public const string ApiError = "ApiError";
        public const string DataImportError = "DataImportError";
        public const string DataExportError = "DataExportError";

        // Обслуживание
        public const string DatabaseMaintenance = "DatabaseMaintenance";
        public const string SystemUpdate = "SystemUpdate";
        public const string BackupCreated = "BackupCreated";
        public const string BackupFailed = "BackupFailed";
    }
}