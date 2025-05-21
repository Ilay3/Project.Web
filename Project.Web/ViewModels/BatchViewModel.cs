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
        public string DetailName { get; set; }
        public string DetailNumber { get; set; }
        public int DetailQuantity { get; set; }
        public string MachineName { get; set; }
        public string Status { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public DateTime? PauseTime { get; set; }
        public DateTime? ResumeTime { get; set; }
        public bool IsSetup { get; set; }
        public string OperatorId { get; set; }
        public string ReasonNote { get; set; }

        // Добавляем плановое время начала
        public DateTime? ScheduledStartTime { get; set; }

        // Расчетные поля для отображения
        public TimeSpan PlannedDuration { get; set; }
        public TimeSpan? ActualDuration { get; set; }
        public double CompletionPercentage { get; set; }

        // Время в работе
        public string ElapsedTimeFormatted
        {
            get
            {
                if (!StartTime.HasValue) return "-";

                TimeSpan elapsed;

                if (Status == "Completed" && EndTime.HasValue)
                {
                    elapsed = EndTime.Value - StartTime.Value;
                }
                else if (Status == "Paused" && PauseTime.HasValue)
                {
                    elapsed = PauseTime.Value - StartTime.Value;
                }
                else
                {
                    elapsed = DateTime.Now - StartTime.Value;
                }

                return $"{elapsed.Hours}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
            }
        }

        // Цвет для отображения статуса
        public string StatusColorClass
        {
            get
            {
                return Status switch
                {
                    "Completed" => "bg-success",
                    "InProgress" => "bg-primary",
                    "Paused" => "bg-secondary",
                    "Waiting" => "bg-warning text-dark",
                    "Pending" => "bg-info text-dark",
                    "Error" => "bg-danger",
                    _ => "bg-light text-dark"
                };
            }
        }

        // Человекопонятный статус
        public string StatusDisplayName
        {
            get
            {
                return Status switch
                {
                    "Completed" => "Завершен",
                    "InProgress" => "В работе",
                    "Paused" => "На паузе",
                    "Waiting" => "В очереди",
                    "Pending" => "Ожидает запуска",
                    "Error" => "Ошибка / Отменен",
                    _ => Status
                };
            }
        }
    }


    public class BatchDetailsViewModel
    {
        public int Id { get; set; }
        public int DetailId { get; set; }
        public string DetailName { get; set; }
        public int Quantity { get; set; }
        public DateTime Created { get; set; }
        public List<SubBatchViewModel> SubBatches { get; set; } = new List<SubBatchViewModel>();

        // Статистика выполнения
        public int TotalStages { get; set; }
        public int CompletedStages { get; set; }
        public int InProgressStages { get; set; }
        public int PendingStages { get; set; }
        public double CompletionPercent { get; set; }

        // Вычисляемые свойства
        public bool IsCompleted => CompletedStages == TotalStages && TotalStages > 0;
        public bool IsInProgress => InProgressStages > 0;
        public bool IsNotStarted => CompletedStages == 0 && InProgressStages == 0;
    }


}
