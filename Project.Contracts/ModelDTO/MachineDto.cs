using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Contracts.ModelDTO
{
    public class MachineDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string InventoryNumber { get; set; }
        public int MachineTypeId { get; set; }
        public string MachineTypeName { get; set; }
        public int Priority { get; set; }
    }

    public class MachineCreateDto
    {
        public string Name { get; set; }
        public string InventoryNumber { get; set; }
        public int MachineTypeId { get; set; }
        public int Priority { get; set; }
    }

    public class MachineEditDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string InventoryNumber { get; set; }
        public int MachineTypeId { get; set; }
        public int Priority { get; set; }
    }

}
