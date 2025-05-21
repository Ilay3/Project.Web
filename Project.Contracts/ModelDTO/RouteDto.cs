using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Contracts.ModelDTO
{
    public class RouteDto
    {
        public int Id { get; set; }
        public int DetailId { get; set; }
        public string DetailName { get; set; }
        public List<RouteStageDto> Stages { get; set; }
    }

    public class RouteCreateDto
    {
        public int DetailId { get; set; }
        public List<RouteStageCreateDto> Stages { get; set; }
    }

    public class RouteEditDto
    {
        public int Id { get; set; }
        public int DetailId { get; set; }
        public List<RouteStageEditDto> Stages { get; set; }
    }

}
