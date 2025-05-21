using Microsoft.EntityFrameworkCore;
using Project.Domain;
using Project.Domain.Entities;
using Project.Domain.Repositories;
using Project.Infrastructure.Data;

namespace Project.Infrastructure.Repositories
{
    public class MachineTypeRepository : IMachineTypeRepository
    {
        private readonly ManufacturingDbContext _db;

        public MachineTypeRepository(ManufacturingDbContext db)
        {
            _db = db;
        }

        public async Task<List<MachineType>> GetAllAsync() =>
            await _db.MachineTypes.AsNoTracking().ToListAsync();

        public async Task<MachineType?> GetByIdAsync(int id) =>
            await _db.MachineTypes.FindAsync(id);

        public async Task AddAsync(MachineType machineType)
        {
            _db.MachineTypes.Add(machineType);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(MachineType machineType)
        {
            _db.MachineTypes.Update(machineType);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await _db.MachineTypes.FindAsync(id);
            if (entity != null)
            {
                _db.MachineTypes.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }
    }
}
