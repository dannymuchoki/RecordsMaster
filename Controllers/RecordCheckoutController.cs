using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using RecordsMaster.Data;
using RecordsMaster.Models;
using RecordsMaster.Services;

namespace RecordsMaster.Controllers
{
    [Authorize] // Only authenticated users may checkout records.
    public class RecordCheckOutController : Controller
    {
        private readonly AppDbContext _context;

        private readonly ILogger<RecordCheckOutController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _config;
        private readonly IEmailSender _emailSender;

        public RecordCheckOutController(AppDbContext context, ILogger<RecordCheckOutController> logger, UserManager<ApplicationUser> userManager, IEmailSender emailSender, IConfiguration config)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
            _emailSender = emailSender;
            _config = config;
        }

        // GET: RecordCheckOut/CheckOut/{id}?
        public async Task<IActionResult> CheckOut(Guid? id, int cis)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Include the navigation property for the user (if loaded, helpful for display)
            var recordItem = await _context.RecordItems
                .Include(r => r.CheckedOutTo)
                .FirstOrDefaultAsync(r => r.ID == id);

            if (recordItem == null)
            {
                return NotFound();
            }

            return View(recordItem);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestRecord(Guid id)
        {
            var recordItem = await _context.RecordItems.FindAsync(id);
            if (recordItem == null)
            {
                return NotFound();
            }

            // Optional: if already checked out return an error message or redirect.
            if (recordItem.CheckedOut)
            {
                TempData["Message"] = "Record is already checked out.";
                return RedirectToAction(nameof(CheckOut), new { id });
            }

            // Get the current authenticated user.
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge(); // Forces the user to log in.
            }

            // Associate the record with the user and mark it as requested.
            recordItem.Requested = true;
            recordItem.CheckedOutToId = user.Id;
            recordItem.CheckedOutTo = user;

            _context.Update(recordItem);
            await _context.SaveChangesAsync();

            var subject = $@"Record Request - {recordItem.BarCode}";
            var message = $@"
                <p><strong>Record:</strong> {recordItem.BarCode}</p>
                <p><strong>Requested by:</strong> {user.Email}</p>
                <p><strong>Time (UTC):</strong> {DateTime.UtcNow}</p>
                <p>Check the RecordsMaster dashboard.</p>";

            // Check appsettings.json dictionary for the 'Notification' key. 
            var adminEmail = _config["Notification:NotificationMailbox"];
            try
            {
                // tell the staff that the request is in.
                await _emailSender.SendEmailAsync(adminEmail!, subject, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(message);
                _logger.LogError(ex, "Failed to send record request email for {recordItemBarCode}", recordItem.BarCode);
            }

            TempData["Message"] = "Record requested.";
            return RedirectToAction(nameof(CheckOut), new { id });
        }

