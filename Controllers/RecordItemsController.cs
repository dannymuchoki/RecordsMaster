using System.IO.Compression;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // For ToListAsync
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Security.Claims;
using RecordsMaster.Models;
using RecordsMaster.Data;
using RecordsMaster.Utilities;
using Microsoft.Extensions.Configuration; // reads from appsettings.json


namespace RecordsMaster.Controllers
{
    [Authorize(Roles = "Admin,User,Court Requestors")]
    public class RecordItemsController : Controller
    {
        /* This was the first controller I wrote. It handles record item operations, such as searching and listing records.
         It uses Entity Framework Core to interact with the database and supports pagination.
         Dependencies:
         - AppDbContext: The database context for accessing RecordItemModel entities.
         - IConfiguration: Used to read settings from appsettings.json.
         - PaginatedList: A utility class for handling pagination of lists. (see Utilities/PaginatedList.cs)
         - RecordItemModel: The model representing a record item in the database.
         - ApplicationUser: Represents a user in the system, used for checking out records.
         https://learn.microsoft.com/en-us/ef/core/ 
         
         */

        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly int _appsettingsPageSize;

        /*
         Constructor to inject dependencies
            - AppDbContext is used to access the database
            - IConfiguration is used to read settings from appsettings.json.
            - The page size for pagination is also set from the configuration in appsettings.json.
            This allows for easy adjustments to pagination settings without changing the controller directly.
        */
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
        [Authorize(Roles = "Admin,Court Requestors")]
        public async Task<IActionResult> SearchRecords(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                //return BadRequest("Please enter a case number or a bar code.");
                return View("NotFound");
            }

            input = input.Trim();

            //var barcodePattern = @"^\d{2}-\d{5}$";
            var barcodePattern = @"^\d{2}(?:-?)\d{5}$"; //recognizes barcodes with/without hyphen. Easier for the user.
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
                // Database values are messy. Thirty years of accumulated errors
                var paddedInput = "0" + input; // sometimes there's a zero in front of the CIS number
                var strippedInput = input.StartsWith("015") ? input[3..] : null; // sometimes the CIS number is missing the 015. 
                var nozeroes = input.StartsWith('0') ? input[1..] : null; //strip out leading zeroes.
                query = query.Where(r => r.CIS == input || r.CIS == paddedInput || (strippedInput != null && r.CIS == strippedInput) || (nozeroes != null && r.CIS == nozeroes));
            }

            var records = await query.ToListAsync();

            if (records.Count == 0)
            {
                return View("NotFound");
            }

