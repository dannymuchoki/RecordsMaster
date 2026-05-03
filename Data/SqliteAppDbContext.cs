using Microsoft.EntityFrameworkCore;

namespace RecordsMaster.Data
{
    /* 
        SqliteAppDbContext inherits from AppDbContext, so it makes the same tables and relationships. 
        At first I thought this was all I needed, but I kept getting type errors at runtime. 

        The solution was weird (to me at least). At design time, instantiate a generic DbContext using
        the DesignTimeDbContextFactory to run migrations. This avoided the type errors. The only thing
        I added was a protected class in AppDbContext. 
    */
    public class SqliteAppDbContext : AppDbContext
    {
        /*
            The sole difference is that at design time, SqliteAppDbContext pulls in parameters from
            the SqliteAppDbContextFactory. Using the IDesignTimeDbContextFactory, it instantiates
            a generic DbContext with SQLite parameters. 
        */
        public SqliteAppDbContext(DbContextOptions<SqliteAppDbContext> options) : base(options) { }
    }
}