       [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReadyForPickup(Guid id)
        {
            var recordItem = await _context.RecordItems.FindAsync(id);
            if (recordItem == null)
            {
                return NotFound();
            }

            if (recordItem.CheckedOut)
            {
                TempData["Message"] = "Record is already checked out.";
                return RedirectToAction(nameof(CheckOut), new { id });
            }

            // I think these two checks are redundant but okay
            if (recordItem.CheckedOutToId == null)
            {
                TempData["Message"] = "No user associated with this record request.";
                return RedirectToAction(nameof(CheckOut), new { id });
            }

            var user = await _userManager.FindByIdAsync(recordItem.CheckedOutToId);
            if (user == null)
            {
                TempData["Message"] = "No user associated with this record request.";
                return RedirectToAction(nameof(CheckOut), new { id });
            }

            // Associate the record with the user and mark it as requested.
            recordItem.Requested = true;
            recordItem.ReadyForPickup = true;

            _context.Update(recordItem);
            await _context.SaveChangesAsync();

            var subject = "You must check your record out in RecordsMaster";
            var message = $@"
                <p>Your record is ready for checkout.</p>
                <p><strong>Record:</strong> {recordItem.BarCode}</p>
                <p><strong>Requested by:</strong> {user.Email}</p>
                <p><strong>Time (UTC):</strong> {DateTime.UtcNow}</p>
                <p><strong>Go to RecordsMaster to check out your record.</strong> Your record will not be issued to you until you have checked out the record.</p>";

            try
            {
                // Tell the user it's ready for pickup.
                await _emailSender.SendEmailAsync(user.Email!, subject, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(message);
                _logger.LogError(ex, "Failed to pickup email to {userEmail}", user.Email);
            }

            TempData["Message"] = "Record requested.";
            return RedirectToAction(nameof(CheckOut), new { id });
        }


        // POST: RecordItems/Checkout/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(Guid id, string? deliveryOption, string? deliveryMessage)
        {
            if (string.IsNullOrEmpty(deliveryOption))
            {
                TempData["Message"] = "Please select a delivery option.";
                return RedirectToAction(nameof(CheckOut), new { id });
            }

            if (deliveryOption == "send" && string.IsNullOrWhiteSpace(deliveryMessage))
            {
                TempData["Message"] = "Please enter a delivery address.";
                return RedirectToAction(nameof(CheckOut), new { id });
            }

            var recordItem = await _context.RecordItems.FindAsync(id);
            if (recordItem == null)
            {
                return NotFound();
            }

            // Optional: if already checked return user to already checked out page
            if (recordItem.CheckedOut)
            {
                TempData["Message"] = "Record is already checked out.";
                return RedirectToAction(nameof(CheckOut), new { id });
            }

            // Get the current authenticated user.
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge(); // Forces the user to log in.
            }

            var resolvedDeliveryMessage = deliveryOption == "send"
                ? deliveryMessage!
                : $"{user.Email} will pick up the record";

            // Associate the record with the user and mark it as checked out.
            recordItem.Requested = false;
            recordItem.ReadyForPickup = false;
            recordItem.CheckedOut = true;
            recordItem.CheckedOutToId = user.Id;
            recordItem.CheckedOutTo = user;

            // Create a checkout history record
            var checkoutHistory = new CheckoutHistory
            {
                RecordItemId = recordItem.ID,
                UserId = user.Id,
                CheckedOutDate = DateTime.UtcNow,
                ReturnedDate = null,
                DeliveryMessage = resolvedDeliveryMessage
            };

            _context.CheckoutHistory.Add(checkoutHistory);
            _context.Update(recordItem);
            await _context.SaveChangesAsync();

            var deliveryInfo = $"<p><strong>Delivery:</strong> {resolvedDeliveryMessage}</p>";

            var subject = $@"Record {recordItem.BarCode} checked out by {user.Email}";
            var message = $@"
                <p><strong>Record:</strong> {recordItem.BarCode}</p>
                <p><strong>Checked Out By:</strong> ({user.Email})</p>
                <p><strong>Time (UTC):</strong> {DateTime.UtcNow}</p>
                {deliveryInfo}";

            var userMessage = $@"
                <h1>Bring this with you</h1>
                <h2>This is your receipt</h2>
                <p><strong>Record:</strong> {recordItem.BarCode}</p>
                <p><strong>Checked Out By:</strong> ({user.Email})</p>
                <p><strong>Time (UTC):</strong> {DateTime.UtcNow}</p>
                {deliveryInfo}";

            // Check appsettings.json dictionary for the 'Notification' key. 
            var adminEmail = _config["Notification:NotificationMailbox"];
            try
            {
                await _emailSender.SendEmailAsync(adminEmail!, subject, message);
                await _emailSender.SendEmailAsync(user.Email!, subject, userMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine(message);
                _logger.LogError(ex, "Failed to send checkout confirmation email to {adminEmail}", adminEmail);
            }

            TempData["Message"] = "Record successfully checked out. Bring this page to receive the record.";
            return RedirectToAction(nameof(CheckOut), new { id });
        }
    }
}