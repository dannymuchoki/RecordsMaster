using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using RecordsMaster.Models; // For ApplicationUser

namespace RecordsMaster.Controllers
{
    // This controller handles user account management, including login, registration, and user roles. The views are in the Views/Account folder.
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }
        // This action returns a view that lists all users and their roles.
        // It uses a tuple to pair each user with a boolean indicating if they are in the "User" role.
        // The view will display the user email and whether they are a regular user or not.
        public async Task<IActionResult> UserRoles()
        {
            var users = _userManager.Users.ToList();
            var model = new List<(ApplicationUser User, bool IsUserRole, bool IsAdminRole)>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                model.Add((user, roles.Contains("User"), roles.Contains("Admin")));
            }
            return View(model);
        }


        [Authorize]
        [HttpPost]
        public async Task<IActionResult> SetUserRole(string userId, string role)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                var currentRoles = await _userManager.GetRolesAsync(user);

                if (role == "None")
                {
                    // Remove both roles
                    if (currentRoles.Contains("Admin"))
                        await _userManager.RemoveFromRoleAsync(user, "Admin");
                    if (currentRoles.Contains("User"))
                        await _userManager.RemoveFromRoleAsync(user, "User");
                }
                // If the user is in the Admin role and the requested role is "User", remove Admin and add User
                else if (currentRoles.Contains("Admin") && role == "User")
                {
                    await _userManager.RemoveFromRoleAsync(user, "Admin");
                    await _userManager.AddToRoleAsync(user, "User");
                }
                // If the user is not in Admin, just ensure they are in User role
                else if (role == "User")
                {
                    if (currentRoles.Contains("Admin"))
                        await _userManager.RemoveFromRoleAsync(user, "Admin");
                    if (!currentRoles.Contains("User"))
                        await _userManager.AddToRoleAsync(user, "User");
                }
                // If the role is Admin, add Admin and remove User
                else if (role == "Admin")
                {
                    if (currentRoles.Contains("User"))
                        await _userManager.RemoveFromRoleAsync(user, "User");
                    if (!currentRoles.Contains("Admin"))
                        await _userManager.AddToRoleAsync(user, "Admin");
                }
            }
            return RedirectToAction(nameof(UserRoles));
        }

        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            var result = await _signInManager.PasswordSignInAsync(email, password, false, false);
            if (result.Succeeded)
            {
                return RedirectToAction("Index", "Home");
            }
            ModelState.AddModelError("", "Invalid login attempt");


            return View();
        }

        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(string email, string password)
        {
            // Create a new ApplicationUser.
            var user = new ApplicationUser { UserName = email, Email = email };
            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", "Home");
            }
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
    }
}