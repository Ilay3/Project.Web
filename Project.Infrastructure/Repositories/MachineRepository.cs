using Microsoft.EntityFrameworkCore;
using Project.Domain;
using Project.Domain.Entities;
using Project.Domain.Repositories;
using Project.Infrastructure.Data;

namespace Project.Infrastructure.Repositories
{
    public class MachineRepository : IMachineRepository
    {
        private readonly ManufacturingDbContext _db;

        public MachineRepository(ManufacturingDbContext db)
        {
            _db = db;
        }

        public async Task<List<Machine>> GetAllAsync() =>
            await _db.Machines.Include(m => m.MachineType).AsNoTracking().ToListAsync();

        public async Task<Machine?> GetByIdAsync(int id) =>
            await _db.Machines.Include(m => m.MachineType).FirstOrDefaultAsync(m => m.Id == id);

        public async Task AddAsync(Machine machine)
        {
            _db.Machines.Add(machine);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(Machine machine)
        {
            _db.Machines.Update(machine);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await _db.Machines.FindAsync(id);
            if (entity != null)
            {
                _db.Machines.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }
    }
}
