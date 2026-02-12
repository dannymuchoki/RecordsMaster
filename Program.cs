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

            await dbContext.Database.MigrateAsync();

            // Custom method for seeding data
            await SeedData.SeedDataAsync(dbContext, userManager, roleManager, configuration);
            await SeedDatabase(dbContext);
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
        app.UseStaticFiles();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        app.MapControllerRoute(
            name: "records-list",
            pattern: "{controller=Home}/{action=List}");

        app.MapControllerRoute(
            name: "search",
            pattern: "Search/{cis?}",
            defaults: new { controller = "RecordItems", action = "Search" });

        app.MapControllerRoute(
            name: "upload",
            pattern: "Upload",
            defaults: new { controller = "Upload", action = "Upload" });

        app.MapControllerRoute(
            name: "labels",
            pattern: "labels/generate/{start?}/{end?}",
            defaults: new { controller = "Labels", action = "GenerateLabels" });

        app.MapControllerRoute(
            name: "labelsForm",
            pattern: "labels/form",
            defaults: new { controller = "Labels", action = "GenerateLabelsForm" });

        await app.RunAsync();
    }

    private static async Task SeedDatabase(AppDbContext context)
    {
        if (!context.SeedHistories.Any(s => s.SeedType == "Initial"))
        {
            var projectDirectory = Directory.GetCurrentDirectory();
            var csvFilePath = Path.Combine(projectDirectory, "file.csv");

            if (File.Exists(csvFilePath))
            {
                var records = CsvRecordReader.ReadRecordsFromCsv(csvFilePath);
                context.RecordItems.AddRange(records);
            }
            else
            {
                context.RecordItems.AddRange(new[]
                {
                    new RecordItemModel
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
                    new RecordItemModel
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
                });
            }

            // Sets up the initial seeding of the database. See the SeedHistory class in RecordItemModel.cs
            context.SeedHistories.Add(new SeedHistory
            {
                SeedType = "Initial",
                AppliedOn = DateTime.Now
            });

            await context.SaveChangesAsync();
        }
    }
}