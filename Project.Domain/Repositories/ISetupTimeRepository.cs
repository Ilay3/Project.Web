using Project.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Repositories
{
    public interface ISetupTimeRepository
    {
        Task<List<SetupTime>> GetAllAsync();
        Task<SetupTime?> GetByIdAsync(int id);
        Task<SetupTime?> GetSetupTimeAsync(int machineId, int fromDetailId, int toDetailId);
        Task<Detail?> GetLastDetailOnMachineAsync(int machineId);
        Task AddAsync(SetupTime entity);
        Task UpdateAsync(SetupTime entity);
        Task DeleteAsync(int id);
    }
}