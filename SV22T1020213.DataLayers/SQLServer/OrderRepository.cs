using Dapper;
using SV22T1020213.DataLayers.Interfaces;
using SV22T1020213.Models.Common;
using SV22T1020213.Models.Sales;


namespace SV22T1020213.DataLayers.SQLServer
{
    public class OrderRepository : BaseRepository, IOrderRepository
    {
        public OrderRepository(string connectionString) : base(connectionString) { }

        public async Task<int> AddOrderAsync(Order data, IEnumerable<OrderDetailViewInfo> details)
        {
            using var connection = GetConnection();
            await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // Xử lý null
                int? customerId = data.CustomerID;
                string deliveryProvince = string.IsNullOrEmpty(data.DeliveryProvince) ? "" : data.DeliveryProvince;
                string deliveryAddress = string.IsNullOrEmpty(data.DeliveryAddress) ? "" : data.DeliveryAddress;

                // 1. Lưu thông tin đơn hàng
                string sqlOrder = @"
            INSERT INTO Orders(CustomerID, EmployeeID, OrderTime, Status, DeliveryProvince, DeliveryAddress)
            VALUES(@CustomerID, @EmployeeID, @OrderTime, @Status, @DeliveryProvince, @DeliveryAddress);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                int orderID = await connection.ExecuteScalarAsync<int>(sqlOrder, new
                {
                    CustomerID = customerId,
                    EmployeeID = data.EmployeeID,
                    OrderTime = data.OrderTime,
                    Status = (int)data.Status,
                    DeliveryProvince = deliveryProvince,
                    DeliveryAddress = deliveryAddress
                }, transaction: transaction);

                if (orderID <= 0)
                {
                    await transaction.RollbackAsync();
                    return 0;
                }

                // 2. Lưu chi tiết đơn hàng
                string sqlDetail = @"
            INSERT INTO OrderDetails(OrderID, ProductID, Quantity, SalePrice)
            VALUES(@OrderID, @ProductID, @Quantity, @SalePrice)";

                foreach (var item in details)
                {
                    await connection.ExecuteAsync(sqlDetail, new
                    {
                        OrderID = orderID,
                        ProductID = item.ProductID,
                        Quantity = item.Quantity,
                        SalePrice = item.SalePrice
                    }, transaction: transaction);
                }

                await transaction.CommitAsync();
                return orderID;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return 0;
            }
        }
        public async Task<bool> DeleteAsync(int orderID)
        {
            using var connection = GetConnection();
            await connection.ExecuteAsync("DELETE FROM OrderDetails WHERE OrderID=@orderID", new { orderID });
            return await connection.ExecuteAsync("DELETE FROM Orders WHERE OrderID=@orderID", new { orderID }) > 0;
        }

        public async Task<OrderViewInfo?> GetAsync(int orderID)
        {
            using var connection = GetConnection();

            string sql = @"
                SELECT 
                    O.*,
                    C.CustomerName,
                    C.ContactName AS CustomerContactName,
                    C.Email AS CustomerEmail,
                    C.Phone AS CustomerPhone,
                    C.Address AS CustomerAddress,
                    E.FullName AS EmployeeName,
                    S.ShipperName,
                    S.Phone AS ShipperPhone
                FROM Orders O
                LEFT JOIN Customers C ON O.CustomerID = C.CustomerID
                LEFT JOIN Employees E ON O.EmployeeID = E.EmployeeID
                LEFT JOIN Shippers S ON O.ShipperID = S.ShipperID
                WHERE O.OrderID = @orderID";

            return await connection.QueryFirstOrDefaultAsync<OrderViewInfo>(sql, new { orderID });
        }

        public async Task<bool> UpdateAsync(Order data)
        {
            using var connection = GetConnection();

            string sql = @"
                UPDATE Orders
                SET CustomerID = @CustomerID,
                    EmployeeID = @EmployeeID,
                    AcceptTime = @AcceptTime,
                    ShipperID = @ShipperID,
                    ShippedTime = @ShippedTime,
                    FinishedTime = @FinishedTime,
                    Status = @Status,
                    DeliveryProvince = @DeliveryProvince,
                    DeliveryAddress = @DeliveryAddress
                WHERE OrderID = @OrderID";

            return await connection.ExecuteAsync(sql, data) > 0;
        }

