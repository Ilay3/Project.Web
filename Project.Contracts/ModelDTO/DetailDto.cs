using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Contracts.ModelDTO
{
    public class DetailDto
    {
        public int Id { get; set; }
        public string Number { get; set; }
        public string Name { get; set; }
    }

    public class DetailCreateDto
    {
        public string Number { get; set; }
        public string Name { get; set; }
    }

    public class DetailEditDto
    {
        public int Id { get; set; }
        public string Number { get; set; }
        public string Name { get; set; }
    }

}
