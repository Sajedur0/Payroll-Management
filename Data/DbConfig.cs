using System.Configuration;
using Microsoft.Data.SqlClient;

namespace DashboardApp.Data
{
    public static class DbConfig
    {
        /// <summary>
        /// Loads ConnectionString from App.config -> connectionStrings -> AttendanceDB
        /// Falls back to a default connection string if not found
        /// </summary>
        public static string ConnectionString
        {
            get
            {
                try
                {
                    var cs = ConfigurationManager.ConnectionStrings["AttendanceDB"]?.ConnectionString;
                    if (!string.IsNullOrWhiteSpace(cs))
                        return cs;
                }
                catch { }

                // Fallback - Try Local SQL Server
                return "Server=.;Database=AttendanceDB;Trusted_Connection=True;TrustServerCertificate=True;";
            }
        }

        /// <summary>
        /// Loads ConnectionString from App.config -> connectionStrings -> FACEIDDB
        /// </summary>
        public static string FaceIdDbConnectionString
        {
            get
            {
                try
                {
                    var cs = ConfigurationManager.ConnectionStrings["FACEIDDB"]?.ConnectionString;
                    if (!string.IsNullOrWhiteSpace(cs))
                        return cs;
                }
                catch { }

                return "Server=.;Database=FACEIDDB;Trusted_Connection=True;TrustServerCertificate=True;";
            }
        }

        // Alternative fallback list for auto-detection
        public static readonly string[] FallbackConnectionStrings = new[]
        {
            "Server=.;Database=AttendanceDB;Trusted_Connection=True;TrustServerCertificate=True;",
            "Server=localhost;Database=AttendanceDB;Trusted_Connection=True;TrustServerCertificate=True;",
            "Server=.\\SQLEXPRESS;Database=AttendanceDB;Trusted_Connection=True;TrustServerCertificate=True;",
            "Server=(localdb)\\MSSQLLocalDB;Database=AttendanceDB;Trusted_Connection=True;TrustServerCertificate=True;"
        };

        // Fallback list for FACEIDDB
        public static readonly string[] FaceIdFallbackConnectionStrings = new[]
        {
            "Server=.;Database=FACEIDDB;Trusted_Connection=True;TrustServerCertificate=True;",
            "Server=localhost;Database=FACEIDDB;Trusted_Connection=True;TrustServerCertificate=True;",
            "Server=.\\SQLEXPRESS;Database=FACEIDDB;Trusted_Connection=True;TrustServerCertificate=True;",
            "Server=(localdb)\\MSSQLLocalDB;Database=FACEIDDB;Trusted_Connection=True;TrustServerCertificate=True;"
        };

        public static SqlConnection CreateConnection()
        {
            return new SqlConnection(ConnectionString);
        }

        /// <summary>
        /// Tests DB Connection - returns true if AttendanceDB is reachable
        /// </summary>
        public static async Task<bool> TestConnectionAsync()
        {
            // Try primary first, then fallbacks
            var allStrings = new List<string> { ConnectionString };
            allStrings.AddRange(FallbackConnectionStrings.Where(s => s != ConnectionString));

            foreach (var cs in allStrings.Distinct())
            {
                try
                {
                    using var conn = new SqlConnection(cs);
                    await conn.OpenAsync();
                    return true;
                }
                catch { continue; }
            }
            return false;
        }

        public static async Task<SqlConnection?> GetOpenConnectionAsync()
        {
            var allStrings = new List<string> { ConnectionString };
            allStrings.AddRange(FallbackConnectionStrings.Where(s => s != ConnectionString));

            foreach (var cs in allStrings.Distinct())
            {
                try
                {
                    var conn = new SqlConnection(cs);
                    await conn.OpenAsync();
                    return conn;
                }
                catch { continue; }
            }
            return null;
        }

        /// <summary>
        /// FACEIDDB Connection - For reading KQZ_Card / KQZ_Employee data
        /// </summary>
        public static async Task<SqlConnection?> GetFaceIdDbConnectionAsync()
        {
            var allStrings = new List<string> { FaceIdDbConnectionString };
            allStrings.AddRange(FaceIdFallbackConnectionStrings.Where(s => s != FaceIdDbConnectionString));

            foreach (var cs in allStrings.Distinct())
            {
                try
                {
                    var conn = new SqlConnection(cs);
                    await conn.OpenAsync();
                    return conn;
                }
                catch { continue; }
            }
            return null;
        }
    }
}
