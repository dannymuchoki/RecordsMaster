using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // For ToListAsync
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
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
        private readonly int _appsettingsPageSize;

        // Constructor to inject dependencies
        // AppDbContext is used to access the database, and IConfiguration is used to read settings from appsettings.json.
        // The page size for pagination is also set from the configuration in appsettings.json.
        // This allows for easy adjustments to pagination settings without changing the code.
        public RecordItemsController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;

            _configuration = configuration;
            // Gets the pagination value from appsettings.json (easier than hardcoding it here)
            _appsettingsPageSize = configuration.GetValue<int>("PaginationSettings:PageSize");
        }

        // GET: RecordItems/Search
        public IActionResult Search()
        {
            return View(); // This looks for Views/RecordItems/Search.cshtml
        }

        // GET: RecordItems/SearchRecords allows searching by case number (CIS) or barcode. 
        public async Task<IActionResult> SearchRecords(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                //return BadRequest("Please enter a case number or a bar code.");
                return View("NotFound");
            }

            input = input.Trim();

            var barcodePattern = @"^\d{2}-\d{5}$";
            bool isBarcode = Regex.IsMatch(input, barcodePattern);

            var query = _context.RecordItems
                .Include(r => r.CheckedOutTo)
                .AsQueryable();

            if (isBarcode)
            {
                query = query.Where(r => r.BarCode == input);
            }
            else
            {
                query = query.Where(r => r.CIS == input);
            }

            var records = await query.ToListAsync();

            if (records.Count == 0)
            {
                return View("NotFound");
            }

            return View("Details", records);
        }

        public async Task<IActionResult> Details(string id)
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
            int pageSize = 21; // Avery 5962: 21 labels per page. Leave this alone. 
            var items = _context.RecordItems.OrderBy(r => r.BarCode); // or your preferred order
            var pagedList = await PaginatedList<RecordItemModel>.CreateAsync(items, pageNumber ?? 1, pageSize);
            return View(pagedList);
        }

        // Paginated list of records (only visible to the Admin user)
        public async Task<IActionResult> List(int pageNumber = 1)
        {
            int pageSize = _appsettingsPageSize; // from appsettings.json

            var recordsQuery = _context.RecordItems
                .Include(r => r.CheckedOutTo)
                .OrderByDescending(r => r.CreatedOn);

            var pagedRecords = await PaginatedList<RecordItemModel>.CreateAsync(recordsQuery, pageNumber, pageSize);


            return View(pagedRecords);
        }

        // Download the CSV
        public IActionResult DownloadCsv()
        {
            var records = _context.RecordItems.ToList();
            var csvString = GenerateCsv(records);

            var fileName = $"RecordItems_{DateTime.Now:yyyyMMddHHmmss}.csv";
            var fileBytes = Encoding.UTF8.GetBytes(csvString);
            return File(fileBytes, "text/csv", fileName);
        }

        private string GenerateCsv(IEnumerable<RecordItemModel> records)
        {
            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine("ID,CIS,BarCode,RecordType,Location,BoxNumber,Digitized,ClosingDate,DestroyDate,CheckedOut,Requested,ReadyForPickup,CheckedOutToId");

            foreach (var record in records)
            {
                csvBuilder.AppendLine(
                    $"{record.ID}," +
                    $"{record.CIS}," +
                    $"{EscapeCsvValue(record.BarCode)}," +
                    $"{EscapeCsvValue(record.RecordType)}," +
                    $"{EscapeCsvValue(record.Location)}," +
                    $"{record.BoxNumber}," +
                    $"{record.Digitized}," +
                    $"{record.ClosingDate?.ToString("o", CultureInfo.InvariantCulture)}," +
                    $"{record.DestroyDate?.ToString("o", CultureInfo.InvariantCulture)}," +
                    $"{record.CheckedOut}," +
                    $"{record.Requested}," +
                    $"{record.ReadyForPickup}," +
                    $"{record.CheckedOutToId}");
            }
            return csvBuilder.ToString();
        }

        private string EscapeCsvValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }

        [HttpGet]
        public async Task<IActionResult> GetCheckedOutRecords(string filter)
        
        // To include CheckedOutTo you need to explicitly search for the user in this way. 
        {
            IQueryable<RecordItemModel> query = _context.RecordItems.Include(r => r.CheckedOutTo);

            if (filter == "checkedOut")
            {
                query = query.Where(r => r.CheckedOut);
            }

            var records = await query.ToListAsync();

            return PartialView("_RecordsTable", records);
        }

    }
}
