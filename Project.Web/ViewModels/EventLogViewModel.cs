using Project.Contracts.ModelDTO;

namespace Project.Web.ViewModels
{
    public class EventLogViewModel
    {
        public EventFilterDto Filter { get; set; }
        public PagedEventsDto<StageEventDto> StageEvents { get; set; }
        public PagedEventsDto<SystemEventDto> SystemEvents { get; set; }
    }
}
