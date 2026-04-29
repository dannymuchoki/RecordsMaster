using Microsoft.EntityFrameworkCore;

namespace RecordsMaster.Data
{
    public class SqliteAppDbContext : AppDbContext
    {
        /* 
           When making a SqliteAppDbContext, you call the parent AppDbContext. This says run AppDbContext, and then pass 'options'. 
           
           But options is empty, so you've instantiated a parent class using this inherited method. 

           In AppDbContext you have (DbContextOptions options)
           Here creating a new class SqliteAppDbContext(DbContextOptions, and the option you're passing is is SqliteAppDbContext)
        */
        public SqliteAppDbContext(DbContextOptions<SqliteAppDbContext> options) : base(options) { }
    }
}
