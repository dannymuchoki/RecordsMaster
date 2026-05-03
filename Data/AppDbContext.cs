using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RecordsMaster.Models;
using System.IO;
using RecordsMaster.Utilities;

namespace RecordsMaster.Data
{
    /* 
       Notes for later since it's been a minute since C++ and Python doesn't 
       have interfaces. 
       
       This is the core database schema and its rules, so you can make
       other DbContexts that inherit from this AppDbContext when it comes to
       creating database tables. 
       
       There's a DbContext for SQLite already, and you can make others for whatever database
       you want at design time using the EF IDesignTimeDbContextFactory. 
       
       Look at SqliteAppDbContextFactory.cs for an example. 

       The SqliteAppDbContextFactory is an interface that (at design time, not runtime): 
        1. Creates a SQLite DbContext 
        2. The SQLite DbContext inherits from AppDbContext
        3. The protected constructor allows for flexible inheritance without causing type errors. 

        For reasons I haven't been able to work out, AppDbContext and SqliteAppDbContexts are different
        types when it comes to generics. (https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/generics).
        This is where the type errors I experienced seem to come from. The design time solution solved it. 
       
       */
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        /*
            Why IdentityDbContext? Because it automatically includes authentication and user roles
            and inherits from the main DbContext class. Easier to do logins this way. 
            
            Whenever you run migrations, dotnet looks for a factory and calls for
            CreateDbContext(args go here). In AppDbContext, we just use the IdentityDbContext
            that comes with dotnet. 
            
            With SQLite, we do something different. We create a new DbContext using the DbContextFactory
            which has a CreateDbContext(*args) call. 
            
            The SqliteAppDbContext factory has this
            CreateDbContext call, and passes the SQLite arguments. 

            CreateDbContext for Sqlite loads from the appsettings.json file(s) and creates a 
            DbContext just for SQLite migrations. This is because SQLite and SQL have subtle 
            differences that cause issues during migration. 
            
        */  
        public AppDbContext(DbContextOptions<AppDbContext> options)
        // This is for regular AppDbContext 
            : base(options)
        { }
        protected AppDbContext(DbContextOptions options)
            : base(options)
        
        /* 
            This protected constructor is what powers the inheritance for SqliteAppDbContext. 

            Since the app might not pass a regular AppDbContext, it might pass a unique one like the 
            SqliteAppDbContext which loads from the appsettings.json 

            Why is protected? This should only be called when creating the SQLite DbContext. 

            DbContextOptions will take the arguments in public SqliteAppDbContext CreateDbContext()
            and pass a working DbContext just for SQLite at design time. 
            */
        { }

        // Tables for RecordItemModel entities.
        public DbSet<RecordItemModel> RecordItems { get; set; }
        public DbSet<PreBarCodeRecordModel> PreBarCodeRecords { get; set; }
        public DbSet<SeedHistory> SeedHistories { get; set; }
        public DbSet<CheckoutHistory> CheckoutHistory { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Call the base OnModelCreating to ensure Identity tables are configured.
            base.OnModelCreating(modelBuilder);

            // Configure the RecordItemModel's primary key and properties.
            modelBuilder.Entity<RecordItemModel>().HasKey(r => r.ID);

            modelBuilder.Entity<RecordItemModel>().Property(r => r.BarCode)
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

            // Configure CheckoutHistory relationships
            modelBuilder.Entity<CheckoutHistory>()
                .HasOne(ch => ch.RecordItem)
                .WithMany(r => r.CheckoutHistoryRecords)
                .HasForeignKey(ch => ch.RecordItemId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CheckoutHistory>()
                .HasOne(ch => ch.PreBarCodeRecord)
                .WithMany(r => r.CheckoutHistoryRecords)
                .HasForeignKey(ch => ch.PreBarCodeRecordId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CheckoutHistory>()
                .HasOne(ch => ch.User)
                .WithMany()
                .HasForeignKey(ch => ch.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

/*
This SHOULD be possible though without the factory, but I kept getting type errors. 

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    // DbSets...
}

public class SqliteAppDbContext : AppDbContext
{
    public SqliteAppDbContext(DbContextOptions<SqliteAppDbContext> options)
        : base(options) { }

    // DbSets...
}

*/