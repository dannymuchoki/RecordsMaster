using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace RecordsMaster.Data
{
    public class SqliteAppDbContextFactory : IDesignTimeDbContextFactory<SqliteAppDbContext>

    /*
        Context factory makes it less annoying to run migrations. 
    */
    {
        public SqliteAppDbContext CreateDbContext(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();

            var options = new DbContextOptionsBuilder<SqliteAppDbContext>()
                .UseSqlite(config.GetConnectionString("DefaultConnection"))
                .Options;

            return new SqliteAppDbContext(options);
        }
    }
}
