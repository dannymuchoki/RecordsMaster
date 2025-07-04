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
    [Authorize] // Only authenticated users may checkout records.
    public class RecordCheckOutController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _config;
        private readonly IEmailSender _emailSender;

        public RecordCheckOutController(AppDbContext context, UserManager<ApplicationUser> userManager, IEmailSender emailSender, IConfiguration config)
        {
            _context = context;
            _userManager = userManager;
            _emailSender = emailSender;
            _config = config;
        }

        // GET: RecordCheckOut/CheckOut/{id}?
        public async Task<IActionResult> CheckOut(Guid? id)
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

        // POST: RecordItems/Checkout/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(Guid id)
        {
            var recordItem = await _context.RecordItems.FindAsync(id);
            if (recordItem == null)
            {
                return NotFound();
            }

            // Optional: if already checked out, you might return an error message or redirect.
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

            // Associate the record with the user and mark it as checked out.
            recordItem.CheckedOut = true;
            recordItem.CheckedOutToId = user.Id;
            recordItem.CheckedOutTo = user;

            _context.Update(recordItem);
            await _context.SaveChangesAsync();

            var subject = "Record Requested";
            var message = $@"
                <p><strong>Record ID:</strong> {recordItem}</p>
                <p><strong>Checked Out By:</strong> ({user.Email})</p>
                <p><strong>Time (UTC):</strong> {DateTime.UtcNow}</p>";

            // Check appsettings.json dictionary for the 'Notification' key. 
            var adminEmail = _config["Notification:AdminEmail"];
            await _emailSender.SendEmailAsync(adminEmail, subject, message);

            TempData["Message"] = "Record successfully checked out.";
            return RedirectToAction(nameof(CheckOut), new { id });
        }
    }
}