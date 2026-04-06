using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SV22T1020213.Admin;
using SV22T1020213.BusinessLayers;
using SV22T1020213.Models.Sales;
using System.Security.Claims;

namespace SV22T1020213.Shop.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int? status = null)
        {
            // Lấy CustomerID từ Claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return RedirectToAction("Login", "ShopAccount");
            }
            int customerId = int.Parse(userIdClaim.Value);

            var input = new OrderSearchInput
            {
                Page = page,
                PageSize = 10,
                SearchValue = "",
                Status = status.HasValue ? (OrderStatusEnum)status.Value : null
            };

            //  Dùng phương thức mới để lấy đơn hàng theo customer
            var orders = await SalesDataService.ListOrdersByCustomerAsync(customerId, input);

            ViewBag.CurrentStatus = status;
            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return RedirectToAction("Login", "ShopAccount");
            }
            int customerId = int.Parse(userIdClaim.Value);

            var order = await SalesDataService.GetOrderAsync(id);
            if (order == null)
            {
                return RedirectToAction("Index");
            }

            //  KIỂM TRA QUYỀN
            if (order.CustomerID != customerId)
            {
                TempData["Error"] = "Bạn không có quyền xem đơn hàng này";
                return RedirectToAction("Index");
            }

            var details = await SalesDataService.ListDetailsAsync(id);
            ViewBag.Details = details;

            return View(order);
        }

        [HttpPost]
        public async Task<IActionResult> CancelOrderShop(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return RedirectToAction("Login", "ShopAccount");
            }
            int customerId = int.Parse(userIdClaim.Value);

            var order = await SalesDataService.GetOrderAsync(id);
            if (order == null)
            {
                return RedirectToAction("Index");
            }

            if (order.CustomerID != customerId)
            {
                TempData["Error"] = "Bạn không có quyền hủy đơn hàng này";
                return RedirectToAction("Detail", new { id });
            }

            if (order.Status != OrderStatusEnum.New)
            {
                TempData["Error"] = "Chỉ có thể hủy đơn hàng đang chờ duyệt";
                return RedirectToAction("Detail", new { id });
            }

            
            // Gọi hàm CancelOrderShop dành riêng cho giao diện khách hàng, chỉ truyền id đơn hàng
            bool result = await SalesDataService.CancelOrderShop(id);

            if (result)
            {
                TempData["Success"] = "Đã hủy đơn hàng thành công";
            }
            else
            {
                TempData["Error"] = "Hủy đơn hàng thất bại";
            }

            return RedirectToAction("Detail", new { id });
        }
    }
}