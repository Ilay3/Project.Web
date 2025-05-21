using System;
using System.Collections.Generic;

namespace Project.Web.ViewModels
{
    public class WorkstationViewModel
    {
        public int MachineId { get; set; }
        public string MachineName { get; set; }
        public string InventoryNumber { get; set; }
        public string MachineTypeName { get; set; }

        // Текущий активный этап на станке
        public StageExecutionViewModel CurrentStage { get; set; }

        // Следующий доступный этап (если текущего нет)
        public StageExecutionViewModel NextStage { get; set; }

        // Очередь этапов на этот станок
        public List<StageExecutionViewModel> QueuedStages { get; set; } = new List<StageExecutionViewModel>();

        // Статистика по станку
        public double TotalWorkHours { get; set; }
        public double TotalSetupHours { get; set; }
        public double TotalIdleHours { get; set; }
        public double EfficiencyPercentage { get; set; }

        // История изменений статуса
        public List<StatusChangeViewModel> RecentStatusChanges { get; set; } = new List<StatusChangeViewModel>();
    }

    public class StatusChangeViewModel
    {
        public DateTime ChangeTime { get; set; }
        public string FromStatus { get; set; }
        public string ToStatus { get; set; }
        public string DetailName { get; set; }
        public string StageName { get; set; }
        public string OperatorId { get; set; }
        public string Note { get; set; }
    }
}