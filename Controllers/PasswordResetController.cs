using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RecordsMaster.Models;
using RecordsMaster.Services;

namespace RecordsMaster.Controllers
{
    public class PasswordResetController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<PasswordResetController> _logger;
        private readonly IConfiguration _config;
        private readonly IEmailSender _emailSender;

        // Slightly inspired by the RecordCheckoutController
        public PasswordResetController(UserManager<ApplicationUser> userManager, ILogger<PasswordResetController> logger, IEmailSender emailSender, IConfiguration config)
        {
            _userManager = userManager;
            _logger = logger;
            _emailSender = emailSender;
            _config = config;
        }

        public class PasswordResetViewModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;

            // Message to display after submit (e.g., success/failure)
            public string? Message { get; set; }
        }

        [HttpGet]
        public IActionResult ResetPassword()
        {
            return View(new PasswordResetViewModel());
        }

        /*
            This changes a user's password and sends it to the registered email address. 
        */
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(PasswordResetViewModel model)
        {
            if (!ModelState.IsValid)
            {

                return RedirectToAction("Index", "Home");
            }

            var email = model.Email.Trim();
            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                string newPassword = GenerateStrongPassword(16);

                try
                {
                    bool hasPassword = await _userManager.HasPasswordAsync(user);
                    if (hasPassword)
                    {
                        var removeResult = await _userManager.RemovePasswordAsync(user);
                        if (!removeResult.Succeeded)
                        {
                            _logger.LogError("Failed to remove existing password for user {Email}: {Errors}",
                                email, string.Join("; ", removeResult.Errors));
                            model.Message = "Failed to reset password.";
                            TempData["PasswordResetMessage"] = model.Message;
                            return RedirectToAction("Index", "Home"); 
                        }
                    }

                    var addResult = await _userManager.AddPasswordAsync(user, newPassword);

                    if (!addResult.Succeeded)
                    {
                        _logger.LogError("Failed to set new password for user {Email}: {Errors}",
                            email, string.Join("; ", addResult.Errors));
                        model.Message = "Failed to reset password.";
                        TempData["PasswordResetMessage"] = model.Message;

                        return RedirectToAction("Index", "Home"); 
                    }

                    // Email the new password
                    var subject = "Your new password";
                    var message = $@"
                        <p><strong>Your new password:</strong> {newPassword}</p>
                        <p>Delete this email after updating your password manager.</p>";

                    var adminEmail = _config["Notification:AdminEmail"];
                    try
                    {
                        await _emailSender.SendEmailAsync(adminEmail, subject, message);
                    }
                    catch
                    {
                        Console.WriteLine(message);
                    }

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception while resetting password for {Email}", email);
                    model.Message = "An error occurred while resetting the password.";

                    TempData["PasswordResetMessage"] = model.Message;

                    return RedirectToAction("Index", "Home"); 
                }
            }

            // Never tell the requestor whether the user exists.
            model.Message = "If the user exists, a password reset has been processed.";

            TempData["PasswordResetMessage"] = model.Message;

            return RedirectToAction("Index", "Home"); 
        }

        private static string GenerateStrongPassword(int length = 16)
        {
            const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lower = "abcdefghijklmnopqrstuvwxyz";
            const string digits = "0123456789";
            const string specials = "!@#$%^&*()-_=+[]{};:,.?/";

            string allChars = upper + lower + digits + specials;

            char[] pwd = new char[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                pwd[0] = upper[GetRandomInt(rng, upper.Length)];
                pwd[1] = lower[GetRandomInt(rng, lower.Length)];
                pwd[2] = digits[GetRandomInt(rng, digits.Length)];
                pwd[3] = specials[GetRandomInt(rng, specials.Length)];

                for (int i = 4; i < length; i++)
                {
                    pwd[i] = allChars[GetRandomInt(rng, allChars.Length)];
                }

                // Shuffle
                for (int i = 0; i < pwd.Length; i++)
                {
                    int j = GetRandomInt(rng, pwd.Length);
                    var temp = pwd[i];
                    pwd[i] = pwd[j];
                    pwd[j] = temp;
                }
            }

            return new string(pwd);

            int GetRandomInt(RandomNumberGenerator rng, int max)
            {
                byte[] buffer = new byte[4];
                rng.GetBytes(buffer);
                uint rnd = BitConverter.ToUInt32(buffer, 0);
                return (int)(rnd % (uint)max);
            }
        }
    }
}