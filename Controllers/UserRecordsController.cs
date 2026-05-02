using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecordsMaster.Models;
using RecordsMaster.Data;
using System.Threading.Tasks;
using System.Security.Claims;

namespace RecordsMaster.Controllers
{
    [Authorize]
    public class UserRecordsController : Controller
    {
        private readonly AppDbContext _context;

        public UserRecordsController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> UserRecords()
        {
            // Get the current logged-in user's ID
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return Challenge(); // Redirect to login if not authenticated
            }

            // Fetch records associated with this user
            var userRecords = await _context.RecordItems
                .Include(r => r.CheckedOutTo)
                .Where(r => r.CheckedOutToId == userId)
                .ToListAsync();

            if (User.IsInRole("Admin"))
            {
                var allCheckedOut = await _context.RecordItems
                    .Include(r => r.CheckedOutTo)
                    .Include(r => r.CheckoutHistoryRecords)
                    .Where(r => r.CheckedOut)
                    .OrderBy(r => r.CheckedOutTo == null ? string.Empty : r.CheckedOutTo.Email)
                    .ThenBy(r => r.CIS)
                    .ToListAsync();
                ViewBag.AllCheckedOutRecords = allCheckedOut;
            }

            return View("UserRecords", userRecords);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminViewUserRecords(string? email = null)
        {
            var userRecords = new List<RecordItemModel>();

            if (!string.IsNullOrEmpty(email))
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == email);

                if (user == null)
                    return NotFound("User not found.");

                userRecords = await _context.RecordItems
                    .Include(r => r.CheckedOutTo)
                    .Where(r => r.CheckedOutToId == user.Id)
                    .ToListAsync();
            }

            var checkedOutQuery = _context.RecordItems
                .Include(r => r.CheckedOutTo)
                .Include(r => r.CheckoutHistoryRecords)
                .Where(r => r.CheckedOut);

            if (!string.IsNullOrEmpty(email))
                checkedOutQuery = checkedOutQuery.Where(r => r.CheckedOutTo != null && r.CheckedOutTo.Email == email);

            var allCheckedOut = await checkedOutQuery
                .OrderBy(r => r.CheckedOutTo == null ? string.Empty : r.CheckedOutTo.Email)
                .ThenBy(r => r.CIS)
                .ToListAsync();

            ViewBag.AllCheckedOutRecords = allCheckedOut;
            ViewBag.FilterEmail = email;

            return View("UserRecords", userRecords);
        }
    }
}