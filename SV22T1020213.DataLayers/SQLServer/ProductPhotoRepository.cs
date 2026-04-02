using Dapper;
using SV22T1020213.DataLayers.Interfaces;
using SV22T1020213.Models.Catalog;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace SV22T1020213.DataLayers.SQLServer
{
    public class ProductPhotoRepository : IProductPhotoRepository
    {
        private readonly string _connectionString;

        public ProductPhotoRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        private IDbConnection OpenConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public async Task<IList<ProductPhoto>> ListAsync(int productID)
        {
            using var connection = OpenConnection();

            string sql = @"
                SELECT *
                FROM ProductPhotos
                WHERE ProductID = @productID
                ORDER BY DisplayOrder
            ";

            var data = await connection.QueryAsync<ProductPhoto>(sql, new { productID });
            return data.ToList();
        }

        public async Task<long> AddAsync(ProductPhoto data)
        {
            using var connection = OpenConnection();

            string sql = @"
                INSERT INTO ProductPhotos
                (
                    ProductID,
                    Photo,
                    Description,
                    DisplayOrder,
                    IsHidden
                )
                VALUES
                (
                    @ProductID,
                    @Photo,
                    @Description,
                    @DisplayOrder,
                    @IsHidden
                );

                SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
            ";

            return await connection.ExecuteScalarAsync<long>(sql, data);
        }

        public async Task<bool> UpdateAsync(ProductPhoto data)
        {
            using var connection = OpenConnection();

            string sql = @"
                UPDATE ProductPhotos
                SET
                    Photo = @Photo,
                    Description = @Description,
                    DisplayOrder = @DisplayOrder,
                    IsHidden = @IsHidden
                WHERE PhotoID = @PhotoID
            ";

            return await connection.ExecuteAsync(sql, data) > 0;
        }

        public async Task<bool> DeleteAsync(long photoID)
        {
            using var connection = OpenConnection();

            string sql = @"DELETE FROM ProductPhotos WHERE PhotoID = @photoID";

            return await connection.ExecuteAsync(sql, new { photoID }) > 0;
        }
    }
}