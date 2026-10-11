using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StajTakip.Business.Services.Interfaces;
using StajTakip.Entities.Concrete;
using System.Security.Claims;

namespace StajTakip.Web.Controllers;

[Authorize(Policy = "StudentOnly")]
public class StudentController : Controller
{
    private readonly IApplicationService _appSvc;
    private readonly INotificationService _notifSvc;
    private readonly IUserService _userSvc;
    private readonly IMessageService _messageSvc;

    public StudentController(IApplicationService appSvc,
        INotificationService notifSvc, IUserService userSvc,
        IMessageService messageSvc)
    {
        _appSvc = appSvc;
        _notifSvc = notifSvc;
        _userSvc = userSvc;
        _messageSvc = messageSvc;
    }

    [HttpPost]
    public async Task<IActionResult> ApplyToPost(
     int internshipPostId, string position, string companyName,
     IFormFile? cvFile)
    {
        string? cvFileName = null;
        string? cvOriginalName = null;

        if (cvFile != null && cvFile.Length > 0)
        {
            var allowedExtensions = new[] { ".pdf", ".doc", ".docx" };
            var extension = Path.GetExtension(cvFile.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
            {
                TempData["Error"] = "Sadece PDF veya Word dosyası yükleyebilirsiniz.";
                return RedirectToAction("Internships");
            }

            if (cvFile.Length > 5 * 1024 * 1024)
            {
                TempData["Error"] = "Dosya boyutu 5MB'dan büyük olamaz.";
                return RedirectToAction("Internships");
            }

            cvFileName = $"{Guid.NewGuid()}{extension}";
            cvOriginalName = cvFile.FileName;

            var uploadPath = Path.Combine(
                Directory.GetCurrentDirectory(), "wwwroot", "uploads", "cv");

            Directory.CreateDirectory(uploadPath);

            var filePath = Path.Combine(uploadPath, cvFileName);
            using var stream = new FileStream(filePath, FileMode.Create);
            await cvFile.CopyToAsync(stream);
        }

        var app = new Application
        {
            StudentId = CurrentUserId,
            InternshipPostId = internshipPostId,
            Position = position,
            CompanyName = companyName,
            Status = ApplicationStatus.Sent,
            ApplicationDate = DateTime.Now,
            CvFileName = cvFileName,
            CvOriginalName = cvOriginalName
        };

        await _appSvc.AddAsync(app);
        TempData["Success"] = "Başvurunuz gönderildi!";
        return RedirectToAction("Applications");
    }
    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── Dashboard ──────────────────────────────────────────
    public async Task<IActionResult> Internships()
    {
        var posts = await _appSvc.GetActiveInternshipPostsAsync();
        return View(posts);
    }
    public async Task<IActionResult> Dashboard()
    {
        var summary = await _appSvc.GetStatusSummaryAsync(CurrentUserId);
        var recent = await _appSvc.GetByStudentIdAsync(CurrentUserId);
        var notifs = await _notifSvc.GetUnreadAsync(CurrentUserId);
        var unreadMessages = await _messageSvc.GetUnreadCountAsync(CurrentUserId);

        ViewBag.Summary = summary;
        ViewBag.Notifs = notifs;
        ViewBag.Total = recent.Count;
        ViewBag.UnreadMessages = unreadMessages;
        return View(recent.Take(5).ToList());
    }

    // ── Başvuru listesi ────────────────────────────────────
    public async Task<IActionResult> Applications(string? status)
    {
        var all = await _appSvc.GetByStudentIdAsync(CurrentUserId);

        if (!string.IsNullOrEmpty(status) &&
            Enum.TryParse<ApplicationStatus>(status, out var s))
            all = all.Where(a => a.Status == s).ToList();

        ViewBag.CurrentStatus = status;
        return View(all);
    }

    // ── Başvuru detay ──────────────────────────────────────
    public async Task<IActionResult> Detail(int id)
    {
        var app = await _appSvc.GetByIdAsync(id);
        if (app == null || app.StudentId != CurrentUserId)
            return NotFound();
        return View(app);
    }

    // ── Yeni başvuru ──────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> AddApplication()
    {
        var companies = await _userSvc.GetCompaniesAsync();
        ViewBag.Companies = companies;
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> AddApplication(
        string companyName, string position, string? city,
        DateTime? applicationDate, string? notes,
        IFormFile? cvFile)
    {

        if (cvFile == null || cvFile.Length == 0)
        {
            TempData["Error"] = "CV dosyası zorunludur.";
            var companies = await _userSvc.GetCompaniesAsync();
            ViewBag.Companies = companies;
            return View();
        }

        string? cvFileName = null;
        string? cvOriginalName = null;

        if (cvFile != null && cvFile.Length > 0)
        {
            // Sadece PDF ve Word dosyalarına izin ver
            var allowedExtensions = new[] { ".pdf", ".doc", ".docx" };
            var extension = Path.GetExtension(cvFile.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
            {
                TempData["Error"] = "Sadece PDF veya Word dosyası yükleyebilirsiniz.";
                return RedirectToAction("AddApplication");
            }

            // Max 5MB
            if (cvFile.Length > 5 * 1024 * 1024)
            {
                TempData["Error"] = "Dosya boyutu 5MB'dan büyük olamaz.";
                return RedirectToAction("AddApplication");
            }

            // Benzersiz dosya adı oluştur
            cvFileName = $"{Guid.NewGuid()}{extension}";
            cvOriginalName = cvFile.FileName;

            var uploadPath = Path.Combine(
                Directory.GetCurrentDirectory(), "wwwroot", "uploads", "cv");

            Directory.CreateDirectory(uploadPath);

            var filePath = Path.Combine(uploadPath, cvFileName);
            using var stream = new FileStream(filePath, FileMode.Create);
            await cvFile.CopyToAsync(stream);
        }

        var app = new Application
        {
            StudentId = CurrentUserId,
            CompanyName = companyName,
            Position = position,
            City = city,
            ApplicationDate = applicationDate,
            Notes = notes,
            Status = ApplicationStatus.Sent,
            CvFileName = cvFileName,
            CvOriginalName = cvOriginalName
        };

        await _appSvc.AddAsync(app);
        TempData["Success"] = "Başvurunuz gönderildi!";
        return RedirectToAction("Applications");
    }

    // ── Başvuru düzenle ────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> EditApplication(int id)
    {
        var app = await _appSvc.GetByIdAsync(id);
        if (app == null || app.StudentId != CurrentUserId)
            return NotFound();
        return View(app);
    }

    [HttpPost]
    public async Task<IActionResult> EditApplication(
        int id, string companyName, string position,
        string? city, DateTime? applicationDate, string? notes)
    {
        var app = await _appSvc.GetByIdAsync(id);
        if (app == null || app.StudentId != CurrentUserId)
            return NotFound();

        app.CompanyName = companyName;
        app.Position = position;
        app.City = city;
        app.ApplicationDate = applicationDate;
        app.Notes = notes;

        await _appSvc.UpdateAsync(app);
        TempData["Success"] = "Başvuru güncellendi!";
        return RedirectToAction("Applications");
    }

    // ── Başvuru sil ────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> DeleteApplication(int id)
    {
        var app = await _appSvc.GetByIdAsync(id);
        if (app == null || app.StudentId != CurrentUserId)
            return NotFound();

        await _appSvc.DeleteAsync(id);
        TempData["Success"] = "Başvuru silindi.";
        return RedirectToAction("Applications");
    }

    // ── Bildirimleri okundu işaretle ───────────────────────
    [HttpPost]
    public async Task<IActionResult> MarkNotifRead(int id)
    {
        await _notifSvc.MarkReadAsync(id, CurrentUserId);
        return Ok();
    }
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _userSvc.GetByIdAsync(CurrentUserId);
        if (user == null) return NotFound();
        return View(user);
    }

    [HttpPost]
    public async Task<IActionResult> Profile(
        string fullName, string? phoneNumber,
        string? studentNumber, string? department)
    {
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length < 3)
        {
            TempData["Error"] = "Ad soyad en az 3 karakter olmalıdır.";
            return RedirectToAction("Profile");
        }

        await _userSvc.UpdateProfileAsync(
            CurrentUserId, fullName, phoneNumber,
            studentNumber, department,
            null, null, null);

        TempData["Success"] = "Profil güncellendi!";
        return RedirectToAction("Profile");
    }
}
