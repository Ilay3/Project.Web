using Project.Contracts.ModelDTO;

namespace Project.Web.ViewModels
{
    public class MainDashboardViewModel
    {
        public List<DetailDto> Details { get; set; } = new List<DetailDto>();
        public List<MachineTypeDto> MachineTypes { get; set; } = new List<MachineTypeDto>();
        public List<MachineDto> Machines { get; set; } = new List<MachineDto>();
        public List<BatchDto> Batches { get; set; } = new List<BatchDto>();
        public List<GanttStageDto> GanttStages { get; set; } = new List<GanttStageDto>();
        public List<StageQueueDto> QueueItems { get; set; } = new List<StageQueueDto>();
    }

}
