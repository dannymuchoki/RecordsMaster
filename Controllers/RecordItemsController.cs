using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // For ToListAsync
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using RecordsMaster.Models;
using RecordsMaster.Data;

namespace RecordsMaster.Controllers
{
    [Authorize]
    public class RecordItemsController : Controller
    {
        private readonly AppDbContext _context;

        public RecordItemsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: RecordItems/List
        public async Task<IActionResult> List()
        {
            // Fetch records asynchronously including the CheckedOutTo user information.
            List<RecordItemModel> records = await _context.RecordItems
                                                    .Include(r => r.CheckedOutTo)
                                                    .ToListAsync();
            return View(records); // Pass data to the view
        }

        // GET: RecordItems/Search
        public IActionResult Search()
        {
            return View(); // This looks for Views/RecordItems/Search.cshtml
        }

        // GET: RecordItems/SearchByCIS?cis=123
        public async Task<IActionResult> SearchByCIS(int? cis)
        {
            if (cis == null)
            {
                return BadRequest("CIS value is required."); // Return 400 if no CIS is provided, but the view manages this in HTML. Will not let user search without a number
            }

            // Turn the results into a list. 
            var record = await _context.RecordItems
                .Where(r => r.CIS == cis)
                .Include(r => r.CheckedOutTo)
                .ToListAsync();

            if (record.Count == 0 || !record.Any())
            {
                return View("NotFound"); // Return NotFound view if no record is found
            }


            return View("Details", record); // Display the record details in the Details view
        }
    }
}
