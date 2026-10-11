using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StajTakip.Business.Services.Interfaces;
using StajTakip.Entities.Concrete;
using System.Security.Claims;

namespace StajTakip.Web.Controllers;

[Authorize(Policy = "CompanyOnly")]
public class CompanyController : Controller
{
    private readonly ICompanyService _companySvc;
    private readonly INotificationService _notifSvc;
    private readonly IUserService _userSvc;
    private readonly IMessageService _messageSvc;

    public CompanyController(ICompanyService companySvc,
        INotificationService notifSvc, IUserService userSvc,
        IMessageService messageSvc)
    {
        _companySvc = companySvc;
        _notifSvc = notifSvc;
        _userSvc = userSvc;
        _messageSvc = messageSvc;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── Dashboard ──────────────────────────────────────────
    public async Task<IActionResult> Dashboard()
    {
        var summary = await _companySvc.GetDashboardSummaryAsync(CurrentUserId);
        var recent = await _companySvc.GetRecentApplicationsAsync(CurrentUserId, 5);
        var notifs = await _notifSvc.GetUnreadAsync(CurrentUserId);
        var unreadMessages = await _messageSvc.GetUnreadCountAsync(CurrentUserId);

        ViewBag.Summary = summary;
        ViewBag.Notifs = notifs;
        ViewBag.UnreadMessages = unreadMessages;
        return View(recent);
    }

    // ── Gelen başvurular ───────────────────────────────────
    public async Task<IActionResult> Applications(string? status, int? postId)
    {
        var apps = await _companySvc.GetApplicationsAsync(CurrentUserId, status, postId);
        var posts = await _companySvc.GetMyPostsAsync(CurrentUserId);

        ViewBag.Posts = posts;
        ViewBag.CurrentStatus = status;
        ViewBag.CurrentPost = postId;
        return View(apps);
    }

    // ── Başvuru detay ──────────────────────────────────────
    public async Task<IActionResult> ApplicationDetail(int id)
    {
        var app = await _companySvc.GetApplicationDetailAsync(id, CurrentUserId);
        if (app == null) return Forbid();
        return View(app);
    }

    // ── Başvuru kabul ──────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> AcceptApplication(int id)
    {
        var app = await _companySvc.UpdateApplicationStatusAsync(
            id, CurrentUserId, ApplicationStatus.Accepted);

        await _notifSvc.CreateAsync(new Notification
        {
            UserId = app.StudentId,
            Title = "Tebrikler! Başvurunuz kabul edildi.",
            Message = $"{app.CompanyName} şirketine yaptığınız başvuru kabul edildi.",
            Type = NotificationType.Success,
            RelatedId = id
        });

        TempData["Success"] = "Başvuru kabul edildi.";
        return RedirectToAction("ApplicationDetail", new { id });
    }

    // ── Başvuru red ────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> RejectApplication(int id, string? note)
    {
        var app = await _companySvc.UpdateApplicationStatusAsync(
            id, CurrentUserId, ApplicationStatus.Rejected);

        await _notifSvc.CreateAsync(new Notification
        {
            UserId = app.StudentId,
            Title = "Başvurunuz reddedildi.",
            Message = $"{app.CompanyName} başvurunuz değerlendirme sonucunda reddedildi.",
            Type = NotificationType.Danger,
            RelatedId = id
        });

        TempData["Warning"] = "Başvuru reddedildi.";
        return RedirectToAction("Applications");
    }

    // ── Mülakat tarihi belirle ─────────────────────────────
    [HttpPost]
    public async Task<IActionResult> SetInterview(
        int applicationId, DateTime interviewDate,
        string? location, string? interviewType, string? note)
    {
        var interview = new Interview
        {
            ApplicationId = applicationId,
            InterviewDate = interviewDate,
            Location = location,
            InterviewType = interviewType,
            Note = note
        };

        var app = await _companySvc.AddInterviewAsync(interview, CurrentUserId);

        await _notifSvc.CreateAsync(new Notification
        {
            UserId = app.StudentId,
            Title = "Mülakat davetiniz var!",
            Message = $"{app.CompanyName} şirketi {interviewDate:dd.MM.yyyy HH:mm} tarihine mülakat belirledi.",
            Type = NotificationType.Info,
            RelatedId = applicationId
        });

        TempData["Success"] = "Mülakat tarihi belirlendi ve öğrenciye bildirim gönderildi.";
        return RedirectToAction("ApplicationDetail", new { id = applicationId });
    }

    // ── Staj ilanı yayınla ─────────────────────────────────
    [HttpGet]
    public IActionResult PostInternship() => View();

    [HttpPost]
    public async Task<IActionResult> PostInternship(
        string title, string description, string? department,
        string? city, DateTime? startDate, DateTime? endDate, int quota)
    {
        var post = new InternshipPost
        {
            CompanyId = CurrentUserId,
            Title = title,
            Description = description,
            Department = department,
            City = city,
            StartDate = startDate,
            EndDate = endDate,
            Quota = quota
        };

        await _companySvc.AddPostAsync(post);
        TempData["Success"] = "Staj ilanı yayınlandı!";
        return RedirectToAction("MyPosts");
    }

    // ── Staj ilanlarım ────────────────────────────────────
    public async Task<IActionResult> MyPosts()
    {
        var posts = await _companySvc.GetMyPostsAsync(CurrentUserId);
        return View(posts);
    }

    [HttpPost]
    public async Task<IActionResult> DeletePost(int id)
    {
        await _companySvc.DeletePostAsync(id, CurrentUserId);
        TempData["Success"] = "İlan silindi.";
        return RedirectToAction("MyPosts");
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
        string? companyName, string? companySector, string? companyCity)
    {
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length < 3)
        {
            TempData["Error"] = "Ad soyad en az 3 karakter olmalıdır.";
            return RedirectToAction("Profile");
        }

        if (string.IsNullOrWhiteSpace(companyName))
        {
            TempData["Error"] = "Şirket adı boş olamaz.";
            return RedirectToAction("Profile");
        }

        await _userSvc.UpdateProfileAsync(
            CurrentUserId, fullName, phoneNumber,
            null, null,
            companyName, companySector, companyCity);

        TempData["Success"] = "Profil güncellendi!";
        return RedirectToAction("Profile");
    }
}