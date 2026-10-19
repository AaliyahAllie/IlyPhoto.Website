using IlyPhotoWeb.Models;
using IlyPhotoWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace IlyPhotoWeb.Controllers;

public class HomeController : Controller
{
    private readonly ServiceStore _services;
    private readonly GalleryStore _galleries;
    private readonly EnquiryStore _enquiries;

    public HomeController(ServiceStore services, GalleryStore galleries, EnquiryStore enquiries)
    {
        _services = services;
        _galleries = galleries;
        _enquiries = enquiries;
    }

    private IActionResult? CheckPortalRedirect()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", User.IsInRole(Roles.Admin) ? "Admin" : "Client");
        }
        return null;
    }

    public IActionResult Index()
    {
        var redirect = CheckPortalRedirect();
        if (redirect is not null) return redirect;
        return View();
    }

    public IActionResult About()
    {
        var redirect = CheckPortalRedirect();
        if (redirect is not null) return redirect;
        return View();
    }

    public async Task<IActionResult> Pricing()
    {
        var redirect = CheckPortalRedirect();
        if (redirect is not null) return redirect;
        return View(await _services.ListAsync());
    }

    public IActionResult Packages()
    {
        var redirect = CheckPortalRedirect();
        if (redirect is not null) return redirect;
        return RedirectToAction(nameof(Pricing));
    }

    /// <summary>
    /// Displays approved portfolio images uploaded by the photographer.
    /// No mock or placeholder images.
    /// </summary>
    public async Task<IActionResult> Portfolio()
    {
        var redirect = CheckPortalRedirect();
        if (redirect is not null) return redirect;

        var approvedGalleries = await _galleries.ListPortfolioAsync();
        var urls = new List<string>();

        foreach (var g in approvedGalleries)
        {
            foreach (var path in g.PhotoPaths)
            {
                urls.Add(_galleries.SignedUrl(path, 120));
            }
        }

        return View(urls);
    }

    [HttpGet]
    public IActionResult Contact()
    {
        var redirect = CheckPortalRedirect();
        if (redirect is not null) return redirect;
        return View(new ContactViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Contact(ContactViewModel model)
    {
        var redirect = CheckPortalRedirect();
        if (redirect is not null) return redirect;

        if (!ModelState.IsValid) return View(model);

        try
        {
            await _enquiries.CreateAsync(new ContactEnquiry
            {
                FullName = model.FullName.Trim(),
                Email = model.Email.Trim(),
                Phone = model.Phone?.Trim() ?? "",
                Message = model.Message.Trim(),
                CreatedAt = Timestamp.GetCurrentTimestamp()
            });
            TempData["ContactSuccess"] = "Thank you! Your enquiry has been saved to Azure Table Storage. Aysha will get in touch with you shortly.";
            return RedirectToAction(nameof(Contact));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"We could not save your enquiry right now ({ex.Message}). Please verify your details and try again.");
            return View(model);
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}
