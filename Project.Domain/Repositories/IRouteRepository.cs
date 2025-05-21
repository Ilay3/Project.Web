using Project.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Repositories
{
    public interface IRouteRepository
    {
        Task<List<Route>> GetAllAsync();
        Task<Route?> GetByIdAsync(int id);
        Task<Route?> GetByDetailIdAsync(int detailId);
        Task AddAsync(Route entity);
        Task UpdateAsync(Route entity);
        Task DeleteAsync(int id);
    }
}