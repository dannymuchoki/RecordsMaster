using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
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
        private readonly IEmailSender _emailSender;

        // Slightly inspired by the RecordCheckoutController
        public PasswordResetController(UserManager<ApplicationUser> userManager, ILogger<PasswordResetController> logger, IEmailSender emailSender)
        {
            _userManager = userManager;
            _logger = logger;
            _emailSender = emailSender;
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

        public class ConfirmPasswordResetViewModel
        {
            [Required]
            public string UserId { get; set; } = string.Empty;

            [Required]
            public string Token { get; set; } = string.Empty;

            [Required]
            [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
            [DataType(DataType.Password)]
            [Display(Name = "New Password")]
            public string NewPassword { get; set; } = string.Empty;

            [DataType(DataType.Password)]
            [Display(Name = "Confirm New Password")]
            [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
            public string ConfirmPassword { get; set; } = string.Empty;

            public string? Message { get; set; }
        }

        [HttpGet]
        public IActionResult ResetPassword()
        {
            return View(new PasswordResetViewModel());
        }

        /*
            This generates a one-time password reset token and sends a reset link to the registered email address.
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
                try
                {
                    // Generate password reset token
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);

                    // Build the reset link
                    var resetLink = Url.Action(
                        "ConfirmPasswordReset",
                        "PasswordReset",
                        new { userId = user.Id, token = token },
                        protocol: Request.Scheme
                    );

                    // Email the reset link
                    var subject = "Password Reset Request";
                    var message = $@"
                        <p>A password reset has been requested for your account.</p>
                        <p>Click the link below to reset your password. This link is valid for a limited time and can only be used once:</p>
                        <p><a href='{resetLink}'>Reset Password</a></p>
                        <p>If you did not request this password reset, please ignore this email.</p>";

                    try
                    {
                        await _emailSender.SendEmailAsync(email, subject, message);
                        _logger.LogInformation("Password reset link sent to {Email}", email);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send password reset email to {Email}", email);
                        Console.WriteLine(message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception while generating password reset token for {Email}", email);
                    model.Message = "An error occurred while processing the password reset request.";
                    TempData["PasswordResetMessage"] = model.Message;
                    return RedirectToAction("Index", "Home");
                }
            }

            // Never tell the requestor whether the user exists.
            model.Message = "If the user exists, a password reset link has been sent to the email address.";
            TempData["PasswordResetMessage"] = model.Message;

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmPasswordReset(string userId, string token)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            {
                TempData["PasswordResetMessage"] = "Invalid password reset link.";
                return RedirectToAction("Index", "Home");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["PasswordResetMessage"] = "Invalid password reset link.";
                return RedirectToAction("Index", "Home");
            }

            var model = new ConfirmPasswordResetViewModel
            {
                UserId = userId,
                Token = token
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPasswordReset(ConfirmPasswordResetViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                model.Message = "Invalid password reset request.";
                return View(model);
            }

            // Reset the password using the token
            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);

            if (result.Succeeded)
            {
                _logger.LogInformation("Password successfully reset for user {UserId}", model.UserId);
                TempData["PasswordResetMessage"] = "Your password has been successfully reset. You can now log in with your new password.";
                return RedirectToAction("Index", "Home");
            }
            else
            {
                _logger.LogWarning("Failed to reset password for user {UserId}: {Errors}",
                    model.UserId, string.Join("; ", result.Errors.Select(e => e.Description)));

                // Check if the token is invalid or expired
                if (result.Errors.Any(e => e.Code == "InvalidToken"))
                {
                    model.Message = "The password reset link has expired or is invalid. Please request a new password reset.";
                }
                else
                {
                    model.Message = "Failed to reset password: " + string.Join("; ", result.Errors.Select(e => e.Description));
                }

                return View(model);
            }
        }
    }
}