using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Services;
using ReadManager.Web.ViewModels;

namespace ReadManager.Web.Controllers;
public class AccountController(AuthApiClient api) : Controller
{
    [HttpGet] public IActionResult Login(string? returnUrl = null) => View(new LoginVm { ReturnUrl = returnUrl });
    [HttpGet] public IActionResult Register() => View(new RegisterVm());
    [HttpGet] public IActionResult AccessDenied() => View();

    // Trang thông tin tài khoản (cần đăng nhập)
    [Authorize] public IActionResult Profile() => View();

    // Đổi mật khẩu: chờ API của Backend 2 nên hiện chỉ kiểm tra form
    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public IActionResult ChangePassword(string? current, string? next, string? confirm)
    {
        if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(next) || next.Length < 6) TempData["Toast"] = "Mật khẩu mới tối thiểu 6 ký tự.";
        else if (next != confirm) TempData["Toast"] = "Xác nhận mật khẩu không khớp.";
        else TempData["Toast"] = "Đổi mật khẩu cần API từ Backend 2 (chưa có), nên mật khẩu chưa được thay đổi.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        try
        {
            using var response = await api.LoginAsync(vm.Email.Trim(), vm.Password);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LoginResult>();
                if (result == null || result.User == null || string.IsNullOrEmpty(result.AccessToken))
                {
                    ModelState.AddModelError("", "Đăng nhập thành công nhưng phản hồi từ API không đúng định dạng.");
                    return View(vm);
                }

                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, result.User.UserId.ToString()),
                    new Claim(ClaimTypes.Name, result.User.DisplayName ?? result.User.Email),
                    new Claim(ClaimTypes.Email, result.User.Email),
                    new Claim(ClaimTypes.Role, result.User.Role ?? "User")
                };

                var properties = new AuthenticationProperties
                {
                    IsPersistent = vm.RememberMe,
                    ExpiresUtc = result.ExpiresAt
                };
                properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = result.AccessToken }]);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
                    properties);

                if (Url.IsLocalUrl(vm.ReturnUrl))
                {
                    return LocalRedirect(vm.ReturnUrl!);
                }

                return result.User.Role == "Admin"
                    ? RedirectToAction("Index", "Stories", new { area = "Admin" })
                    : RedirectToAction("Index", "Home");
            }

            // Xử lý các mã lỗi HTTP cụ thể từ API
            var errorMessage = response.StatusCode switch
            {
                System.Net.HttpStatusCode.TooManyRequests => "Bạn thử đăng nhập quá nhiều lần. Vui lòng chờ một phút.",
                System.Net.HttpStatusCode.Unauthorized => "Email hoặc mật khẩu không đúng, hoặc tài khoản không hoạt động.",
                System.Net.HttpStatusCode.Forbidden => "Tài khoản của bạn đã bị khóa.",
                _ => $"API trả về lỗi (Mã: {(int)response.StatusCode}). Vui lòng thử lại."
            };

            ModelState.AddModelError("", errorMessage);
        }
        catch (HttpRequestException ex)
        {
            ModelState.AddModelError("", $"Không thể kết nối đến máy chủ API: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            ModelState.AddModelError("", "API phản hồi quá chậm (Timeout). Vui lòng thử lại.");
        }

        vm.Password = "";
        ModelState.Remove(nameof(vm.Password));
        return View(vm);
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var token = await HttpContext.GetTokenAsync("access_token");
        if (token is not null)
        {
            try
            {
                using var response = await api.LogoutAsync(token);
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVm vm)
    {
        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        try
        {
            using var response = await api.RegisterAsync(vm.DisplayName.Trim(), vm.Email.Trim(), vm.Password);

            if (response.IsSuccessStatusCode)
            {
                TempData["RegistrationSuccess"] = "Đăng ký thành công. Hãy đăng nhập.";
                return RedirectToAction("Login");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                ModelState.AddModelError("", "Email này đã được sử dụng. Vui lòng chọn email khác.");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                ModelState.AddModelError("", "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại.");
            }
            else
            {
                ModelState.AddModelError("", $"API xử lý không thành công (Mã: {(int)response.StatusCode}).");
            }
        }
        catch (HttpRequestException ex)
        {
            ModelState.AddModelError("", $"Không thể kết nối đến máy chủ API: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            ModelState.AddModelError("", "API phản hồi quá chậm (Timeout). Vui lòng thử lại.");
        }

        vm.Password = "";
        vm.ConfirmPassword = "";
        ModelState.Remove(nameof(vm.Password));
        ModelState.Remove(nameof(vm.ConfirmPassword));
        return View(vm);
    }
}