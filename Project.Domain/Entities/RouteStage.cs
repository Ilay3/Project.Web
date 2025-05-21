using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class RouteStage
    {
        public int Id { get; set; }
        public int RouteId { get; set; }
        public Route Route { get; set; }

        public int Order { get; set; } // Порядковый номер в маршруте
        public string Name { get; set; } = null!;
        public int MachineTypeId { get; set; }
        public MachineType MachineType { get; set; }
        public double NormTime { get; set; } // Часы на 1 деталь
        public double SetupTime { get; set; } // Переналадка, если это переналадка

        // Тип этапа: "Operation" или "Setup"
        public string StageType { get; set; } = "Operation";
    }

}
