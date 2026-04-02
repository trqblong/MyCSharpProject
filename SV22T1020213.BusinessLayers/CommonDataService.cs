using SV22T1020213.Models.Common;

namespace SV22T1020213.BusinessLayers
{
    /// <summary>
    /// Các chức năng dữ liệu chung của hệ thống
    /// </summary>
    public static class CommonDataService
    {
        public static PaginationSearchInput CreateSearchInput(
            int page,
            int pageSize,
            string searchValue)
        {
            return new PaginationSearchInput()
            {
                Page = page,
                PageSize = pageSize,
                SearchValue = searchValue ?? ""
            };
        }

    }
}