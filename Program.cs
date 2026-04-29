using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RecordsMaster.Data;
using RecordsMaster.Models;
using RecordsMaster.Services;
using RecordsMaster.Utilities;
using Microsoft.Extensions.DependencyInjection;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Use SQLite in development. Use SqlServer in Prod.
        // SQLite migrations live in Migrations/SQLite/ (context: SqliteAppDbContext).
        // SQL Server migrations live in Migrations/ (context: AppDbContext).
        if (builder.Environment.IsDevelopment())
            {
                builder.Services.AddDbContext<SqliteAppDbContext>(options =>
                    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
                // Forward AppDbContext resolution to SqliteAppDbContext so Identity and the rest of the app work unchanged.
                builder.Services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<SqliteAppDbContext>());
            }
            else if (builder.Environment.IsProduction())
            {
                // Make sure you have the rights to run migrations and create tables on this server
                builder.Services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServerConnection")));
            }

        builder.Services.AddDefaultIdentity<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        builder.Services.ConfigureApplicationCookie(options =>
            {
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                options.LoginPath = "/Account/Login";
            });


        builder.Services.Configure<DataProtectionTokenProviderOptions>(opt =>
            opt.TokenLifespan = TimeSpan.FromHours(1)); // Reset token is valid for one hour


        builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
        //builder.Services.AddScoped<LabelPrintService>(); // useful in development. Keep it around because PDFs were annoying to work out. 
        builder.Services.AddScoped<PDFPrintService>();

        // Add MVC services to the container.
        builder.Services.AddControllersWithViews();

        var app = builder.Build();

        // Run with --download-migrations to pull __EFMigrationsHistory from the DB and write stub .cs files to Migrations/
        if (args.Contains("--download-migrations"))
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var migrationsDir = app.Environment.IsDevelopment()
                ? Path.Combine(Directory.GetCurrentDirectory(), "Migrations", "SQLite")
                : Path.Combine(Directory.GetCurrentDirectory(), "Migrations");
            await DownloadMigrationsAsync(dbContext, migrationsDir);
            return;
        }

        // Apply migrations and seed data dynamically.
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var dbContext = services.GetRequiredService<AppDbContext>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var configuration = builder.Configuration; // retrieve configuration from appsettings.json

            /* 
            YOU DO NOT NEED TO RUN THIS AGAIN. This was a one-time fix. I left the code here because it was annoying to figure out, and if the issue occurs again, you now know that you can run SQL queries right here in Program.cs. 
            
            Basically, the migrations, database, and schema were out of alignment. Probably my mistake during development. Instead of dropping the SQL table and starting over, I created a schema fix and registered all migrations manually for production. This worked for a bit, but ultimately it was easier to get a new database and re-run the migrations. 
            
            This ran exactly once at at the first dotnet run (no database update required.) MAKE SURE YOU ARE WRITING TO THE RIGHT DATABASE! Change the Development server to SQL above to be double-plus sure. 

            if (dbContext.Database.IsSqlServer())
            {
                // Step 1: ensure history table exists before referencing it
                dbContext.Database.ExecuteSqlRaw(@"
                    IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
                    CREATE TABLE [__EFMigrationsHistory] (
                        [MigrationId] nvarchar(150) NOT NULL,
                        [ProductVersion] nvarchar(32) NOT NULL,
                        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
                    )");

                // Step 2: register already-applied migrations (each as its own call)
                dbContext.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260304135348_InitialMigration') INSERT INTO [__EFMigrationsHistory] VALUES ('20260304135348_InitialMigration', '9.0.1')");
                dbContext.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260304135403_AddIdentityRoleSupport') INSERT INTO [__EFMigrationsHistory] VALUES ('20260304135403_AddIdentityRoleSupport', '9.0.1')");
                dbContext.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260328113713_AddPreBarCodeCheckoutHistory') INSERT INTO [__EFMigrationsHistory] VALUES ('20260328113713_AddPreBarCodeCheckoutHistory', '9.0.1')");

                // Step 3: apply the new schema change
                dbContext.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CheckoutHistory') AND name = 'PreBarCodeRecordId') ALTER TABLE [CheckoutHistory] ADD [PreBarCodeRecordId] uniqueidentifier NULL");
                dbContext.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('CheckoutHistory') AND name = 'IX_CheckoutHistory_PreBarCodeRecordId') CREATE INDEX [IX_CheckoutHistory_PreBarCodeRecordId] ON [CheckoutHistory] ([PreBarCodeRecordId])");

                // Step 4: register the new migration so MigrateAsync skips it
                dbContext.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260420222218_AddPreBarCodeRecordIdToCheckoutHistory') INSERT INTO [__EFMigrationsHistory] VALUES ('20260420222218_AddPreBarCodeRecordIdToCheckoutHistory', '9.0.1')");
            }
            */

            //await dbContext.Database.MigrateAsync();

            // Custom method for seeding data
            await SeedData.SeedDataAsync(dbContext, userManager, roleManager, configuration);
            // If users have records checked out already, this will associate the user with the record and create their account
            await SeedDatabase(dbContext, userManager);
            // Populate CheckoutHistory from a checkout.csv file if present
            await SeedCheckoutHistory(dbContext, userManager);
            // btw, SeedData is in the Data directory.
            
        }

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles(); //.NET 9

        //For .NET 10 use: app.MapStaticAssets();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");
        
        //For .NET10: app.MapRaxorPages()

        app.MapControllerRoute(
            name: "records-list",
            pattern: "{controller=Home}/{action=List}");

        //For .NET10: app.MapRaxorPages()

        app.MapControllerRoute(
            name: "upload",
            pattern: "Upload",
            defaults: new { controller = "Upload", action = "Upload" });
        
        //For .NET10: app.MapRaxorPages()

        app.MapControllerRoute(
            name: "update",
            pattern: "Update",
            defaults: new { controller = "Update", action = "Update" });
        
        //For .NET10: app.MapRaxorPages()

        app.MapControllerRoute(
            name: "labels",
            pattern: "labels/generate/{start?}/{end?}",
            defaults: new { controller = "Labels", action = "GenerateLabels" });
        
        //For .NET10: app.MapRaxorPages()

        app.MapControllerRoute(
            name: "labelsForm",
            pattern: "labels/form",
            defaults: new { controller = "Labels", action = "GenerateLabelsForm" });
        
        //For .NET10: app.MapRaxorPages()

        await app.RunAsync();
    }
    

    /* Get the migrations from database just to sanity check */
    private static async Task DownloadMigrationsAsync(AppDbContext dbContext, string outputDir)
    {
        var migrations = new List<(string MigrationId, string ProductVersion)>();

        await using var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT MigrationId, ProductVersion FROM __EFMigrationsHistory ORDER BY MigrationId";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            migrations.Add((reader.GetString(0), reader.GetString(1)));

        if (migrations.Count == 0)
        {
            Console.WriteLine("No migrations found in __EFMigrationsHistory.");
            return;
        }

        Directory.CreateDirectory(outputDir);

        foreach (var (migrationId, productVersion) in migrations)
        {
            var underscoreIndex = migrationId.IndexOf('_');
            var name = underscoreIndex >= 0 ? migrationId[(underscoreIndex + 1)..] : migrationId;

            var migrationContent = $$"""
                            using Microsoft.EntityFrameworkCore.Migrations;

                            #nullable disable

                            namespace RecordsMaster.Migrations
                            {
                                public partial class {{name}} : Migration
                                {
                                    protected override void Up(MigrationBuilder migrationBuilder) { }
                                    protected override void Down(MigrationBuilder migrationBuilder) { }
                                }
                            }
                            """;

                                        var designerContent = $$"""
                            // <auto-generated />
                            using Microsoft.EntityFrameworkCore;
                            using Microsoft.EntityFrameworkCore.Infrastructure;
                            using Microsoft.EntityFrameworkCore.Migrations;
                            using RecordsMaster.Data;

                            #nullable disable

                            namespace RecordsMaster.Migrations
                            {
                                [DbContext(typeof(AppDbContext))]
                                [Migration("{{migrationId}}")]
                                partial class {{name}}
                                {
                                    protected override void BuildTargetModel(ModelBuilder modelBuilder)
                                    {
                            #pragma warning disable 612, 618
                                        modelBuilder.HasAnnotation("ProductVersion", "{{productVersion}}");
                            #pragma warning restore 612, 618
                                    }
                                }
                            }
                            """;

            var migrationPath = Path.Combine(outputDir, $"{migrationId}.cs");
            var designerPath = Path.Combine(outputDir, $"{migrationId}.Designer.cs");

            await File.WriteAllTextAsync(migrationPath, migrationContent);
            Console.WriteLine($"Written: {migrationPath}");

            await File.WriteAllTextAsync(designerPath, designerContent);
            Console.WriteLine($"Written: {designerPath}");
        }

        Console.WriteLine($"Done. {migrations.Count} migration(s) written to {outputDir}");
    }

    
    /* 
        You can use the downloaded checkout history .csv from 'Download All' .zip file here. No fancy mapping required.
        It will even re-create the users for you. 
    */
    private static async Task SeedCheckoutHistory(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        var csvFilePath = Path.Combine(Directory.GetCurrentDirectory(), "checkout_history_backup.csv");

        if (!File.Exists(csvFilePath))
            return;

        using var reader = new StreamReader(csvFilePath);
        using var csv = new CsvHelper.CsvReader(reader, new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null
        });

        csv.Read();
        csv.ReadHeader();

        var existingKeys = context.CheckoutHistory
            .Select(h => new { h.RecordItemId, h.UserId, h.CheckedOutDate })
            .ToHashSet();

        while (csv.Read())
        {
            var barCode = csv.GetField<string>("BarCode")?.Trim();
            var checkedOutTo = csv.GetField<string>("CheckedOutTo")?.Trim();
            var checkedOutDateRaw = csv.GetField<string>("CheckedOutDate")?.Trim();
            var returnedDateRaw = csv.TryGetField<string>("ReturnedDate", out var rd) ? rd?.Trim() : null;

            if (string.IsNullOrWhiteSpace(barCode) || string.IsNullOrWhiteSpace(checkedOutTo) || string.IsNullOrWhiteSpace(checkedOutDateRaw))
                continue;

            if (!DateTime.TryParse(checkedOutDateRaw, out var checkedOutDate))
                continue;

            checkedOutDate = DateTime.SpecifyKind(checkedOutDate, DateTimeKind.Utc);

            DateTime? returnedDate = null;
            if (!string.IsNullOrWhiteSpace(returnedDateRaw) && DateTime.TryParse(returnedDateRaw, out var rd2))
                returnedDate = DateTime.SpecifyKind(rd2, DateTimeKind.Utc);

            var record = context.RecordItems.FirstOrDefault(r => r.BarCode == barCode);
            if (record == null)
                continue;

            var user = await userManager.FindByEmailAsync(checkedOutTo)
                       ?? await userManager.FindByNameAsync(checkedOutTo);

            if (user == null)
            {
                user = new ApplicationUser { UserName = checkedOutTo, Email = checkedOutTo };
                await userManager.CreateAsync(user);
                await userManager.AddToRoleAsync(user, "User");
            }

            var key = new { RecordItemId = (Guid?)record.ID, UserId = user.Id, CheckedOutDate = checkedOutDate };
            if (existingKeys.Contains(key))
                continue;

            context.CheckoutHistory.Add(new CheckoutHistory
            {
                RecordItemId = record.ID,
                UserId = user.Id,
                CheckedOutDate = checkedOutDate,
                ReturnedDate = returnedDate
            });

            existingKeys.Add(key);
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedDatabase(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        var projectDirectory = Directory.GetCurrentDirectory();
        var csvFilePath = Path.Combine(projectDirectory, "file.csv");

        if (File.Exists(csvFilePath))
        {
            var records = CsvRecordReader.ReadRecordsFromCsv(csvFilePath).ToList();
            var existingKeys = new HashSet<string>(
                context.RecordItems
                    .Select(r => new { r.CIS, r.BarCode })
                    .AsEnumerable()
                    .Select(r => r.CIS + "|" + (r.BarCode ?? ""))
            );

            var uploadedBy = $"Initial upload - {DateTime.UtcNow:yyyy-MM-dd}";

            foreach (var record in records)
            {
                if (existingKeys.Contains(record.CIS + "|" + (record.BarCode ?? "")))
                    continue;

                record.UploadedBy = uploadedBy;

                if (record.CheckedOut && !string.IsNullOrWhiteSpace(record.CheckedOutToName))
                {
                    var user = await userManager.FindByNameAsync(record.CheckedOutToName)
                               ?? await userManager.FindByEmailAsync(record.CheckedOutToName);

                    if (user == null)
                    {
                        user = new ApplicationUser
                        {
                            UserName = record.CheckedOutToName,
                            Email = record.CheckedOutToName
                        };
                        await userManager.CreateAsync(user);
                        await userManager.AddToRoleAsync(user, "User");
                    }

                    record.CheckedOutToId = user.Id;
                    record.CheckoutHistoryRecords.Add(new CheckoutHistory
                    {
                        RecordItemId = record.ID,
                        UserId = user.Id,
                        CheckedOutDate = DateTime.UtcNow
                    });
                }

                context.RecordItems.Add(record);
            }

            await context.SaveChangesAsync();
        }
        else if (!context.SeedHistories.Any(s => s.SeedType == "Initial"))
        {
            context.RecordItems.AddRange([
                new()
                {
                    ID = new Guid("11111111-1111-1111-1111-111111111111"),
                    CIS = "1001",
                    BarCode = "93-98765",
                    RecordType = "Type A",
                    Location = "Records Room",
                    BoxNumber = 10,
                    Digitized = true,
                    ClosingDate = new DateTime(2023, 1, 1),
                    DestroyDate = new DateTime(2028, 1, 1),
                    UploadedBy = $"Initial upload - {DateTime.UtcNow:yyyy-MM-dd}"
                },
                new()
                {
                    ID = new Guid("22222222-2222-2222-2222-222222222222"),
                    CIS = "1002D",
                    BarCode = "93-98766",
                    RecordType = "Type B",
                    Location = "Records Room",
                    BoxNumber = 20,
                    Digitized = false,
                    ClosingDate = new DateTime(2024, 1, 1),
                    DestroyDate = new DateTime(2029, 1, 1),
                    UploadedBy = $"Initial upload - {DateTime.UtcNow:yyyy-MM-dd}"
                }
            ]);

            context.SeedHistories.Add(new SeedHistory
            {
                SeedType = "Initial",
                AppliedOn = DateTime.Now
            });

            await context.SaveChangesAsync();
        }
    }
}