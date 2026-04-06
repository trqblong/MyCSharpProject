using SV22T1020213.Models.Common;
using SV22T1020213.Models.Catalog;

namespace SV22T1020213.Shop.Models
{
    public class ProductSearchViewModel
    {
        public ProductSearchInput SearchInput { get; set; } = new();
        public PagedResult<Product> SearchResult { get; set; } = new();
        public List<Category> Categories { get; set; } = new();
    }
}