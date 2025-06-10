using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RecordsMaster.Models;
using System;

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

            // Seed test data for RecordItemModel (adjust or remove as needed).
            modelBuilder.Entity<RecordItemModel>().HasData(
                new RecordItemModel
                {
                    ID = new Guid("11111111-1111-1111-1111-111111111111"),
                    CIS = 1001,
                    BarCode = "24-98765",
                    RecordType = "Type A",
                    Location = "Records Room",
                    BoxNumber = 10,
                    Digitized = true,
                    ClosingDate = new DateTime(2023, 1, 1),
                    DestroyDate = new DateTime(2028, 1, 1)
                },
                new RecordItemModel
                {
                    ID = new Guid("22222222-2222-2222-2222-222222222222"),
                    CIS = 1002,
                    BarCode = "24-98766",
                    RecordType = "Type B",
                    Location = "Records Room",
                    BoxNumber = 20,
                    Digitized = false,
                    ClosingDate = new DateTime(2024, 1, 1),
                    DestroyDate = new DateTime(2029, 1, 1)
                }
            );
        }
    }
}