using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RecordsMaster.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RecordsMaster.Data
{
    public static class SeedData
    {
        public static async Task SeedDataAsync(AppDbContext context, UserManager<IdentityUser> userManager)
        {
            await SeedUsersAsync(userManager); // Create the main admin user
        }

        private static async Task SeedUsersAsync(UserManager<IdentityUser> userManager)
        {
            string defaultEmail = "admin@example.com";
            string defaultPassword = "Admin@123"; // replace later

            if (await userManager.FindByEmailAsync(defaultEmail) == null)
            {
                var user = new IdentityUser
                {
                    UserName = defaultEmail,
                    Email = defaultEmail,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(user, defaultPassword);

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, "Admin"); // Assign Admin role
                }
            }
        }
    }
}
