using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecordsMaster.Data;
using RecordsMaster.Models;
using RecordsMaster.Services;

namespace RecordsMaster.Controllers
{
    [Authorize] // Only authenticated users may check in/out records.
    public class RecordCheckInController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        private readonly IConfiguration _config;
        private readonly IEmailSender _emailSender;

        public RecordCheckInController(AppDbContext context, UserManager<ApplicationUser> userManager, IConfiguration config, IEmailSender emailSender)
        {
            _context = context;
            _userManager = userManager;
            _emailSender = emailSender;
            _config = config;
        }

        // POST: RecordItems/CheckIn/{id}
        // GET: RecordCheckOut/CheckOut/{id}?
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckIn(Guid id)
        {
            // Find the record by its unique identifier.
            var recordItem = await _context.RecordItems.FindAsync(id);
            if (recordItem == null)
            {
                return NotFound();
            }

            // Check if the record is already checked in.
            if (!recordItem.CheckedOut)
            {
                TempData["Message"] = "Record is already checked in.";
                return RedirectToAction("CheckOut", "RecordCheckOut", new { id });
            }

            // Update the checkout history to record the return date
            var checkoutHistory = await _context.CheckoutHistory
                .Where(ch => ch.RecordItemId == id && ch.ReturnedDate == null)
                .OrderByDescending(ch => ch.CheckedOutDate)
                .FirstOrDefaultAsync();

            if (checkoutHistory != null)
            {
                checkoutHistory.ReturnedDate = DateTime.UtcNow;
                _context.Update(checkoutHistory);
            }

            // Reset the record's checkout properties.
            recordItem.CheckedOut = false;
            recordItem.Requested = false;
            recordItem.ReadyForPickup = false;
            recordItem.CheckedOutToId = null;
            recordItem.CheckedOutTo = null;

            _context.Update(recordItem);
            await _context.SaveChangesAsync();

            if (checkoutHistory != null)
            {
                var checkedOutUser = await _userManager.FindByIdAsync(checkoutHistory.UserId);
                if (checkedOutUser?.Email != null)
                {
                    var subject = $"Record {recordItem.BarCode} has been checked in";
                    var message = $"<p>The record <strong>{recordItem.BarCode}</strong> that you had checked out has been returned and is now checked in.</p>" +
                                  $"<p>Returned: {checkoutHistory.ReturnedDate:f} UTC</p>";
                    try
                    {
                        await _emailSender.SendEmailAsync(checkedOutUser.Email, subject, message);
                    }
                    catch (Exception ex)
                    {
                        // Log but don't fail the check-in if email delivery fails
                        Console.Error.WriteLine($"Failed to send check-in notification email: {ex.Message}");
                    }
                }
            }

            TempData["Message"] = "Record successfully checked in.";
            return RedirectToAction("CheckOut", "RecordCheckOut", new { id });
        }
    }
}