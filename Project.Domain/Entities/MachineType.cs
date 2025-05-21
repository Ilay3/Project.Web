using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class MachineType
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public ICollection<Machine> Machines { get; set; }
    }

}
