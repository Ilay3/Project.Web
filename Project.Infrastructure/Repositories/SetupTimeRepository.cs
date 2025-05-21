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
    public class SetupTimeRepository : ISetupTimeRepository
    {
        private readonly ManufacturingDbContext _db;
        public SetupTimeRepository(ManufacturingDbContext db) => _db = db;

        public async Task<List<SetupTime>> GetAllAsync() =>
            await _db.SetupTimes
                .Include(st => st.Machine)
                .Include(st => st.FromDetail)
                .Include(st => st.ToDetail)
                .AsNoTracking()
                .ToListAsync();

        public async Task<SetupTime?> GetByIdAsync(int id) =>
            await _db.SetupTimes
                .Include(st => st.Machine)
                .Include(st => st.FromDetail)
                .Include(st => st.ToDetail)
                .FirstOrDefaultAsync(st => st.Id == id);

        public async Task AddAsync(SetupTime entity)
        {
            _db.SetupTimes.Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(SetupTime entity)
        {
            _db.SetupTimes.Update(entity);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var st = await _db.SetupTimes.FindAsync(id);
            if (st != null)
            {
                _db.SetupTimes.Remove(st);
                await _db.SaveChangesAsync();
            }
        }
    }

}
