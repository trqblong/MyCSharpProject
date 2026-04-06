using Microsoft.AspNetCore.Mvc;
using SV22T1020213.BusinessLayers;
using SV22T1020213.Models.Catalog;
using SV22T1020213.Models.Common;

namespace SV22T1020213.Shop.Controllers
{
    public class ShopHomeController : Controller
    {
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var input = new ProductSearchInput
            {
                Page = 1,
                PageSize = 8,
                SearchValue = "",
                CategoryID = 0,
                SupplierID = 0,
                MinPrice = null,
                MaxPrice = null
            };

            // Lấy sản phẩm nổi bật
            var featuredProducts = await CatalogDataService.ListProductsAsync(input);

            // Lấy danh mục
            var catInput = new PaginationSearchInput { Page = 1, PageSize = 100, SearchValue = "" };
            var categories = await CatalogDataService.ListCategoriesAsync(catInput);

            // Truyền dữ liệu qua ViewBag (vì không có Model cụ thể)
            ViewBag.Categories = categories?.DataItems ?? new List<Category>();
            ViewBag.FeaturedProducts = featuredProducts?.DataItems ?? new List<Product>();

          
            return View(); // Không truyền Model hoặc truyền 1 object rỗng
        }

        [HttpGet]
        public async Task<IActionResult> Categories(int? id)
        {
            var input = new PaginationSearchInput { Page = 1, PageSize = 100, SearchValue = "" };
            var categories = await CatalogDataService.ListCategoriesAsync(input);
            return View(categories?.DataItems ?? new List<Category>());
        }

        [HttpGet]
        public async Task<IActionResult> Search(string keyword, int page = 1)
        {
            var input = new ProductSearchInput
            {
                Page = page,
                PageSize = 12,
                SearchValue = keyword ?? "",
                CategoryID = 0,
                SupplierID = 0,
                MinPrice = null,
                MaxPrice = null
            };

            var products = await CatalogDataService.ListProductsAsync(input);
            ViewBag.Keyword = keyword;
            return View(products ?? new PagedResult<Product>());
        }
    }
}