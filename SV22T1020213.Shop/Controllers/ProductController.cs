using Microsoft.AspNetCore.Mvc;
using SV22T1020213.Admin;
using SV22T1020213.BusinessLayers;
using SV22T1020213.Models.Catalog;
using SV22T1020213.Models.Common;
using SV22T1020213.Shop.Models;

namespace SV22T1020213.Shop.Controllers
{
    public class ProductController : Controller
    {
        private const int PAGE_SIZE = 12;
        private const string SEARCH_PRODUCT_SHOP = "SearchProductShop";

        [HttpGet]
        //  Chuyển thành async Task để đợi (await) dữ liệu từ database
        public async Task<IActionResult> Index(int categoryId = 0, string searchValue = "")
        {
            // Lấy điều kiện tìm kiếm từ Session (nếu có)
            var input = ApplicationContext.GetSessionData<ProductSearchInput>(SEARCH_PRODUCT_SHOP);


            if (input == null)
            {
                input = new ProductSearchInput()
                {
                    Page = 1,
                    PageSize = PAGE_SIZE,
                    SearchValue = searchValue,
                    CategoryID = categoryId,
                    SupplierID = 0,
                    SortOrder = "newest"
                };
            }
            else
            {
                // Ưu tiên dữ liệu từ URL truyền vào (trang chủ hoặc link trực tiếp)
                if (Request.Query.ContainsKey("categoryId"))
                {
                    input.CategoryID = categoryId;
                    input.Page = 1;
                }

                if (Request.Query.ContainsKey("searchValue"))
                {
                    input.SearchValue = searchValue ?? "";
                    input.Page = 1;
                }
            }

            // Lưu lại cấu hình mới vào Session
            ApplicationContext.SetSessionData(SEARCH_PRODUCT_SHOP, input);

            //  Lấy danh sách Categories (Giống hệt code cũ bạn đưa)
            var categoriesInput = new PaginationSearchInput { Page = 1, PageSize = 100, SearchValue = "" };
            var categoriesResult = await CatalogDataService.ListCategoriesAsync(categoriesInput);

            //  Khởi tạo Model và nạp dữ liệu
            var model = new ProductSearchViewModel()
            {
                SearchInput = input,
                // Lấy DataItems từ kết quả PagedResult
                Categories = categoriesResult.DataItems
            };

            return View(model);
        }

        /// <summary>
        /// Tìm kiếm và trả về kết quả phân trang (Partial View)
        /// </summary>
        public async Task<IActionResult> SearchProduct(ProductSearchInput input)
        {
            // Đảm bảo PageSize luôn chuẩn
            input.PageSize = PAGE_SIZE;

            var products = await CatalogDataService.ListProductsAsync(input);

            // Lưu lại điều kiện tìm kiếm vào session để đồng bộ
            ApplicationContext.SetSessionData(SEARCH_PRODUCT_SHOP, input);

            return View("Search", products);
        }

        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var product = await CatalogDataService.GetProductAsync(id);
            if (product == null)
            {
                return RedirectToAction("Index");
            }

            var attributes = await CatalogDataService.ListAttributesAsync(id);
            var photos = await CatalogDataService.ListPhotosAsync(id);

            ViewBag.Attributes = attributes;
            ViewBag.Photos = photos;

            return View(product);
        }
    }
}