using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Contracts.ModelDTO
{
    public class MachineTypeDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class MachineTypeCreateDto
    {
        public string Name { get; set; }
    }

    public class MachineTypeEditDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

}
