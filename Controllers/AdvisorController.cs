using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StajTakip.Business.Services.Interfaces;
using StajTakip.Entities.Concrete;
using System.Security.Claims;

namespace StajTakip.Web.Controllers;

[Authorize(Policy = "AdvisorOnly")]
public class AdvisorController : Controller
{
    private readonly IAdvisorService _advisorSvc;
    private readonly IApplicationService _appSvc;
    private readonly INotificationService _notifSvc;
    private readonly IUserService _userSvc;
    private readonly IMessageService _messageSvc;

    public AdvisorController(IAdvisorService advisorSvc,
        IApplicationService appSvc, INotificationService notifSvc,
        IUserService userSvc, IMessageService messageSvc)
    {
        _advisorSvc = advisorSvc;
        _appSvc = appSvc;
        _notifSvc = notifSvc;
        _userSvc = userSvc;
        _messageSvc = messageSvc;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── Dashboard ──────────────────────────────────────────
    public async Task<IActionResult> Dashboard()
    {
        var students = await _advisorSvc.GetMyStudentsAsync(CurrentUserId);
        var pending = await _advisorSvc.GetPendingApprovalsAsync(CurrentUserId);
        var notifs = await _notifSvc.GetUnreadAsync(CurrentUserId);
        var unreadMessages = await _messageSvc.GetUnreadCountAsync(CurrentUserId);

        ViewBag.StudentCount = students.Count;
        ViewBag.PendingCount = pending.Count;
        ViewBag.Notifs = notifs;
        ViewBag.UnreadMessages = unreadMessages;
        return View(students);
    }

    // ── Öğrenci listesi ────────────────────────────────────
    public async Task<IActionResult> Students()
    {
        var students = await _advisorSvc.GetMyStudentsAsync(CurrentUserId);
        return View(students);
    }

    // ── Öğrencinin başvuruları ─────────────────────────────
    public async Task<IActionResult> StudentApplications(int studentId)
    {
        var student = await _advisorSvc.GetStudentAsync(studentId, CurrentUserId);
        if (student == null) return Forbid();

        var apps = await _appSvc.GetByStudentIdAsync(studentId);
        ViewBag.Student = student;
        return View(apps);
    }

    // ── Başvuru detay + onay ───────────────────────────────
    public async Task<IActionResult> ApplicationDetail(int id)
    {
        var app = await _advisorSvc.GetApplicationForAdvisorAsync(id, CurrentUserId);
        if (app == null) return Forbid();
        return View(app);
    }

    // ── Staj onayı ─────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> ApproveApplication(int id, string? note)
    {
        await _advisorSvc.ApproveAsync(id, CurrentUserId, note);
        TempData["Success"] = "Staj başvurusu onaylandı.";
        return RedirectToAction("ApplicationDetail", new { id });
    }

    // ── Staj reddi ─────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> RejectApplication(int id, string? note)
    {
        await _advisorSvc.RejectAsync(id, CurrentUserId, note);
        TempData["Warning"] = "Staj başvurusu reddedildi.";
        return RedirectToAction("ApplicationDetail", new { id });
    }

    // ── Geri bildirim gönder ───────────────────────────────
    [HttpPost]
    public async Task<IActionResult> SendFeedback(int applicationId, int studentId, string message)
    {
        var feedback = new Feedback
        {
            ApplicationId = applicationId,
            AdvisorId = CurrentUserId,
            StudentId = studentId,
            Message = message
        };

        await _advisorSvc.SendFeedbackAsync(feedback);

        // Öğrenciye bildirim
        await _notifSvc.CreateAsync(new Notification
        {
            UserId = studentId,
            Title = "Danışmanınızdan geri bildirim",
            Message = "Danışmanınız bir başvurunuz için geri bildirim gönderdi.",
            Type = NotificationType.Info,
            RelatedId = applicationId
        });

        TempData["Success"] = "Geri bildirim gönderildi.";
        return RedirectToAction("ApplicationDetail", new { id = applicationId });
    }

    // ── Onay bekleyenler ───────────────────────────────────
    public async Task<IActionResult> PendingApprovals()
    {
        var pending = await _advisorSvc.GetPendingApprovalsAsync(CurrentUserId);
        return View(pending);
    }

    // ── İstatistik raporu ──────────────────────────────────
    public async Task<IActionResult> Statistics()
    {
        var stats = await _advisorSvc.GetStatisticsAsync(CurrentUserId);
        return View(stats);
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
        string fullName, string? phoneNumber, string? department)
    {
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length < 3)
        {
            TempData["Error"] = "Ad soyad en az 3 karakter olmalıdır.";
            return RedirectToAction("Profile");
        }

        await _userSvc.UpdateProfileAsync(
            CurrentUserId, fullName, phoneNumber,
            null, department,
            null, null, null);

        TempData["Success"] = "Profil güncellendi!";
        return RedirectToAction("Profile");
    }

}