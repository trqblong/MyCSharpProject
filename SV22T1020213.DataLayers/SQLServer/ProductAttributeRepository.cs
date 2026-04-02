using Dapper;
using SV22T1020213.DataLayers.Interfaces;
using SV22T1020213.Models.Catalog;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace SV22T1020213.DataLayers.SQLServer
{
    public class ProductAttributeRepository : IProductAttributeRepository
    {
        private readonly string _connectionString;

        public ProductAttributeRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        private IDbConnection OpenConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public async Task<IList<ProductAttribute>> ListAsync(int productID)
        {
            using var connection = OpenConnection();

            string sql = @"
                SELECT *
                FROM ProductAttributes
                WHERE ProductID = @productID
                ORDER BY DisplayOrder
            ";

            var data = await connection.QueryAsync<ProductAttribute>(sql, new { productID });
            return data.ToList();
        }

        public async Task<long> AddAsync(ProductAttribute data)
        {
            using var connection = OpenConnection();

            string sql = @"
                INSERT INTO ProductAttributes
                (
                    ProductID,
                    AttributeName,
                    AttributeValue,
                    DisplayOrder
                )
                VALUES
                (
                    @ProductID,
                    @AttributeName,
                    @AttributeValue,
                    @DisplayOrder
                );

                SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
            ";

            return await connection.ExecuteScalarAsync<long>(sql, data);
        }

        public async Task<bool> UpdateAsync(ProductAttribute data)
        {
            using var connection = OpenConnection();

            string sql = @"
                UPDATE ProductAttributes
                SET
                    AttributeName = @AttributeName,
                    AttributeValue = @AttributeValue,
                    DisplayOrder = @DisplayOrder
                WHERE AttributeID = @AttributeID
            ";

            return await connection.ExecuteAsync(sql, data) > 0;
        }

        public async Task<bool> DeleteAsync(long attributeID)
        {
            using var connection = OpenConnection();

            string sql = @"DELETE FROM ProductAttributes WHERE AttributeID = @attributeID";

            return await connection.ExecuteAsync(sql, new { attributeID }) > 0;
        }
    }
}