        public async Task<PagedResult<OrderViewInfo>> ListAsync(OrderSearchInput input)
        {
            using var connection = GetConnection();

            var condition = "";
            var parameters = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(input.SearchValue))
            {
                condition += " AND C.CustomerName LIKE @SearchValue";
                parameters.Add("SearchValue", $"%{input.SearchValue}%");
            }

            if (input.Status.HasValue)
            {
                condition += " AND O.Status = @Status";
                parameters.Add("Status", (int)input.Status.Value);
            }

            if (input.DateFrom.HasValue)
            {
                condition += " AND O.OrderTime >= @FromTime";
                parameters.Add("FromTime", input.DateFrom.Value);
            }

            if (input.DateTo.HasValue)
            {
                condition += " AND O.OrderTime <= @ToTime";
                parameters.Add("ToTime", input.DateTo.Value);
            }

            string sqlCount;
            if (!string.IsNullOrWhiteSpace(input.SearchValue))
            {
                sqlCount = @"
                    SELECT COUNT(*)
                    FROM Orders O
                    LEFT JOIN Customers C ON O.CustomerID = C.CustomerID
                    WHERE 1=1 " + condition;
            }
            else
            {
                sqlCount = @"
                    SELECT COUNT(*)
                    FROM Orders O
                    WHERE 1=1 " + condition;
            }

            int count = await connection.ExecuteScalarAsync<int>(sqlCount, parameters);

            string sqlData = @"
                SELECT 
                    O.OrderID,
                    O.CustomerID,
                    O.EmployeeID,
                    O.OrderTime,
                    O.AcceptTime,
                    O.Status,
                    C.CustomerName,
                    C.Phone AS CustomerPhone,
                    E.FullName AS EmployeeName,
                    ISNULL(T.TotalPrice, 0) AS TotalPrice
                FROM Orders O
                LEFT JOIN Customers C ON O.CustomerID = C.CustomerID
                LEFT JOIN Employees E ON O.EmployeeID = E.EmployeeID
                OUTER APPLY (
                    SELECT SUM(Quantity * SalePrice) AS TotalPrice
                    FROM OrderDetails
                    WHERE OrderID = O.OrderID
                ) T
                WHERE 1=1 " + condition + @"
                ORDER BY O.OrderTime DESC
                OFFSET @offset ROWS FETCH NEXT @pagesize ROWS ONLY";

            parameters.Add("offset", (input.Page - 1) * input.PageSize);
            parameters.Add("pagesize", input.PageSize);

            var data = await connection.QueryAsync<OrderViewInfo>(sqlData, parameters);

