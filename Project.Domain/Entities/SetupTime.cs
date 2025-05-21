using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class SetupTime
    {
        public int Id { get; set; }
        public int MachineId { get; set; }
        public Machine Machine { get; set; }
        public int FromDetailId { get; set; }
        public Detail FromDetail { get; set; }
        public int ToDetailId { get; set; }
        public Detail ToDetail { get; set; }
        public double Time { get; set; } // В часах
    }

}
