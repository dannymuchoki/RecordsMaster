using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace RecordsMaster.Data
{
    public class SqliteAppDbContextFactory : IDesignTimeDbContextFactory<SqliteAppDbContext>

    /*
        When EF Core starts, it looks for a DbContext. It looks in Program.cs, a parameter-less constructor, or for an IDesignTimeDbContextFactory. EF looks for the factory first. 

        IDesignTimeDbCContextFactory is a generic interface that makes it easier to have different databases/DbContexts. 
    
        Read more here: https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation?tabs=dotnet-core-cli and
        https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.design.idesigntimedbcontextfactory-1?view=efcore-10.0 

        When EF Core needs a SqliteAppDbContext this context factory makes one at design time (not while the app is actually running).
        IDesignTimeDbContextFactory is the interface that EF Core uses make all this happen.

        You call the different DbContexts using the --context flag:

        dotnet ef migrations add NameOfMigration --context AppDbContext (or SqliteAppDbContext)
        dotnet ef database update  --context AppDbContext (or SqliteAppDbContext).

        Why did I do all this?
        
        dotnet is strongly typed. In this app, the default AppDbContext inherits from EF's IdentityDBContext (which itself
        inherits from DbContext, but contains authentication and user role parameters). 
        
        At first I thought I could just make a SqliteAppDbContext that inherits from AppDbContext, but I kept getting type errors (though this inheritance SHOULD be possible because it DOES inherit the tables and relationships from AppDbContext).

        The migrations between SQL and SQLite also began to drift because SQL supports features that SQLite doesn't. The migrations that
        worked for SQL wouldn't work for SQLite (and vice versa). 
        
        My solution was, at design-time (i.e when running initial migrations):
            1. Instantiate a DbContext using a generic interface (like IDesignTimeDbContextFactory) using this SqliteAppDbContextFactory, and then
            2. It's this instantiated DbContext that can inherit from AppDbContext. 
            3. Pass the SQLite parameters to EF at migration. 

        It works like this:
            IDesignTimeDbContextFactory<T>
                    ↑ (implemented by)
            SqliteAppDbContextFactory

            SqliteAppDbContext (calls the SqliteAppDbContextFactory)
                    ↑ (inherits from)
            AppDbContext
                    ↑ (inherits from)
            IdentityDbContext<ApplicationUser>

        
    */
    {
        public SqliteAppDbContext CreateDbContext(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                // I don't have an appsettings.Development.json but you can if you want.
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();

            var options = new DbContextOptionsBuilder<SqliteAppDbContext>()
                // Look in the appsettings.json for the "DefaultConnection" string
                .UseSqlite(config.GetConnectionString("DefaultConnection"))
                .Options;

            return new SqliteAppDbContext(options);
        }
    }
}
