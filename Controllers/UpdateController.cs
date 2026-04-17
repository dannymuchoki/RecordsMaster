using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using CsvHelper;
using RecordsMaster.Services;
using CsvHelper.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using RecordsMaster.Models;
using RecordsMaster.Data;
using System;

namespace RecordsMaster.Controllers
{
    [Authorize]
    public class UpdateController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;

        public UpdateController(AppDbContext context, UserManager<ApplicationUser> userManager, IEmailSender emailSender)
        {
            _context = context;
            _userManager = userManager;
            _emailSender = emailSender;
        }

        // GET: Update/Upload
        public IActionResult Upload()
        {
            return View("Update");
        }

        // POST: Update/Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError("file", "Please upload a valid CSV file.");
                return View();
            }

            // Each entry holds the original row fields plus an "Error" column
            var errorRows = new List<Dictionary<string, string>>();
            var newUsers = new List<ApplicationUser>();
            int rowNumber = 2; // Assuming header is row 1
            string[]? headers = null;

            try
            {
                using (var stream = new StreamReader(file.OpenReadStream()))
                {
                    var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
                    {
                        HasHeaderRecord = true,
                        HeaderValidated = null,
                        MissingFieldFound = null
                    };

                    using (var csv = new CsvReader(stream, csvConfig))
                    {
                        csv.Read();
                        csv.ReadHeader();
                        headers = csv.HeaderRecord;

                        while (csv.Read())
                        {
                            // Capture all raw field values for this row
                            var rawFields = new Dictionary<string, string>();
                            if (headers != null)
                            {
                                foreach (var header in headers)
                                    rawFields[header] = csv.GetField(header) ?? string.Empty;
                            }

                            string barcodeField = csv.GetField(1)!;
                            if (string.IsNullOrWhiteSpace(barcodeField))
                            {
                                rawFields["Error"] = $"Row {rowNumber}: BarCode is empty.";
                                errorRows.Add(rawFields);
                                rowNumber++;
                                continue;
                            }

                            // For updates, just by barcode works
                            var record = _context.RecordItems.FirstOrDefault(r => r.BarCode == barcodeField);
                            if (record == null)
                            {
                                rawFields["Error"] = $"Row {rowNumber}: No record found with BarCode '{barcodeField}'.";
                                errorRows.Add(rawFields);
                                rowNumber++;
                                continue;
                            }

                            if (csv.HeaderRecord != null && csv.HeaderRecord.Length > 2)
                            {
                                var locationField = csv.GetField(3);
                                if (!string.IsNullOrWhiteSpace(locationField))
                                {
                                    record.Location = locationField;
                                }

                                var boxNumberField = csv.GetField(4);
                                if (!string.IsNullOrWhiteSpace(boxNumberField) && int.TryParse(boxNumberField, out var boxnumber))
                                {
                                    record.BoxNumber = boxnumber;
                                }

                                var digitizedField = csv.GetField(5);
                                if (!string.IsNullOrWhiteSpace(digitizedField) && bool.TryParse(digitizedField, out var digitized))
                                {
                                    record.Digitized = digitized;
                                }

                                var checkedOutTo = csv.GetField(11);
                                if (!string.IsNullOrWhiteSpace(checkedOutTo) && checkedOutTo.Contains('@'))
                                {
                                    var user = await _userManager.FindByEmailAsync(checkedOutTo);
                                    if (user == null)
                                    {
                                        user = new ApplicationUser
                                        {
                                            UserName = checkedOutTo,
                                            Email = checkedOutTo,
                                        };
                                        await _userManager.CreateAsync(user);
                                        await _userManager.AddToRoleAsync(user, "User");
                                        newUsers.Add(user);
                                    }

                                    record.CheckedOutToId = user.Id;
                                    record.CheckedOut = true;

                                    _context.CheckoutHistory.Add(new CheckoutHistory
                                    {
                                        RecordItemId = record.ID,
                                        UserId = user.Id,
                                        CheckedOutDate = DateTime.UtcNow
                                    });
                                }

                            }

                            rowNumber++;
                        }
                    }
                }

                // Save all valid records regardless of errors
                await _context.SaveChangesAsync();

                // Send each newly created user a password reset link so they can set their password
                foreach (var newUser in newUsers)
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(newUser);
                    var resetLink = Url.Action(
                        "ConfirmPasswordReset",
                        "PasswordReset",
                        new { userId = newUser.Id, token },
                        protocol: Request.Scheme
                    );
                    var subject = "You have been added to RecordsMaster";
                    var message = $@"
                        <p>An account has been created for you in RecordsMaster.</p>
                        <p>Click the link below to set your password. This link can only be used once:</p>
                        <p><a href='{resetLink}'>Set Password</a></p>";
                    try
                    {
                        await _emailSender.SendEmailAsync(newUser.Email!, subject, message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to send welcome email to {newUser.Email}: {ex.Message}");
                    }
                }

                if (errorRows.Any())
                {
                    var errorMessages = errorRows
                        .Select(r => r.TryGetValue("Error", out var e) ? e : null)
                        .Where(e => e != null)
                        .ToList();
                    TempData["Warning"] = $"{rowNumber - 2 - errorRows.Count} record(s) updated. {errorRows.Count} row(s) had errors.";
                    TempData["Errors"] = string.Join("|", errorMessages);
                    return RedirectToAction("Upload");
                }

                TempData["Success"] = $"{rowNumber - 2} record(s) updated successfully.";
                return RedirectToAction("Upload");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred while processing the file: {ex.Message}";
                return RedirectToAction("Upload");
            }
        }
    }
}