            return new PagedResult<OrderViewInfo>()
            {
                Page = input.Page,
                PageSize = input.PageSize,
                RowCount = count,
                DataItems = data.ToList()
            };
        }

        public async Task<List<OrderDetailViewInfo>> ListDetailsAsync(int orderID)
        {
            using var connection = GetConnection();

            string sql = @"
                SELECT 
                    OD.OrderID, 
                    OD.ProductID, 
                    OD.Quantity, 
                    OD.SalePrice,
                    P.ProductName, 
                    P.Unit, 
                    P.Photo
                FROM OrderDetails OD
                JOIN Products P ON OD.ProductID = P.ProductID
                WHERE OD.OrderID = @orderID";

            var data = await connection.QueryAsync<OrderDetailViewInfo>(sql, new { orderID });
            return data.ToList();
        }

        public async Task<OrderDetailViewInfo?> GetDetailAsync(int orderID, int productID)
        {
            using var connection = GetConnection();

            string sql = @"
                SELECT 
                    OD.OrderID, 
                    OD.ProductID, 
                    OD.Quantity, 
                    OD.SalePrice,
                    P.ProductName, 
                    P.Unit, 
                    P.Photo
                FROM OrderDetails OD
                JOIN Products P ON OD.ProductID = P.ProductID
                WHERE OD.OrderID = @orderID AND OD.ProductID = @productID";

            return await connection.QueryFirstOrDefaultAsync<OrderDetailViewInfo>(sql, new { orderID, productID });
        }

        public async Task<bool> AddDetailAsync(OrderDetail data)
        {
            using var connection = GetConnection();

            string sql = @"
                INSERT INTO OrderDetails(OrderID, ProductID, Quantity, SalePrice)
                VALUES(@OrderID, @ProductID, @Quantity, @SalePrice)";

            return await connection.ExecuteAsync(sql, data) > 0;
        }

        public async Task<bool> UpdateDetailAsync(OrderDetail data)
        {
            using var connection = GetConnection();

            string sql = @"
                UPDATE OrderDetails
                SET Quantity = @Quantity,
                    SalePrice = @SalePrice
                WHERE OrderID = @OrderID AND ProductID = @ProductID";

            return await connection.ExecuteAsync(sql, data) > 0;
        }

        public async Task<bool> DeleteDetailAsync(int orderID, int productID)
        {
            using var connection = GetConnection();

            string sql = @"DELETE FROM OrderDetails WHERE OrderID = @orderID AND ProductID = @productID";

            return await connection.ExecuteAsync(sql, new { orderID, productID }) > 0;
        }

        // Thêm phương thức này vào class OrderRepository
        public async Task<PagedResult<OrderViewInfo>> ListByCustomerAsync(int customerId, OrderSearchInput input)
        {
            using var connection = GetConnection();

            var parameters = new DynamicParameters();
            parameters.Add("CustomerID", customerId);

            string condition = " AND O.CustomerID = @CustomerID";

            // 🔍 Status
            if (input.Status.HasValue)
            {
                condition += " AND O.Status = @Status";
                parameters.Add("Status", (int)input.Status.Value);
            }

            // 🔍 From date
            if (input.DateFrom.HasValue)
            {
                condition += " AND O.OrderTime >= @FromTime";
                parameters.Add("FromTime", input.DateFrom.Value);
            }

            // 🔍 To date
            if (input.DateTo.HasValue)
            {
                condition += " AND O.OrderTime <= @ToTime";
                parameters.Add("ToTime", input.DateTo.Value);
            }

            // 🔢 COUNT
            string sqlCount = $@"
        SELECT COUNT(*)
        FROM Orders O
        WHERE 1=1 {condition}";

            int count = await connection.ExecuteScalarAsync<int>(sqlCount, parameters);

            // 📄 DATA
            string sqlData = $@"
        SELECT 
            O.OrderID,
            O.CustomerID,
            O.EmployeeID,
            O.OrderTime,
            O.AcceptTime,
            O.Status,
            C.CustomerName,
            C.Phone AS CustomerPhone,
            E.FullName AS EmployeeName,
            ISNULL(T.TotalPrice, 0) AS TotalPrice
        FROM Orders O
        LEFT JOIN Customers C ON O.CustomerID = C.CustomerID
        LEFT JOIN Employees E ON O.EmployeeID = E.EmployeeID
        OUTER APPLY (
            SELECT SUM(Quantity * SalePrice) AS TotalPrice
            FROM OrderDetails
            WHERE OrderID = O.OrderID
        ) T
        WHERE 1=1 {condition}
        ORDER BY O.OrderTime DESC
        OFFSET @offset ROWS FETCH NEXT @pagesize ROWS ONLY";

            parameters.Add("offset", (input.Page - 1) * input.PageSize);
            parameters.Add("pagesize", input.PageSize);

            var data = await connection.QueryAsync<OrderViewInfo>(sqlData, parameters);

            return new PagedResult<OrderViewInfo>()
            {
                Page = input.Page,
                PageSize = input.PageSize,
                RowCount = count,
                DataItems = data.ToList()
            };
        }
    }
}