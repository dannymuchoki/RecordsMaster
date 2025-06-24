using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // For ToListAsync
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using RecordsMaster.Models;
using RecordsMaster.Data;
using RecordsMaster.Utilities;
using Microsoft.Extensions.Configuration; // reads from appsettings.json


namespace RecordsMaster.Controllers
{
    [Authorize]
    public class RecordItemsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly int _pageSize;

        public RecordItemsController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;

            _configuration = configuration;
            // Gets the pagination value from appsettings.json (easier than hardcoding it here)
            _pageSize = configuration.GetValue<int>("PaginationSettings:PageSize");
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

        // Paginated list of records
        public async Task<IActionResult> List(int pageNumber = 1)
        {
            int pageSize = 20; // Or any size you want

            var recordsQuery = _context.RecordItems
                .Include(r => r.CheckedOutTo)
                .OrderBy(r => r.BarCode);

            var pagedRecords = await PaginatedList<RecordItemModel>.CreateAsync(recordsQuery, pageNumber, pageSize);
            

            return View(pagedRecords); 
        }
    }
}
