using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RecordsMaster.Models;
using System.IO;
using RecordsMaster.Utilities;

namespace RecordsMaster.Data
{
    // Now using ApplicationUser as the Identity user model.
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        { }

        // Table for RecordItemModel entities.
        public DbSet<RecordItemModel> RecordItems { get; set; }
        public DbSet<SeedHistory> SeedHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Call the base OnModelCreating to ensure Identity tables are configured.
            base.OnModelCreating(modelBuilder);

            // Configure the RecordItemModel's primary key and properties.
            modelBuilder.Entity<RecordItemModel>().HasKey(r => r.ID);

            modelBuilder.Entity<RecordItemModel>().Property(r => r.BarCode)
                .IsRequired()
                .HasMaxLength(100);

            modelBuilder.Entity<RecordItemModel>().Property(r => r.RecordType)
                .IsRequired()
                .HasMaxLength(50);

            // Configure the one-to-many relationship:
            // One ApplicationUser can have many RecordItemModel records checked out.
            modelBuilder.Entity<RecordItemModel>()
                .HasOne(r => r.CheckedOutTo)
                .WithMany(u => u.CheckedOutRecords)
                .HasForeignKey(r => r.CheckedOutToId);
        }
    }
}