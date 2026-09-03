using Microsoft.Data.SqlClient;
using System.Data;

namespace PayrollManagement.Data
{
    public static class DbHelper
    {
        public static string ConnectionString
        {
            get => DbConfig.ConnectionString;
            set { /* kept for compatibility with spec - actual string is in DbConfig/App.config */ }
        }

        public static SqlConnection GetConnection()
        {
            var conn = new SqlConnection(DbConfig.ConnectionString);
            conn.Open();
            return conn;
        }

        public static async Task<SqlConnection> GetConnectionAsync()
        {
            var cs = DbConfig.ConnectionString;
            var conn = new SqlConnection(cs);
            await conn.OpenAsync();
            return conn;
        }

        /// <summary>Parameterized SELECT -> List of object[] (row-based).</summary>
        public static List<object[]> FetchRows(string sql, params (string name, object value)[] parameters)
        {
            var rows = new List<object[]>();
            using var conn = GetConnection();
            using var cmd = new SqlCommand(sql, conn);
            foreach (var (name, value) in parameters)
                cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var row = new object[reader.FieldCount];
                reader.GetValues(row);
                for (int i = 0; i < row.Length; i++)
                    if (row[i] is DBNull) row[i] = null!;
                rows.Add(row);
            }
            return rows;
        }

        public static int ExecuteNonQuery(string sql, params (string name, object value)[] parameters)
        {
            using var conn = GetConnection();
            using var cmd = new SqlCommand(sql, conn);
            foreach (var (name, value) in parameters)
                cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
            return cmd.ExecuteNonQuery();
        }

        public static object? ExecuteScalar(string sql, params (string name, object value)[] parameters)
        {
            using var conn = GetConnection();
            using var cmd = new SqlCommand(sql, conn);
            foreach (var (name, value) in parameters)
                cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
            var result = cmd.ExecuteScalar();
            return result is DBNull ? null : result;
        }

        public static bool IsDbConfigured()
        {
            if (string.IsNullOrWhiteSpace(DbConfig.ConnectionString)) return false;
            try
            {
                using var conn = GetConnection();
                return true;
            }
            catch { return false; }
        }
    }
}
