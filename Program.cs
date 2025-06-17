using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using RecordsMaster.Data;
using RecordsMaster.Models; // For ApplicationUser

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

        // Configure Identity with roles and ApplicationUser
        builder.Services.AddDefaultIdentity<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        builder.Services.AddControllersWithViews();

        var app = builder.Build();

        // Apply migrations and seed data dynamically.
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var dbContext = services.GetRequiredService<AppDbContext>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var configuration = builder.Configuration; // retrieve configuration

            await dbContext.Database.MigrateAsync(); // Apply migrations asynchronously

            // Call seed method, passing configuration
            await SeedData.SeedDataAsync(dbContext, userManager, roleManager, configuration);
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
            pattern: "{controller=Home}/{action=Index}/{id?}")
            .WithStaticAssets();

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

        await app.RunAsync();
    }
}