using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class Machine
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string InventoryNumber { get; set; } = null!;
        public int MachineTypeId { get; set; }
        public MachineType MachineType { get; set; }
        public int Priority { get; set; } = 0; // 0 - обычный, больше - выше приоритет
    }
}
