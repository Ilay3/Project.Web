using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Contracts.ModelDTO
{
    public class RouteStageDto
    {
        public int Id { get; set; }
        public int Order { get; set; }
        public string Name { get; set; }
        public int MachineTypeId { get; set; }
        public string MachineTypeName { get; set; }
        public double NormTime { get; set; }
        public double SetupTime { get; set; }
        public string StageType { get; set; }
    }

    public class RouteStageCreateDto
    {
        public int Order { get; set; }
        public string Name { get; set; }
        public int MachineTypeId { get; set; }
        public double NormTime { get; set; }
        public double SetupTime { get; set; }
        public string StageType { get; set; } // "Operation" или "Setup"
    }

    public class RouteStageEditDto : RouteStageCreateDto
    {
        public int Id { get; set; }
    }

}
