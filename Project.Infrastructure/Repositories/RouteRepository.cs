using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Domain.Repositories;
using Project.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Infrastructure.Repositories
{

    public class RouteRepository : IRouteRepository
    {
        private readonly ManufacturingDbContext _db;

        public RouteRepository(ManufacturingDbContext db)
        {
            _db = db;
        }

        public async Task<List<Route>> GetAllAsync() =>
            await _db.Routes
                .Include(r => r.Detail)
                .Include(r => r.Stages)
                    .ThenInclude(s => s.MachineType)
                .AsNoTracking()
                .ToListAsync();

        public async Task<Route?> GetByIdAsync(int id) =>
            await _db.Routes
                .Include(r => r.Detail)
                .Include(r => r.Stages)
                    .ThenInclude(s => s.MachineType)
                .FirstOrDefaultAsync(r => r.Id == id);

        public async Task AddAsync(Route entity)
        {
            _db.Routes.Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(Route entity)
        {
            _db.Routes.Update(entity);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var r = await _db.Routes.FindAsync(id);
            if (r != null)
            {
                _db.Routes.Remove(r);
                await _db.SaveChangesAsync();
            }
        }
    }

}
