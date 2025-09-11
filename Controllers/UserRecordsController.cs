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

            //return View(userRecords);
            return View("UserRecords", userRecords);
        }

        public async Task<IActionResult> AdminViewUserRecords(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return BadRequest("Email address is required.");
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                return NotFound("User not found.");
            }

            var userRecords = await _context.RecordItems
                .Include(r => r.CheckedOutTo)
                .Where(r => r.CheckedOutToId == user.Id)
                .ToListAsync();

            return View("UserRecords", userRecords);
        }
    }
}