using Project.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Repositories
{
    public interface IMachineRepository
    {
        Task<List<Machine>> GetAllAsync();
        Task<Machine?> GetByIdAsync(int id);
        Task AddAsync(Machine machine);
        Task UpdateAsync(Machine machine);
        Task DeleteAsync(int id);
    }

}
