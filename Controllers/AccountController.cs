using Microsoft.AspNetCore.Mvc;
using StajTakip.Business.Services.Interfaces;
using StajTakip.Entities.Concrete;

namespace StajTakip.Web.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _auth;
    private readonly IUserService _userSvc;
    public AccountController(IAuthService auth, IUserService userSvc)
    {
        _auth = auth;
        _userSvc = userSvc;
    }
    

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    [HttpPost]
    public async Task<IActionResult> Login(string email, string password, string? selectedRole)
    {
        var token = await _auth.LoginAsync(email, password);
        if (token == null)
        {
            ViewBag.Error = "E-posta veya şifre hatalı.";
            return View();
        }

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var role = jwt.Claims
                         .FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)
                         ?.Value;

        // Seçilen rol ile gerçek rol eşleşmiyor mu?
        if (!string.IsNullOrEmpty(selectedRole) && role != selectedRole)
        {
            ViewBag.Error = "Seçtiğiniz rol ile hesabınızın rolü eşleşmiyor.";
            return View();
        }

        Response.Cookies.Append("jwt", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = false,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddHours(8)
        });

        return role switch
        {
            "Student" => RedirectToAction("Dashboard", "Student"),
            "Advisor" => RedirectToAction("Dashboard", "Advisor"),
            "Company" => RedirectToAction("Dashboard", "Company"),
            _ => RedirectToAction("Login")
        };
    }

    [HttpGet]
    
    public async Task<IActionResult> Register()
    {
        var advisors = await _userSvc.GetAdvisorsAsync();
        ViewBag.Advisors = advisors;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(
    string fullName, string email, string password,
    string role, string? studentNumber, string? department,
    string? companyName, string? companySector, string? companyCity,
    int? advisorId)
    {
        // Validation kontrolleri
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length < 3)
        {
            ViewBag.Error = "Ad soyad en az 3 karakter olmalıdır.";
            ViewBag.Advisors = await _userSvc.GetAdvisorsAsync();
            return View();
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
        {
            ViewBag.Error = "Geçerli bir e-posta adresi giriniz.";
            ViewBag.Advisors = await _userSvc.GetAdvisorsAsync();
            return View();
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            ViewBag.Error = "Şifre en az 6 karakter olmalıdır.";
            ViewBag.Advisors = await _userSvc.GetAdvisorsAsync();
            return View();
        }

        if (!password.Any(char.IsUpper))
        {
            ViewBag.Error = "Şifre en az bir büyük harf içermelidir.";
            ViewBag.Advisors = await _userSvc.GetAdvisorsAsync();
            return View();
        }

        if (!password.Any(char.IsDigit))
        {
            ViewBag.Error = "Şifre en az bir rakam içermelidir.";
            ViewBag.Advisors = await _userSvc.GetAdvisorsAsync();
            return View();
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            ViewBag.Error = "Lütfen bir rol seçiniz.";
            ViewBag.Advisors = await _userSvc.GetAdvisorsAsync();
            return View();
        }

        if (role == "Student" && string.IsNullOrWhiteSpace(department))
        {
            ViewBag.Error = "Lütfen bölümünüzü seçiniz.";
            ViewBag.Advisors = await _userSvc.GetAdvisorsAsync();
            return View();
        }

        if (role == "Company" && string.IsNullOrWhiteSpace(companyName))
        {
            ViewBag.Error = "Şirket adı boş olamaz.";
            ViewBag.Advisors = await _userSvc.GetAdvisorsAsync();
            return View();
        }

        var roleId = role switch
        {
            "Student" => 1,
            "Advisor" => 2,
            "Company" => 3,
            _ => 1
        };

        var user = new User
        {
            FullName = fullName,
            Email = email,
            RoleId = roleId,
            StudentNumber = studentNumber,
            Department = department,
            CompanyName = companyName,
            CompanySector = companySector,
            CompanyCity = companyCity,
            AdvisorId = role == "Student" ? advisorId : null
        };

        var success = await _auth.RegisterAsync(user, password);
        if (!success)
        {
            ViewBag.Error = "Bu e-posta adresi zaten kayıtlı.";
            ViewBag.Advisors = await _userSvc.GetAdvisorsAsync();
            return View();
        }

        TempData["Success"] = "Kayıt başarılı! Giriş yapabilirsiniz.";
        return RedirectToAction("Login");
    }

    public IActionResult Logout()
    {
        Response.Cookies.Delete("jwt");
        return RedirectToAction("Login");
    }
}