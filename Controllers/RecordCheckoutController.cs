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
    [Authorize] // Only authenticated users may checkout records.
    public class RecordCheckOutController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public RecordCheckOutController(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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

            TempData["Message"] = "Record successfully checked out.";
            return RedirectToAction(nameof(CheckOut), new { id });
        }
    }
}