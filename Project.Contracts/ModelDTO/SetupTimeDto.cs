using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Contracts.ModelDTO
{
    public class SetupTimeDto
    {
        public int Id { get; set; }
        public int MachineId { get; set; }
        public int FromDetailId { get; set; }
        public int ToDetailId { get; set; }
        public double Time { get; set; }
    }

}
