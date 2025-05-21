namespace Project.Web.ViewModels
{
    public class OperatorWorkspaceViewModel
    {
        // Информация о станке
        public int MachineId { get; set; }
        public string MachineName { get; set; }
        public string InventoryNumber { get; set; }
        public string MachineTypeName { get; set; }

        // Текущий этап
        public StageExecutionViewModel CurrentStage { get; set; }

        // Следующий доступный этап
        public StageExecutionViewModel NextStage { get; set; }

        // Очередь этапов
        public List<StageExecutionViewModel> QueuedStages { get; set; } = new List<StageExecutionViewModel>();

        // Статистика работы станка
        public double TotalWorkHours { get; set; }
        public double TotalSetupHours { get; set; }
        public double TotalIdleHours { get; set; }
        public double EfficiencyPercentage { get; set; }

        // Информация об операторе
        public string OperatorId { get; set; }
        public string OperatorName { get; set; }
        public bool IsAuthenticated { get; set; }

        // Настройки интерфейса
        public bool EnableAutoRefresh { get; set; } = true;
        public int RefreshIntervalSeconds { get; set; } = 10;
        public bool ShowCompletedStages { get; set; } = true;
        public bool PlaySoundNotifications { get; set; } = true;

        // История изменений статуса
        public List<StatusChangeViewModel> RecentStatusChanges { get; set; } = new List<StatusChangeViewModel>();

        // Недавно завершенные этапы
        public List<StageExecutionViewModel> RecentlyCompletedStages { get; set; } = new List<StageExecutionViewModel>();
    }

}
