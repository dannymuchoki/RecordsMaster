using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using RecordsMaster.Data;
using RecordsMaster.Models; // For ApplicationUser
using RecordsMaster.Services;
// For ActiveDirectory  (below)
//using Microsoft.AspNetCore.Authentication.OpenIdConnect;
// using Microsoft.Identity.Web; 

public class Program
{
    public static async Task Main(string[] args)
    {
        // Create a builder for the web application
        // This is the entry point for the ASP.NET Core application.
        // It sets up the configuration, services, and middleware for the application.
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

        // Configure Identity with roles and ApplicationUser
        builder.Services.AddDefaultIdentity<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        // Add custom email sender service. Configuration is in appsettings.json 
        builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

        // Print labels 
        builder.Services.AddScoped<LabelPrintService>();
        

        // Add authentication with Microsoft Identity Web (Azure AD). No idea if this works yet.
        //builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        //    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

        //builder.Services.AddAuthorization();

        // Add MVC services to the container. This adds support for controllers and views, enabling MVC pattern.
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

        // Middleware to handle HTTPS redirection, static files, routing, authentication, and authorization.
        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        // Map controller routes
        // This sets up the routing for the application, defining how URLs map to controllers and actions
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
}