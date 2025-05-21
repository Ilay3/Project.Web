using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities; // Импортируй пространство имен моделей (подставь своё имя)

namespace Project.Infrastructure.Data
{
    public class ManufacturingDbContext : DbContext
    {
        public ManufacturingDbContext(DbContextOptions<ManufacturingDbContext> options) : base(options) { }

        // DbSets
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
            // Detail
            modelBuilder.Entity<Detail>()
                .HasIndex(d => d.Number)
                .IsUnique();

            // MachineType
            modelBuilder.Entity<MachineType>()
                .HasMany(mt => mt.Machines)
                .WithOne(m => m.MachineType)
                .HasForeignKey(m => m.MachineTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Machine
            modelBuilder.Entity<Machine>()
                .HasIndex(m => m.InventoryNumber)
                .IsUnique();

            // Route
            modelBuilder.Entity<Route>()
                .HasOne(r => r.Detail)
                .WithMany(d => d.Routes)
                .HasForeignKey(r => r.DetailId)
                .OnDelete(DeleteBehavior.Cascade);

            // RouteStage
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

            // Batch
            modelBuilder.Entity<Batch>()
                .HasOne(b => b.Detail)
                .WithMany()
                .HasForeignKey(b => b.DetailId)
                .OnDelete(DeleteBehavior.Restrict);

            // SubBatch
            modelBuilder.Entity<SubBatch>()
                .HasOne(sb => sb.Batch)
                .WithMany(b => b.SubBatches)
                .HasForeignKey(sb => sb.BatchId)
                .OnDelete(DeleteBehavior.Cascade);

            // StageExecution
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

            // SetupTime
            modelBuilder.Entity<SetupTime>()
                .HasOne(st => st.Machine)
                .WithMany()
                .HasForeignKey(st => st.MachineId)
                .OnDelete(DeleteBehavior.Restrict);

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

            // Enum to string for StageExecutionStatus
            modelBuilder.Entity<StageExecution>()
                .Property(se => se.Status)
                .HasConversion<string>();
        }
    }
}
