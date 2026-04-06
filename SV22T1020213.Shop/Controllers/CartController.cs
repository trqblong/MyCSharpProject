using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SV22T1020213.BusinessLayers;
using SV22T1020213.Models.Sales;
using SV22T1020213.Shop.Models;
using System.Security.Claims;
using System.Text.Json;

namespace SV22T1020213.Shop.Controllers
{
    public class CartController : Controller
    {
        private const string CART_SESSION_KEY = "ShoppingCart";
        // Key lưu trữ những mặt hàng người dùng tích chọn để mua
        private const string SELECTED_CART_SESSION_KEY = "SelectedCartItems";

        private List<CartItem> GetCartFromSession()
        {
            var cartJson = HttpContext.Session.GetString(CART_SESSION_KEY);
            if (string.IsNullOrEmpty(cartJson))
            {
                return new List<CartItem>();
            }
            return JsonSerializer.Deserialize<List<CartItem>>(cartJson) ?? new List<CartItem>();
        }

        private void SaveCartToSession(List<CartItem> cart)
        {
            var cartJson = JsonSerializer.Serialize(cart);
            HttpContext.Session.SetString(CART_SESSION_KEY, cartJson);
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Index()
        {
            var cart = GetCartFromSession();
            return View(cart);
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            if (quantity <= 0) quantity = 1;

            var product = await CatalogDataService.GetProductAsync(productId);
            if (product == null)
            {
                return Json(new { success = false, message = "Sản phẩm không tồn tại" });
            }

            var cart = GetCartFromSession();
            var existingItem = cart.FirstOrDefault(x => x.ProductID == productId);

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                cart.Add(new CartItem
                {
                    ProductID = product.ProductID,
                    ProductName = product.ProductName,
                    Photo = string.IsNullOrEmpty(product.Photo) ? "nophoto.png" : product.Photo,
                    Unit = product.Unit,
                    Quantity = quantity,
                    SalePrice = product.Price
                });
            }

            SaveCartToSession(cart);
            int totalItems = cart.Sum(x => x.Quantity);

            return Json(new { success = true, cartCount = totalItems });
        }

        [AllowAnonymous]
        [HttpPost]
        public IActionResult UpdateCart(int productId, int quantity)
        {
            if (quantity < 0) quantity = 0;

            var cart = GetCartFromSession();
            var item = cart.FirstOrDefault(x => x.ProductID == productId);

            if (item != null)
            {
                if (quantity == 0)
                {
                    cart.Remove(item);
                }
                else
                {
                    item.Quantity = quantity;
                }
                SaveCartToSession(cart);
            }

            return RedirectToAction("Index");
        }

        [AllowAnonymous]
        [HttpPost]
        public IActionResult RemoveFromCart(int productId)
        {
            var cart = GetCartFromSession();
            var item = cart.FirstOrDefault(x => x.ProductID == productId);
            if (item != null)
            {
                cart.Remove(item);
                SaveCartToSession(cart);
            }
            return RedirectToAction("Index");
        }

        [AllowAnonymous]
        [HttpPost]
        public IActionResult ClearCart()
        {
            HttpContext.Session.Remove(CART_SESSION_KEY);
            return RedirectToAction("Index");
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult GetCartCount()
        {
            var cart = GetCartFromSession();
            int count = cart.Sum(x => x.Quantity);
            return Json(new { count });
        }

        [AllowAnonymous]
        [HttpPost]
        public IActionResult PrepareCheckout(List<int> selectedIds)
        {
            if (selectedIds == null || selectedIds.Count == 0)
            {
                TempData["Error"] = "Vui lòng chọn ít nhất một sản phẩm.";
                return RedirectToAction("Index");
            }

            // Lưu danh sách ID các món muốn thanh toán vào Session riêng
            HttpContext.Session.SetString(SELECTED_CART_SESSION_KEY, JsonSerializer.Serialize(selectedIds));

            return RedirectToAction("Checkout");
        }

        [Authorize]
        [HttpGet]
        public IActionResult Checkout()
        {
            var cart = GetCartFromSession();

            // Lấy danh sách ID đã tích chọn từ Session
            var selectedJson = HttpContext.Session.GetString(SELECTED_CART_SESSION_KEY);
            if (string.IsNullOrEmpty(selectedJson))
            {
                return RedirectToAction("Index");
            }

            var selectedIds = JsonSerializer.Deserialize<List<int>>(selectedJson);

            // CHỈ lọc ra những mặt hàng nằm trong giỏ VÀ đã được chọn
            var checkoutItems = cart.Where(x => selectedIds.Contains(x.ProductID)).ToList();

            if (checkoutItems.Count == 0)
            {
                return RedirectToAction("Index");
            }

            return View(checkoutItems); // Truyền qua View danh sách lọc
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Checkout(string deliveryProvince, string deliveryAddress)
        {
            var cart = GetCartFromSession();

            var selectedJson = HttpContext.Session.GetString(SELECTED_CART_SESSION_KEY);
            if (string.IsNullOrEmpty(selectedJson))
            {
                return RedirectToAction("Index");
            }

            var selectedIds = JsonSerializer.Deserialize<List<int>>(selectedJson);

            // CHỈ tạo đơn hàng cho các sản phẩm đã chọn
            var checkoutItems = cart.Where(x => selectedIds.Contains(x.ProductID)).ToList();

            if (checkoutItems.Count == 0)
            {
                TempData["Error"] = "Không có sản phẩm hợp lệ để thanh toán.";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(deliveryProvince) || string.IsNullOrWhiteSpace(deliveryAddress))
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ thông tin giao hàng";
                return RedirectToAction("Checkout");
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return RedirectToAction("Login", "ShopAccount");

            int customerId = int.Parse(userIdClaim.Value);

            var orderDetails = checkoutItems.Select(item => new OrderDetailViewInfo
            {
                ProductID = item.ProductID,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                SalePrice = item.SalePrice,
                Unit = item.Unit,
                Photo = item.Photo
            }).ToList();

            int orderId = await SalesDataService.AddOrderAsync(customerId, deliveryProvince, deliveryAddress, orderDetails);

            if (orderId > 0)
            {
                //  Chỉ xóa những sản phẩm đã thanh toán thành công khỏi giỏ hàng chính
                cart.RemoveAll(x => selectedIds.Contains(x.ProductID));
                SaveCartToSession(cart);

                // Xóa Session chọn món tạm thời
                HttpContext.Session.Remove(SELECTED_CART_SESSION_KEY);

                return RedirectToAction("Success", new { orderId });
            }
            else
            {
                TempData["Error"] = "Đặt hàng thất bại, vui lòng thử lại sau";
                return RedirectToAction("Checkout");
            }
        }

        [Authorize]
        [HttpGet]
        public IActionResult Success(int orderId)
        {
            ViewBag.OrderId = orderId;
            return View();
        }
    }
}