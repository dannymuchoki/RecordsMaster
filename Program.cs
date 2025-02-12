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

        // Use ApplicationUser with Identity and add role support TOOK TOO LONG TO WORK OUT OMG
        builder.Services.AddDefaultIdentity<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        builder.Services.AddControllersWithViews();

        var app = builder.Build();

        // Apply migrations and seed data dynamically.
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            dbContext.Database.Migrate(); // Apply migrations

            // Now pass the roleManager along with dbContext and userManager.
            await SeedData.SeedDataAsync(dbContext, userManager, roleManager);
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

        app.Run();
    }
}