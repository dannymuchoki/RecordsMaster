using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // For ToListAsync
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using RecordsMaster.Models;
using RecordsMaster.Data;

namespace RecordsMaster.Controllers
{
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
            // Fetch records asynchronously from the database
            List<RecordItemModel> records = await _context.RecordItems.ToListAsync();

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
                return BadRequest("CIS value is required."); // Return 400 if no CIS is provided
            }

            var record = await _context.RecordItems
                .FirstOrDefaultAsync(r => r.CIS == cis);

            if (record == null)
            {
                return View("NotFound"); // Return NotFound view if no record is found
            }

            return View("Details", record); // Display the record details in the Details view
        }
    }
}
