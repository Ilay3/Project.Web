namespace Project.Web.ViewModels
{
    public class RouteViewModel
    {
        public int Id { get; set; }
        public string DetailName { get; set; }
        public List<RouteStageViewModel> Stages { get; set; }
    }
    public class RouteStageViewModel
    {
        public int Id { get; set; }
        public int Order { get; set; }
        public string Name { get; set; }
        public string MachineTypeName { get; set; }
        public double NormTime { get; set; }
        public double SetupTime { get; set; }
        public string StageType { get; set; }
    }

}
