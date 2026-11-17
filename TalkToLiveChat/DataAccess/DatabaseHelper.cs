using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace TalkToLiveChat.DataAccess
{
    public interface IDatabaseHelper
    {
        SqlConnection CreateConnection();
        Task<int> ExecuteNonQueryAsync(string commandText, CommandType commandType, params SqlParameter[] parameters);
        Task<object?> ExecuteScalarAsync(string commandText, CommandType commandType, params SqlParameter[] parameters);
        Task<List<T>> ExecuteReaderListAsync<T>(string commandText, CommandType commandType, Func<SqlDataReader, T> mapFunction, params SqlParameter[] parameters);
        Task<T?> ExecuteReaderSingleAsync<T>(string commandText, CommandType commandType, Func<SqlDataReader, T> mapFunction, params SqlParameter[] parameters);
        Task ExecuteReaderMultiAsync(string commandText, CommandType commandType, Action<SqlDataReader> processResults, params SqlParameter[] parameters);
    }

    public class DatabaseHelper : IDatabaseHelper
    {
        private readonly string _connectionString;

        public DatabaseHelper(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("DefaultConnection string is not configured.");
        }

        public SqlConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public async Task<int> ExecuteNonQueryAsync(string commandText, CommandType commandType, params SqlParameter[] parameters)
        {
            using var connection = CreateConnection();
            using var command = new SqlCommand(commandText, connection)
            {
                CommandType = commandType
            };

            if (parameters != null && parameters.Length > 0)
            {
                command.Parameters.AddRange(parameters);
            }

            await connection.OpenAsync();
            return await command.ExecuteNonQueryAsync();
        }

        public async Task<object?> ExecuteScalarAsync(string commandText, CommandType commandType, params SqlParameter[] parameters)
        {
            using var connection = CreateConnection();
            using var command = new SqlCommand(commandText, connection)
            {
                CommandType = commandType
            };

            if (parameters != null && parameters.Length > 0)
            {
                command.Parameters.AddRange(parameters);
            }

            await connection.OpenAsync();
            return await command.ExecuteScalarAsync();
        }

        public async Task<List<T>> ExecuteReaderListAsync<T>(string commandText, CommandType commandType, Func<SqlDataReader, T> mapFunction, params SqlParameter[] parameters)
        {
            var result = new List<T>();
            using var connection = CreateConnection();
            using var command = new SqlCommand(commandText, connection)
            {
                CommandType = commandType
            };

            if (parameters != null && parameters.Length > 0)
            {
                command.Parameters.AddRange(parameters);
            }

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(mapFunction(reader));
            }

            return result;
        }

        public async Task<T?> ExecuteReaderSingleAsync<T>(string commandText, CommandType commandType, Func<SqlDataReader, T> mapFunction, params SqlParameter[] parameters)
        {
            using var connection = CreateConnection();
            using var command = new SqlCommand(commandText, connection)
            {
                CommandType = commandType
            };

            if (parameters != null && parameters.Length > 0)
            {
                command.Parameters.AddRange(parameters);
            }

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return mapFunction(reader);
            }

            return default;
        }

        public async Task ExecuteReaderMultiAsync(string commandText, CommandType commandType, Action<SqlDataReader> processResults, params SqlParameter[] parameters)
        {
            using var connection = CreateConnection();
            using var command = new SqlCommand(commandText, connection)
            {
                CommandType = commandType
            };

            if (parameters != null && parameters.Length > 0)
            {
                command.Parameters.AddRange(parameters);
            }

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            processResults(reader);
        }
    }

    public static class SqlDataReaderExtensions
    {
        public static string GetSafeString(this SqlDataReader reader, string columnName, string defaultValue = "")
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? defaultValue : reader.GetString(ordinal);
        }

        public static int GetSafeInt32(this SqlDataReader reader, string columnName, int defaultValue = 0)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? defaultValue : reader.GetInt32(ordinal);
        }

        public static int? GetSafeNullableInt32(this SqlDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
        }

        public static bool GetSafeBoolean(this SqlDataReader reader, string columnName, bool defaultValue = false)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? defaultValue : reader.GetBoolean(ordinal);
        }

        public static DateTime GetSafeDateTime(this SqlDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? DateTime.MinValue : reader.GetDateTime(ordinal);
        }

        public static DateTime? GetSafeNullableDateTime(this SqlDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
        }

        public static double? GetSafeNullableDouble(this SqlDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : Convert.ToDouble(reader.GetValue(ordinal));
        }
    }
}
