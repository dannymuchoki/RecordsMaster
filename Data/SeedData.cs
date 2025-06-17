using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using RecordsMaster.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecordsMaster.Data
{
    public static class SeedData
    {
        // Seed roles and users, loading user info from configuration.
        public static async Task SeedDataAsync(AppDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IConfiguration configuration)
        {
            // Seed roles before seeding users
            await SeedRolesAsync(roleManager);

            // Load user seed data from configuration
            var userSeedData = configuration.GetSection("UserSeedData").Get<Dictionary<string, UserSeedInfo>>();

            if (userSeedData != null)
            {
                foreach (var userEntry in userSeedData)
                {
                    var userInfo = userEntry.Value;
                    await SeedUserAsync(userManager, userInfo.Email, userInfo.Password, userInfo.Role);
                }
            }
        }

        private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            }
            if (!await roleManager.RoleExistsAsync("User"))
            {
                await roleManager.CreateAsync(new IdentityRole("User"));
            }
        }

        // Class to model user seed info
        public class UserSeedInfo
        {
            public string Email { get; set; }
            public string Password { get; set; }
            public string Role { get; set; }
        }

        private static async Task SeedUserAsync(UserManager<ApplicationUser> userManager, string email, string password, string role)
        {
            var existingUser = await userManager.FindByEmailAsync(email);
            if (existingUser == null)
            {
                var newUser = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(newUser, password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(newUser, role);
                }
                else
                {
                    // Log errors if needed
                    foreach (var error in result.Errors)
                    {
                        Console.WriteLine($"Error creating user {email}: {error.Description}");
                    }
                }
            }
        }
    }
}