using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RecordsMaster.Models;

namespace RecordsMaster.Data
{
    //AppDbContext is how to get to the database via the views.
    public class AppDbContext : IdentityDbContext<IdentityUser>
    {
        //Call the DbContextOptions constructor and the options class
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { } //pass the options 

        // Only need one DbSet because there's only one model
        public DbSet<RecordItemModel> RecordItems { get; set; } // Table for RecordItemModel entities. Use this in the controllers. C# tradition is that the DbSet property name is pluralized 

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // Ensure Identity tables are configured correctly

            modelBuilder.Entity<RecordItemModel>().HasKey(r => r.ID); // Define primary key

            modelBuilder.Entity<RecordItemModel>().Property(r => r.BarCode)
                .IsRequired()
                .HasMaxLength(100); // Match model constraints

            modelBuilder.Entity<RecordItemModel>().Property(r => r.RecordType)
                .IsRequired()
                .HasMaxLength(50);

            // Seed test data
            modelBuilder.Entity<RecordItemModel>().HasData(
                new RecordItemModel
                {
                    ID =  new Guid("11111111-1111-1111-1111-111111111111"),
                    CIS = 1001,
                    BarCode = "24-987654",
                    RecordType = "Type A",
                    BoxNumber = 10,
                    Digitized = true,
                    ClosingDate = new DateTime(2023, 1, 1),
                    DestroyDate = new DateTime(2028, 1, 1)
                },
                new RecordItemModel
                {
                    ID = new Guid("22222222-2222-2222-2222-222222222222"),
                    CIS = 1002,
                    BarCode = "24-987655",
                    RecordType = "Type B",
                    BoxNumber = 20,
                    Digitized = false,
                    ClosingDate = new DateTime(2024, 1, 1),
                    DestroyDate = new DateTime(2029, 1, 1)
                }
            );
        }
    }
}
