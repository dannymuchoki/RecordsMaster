using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecordsMaster.Data;
using RecordsMaster.Models;

namespace RecordsMaster.Controllers
{
    [Authorize] // Only authenticated users may check in/out records.
    public class RecordCheckInController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public RecordCheckInController(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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

            TempData["Message"] = "Record successfully checked in.";
            return RedirectToAction("CheckOut", "RecordCheckOut", new { id });
        }
    }
}