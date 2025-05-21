using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class Route
    {
        public int Id { get; set; }
        public int DetailId { get; set; }
        public Detail Detail { get; set; }
        public ICollection<RouteStage> Stages { get; set; }
    }

}
