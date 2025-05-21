using Project.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Repositories
{
    public interface IDetailRepository
    {
        Task<List<Detail>> GetAllAsync();
        Task<Detail?> GetByIdAsync(int id);
        Task AddAsync(Detail detail);
        Task UpdateAsync(Detail detail);
        Task DeleteAsync(int id);
    }

}
