namespace Project.Contracts.Enums
{
    /// <summary>
    /// Статус станка согласно ТЗ
    /// </summary>
    public enum MachineStatus
    {
        /// <summary>
        /// Свободен - станок готов к работе
        /// </summary>
        Free = 0,

        /// <summary>
        /// Занят - на станке выполняется операция
        /// </summary>
        Busy = 1,

        /// <summary>
        /// Переналадка - выполняется переналадка станка
        /// </summary>
        Setup = 2,

        /// <summary>
        /// Неисправен - станок неисправен или на обслуживании
        /// </summary>
        Broken = 3
    }

    /// <summary>
    /// Статус этапа выполнения согласно ТЗ
    /// </summary>
    public enum StageStatus
    {
        /// <summary>
        /// Ожидает запуска - этап готов к выполнению
        /// </summary>
        AwaitingStart = 0,

        /// <summary>
        /// В очереди - этап ожидает освобождения станка
        /// </summary>
        InQueue = 1,

        /// <summary>
        /// В работе - этап выполняется
        /// </summary>
        InProgress = 2,

        /// <summary>
        /// На паузе - этап приостановлен пользователем
        /// </summary>
        Paused = 3,

        /// <summary>
        /// Завершено - этап выполнен
        /// </summary>
        Completed = 4,

        /// <summary>
        /// Отменено - этап отменен
        /// </summary>
        Cancelled = 5
    }

    /// <summary>
    /// Приоритет этапа или партии
    /// </summary>
    public enum Priority
    {
        /// <summary>
        /// Низкий приоритет
        /// </summary>
        Low = 1,

        /// <summary>
        /// Обычный приоритет
        /// </summary>
        Normal = 5,

        /// <summary>
        /// Высокий приоритет
        /// </summary>
        High = 8,

        /// <summary>
        /// Критический приоритет (не прерывается вне рабочего времени)
        /// </summary>
        Critical = 10
    }
}