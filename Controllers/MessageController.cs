using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StajTakip.Business.Services.Interfaces;
using StajTakip.Entities.Concrete;
using System.Security.Claims;

namespace StajTakip.Web.Controllers;

[Authorize]
public class MessageController : Controller
{
    private readonly IMessageService _messageSvc;
    private readonly IUserService _userSvc;

    public MessageController(IMessageService messageSvc, IUserService userSvc)
    {
        _messageSvc = messageSvc;
        _userSvc = userSvc;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private string CurrentRole =>
        User.FindFirstValue(ClaimTypes.Role)!;

    // Gelen kutusu
    public async Task<IActionResult> Inbox()
    {
        var messages = await _messageSvc.GetInboxAsync(CurrentUserId);
        var user = await _userSvc.GetByIdAsync(CurrentUserId);
        ViewBag.CurrentUser = user;
        return View(messages);
    }

    // Konuşma detayı
    public async Task<IActionResult> Conversation(int userId)
    {
        var otherUser = await _userSvc.GetByIdAsync(userId);
        if (otherUser == null) return NotFound();

        var messages = await _messageSvc.GetConversationAsync(CurrentUserId, userId);

        foreach (var msg in messages.Where(m => m.ReceiverId == CurrentUserId && !m.IsRead))
            await _messageSvc.MarkReadAsync(msg.Id, CurrentUserId);

        ViewBag.OtherUser = otherUser;
        ViewBag.CurrentUserId = CurrentUserId;
        return View(messages);
    }

    // Mesaj gönder
    [HttpPost]
    public async Task<IActionResult> Send(int receiverId, string content, int? applicationId)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            TempData["Error"] = "Mesaj boş olamaz.";
            return RedirectToAction("Conversation", new { userId = receiverId });
        }

        await _messageSvc.SendAsync(CurrentUserId, receiverId, content, applicationId);
        return RedirectToAction("Conversation", new { userId = receiverId });
    }

    // Yeni mesaj — kime gönderileceği seçilir
    public async Task<IActionResult> NewMessage()
    {
        List<User> contacts = new();

        if (CurrentRole == "Student")
        {
            var currentUser = await _userSvc.GetByIdAsync(CurrentUserId);
            // Öğrenci → akademisyenine ve başvurduğu şirketlere mesaj atabilir
            if (currentUser?.AdvisorId != null)
            {
                var advisor = await _userSvc.GetByIdAsync(currentUser.AdvisorId.Value);
                if (advisor != null) contacts.Add(advisor);
            }
            var companies = await _userSvc.GetCompaniesAsync();
            contacts.AddRange(companies);
        }
        else if (CurrentRole == "Advisor")
        {
            var currentUser = await _userSvc.GetByIdAsync(CurrentUserId);
            var allStudents = await _userSvc.GetAllStudentsAsync();
            contacts.AddRange(allStudents);
        }
        else if (CurrentRole == "Company")
        {
            var allStudents = await _userSvc.GetAllStudentsAsync();
            contacts.AddRange(allStudents);
        }

        ViewBag.Contacts = contacts;
        return View();
    }
}