namespace Project.Web.ViewModels
{
    public class BatchViewModel
    {
        public int Id { get; set; }
        public string DetailName { get; set; }
        public int Quantity { get; set; }
        public DateTime Created { get; set; }
        public List<SubBatchViewModel> SubBatches { get; set; }
    }
    public class SubBatchViewModel
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
        public List<StageExecutionViewModel> StageExecutions { get; set; }
    }
    public class StageExecutionViewModel
    {
        public int Id { get; set; }
        public string StageName { get; set; }
        public string? MachineName { get; set; }
        public string Status { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool IsSetup { get; set; }
    }

}
