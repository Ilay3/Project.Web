using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;

namespace Project.Infrastructure.Data
{
    public class ManufacturingDbContext : DbContext
    {
        public ManufacturingDbContext(DbContextOptions<ManufacturingDbContext> options)
            : base(options)
        {
        }

        public DbSet<Detail> Details { get; set; }
        public DbSet<MachineType> MachineTypes { get; set; }
        public DbSet<Machine> Machines { get; set; }
        public DbSet<Route> Routes { get; set; }
        public DbSet<RouteStage> RouteStages { get; set; }
        public DbSet<Batch> Batches { get; set; }
        public DbSet<SubBatch> SubBatches { get; set; }
        public DbSet<StageExecution> StageExecutions { get; set; }
        public DbSet<SetupTime> SetupTimes { get; set; }

       

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Настройка для PostgreSQL - все DateTime автоматически в UTC
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                    {
                        property.SetColumnType("timestamp with time zone");
                    }
                }
            }

            // Настройка Detail
            modelBuilder.Entity<Detail>()
                .HasIndex(d => d.Number)
                .IsUnique();

            // Настройка MachineType
            modelBuilder.Entity<MachineType>()
                .HasIndex(mt => mt.Name)
                .IsUnique();

            // Настройка Machine
            modelBuilder.Entity<Machine>()
                .HasIndex(m => m.InventoryNumber)
                .IsUnique();

            modelBuilder.Entity<Machine>()
                .HasOne(m => m.MachineType)
                .WithMany(mt => mt.Machines)
                .HasForeignKey(m => m.MachineTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Настройка Route
            modelBuilder.Entity<Route>()
                .HasOne(r => r.Detail)
                .WithMany(d => d.Routes)
                .HasForeignKey(r => r.DetailId)
                .OnDelete(DeleteBehavior.Cascade);

            // Настройка RouteStage
            modelBuilder.Entity<RouteStage>()
                .HasOne(rs => rs.Route)
                .WithMany(r => r.Stages)
                .HasForeignKey(rs => rs.RouteId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RouteStage>()
                .HasOne(rs => rs.MachineType)
                .WithMany()
                .HasForeignKey(rs => rs.MachineTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Настройка Batch
            modelBuilder.Entity<Batch>()
                .HasOne(b => b.Detail)
                .WithMany()
                .HasForeignKey(b => b.DetailId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Batch>()
                .Property(b => b.CreatedUtc)
                .HasConversion(
                    v => v.ToUniversalTime(),
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            // Настройка SubBatch
            modelBuilder.Entity<SubBatch>()
                .HasOne(sb => sb.Batch)
                .WithMany(b => b.SubBatches)
                .HasForeignKey(sb => sb.BatchId)
                .OnDelete(DeleteBehavior.Cascade);

            // Настройка StageExecution
            modelBuilder.Entity<StageExecution>()
                .HasOne(se => se.SubBatch)
                .WithMany(sb => sb.StageExecutions)
                .HasForeignKey(se => se.SubBatchId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StageExecution>()
                .HasOne(se => se.RouteStage)
                .WithMany()
                .HasForeignKey(se => se.RouteStageId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StageExecution>()
                .HasOne(se => se.Machine)
                .WithMany()
                .HasForeignKey(se => se.MachineId)
                .OnDelete(DeleteBehavior.Restrict);

            // Конверсия DateTime для StageExecution
            modelBuilder.Entity<StageExecution>().Property(x => x.StartTimeUtc)
                .HasConversion(v => v.HasValue ? v.Value.ToUniversalTime() : (DateTime?)null, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null);
            modelBuilder.Entity<StageExecution>().Property(x => x.EndTimeUtc)
                .HasConversion(v => v.HasValue ? v.Value.ToUniversalTime() : (DateTime?)null, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null);
            modelBuilder.Entity<StageExecution>().Property(x => x.PauseTimeUtc)
                .HasConversion(v => v.HasValue ? v.Value.ToUniversalTime() : (DateTime?)null, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null);
            modelBuilder.Entity<StageExecution>().Property(x => x.ResumeTimeUtc)
                .HasConversion(v => v.HasValue ? v.Value.ToUniversalTime() : (DateTime?)null, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null);
            modelBuilder.Entity<StageExecution>().Property(x => x.ScheduledStartTimeUtc)
                .HasConversion(v => v.HasValue ? v.Value.ToUniversalTime() : (DateTime?)null, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null);
            modelBuilder.Entity<StageExecution>().Property(x => x.StatusChangedTimeUtc)
                .HasConversion(v => v.HasValue ? v.Value.ToUniversalTime() : (DateTime?)null, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null);

            // Настройка SetupTime
            modelBuilder.Entity<SetupTime>()
                .HasOne(st => st.Machine)
                .WithMany()
                .HasForeignKey(st => st.MachineId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SetupTime>()
                .HasOne(st => st.FromDetail)
                .WithMany()
                .HasForeignKey(st => st.FromDetailId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SetupTime>()
                .HasOne(st => st.ToDetail)
                .WithMany()
                .HasForeignKey(st => st.ToDetailId)
                .OnDelete(DeleteBehavior.Restrict);

            // Создание составного индекса для таблицы SetupTime
            modelBuilder.Entity<SetupTime>()
                .HasIndex(st => new { st.MachineId, st.FromDetailId, st.ToDetailId })
                .IsUnique();

           ;

            // Создание индексов для ускорения запросов
            modelBuilder.Entity<StageExecution>()
                .HasIndex(se => se.Status);

            modelBuilder.Entity<StageExecution>()
                .HasIndex(se => se.StartTimeUtc);

            modelBuilder.Entity<StageExecution>()
                .HasIndex(se => se.MachineId);

            modelBuilder.Entity<StageExecution>()
                .HasIndex(se => se.IsSetup);

            modelBuilder.Entity<StageExecution>()
                .HasIndex(se => se.SubBatchId);

            modelBuilder.Entity<StageExecution>()
                .HasIndex(se => se.ScheduledStartTimeUtc);

            modelBuilder.Entity<StageExecution>()
                .HasIndex(se => se.IsProcessedByScheduler);

            modelBuilder.Entity<StageExecution>()
                .HasIndex(se => se.StatusChangedTimeUtc);

            modelBuilder.Entity<StageExecution>()
                .HasIndex(se => new { se.MachineId, se.Status, se.QueuePosition });

            modelBuilder.Entity<StageExecution>()
                .HasIndex(se => new { se.Status, se.Priority, se.ScheduledStartTimeUtc });

            // Настройка длины строковых полей
            modelBuilder.Entity<StageExecution>()
                .Property(se => se.OperatorId)
                .HasMaxLength(50);

            modelBuilder.Entity<StageExecution>()
                .Property(se => se.ReasonNote)
                .HasMaxLength(500);

            modelBuilder.Entity<StageExecution>()
                .Property(se => se.LastErrorMessage)
                .HasMaxLength(1000);

            modelBuilder.Entity<StageExecution>()
                .Property(se => se.DeviceId)
                .HasMaxLength(100);
        }

        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is StageExecution &&
                           (e.State == EntityState.Added || e.State == EntityState.Modified));

            foreach (var entry in entries)
            {
                var stageExecution = (StageExecution)entry.Entity;

                if (entry.State == EntityState.Added)
                {
                    if (!stageExecution.StatusChangedTimeUtc.HasValue)
                        stageExecution.StatusChangedTimeUtc = DateTime.UtcNow;
                }
                else if (entry.State == EntityState.Modified)
                {
                    if (entry.Property(nameof(StageExecution.Status)).IsModified)
                    {
                        stageExecution.StatusChangedTimeUtc = DateTime.UtcNow;
                    }
                }
            }
        }
    }
}