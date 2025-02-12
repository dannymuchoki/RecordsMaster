using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RecordsMaster.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RecordsMaster.Data
{
    public static class SeedData
    {
        // Modify SeedDataAsync to accept a RoleManager, in addition to UserManager.
        public static async Task SeedDataAsync(AppDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            // Seed roles before seeding users
            await SeedRolesAsync(roleManager);
            await SeedUsersAsync(userManager);
        }

        // Check for the existence of roles. Create ones that are missing.
        private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            // Example role: "Admin"
            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            }

            // add additional roles here if you want
            // if (!await roleManager.RoleExistsAsync("User"))
            // {
            //     await roleManager.CreateAsync(new IdentityRole("User"));
            // }
        }

        // Seed our admin user and assign the Admin role.
        private static async Task SeedUsersAsync(UserManager<ApplicationUser> userManager)
        {
            string defaultEmail = "admin@example.com";
            string defaultPassword = "Admin@123"; // Replace with a stronger password for production

            // Check if the admin user exists.
            ApplicationUser adminUser = await userManager.FindByEmailAsync(defaultEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = defaultEmail,
                    Email = defaultEmail,
                    EmailConfirmed = true
                    // The CheckedOutRecords navigation property is automatically instantiated
                };

                IdentityResult result = await userManager.CreateAsync(adminUser, defaultPassword);
                if (result.Succeeded)
                {
                    // Assign the "Admin" role to the new user.
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
        }
    }
}