using Microsoft.EntityFrameworkCore;
using Project.Domain;
using Project.Domain.Entities;
using Project.Domain.Repositories;
using Project.Infrastructure.Data;

namespace Project.Infrastructure.Repositories
{
    public class DetailRepository : IDetailRepository
    {
        private readonly ManufacturingDbContext _db;

        public DetailRepository(ManufacturingDbContext db)
        {
            _db = db;
        }

        public async Task<List<Detail>> GetAllAsync() =>
            await _db.Details.AsNoTracking().ToListAsync();

        public async Task<Detail?> GetByIdAsync(int id) =>
            await _db.Details.FindAsync(id);

        public async Task AddAsync(Detail detail)
        {
            _db.Details.Add(detail);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(Detail detail)
        {
            _db.Details.Update(detail);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await _db.Details.FindAsync(id);
            if (entity != null)
            {
                _db.Details.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }
    }
}
