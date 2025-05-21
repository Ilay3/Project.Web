using Project.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Repositories
{
    public interface IMachineTypeRepository
    {
        Task<List<MachineType>> GetAllAsync();
        Task<MachineType?> GetByIdAsync(int id);
        Task AddAsync(MachineType machineType);
        Task UpdateAsync(MachineType machineType);
        Task DeleteAsync(int id);
    }

}
