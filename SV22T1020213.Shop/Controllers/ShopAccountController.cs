// SV22T1020158.Shop/Controllers/ShopAccountController.cs
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SV22T1020213.Admin;
using SV22T1020213.BusinessLayers;
using SV22T1020213.Models.Partner;
using SV22T1020213.Shop.Models;
using System.Security.Claims;

namespace SV22T1020213.Shop.Controllers
{
    public class ShopAccountController : Controller
    {
        private const string CART_SESSION_KEY = "ShoppingCart";

        #region Register

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "ShopHome");
            }
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "ShopHome");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Kiểm tra email đã tồn tại chưa
            bool emailExists = !(await PartnerDataService.ValidateCustomerEmailAsync(model.Email, 0));
            if (emailExists)
            {
                ModelState.AddModelError("Email", "Email này đã được đăng ký");
                return View(model);
            }

            // Mã hóa mật khẩu
            string hashedPassword = CryptHelper.HashMD5(model.Password);

            // Debug
            System.Diagnostics.Debug.WriteLine($"Register - Raw password: {model.Password}");
            System.Diagnostics.Debug.WriteLine($"Register - Hashed password: {hashedPassword}");

            // Tạo khách hàng mới
            var customer = new Customer
            {
                CustomerName = model.CustomerName,
                ContactName = string.IsNullOrEmpty(model.ContactName) ? model.CustomerName : model.ContactName,
                Email = model.Email,
                Password = hashedPassword,  // Lưu password đã hash
                Phone = model.Phone ?? "",
                Address = model.Address ?? "",
                Province = model.Province ?? "",
                IsLocked = false
            };

            int customerId = await PartnerDataService.AddCustomerAsync(customer);
            if (customerId <= 0)
            {
                ModelState.AddModelError("", "Đăng ký thất bại, vui lòng thử lại sau");
                return View(model);
            }

            // Tự động đăng nhập sau khi đăng ký thành công
            await SignInCustomer(customer.Email, customer.CustomerName, customerId.ToString());

            TempData["SuccessMessage"] = "Đăng ký thành công! Chào mừng bạn đến với Long Shop.";
            return RedirectToAction("Index", "ShopHome");
        }

        #endregion

        #region Login / Logout

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "ShopHome");
            }

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "ShopHome");

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ thông tin";
                return View();
            }

            //  KHÔNG HASH 
            var userAccount = await SecurityDataService.AuthorizeAsync(email, password);

            if (userAccount == null)
            {
                ViewBag.Error = "Email hoặc mật khẩu không đúng";
                ViewBag.Email = email;
                return View();
            }

            if (!userAccount.RoleNames.Contains("customer"))
            {
                ViewBag.Error = "Tài khoản không có quyền truy cập";
                return View();
            }

            await SignInCustomer(userAccount.UserName, userAccount.DisplayName, userAccount.UserId);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "ShopHome");
        }
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Remove(CART_SESSION_KEY);
            await HttpContext.SignOutAsync();
            return RedirectToAction("Index", "ShopHome");
        }

        #endregion

        #region Profile Management

        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login");
            }

            int customerId = int.Parse(userId);
            var customer = await PartnerDataService.GetCustomerAsync(customerId);
            if (customer == null)
            {
                return RedirectToAction("Login");
            }

            var model = new UpdateProfileViewModel
            {
                CustomerID = customer.CustomerID,
                CustomerName = customer.CustomerName,
                ContactName = customer.ContactName,
                Email = customer.Email,
                Phone = customer.Phone,
                Address = customer.Address,
                Province = customer.Province
            };

            return View(model);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Profile(UpdateProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login");
            }

            int customerId = int.Parse(userId);

            bool isValidEmail = await PartnerDataService.ValidateCustomerEmailAsync(model.Email, customerId);
            if (!isValidEmail)
            {
                ModelState.AddModelError("Email", "Email này đã được sử dụng bởi tài khoản khác");
                return View(model);
            }

            var customer = new Customer
            {
                CustomerID = customerId,
                CustomerName = model.CustomerName,
                ContactName = model.ContactName,
                Email = model.Email,
                Phone = model.Phone ?? "",
                Address = model.Address ?? "",
                Province = model.Province ?? "",
                IsLocked = false
            };

            bool result = await PartnerDataService.UpdateCustomerAsync(customer);
            if (!result)
            {
                ModelState.AddModelError("", "Cập nhật thông tin thất bại");
                return View(model);
            }

            TempData["SuccessMessage"] = "Cập nhật thông tin thành công!";
            return RedirectToAction("Profile");
        }

        #endregion

        #region Change Password

        [Authorize]
        public IActionResult ChangePassword()
        {
            return View();
        }
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email))
                return RedirectToAction("Login");

            // ❌ KHÔNG HASH
            bool isValid = await SecurityDataService.VerifyPasswordAsync(email, model.OldPassword);
            if (!isValid)
            {
                ModelState.AddModelError("OldPassword", "Mật khẩu cũ không đúng");
                return View(model);
            }

            bool result = await SecurityDataService.ChangePasswordAsync(email, model.NewPassword);
            if (!result)
            {
                ModelState.AddModelError("", "Đổi mật khẩu thất bại");
                return View(model);
            }

            TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
            return RedirectToAction("Profile");
        }
        #endregion

        #region Access Denied

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        #endregion

        #region Helper Methods

        private async Task SignInCustomer(string email, string displayName, string userId)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, email),
                new Claim(ClaimTypes.Email, email),
                new Claim("DisplayName", displayName),
                new Claim(ClaimTypes.Role, "customer")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
        }

        #endregion
    }
}