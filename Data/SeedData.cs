using Microsoft.AspNetCore.Identity;
using RecordsMaster.Models;

namespace RecordsMaster.Data
{
    public static class SeedData
    {
        // Creates the admin user and test user.
        public static async Task SeedDataAsync(AppDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            // Seed roles before seeding users
            await SeedRolesAsync(roleManager);
            await SeedUsersAsync(userManager);  // Creates the admin user
            await SeedTestUserAsync(userManager); // Call to seed 'test-user'
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

        private static async Task SeedUsersAsync(UserManager<ApplicationUser> userManager)
        {
            string defaultEmail = "admin@example.com";
            string defaultPassword = "Admin@123"; // Replace with a stronger password for production

            ApplicationUser adminUser = await userManager.FindByEmailAsync(defaultEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = defaultEmail,
                    Email = defaultEmail,
                    EmailConfirmed = true
                };

                IdentityResult result = await userManager.CreateAsync(adminUser, defaultPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
        }

        private static async Task SeedTestUserAsync(UserManager<ApplicationUser> userManager)
        {
            string testUserEmail = "test@example.com"; 
            string testUserPassword = "Test@123"; // passwords can't have hyphens
        
            // Check if 'test-user@example.com' already exists
            var existingUser = await userManager.FindByEmailAsync(testUserEmail);
            if (existingUser == null)
            {
                var testUser = new ApplicationUser
                {
                    UserName = testUserEmail,
                    Email = testUserEmail,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(testUser, testUserPassword);
                // Assign the role
                if (result.Succeeded)
                {
                    // assign the 'User' role:
                    await userManager.AddToRoleAsync(testUser, "User");
                }
                else
                {
                    // Handle errors
                    foreach (var error in result.Errors)
                    {
                        Console.WriteLine($"Error creating test user: {error.Description}");
                    }
                }
            }
        }
    }
}