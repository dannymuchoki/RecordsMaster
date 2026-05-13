using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // For ToListAsync
using RecordsMaster.Data;
using RecordsMaster.Models;

namespace RecordsMaster.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly AppDbContext _context;

    public HomeController(ILogger<HomeController> logger, AppDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    //public IActionResult Index()
     public async Task<IActionResult> Index()
        {

             var requestedRecords = await _context.RecordItems
                .Include(r => r.CheckedOutTo)
                .Where(r => r.Requested)
                .ToListAsync();

            // The PasswordResetController sends messages to the index view.
            if (TempData.ContainsKey("PasswordResetMessage"))
            {
                ViewData["PasswordResetMessage"] = TempData["PasswordResetMessage"];
            }


            // in dotnet you can't just send two models into a view (like in Django)
            var fiveDaysAgo = DateTime.UtcNow.AddDays(-5);
            var canceledRequests = await _context.CheckoutHistory
            .Include(c => c.RecordItem)
            .Where(c => EF.Functions.Like(c.DeliveryMessage, "Canceled by %@% on ____-__-__")
                        && c.ReturnedDate >= fiveDaysAgo)
            .ToListAsync();

            // So you have to do a View model. 
            var vm = new HomeIndexViewModel
            {
                RequestedRecords = requestedRecords,
                CanceledRequests = canceledRequests
            };

            return View("Index", vm);
        }
    

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
