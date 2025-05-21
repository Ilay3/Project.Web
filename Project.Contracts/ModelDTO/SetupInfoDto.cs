using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Contracts.ModelDTO
{
    // DTO для информации о необходимости переналадки
    public class SetupInfoDto
    {
        public bool SetupNeeded { get; set; }
        public int? FromDetailId { get; set; }
        public string FromDetailName { get; set; }
        public int ToDetailId { get; set; }
        public double SetupTime { get; set; }
    }
}
