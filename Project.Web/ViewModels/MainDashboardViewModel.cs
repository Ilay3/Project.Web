using Project.Contracts.ModelDTO;

namespace Project.Web.ViewModels
{
    public class MainDashboardViewModel
    {
        public List<DetailDto> Details { get; set; } = new();
        public List<MachineTypeDto> MachineTypes { get; set; } = new();
        public List<MachineDto> Machines { get; set; } = new();
        public List<BatchDto> Batches { get; set; } = new();
        public List<GanttStageDto> GanttStages { get; set; } = new();
        public List<StageQueueDto> QueueItems { get; set; } = new();
    }
}