            return View("Details", records);
        }

        /* 
             workers can only search for what they need to know, so they need to provide a closing date. 
            Or a barcode. 
        */
        public async Task<IActionResult> CaseWorkerSearch(string? input, DateTime? closingDate)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return View("NotFound");
            }

            input = input.Trim();

            var barcodePattern = @"^\d{2}-\d{5}$";
            bool isBarcode = Regex.IsMatch(input, barcodePattern);

            if (!isBarcode && closingDate == null)
            {
                return View("NotFound");
            }

            var query = _context.RecordItems
                .Include(r => r.CheckedOutTo)
                .AsQueryable();

            if (isBarcode)
            {
                query = query.Where(r => r.BarCode == input);
            }
            else
            {
                var paddedInput = "0" + input;
                var strippedInput = input.StartsWith("015") ? input[3..] : null;
                var nozeroes = input.StartsWith('0') ? input[1..] : null;
                query = query.Where(r => (r.CIS == input || r.CIS == paddedInput || (strippedInput != null && r.CIS == strippedInput) || (nozeroes != null && r.CIS == nozeroes))
                    && r.ClosingDate.HasValue && r.ClosingDate.Value.Date == closingDate!.Value.Date);
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
            //if (record == null)
            if (!record.Any())
            {
                return NotFound();
            }
            return View("Details", record);
        }

        
        [Authorize(Roles = "Admin")]
        public IActionResult BoxDigitizationCheck(int? boxNumber)
        {
            // Surface any confirmation messages carried over from a redirect.
            if (TempData["BoxMessage"] is string shipMessage)
            {
                ViewData["BoxMessage"] = shipMessage;
            }
            if (TempData["ReturnedBoxMessage"] is string returnMessage)
            {
                ViewData["ReturnedBoxMessage"] = returnMessage;
            }

            // A box cannot be shipped if ANY record in it is checked out.
            var checkedOutBoxes = _context.RecordItems
                .Where(r => r.BoxNumber != null && r.CheckedOut)
                .Select(r => r.BoxNumber!.Value)
                .Distinct()
                .ToHashSet();

            // Tell the records room team which next ten boxes can be shipped next.
            ViewData["ToBeDigitizedBoxes"] = _context.RecordItems
                .Where(r => r.BoxNumber != null && r.Digitized != true && r.ShippedForDigitization != true)
                .OrderBy(r => r.BoxNumber)
                .AsEnumerable()
                .GroupBy(r => r.BoxNumber)
                .Where(g => !checkedOutBoxes.Contains(g.Key!.Value))
                .Take(10)
                .ToDictionary(g => g.Key!.Value, g => g.ToList());

            if (boxNumber == null)
            {
                return View("BoxChecker", Enumerable.Empty<RecordItemModel>());
            }

            var allRecordsInBox = _context.RecordItems
                .Where(r => r.BoxNumber == boxNumber)
                .ToList();

            if (!allRecordsInBox.Any())
            {
                ViewData["BoxMessage"] = $"No records found in box {boxNumber}.";
                return View("BoxChecker", Enumerable.Empty<RecordItemModel>());
            }

            if (allRecordsInBox.All(r => r.Digitized))
            {
                ViewData["BoxMessage"] = $"All records in box {boxNumber} are digitized.";
                return View("BoxChecker", Enumerable.Empty<RecordItemModel>());
            }

            var undigitizedRecords = allRecordsInBox.Where(r => !r.Digitized).ToList();
            ViewData["BoxNumber"] = boxNumber;
            return View("BoxChecker", undigitizedRecords);
        }

        // POST: mark every checked-in record in a box as shipped to the digitization vendor.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ShippedForDigitization(int boxNumber)
        {
            var recordsInBox = await _context.RecordItems
                .Where(r => r.BoxNumber == boxNumber && r.CheckedOut != true )
                .ToListAsync();

            if (!recordsInBox.Any())
            {
                TempData["BoxMessage"] = $"No records found in box {boxNumber}.";
                return RedirectToAction(nameof(BoxDigitizationCheck));
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier); // the admin confirming the shipment
            var shippedOn = DateTime.UtcNow;

            foreach (var record in recordsInBox)
            {
                record.ShippedForDigitization = true;

                // Audit row (not an open checkout)
                _context.CheckoutHistory.Add(new CheckoutHistory
                {
                    RecordItemId = record.ID,
                    UserId = currentUserId!,
                    CheckedOutDate = shippedOn,
                    ReturnedDate = shippedOn,
                    DeliveryMessage = $"Confirmed shipped for digitization on {shippedOn:yyyy-MM-dd}"
                });
            }

            await _context.SaveChangesAsync();

            TempData["BoxMessage"] = $"Box {boxNumber} marked as shipped for digitization.";
            return RedirectToAction(nameof(BoxDigitizationCheck));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ReturnedFromDigitization(int boxNumber)
        {
            var recordsInBox = await _context.RecordItems
                .Where(r => r.BoxNumber == boxNumber)
                .ToListAsync();

            if (!recordsInBox.Any())
            {
                TempData["ReturnedBoxMessage"] = $"No records found in box {boxNumber}.";
                return RedirectToAction(nameof(BoxDigitizationCheck));
            }

            foreach (var record in recordsInBox)
            {
                record.ReturnedFromDigitization = true;
            }

            await _context.SaveChangesAsync();

            TempData["ReturnedBoxMessage"] = $"Box {boxNumber} marked as returned from digitization.";
            return RedirectToAction(nameof(BoxDigitizationCheck));
        }




        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Labels(int? pageNumber)
        {
            int pageSize = 21; // Avery 5962: 21 labels per page. Leave this alone. 
            var items = _context.RecordItems.OrderBy(r => r.BarCode); // or your preferred order, but really leave this alone. 
            var pagedList = await PaginatedList<RecordItemModel>.CreateAsync(items, pageNumber ?? 1, pageSize);
            return View(pagedList);
        }

        [Authorize(Roles = "Admin")]
        // Paginated list of records (only visible to the Admin user)
        public async Task<IActionResult> List(int pageNumber = 1)
        {
            int pageSize = _appsettingsPageSize; // from appsettings.json

            var recordsQuery = _context.RecordItems
                .Include(r => r.CheckedOutTo)
                .OrderBy(r => r.CreatedOn);

            var pagedRecords = await PaginatedList<RecordItemModel>.CreateAsync(recordsQuery, pageNumber, pageSize);


            return View(pagedRecords);
        }

        // Download a ZIP containing RecordItems.csv and CheckoutHistory.csv
        [Authorize(Roles = "Admin")]
        public IActionResult DownloadCsv()
        {
            var records = _context.RecordItems.Include(r => r.CheckedOutTo).OrderBy(r => r.CreatedOn).ToList();

            var history = _context.CheckoutHistory
                .Include(ch => ch.User)
                .Include(ch => ch.RecordItem)
                .Include(ch => ch.PreBarCodeRecord)
                .ToList();

            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var recordsEntry = archive.CreateEntry($"RecordItems_{timestamp}.csv");
                using (var writer = new StreamWriter(recordsEntry.Open(), Encoding.UTF8))
                    writer.Write(GenerateCsv(records));

                var historyEntry = archive.CreateEntry($"CheckoutHistory_{timestamp}.csv");
                using (var writer = new StreamWriter(historyEntry.Open(), Encoding.UTF8))
                    writer.Write(GenerateCheckoutHistoryCsv(history));
            }

            memoryStream.Position = 0;
            return File(memoryStream.ToArray(), "application/zip", $"Export_{timestamp}.zip");
        }

        private string GenerateCsv(IEnumerable<RecordItemModel> records)
        {
            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine("CIS,BarCode,RecordType,Location,BoxNumber,Digitized,ClosingDate,DestroyDate,UploadedBy,CheckedOut,Requested,ReadyForPickup,CheckedOutTo");

            foreach (var record in records)
            {
                csvBuilder.AppendLine(
                    $"{record.CIS}," +
                    $"{EscapeCsvValue(record.BarCode ?? string.Empty)}," +
                    $"{EscapeCsvValue(record.RecordType ?? string.Empty)}," +
                    $"{EscapeCsvValue(record.Location ?? string.Empty)}," +
                    $"{record.BoxNumber}," +
                    $"{record.Digitized}," +
                    $"{record.ClosingDate?.ToString("o", CultureInfo.InvariantCulture)}," +
                    $"{record.DestroyDate?.ToString("o", CultureInfo.InvariantCulture)}," +
                     $"{record.UploadedBy}," +
                    $"{record.CheckedOut}," +
                    $"{record.Requested}," +
                    $"{record.ReadyForPickup}," +
                    $"{record.CheckedOutTo?.Email}");
            }
            return csvBuilder.ToString();
        }

        /* 
            This isn't connected to a CSV map because the columns match what SeedCheckoutHistory is expecting in Program.cs
            So you can use this output to re-populate the checkout history table if you need to. 
        */
        private string GenerateCheckoutHistoryCsv(IEnumerable<CheckoutHistory> history)
        {
            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine("CIS,BarCode,UserEmail,CheckedOutDate,ReturnedDate,DeliveryMessage");

            foreach (var ch in history)
            {
                var cis = ch.RecordItem?.CIS ?? ch.PreBarCodeRecord?.CIS ?? string.Empty;
                var barCode = ch.RecordItem?.BarCode ?? string.Empty;

                csvBuilder.AppendLine(
                    //$"{ch.Id}," +
                    //$"{ch.RecordItemId}," +
                    //$"{ch.PreBarCodeRecordId}," +
                    $"{EscapeCsvValue(cis)}," +
                    $"{EscapeCsvValue(barCode)}," +
                    $"{EscapeCsvValue(ch.User?.Email ?? string.Empty)}," +
                    $"{ch.CheckedOutDate.ToString("o", CultureInfo.InvariantCulture)}," +
                    $"{ch.ReturnedDate?.ToString("o", CultureInfo.InvariantCulture)}," +
                    $"{EscapeCsvValue(ch.DeliveryMessage ?? string.Empty)}");
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

        // GET: RecordItems/CheckoutHistory/{id}
        // Displays the checkout history for a specific record
        public async Task<IActionResult> CheckoutHistory(Guid id)
        {
            var record = await _context.RecordItems
                .Include(r => r.CheckoutHistoryRecords)
                    .ThenInclude(ch => ch.User)
                .FirstOrDefaultAsync(r => r.ID == id);

            if (record == null)
            {
                return NotFound();
            }

            ViewData["RecordCIS"] = record.CIS;
            ViewData["RecordBarCode"] = record.BarCode;

            return View(record.CheckoutHistoryRecords.OrderByDescending(ch => ch.CheckedOutDate).ToList());
        }

        // POST: RecordItems/ClearPdfAlert
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ClearPdfAlert()
        {
            TempData.Remove("PdfFileName");
            TempData.Remove("SuccessMessage");
            return Ok();
        }

        /* POST: RecordItems/WipeRecords
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> WipeRecords()
        {
            var adminEmail = _configuration["Notification:AdminEmail"];
            var currentEmail = User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(adminEmail)
                || string.IsNullOrWhiteSpace(currentEmail)
                || !string.Equals(adminEmail, currentEmail, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var allRecords = await _context.RecordItems.ToListAsync();
            _context.RecordItems.RemoveRange(allRecords);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "All records have been wiped.";
            return RedirectToAction("List");
        }
            */
    }
}
