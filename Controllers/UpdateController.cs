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
    [Authorize(Roles = "Admin")]
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

        public IActionResult Update()
        {
            return View();
        }

        // Fields that may be bulk-updated. Key = value posted by the form/third CSV column target,
        // Value = friendly name used in status messages.
        private static readonly Dictionary<string, string> UpdatableFields = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Location"] = "Location",
            ["BoxNumber"] = "Box Number",
            ["Digitized"] = "Digitized",
            ["ClosingDate"] = "Closing Date",
            ["DestroyDate"] = "Destroy Date",
            ["Expunged"] = "Expunged",
            ["CheckedOutTo"] = "Checked Out To",
        };

        // A queued email alert: who to notify, with subject and HTML body.
        private record Notification(string Email, string Subject, string HtmlMessage);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(IFormFile file, string updateField)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please upload a valid CSV file.";
                return RedirectToAction("Update");
            }

            if (string.IsNullOrWhiteSpace(updateField) || !UpdatableFields.ContainsKey(updateField))
            {
                TempData["Error"] = "Please choose a valid field to update.";
                return RedirectToAction("Update");
            }

            var errorMessages = new List<string>();
            var newUsers = new List<ApplicationUser>();
            var notifications = new List<Notification>(); // checkout/reassignment alerts, sent after a successful save
            var currentUserId = _userManager.GetUserId(User); // the admin performing the update, recorded on each audit entry
            int updatedCount = 0;
            int rowNumber = 2; // Assuming header is row 1

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

                        while (csv.Read())
                        {
                            // Column layout: 0 = CIS/case number, 1 = barcode, 2 = new value for the chosen field.
                            var barcode = csv.GetField(1)?.Trim();
                            var rawValue = csv.GetField(2)?.Trim();

                            if (string.IsNullOrWhiteSpace(barcode))
                            {
                                errorMessages.Add($"Row {rowNumber}: barcode is required.");
                                rowNumber++;
                                continue;
                            }

                            // Records are matched by barcode alone.
                            var record = _context.RecordItems.FirstOrDefault(r => r.BarCode == barcode);
                            if (record == null)
                            {
                                errorMessages.Add($"Row {rowNumber}: No record found with barcode '{barcode}'.");
                                rowNumber++;
                                continue;
                            }

                            // CheckedOutTo needs async user lookup/creation and the email-sender, so it's handled separately.
                            var (applyError, changeMessage) = updateField.Equals("CheckedOutTo", StringComparison.OrdinalIgnoreCase)
                                ? await ApplyCheckedOutToUpdate(record, rawValue, newUsers, notifications)
                                : ApplyFieldUpdate(record, updateField, rawValue);
                            if (applyError != null)
                            {
                                errorMessages.Add($"Row {rowNumber}: {applyError}");
                                rowNumber++;
                                continue;
                            }

                            // Log each actual change as a history entry describing the former and new value.
                            if (changeMessage != null)
                            {
                                _context.CheckoutHistory.Add(new CheckoutHistory
                                {
                                    RecordItemId = record.ID,
                                    UserId = currentUserId!,
                                    CheckedOutDate = DateTime.UtcNow,
                                    DeliveryMessage = changeMessage
                                });
                                updatedCount++;
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

                // Alert the former and new holders about each reassignment.
                foreach (var note in notifications)
                {
                    try
                    {
                        await _emailSender.SendEmailAsync(note.Email, note.Subject, note.HtmlMessage);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to send checkout notification to {note.Email}: {ex.Message}");
                    }
                }

                if (errorMessages.Any())
                {
                    TempData["Warning"] = $"{updatedCount} record(s) updated. {errorMessages.Count} row(s) had errors.";
                    TempData["Errors"] = string.Join("|", errorMessages);
                    return RedirectToAction("Update");
                }

                TempData["Success"] = $"{updatedCount} record(s) updated successfully ({UpdatableFields[updateField]}).";
                return RedirectToAction("Update");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred while processing the file: {ex.Message}";
                return RedirectToAction("Update");
            }
        }

        // Parses rawValue for the chosen field and applies it to the record.
        // Returns (error, changeMessage): error is non-null on a parse failure; changeMessage describes an
        // actual change and is null when the value was blank/unchanged (so nothing is logged or counted).
        private static (string? error, string? changeMessage) ApplyFieldUpdate(RecordItemModel record, string field, string? rawValue)
        {
            // For the nullable fields below, a blank value means "leave unchanged" rather than clearing.
            switch (field)
            {
                case "Location":
                    if (string.IsNullOrWhiteSpace(rawValue) || rawValue == record.Location) return (null, null);
                    var locMsg = $"Location changed from '{record.Location ?? "(none)"}' to '{rawValue}'.";
                    record.Location = rawValue;
                    return (null, locMsg);

                case "BoxNumber":
                    if (string.IsNullOrWhiteSpace(rawValue)) return (null, null);
                    if (!int.TryParse(rawValue, out var box) || box < 1)
                        return ($"'{rawValue}' is not a valid positive box number.", null);
                    if (box == record.BoxNumber) return (null, null);
                    var boxMsg = $"Box Number changed from '{record.BoxNumber?.ToString() ?? "(none)"}' to '{box}'.";
                    record.BoxNumber = box;
                    return (null, boxMsg);

                case "ClosingDate":
                    if (string.IsNullOrWhiteSpace(rawValue)) return (null, null);
                    if (!DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var closing))
                        return ($"'{rawValue}' is not a valid date.", null);
                    if (closing == record.ClosingDate) return (null, null);
                    var closingMsg = $"Closing Date changed from '{record.ClosingDate?.ToString("yyyy-MM-dd") ?? "(none)"}' to '{closing:yyyy-MM-dd}'.";
                    record.ClosingDate = closing;
                    return (null, closingMsg);

                case "DestroyDate":
                    if (string.IsNullOrWhiteSpace(rawValue)) return (null, null);
                    if (!DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var destroy))
                        return ($"'{rawValue}' is not a valid date.", null);
                    if (destroy == record.DestroyDate) return (null, null);
                    var destroyMsg = $"Destroy Date changed from '{record.DestroyDate?.ToString("yyyy-MM-dd") ?? "(none)"}' to '{destroy:yyyy-MM-dd}'.";
                    record.DestroyDate = destroy;
                    return (null, destroyMsg);

                // For the boolean fields, a blank value is treated as false.
                case "Digitized":
                    bool digitized = false;
                    if (!string.IsNullOrWhiteSpace(rawValue) && !TryParseBool(rawValue, out digitized))
                        return ($"'{rawValue}' is not a valid true/false value.", null);
                    if (digitized == record.Digitized) return (null, null);
                    var digMsg = $"Digitized changed from '{record.Digitized}' to '{digitized}'.";
                    record.Digitized = digitized;
                    return (null, digMsg);

                case "Expunged":
                    bool expunged = false;
                    if (!string.IsNullOrWhiteSpace(rawValue) && !TryParseBool(rawValue, out expunged))
                        return ($"'{rawValue}' is not a valid true/false value.", null);
                    if (expunged == record.Expunged) return (null, null);
                    var expMsg = $"Expunged changed from '{record.Expunged}' to '{expunged}'.";
                    record.Expunged = expunged;
                    return (null, expMsg);

                default:
                    return ("Unknown field.", null);
            }
        }

        // Reassigns a record's checkout to the user identified by the email in rawValue, creating the user if needed.
        // If the record is already checked out, it is first checked in from the current holder; both the former and
        // new holders are queued for an email alert. A blank value leaves the checkout unchanged.
        // Returns (error, changeMessage); changeMessage is null when nothing changed.
        private async Task<(string? error, string? changeMessage)> ApplyCheckedOutToUpdate(
            RecordItemModel record, string? rawValue, List<ApplicationUser> newUsers, List<Notification> notifications)
        {
            if (string.IsNullOrWhiteSpace(rawValue)) return (null, null);
            if (!rawValue.Contains('@'))
                return ($"'{rawValue}' is not a valid email address.", null);

            var newUser = await _userManager.FindByEmailAsync(rawValue);
            if (newUser == null)
            {
                newUser = new ApplicationUser
                {
                    UserName = rawValue,
                    Email = rawValue,
                };
                var createResult = await _userManager.CreateAsync(newUser);
                if (!createResult.Succeeded)
                    return ($"Could not create user '{rawValue}': {string.Join("; ", createResult.Errors.Select(e => e.Description))}", null);
                await _userManager.AddToRoleAsync(newUser, "User");
                newUsers.Add(newUser);
            }

            // Already checked out to this user: nothing to change.
            if (record.CheckedOutToId == newUser.Id && record.CheckedOut)
                return (null, null);

            var recordLabel = $"barcode <strong>{record.BarCode}</strong> (case {record.CIS})";

            // Check the record in from the current holder (if any) and alert them.
            var formerAssignee = "(none)";
            var formerHolderId = record.CheckedOutToId;
            if (!string.IsNullOrEmpty(formerHolderId))
            {
                // Close the former holder's open checkout(s). DeliveryMessage == null excludes audit-log rows.
                var openCheckouts = _context.CheckoutHistory
                    .Where(h => h.RecordItemId == record.ID && h.UserId == formerHolderId
                                && h.ReturnedDate == null && h.DeliveryMessage == null)
                    .ToList();
                foreach (var oc in openCheckouts)
                    oc.ReturnedDate = DateTime.UtcNow;

                var formerUser = await _userManager.FindByIdAsync(formerHolderId);
                formerAssignee = formerUser?.Email ?? formerHolderId;
                if (formerUser?.Email != null)
                {
                    notifications.Add(new Notification(
                        formerUser.Email,
                        "A record has been checked in from you",
                        $"<p>The record with {recordLabel} that was checked out to you has been checked in and reassigned to another user.</p>"));
                }
            }

            // Assign to the new user and record the new active checkout.
            record.CheckedOutToId = newUser.Id;
            record.CheckedOut = true;

            _context.CheckoutHistory.Add(new CheckoutHistory
            {
                RecordItemId = record.ID,
                UserId = newUser.Id,
                CheckedOutDate = DateTime.UtcNow
            });

            if (newUser.Email != null)
            {
                notifications.Add(new Notification(
                    newUser.Email,
                    "A record has been checked out to you",
                    $"<p>The record with {recordLabel} has been checked out to you.</p>"));
            }

            return (null, $"Checked Out To changed from '{formerAssignee}' to '{newUser.Email}'.");
        }

        // Accepts true/false plus common yes/no and 1/0 spellings.
        private static bool TryParseBool(string? value, out bool result)
        {
            result = false;
            if (string.IsNullOrWhiteSpace(value)) return false;
            switch (value.Trim().ToLowerInvariant())
            {
                case "true": case "yes": case "y": case "1": result = true; return true;
                case "false": case "no": case "n": case "0": result = false; return true;
                default: return bool.TryParse(value, out result);
            }
        }
    }
}