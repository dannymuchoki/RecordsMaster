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
        if (builder.Environment.IsDevelopment())
            {
                builder.Services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
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

        builder.Services.Configure<DataProtectionTokenProviderOptions>(opt =>
            opt.TokenLifespan = TimeSpan.FromHours(1)); // Reset token is valid for one hour


        builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
        //builder.Services.AddScoped<LabelPrintService>(); // useful in development. Keep it around because PDFs were annoying to work out. 
        builder.Services.AddScoped<PDFPrintService>();

        // Add MVC services to the container.
        builder.Services.AddControllersWithViews();

        var app = builder.Build();

        // Apply migrations and seed data dynamically.
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var dbContext = services.GetRequiredService<AppDbContext>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var configuration = builder.Configuration; // retrieve configuration from appsettings.json

            // TEMPORARY: register already-applied migrations so MigrateAsync doesn't try to re-run them.
            // Remove this block after running dotnet run once. Then stop the app. 
            if (app.Environment.IsProduction())
            {
                dbContext.Database.ExecuteSqlRaw(@"
                    IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260304135348_InitialMigration')
                        INSERT INTO [__EFMigrationsHistory] VALUES ('20260304135348_InitialMigration', '9.0.1');
                    IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260304135403_AddIdentityRoleSupport')
                        INSERT INTO [__EFMigrationsHistory] VALUES ('20260304135403_AddIdentityRoleSupport', '9.0.1');
                    IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260328113713_AddPreBarCodeCheckoutHistory')
                        INSERT INTO [__EFMigrationsHistory] VALUES ('20260328113713_AddPreBarCodeCheckoutHistory', '9.0.1');
                ");
            }

            //After this dotnet ef migrations add AddPreBarCodeRecordIdToCheckoutHistory

            await dbContext.Database.MigrateAsync();

            // Custom method for seeding data
            await SeedData.SeedDataAsync(dbContext, userManager, roleManager, configuration);
            // If users have records checked out already, this will associate the user with the record and create their account
            await SeedDatabase(dbContext, userManager);
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
    


    private static async Task SeedDatabase(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        var projectDirectory = Directory.GetCurrentDirectory();
        var csvFilePath = Path.Combine(projectDirectory, "file.csv");

        if (File.Exists(csvFilePath))
        {
            var records = CsvRecordReader.ReadRecordsFromCsv(csvFilePath).ToList();
            var existingKeys = context.RecordItems
                .Select(r => new { r.CIS, r.BarCode })
                .ToHashSet();

            foreach (var record in records)
            {
                if (existingKeys.Contains(new { record.CIS, record.BarCode }))
                    continue;

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
                    DestroyDate = new DateTime(2028, 1, 1)
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
                    DestroyDate = new DateTime(2029, 1, 1)
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