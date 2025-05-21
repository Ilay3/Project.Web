using Project.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Repositories
{
    public interface IBatchRepository
    {
        Task<List<Batch>> GetAllAsync();
        Task<Batch?> GetByIdAsync(int id);
        Task AddAsync(Batch entity);
        Task UpdateAsync(Batch entity);
        Task DeleteAsync(int id);
    }

}
