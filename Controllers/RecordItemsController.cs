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
        // This was the first controller I wrote. It handles record item operations, such as searching and listing records.
        // It uses Entity Framework Core to interact with the database and supports pagination.
        // Dependencies:
        // - AppDbContext: The database context for accessing RecordItemModel entities.
        // - IConfiguration: Used to read settings from appsettings.json.
        // - PaginatedList: A utility class for handling pagination of lists. (see Utilities/PaginatedList.cs)
        // - RecordItemModel: The model representing a record item in the database.
        // - ApplicationUser: Represents a user in the system, used for checking out records.
        // https://learn.microsoft.com/en-us/ef/core/
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly int _pageSize;

        // Constructor to inject dependencies
        // AppDbContext is used to access the database, and IConfiguration is used to read settings from appsettings.json.
        // The page size for pagination is also set from the configuration in appsettings.json.
        // This allows for easy adjustments to pagination settings without changing the code.
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

        public async Task<IActionResult> Details(int id)
        {
            var record = await _context.RecordItems.Where(r => r.CIS == id).ToListAsync();
            if (record == null)
            {
                return NotFound();
            }
            return View("Details", record);
        }

        public async Task<IActionResult> Labels(int? pageNumber)
        {
            int pageSize = 21; // Avery 5962: 21 labels per page
            var items = _context.RecordItems.OrderBy(r => r.BarCode); // or your preferred order
            var pagedList = await PaginatedList<RecordItemModel>.CreateAsync(items, pageNumber ?? 1, pageSize);
            return View(pagedList);
        }

        // Paginated list of records (only visible to the Admin user)
        public async Task<IActionResult> List(int pageNumber = 1)
        {
            int pageSize = 50; // Or any size you want

            var recordsQuery = _context.RecordItems
                .Include(r => r.CheckedOutTo)
                .OrderBy(r => r.BarCode);

            var pagedRecords = await PaginatedList<RecordItemModel>.CreateAsync(recordsQuery, pageNumber, pageSize);
            

            return View(pagedRecords); 
        }
    }
}
