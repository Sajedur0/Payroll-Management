using Microsoft.Data.SqlClient;
using System.Data;

namespace PayrollManagement.Data
{
    public static class DbHelper
    {
        private static bool _tablesEnsured = false;
        private static readonly object _ensureLock = new();

        public static string ConnectionString
        {
            get => DbConfig.ActiveConnectionString ?? DbConfig.ConnectionString;
            set { /* kept for compatibility with spec - actual string is in DbConfig/App.config */ }
        }

        public static SqlConnection GetConnection()
        {
            if (!string.IsNullOrEmpty(DbConfig.ActiveConnectionString))
            {
                try
                {
                    var conn = new SqlConnection(DbConfig.ActiveConnectionString);
                    conn.Open();
                    EnsureDatabaseTables(conn);
                    return conn;
                }
                catch { }
            }

            var allStrings = new List<string> { DbConfig.ConnectionString };
            allStrings.AddRange(DbConfig.FallbackConnectionStrings.Where(s => s != DbConfig.ConnectionString));

            foreach (var cs in allStrings.Distinct())
            {
                try
                {
                    var conn = new SqlConnection(cs);
                    conn.Open();
                    DbConfig.ActiveConnectionString = cs;
                    EnsureDatabaseTables(conn);
                    return conn;
                }
                catch { continue; }
            }

            // Fall back to opening primary to surface exception with details
            var finalConn = new SqlConnection(DbConfig.ConnectionString);
            finalConn.Open();
            EnsureDatabaseTables(finalConn);
            return finalConn;
        }

        public static async Task<SqlConnection> GetConnectionAsync()
        {
            var conn = await DbConfig.GetOpenConnectionAsync();
            if (conn != null) return conn;

            var finalConn = new SqlConnection(DbConfig.ConnectionString);
            await finalConn.OpenAsync();
            EnsureDatabaseTables(finalConn);
            return finalConn;
        }

        /// <summary>
        /// Idempotently ensures all application tables exist in the connected database.
        /// </summary>
        public static void EnsureDatabaseTables(SqlConnection conn)
        {
            if (_tablesEnsured) return;
            lock (_ensureLock)
            {
                if (_tablesEnsured) return;
                try
                {
                    var ddl = @"
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='CompanyDetails')
                        BEGIN
                            CREATE TABLE dbo.CompanyDetails (
                                ID INT PRIMARY KEY CHECK (ID=1),
                                Name NVARCHAR(200) NOT NULL,
                                Address NVARCHAR(500) NULL,
                                Number NVARCHAR(50) NULL
                            );
                            INSERT INTO dbo.CompanyDetails (ID, Name, Address, Number) VALUES (1, 'PAYROLL SYSTEM', '', '');
                        END
                        ELSE IF NOT EXISTS (SELECT 1 FROM dbo.CompanyDetails WHERE ID=1)
                        BEGIN
                            INSERT INTO dbo.CompanyDetails (ID, Name, Address, Number) VALUES (1, 'PAYROLL SYSTEM', '', '');
                        END;

                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='WeekEnd')
                        BEGIN
                            CREATE TABLE dbo.WeekEnd (
                                DayName NVARCHAR(20) PRIMARY KEY
                            );
                            INSERT INTO dbo.WeekEnd (DayName) VALUES ('Friday');
                        END;

                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='CompanyHoliday')
                        BEGIN
                            CREATE TABLE dbo.CompanyHoliday (
                                HolidayID INT IDENTITY(1,1) PRIMARY KEY,
                                FestivalName NVARCHAR(200) NOT NULL,
                                FromDate NVARCHAR(20) NOT NULL,
                                ToDate NVARCHAR(20) NOT NULL,
                                Notes NVARCHAR(500) NULL
                            );
                        END;

                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='EmployeeExitInfo')
                        BEGIN
                            CREATE TABLE dbo.EmployeeExitInfo (
                                ExitID INT IDENTITY(1,1) PRIMARY KEY,
                                EmpID INT NOT NULL,
                                ExitType NVARCHAR(50) NOT NULL,
                                ExitDate NVARCHAR(20) NOT NULL,
                                Reason NVARCHAR(500) NULL,
                                CreatedAt DATETIME DEFAULT GETDATE()
                            );
                        END;

                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='ShiftSchedule')
                        BEGIN
                            CREATE TABLE dbo.ShiftSchedule (
                                ScheduleID INT IDENTITY(1,1) PRIMARY KEY,
                                ShiftName NVARCHAR(50) NOT NULL,
                                DutyType NVARCHAR(20) NULL,
                                InTime NVARCHAR(20) NULL,
                                OutTime NVARCHAR(20) NULL,
                                Notes NVARCHAR(200) NULL
                            );
                        END;

                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='ShiftCycleConfig')
                        BEGIN
                            CREATE TABLE dbo.ShiftCycleConfig (
                                ShiftName NVARCHAR(50) PRIMARY KEY,
                                DutyType NVARCHAR(20) NOT NULL,
                                StartDate NVARCHAR(20) NOT NULL
                            );
                        END;

                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='ShiftRotationSchedule')
                        BEGIN
                            CREATE TABLE dbo.ShiftRotationSchedule (
                                RotationID INT IDENTITY(1,1) PRIMARY KEY,
                                ScheduleDate NVARCHAR(20) NOT NULL,
                                ShiftName NVARCHAR(50) NOT NULL,
                                DutyType NVARCHAR(20) NOT NULL,
                                InDateTime NVARCHAR(30) NOT NULL,
                                OutDateTime NVARCHAR(30) NOT NULL,
                                IsOvertime INT DEFAULT 0
                            );
                        END;

                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='EmployeeInfo')
                        BEGIN
                            CREATE TABLE dbo.EmployeeInfo (
                                SL INT IDENTITY(1,1) PRIMARY KEY,
                                Name NVARCHAR(100) NOT NULL,
                                EmpID INT UNIQUE,
                                Gender NVARCHAR(20) NULL,
                                Designation NVARCHAR(100) NULL,
                                Section NVARCHAR(100) NULL,
                                Department NVARCHAR(100) NULL,
                                Shift NVARCHAR(20) NULL,
                                Category NVARCHAR(50) NULL,
                                Status NVARCHAR(50) DEFAULT 'Active',
                                RocketAC NVARCHAR(50) NULL,
                                GrossWages FLOAT NULL,
                                Religion NVARCHAR(50) NULL,
                                DOJ NVARCHAR(50) NULL,
                                FatherName NVARCHAR(100) NULL,
                                NID NVARCHAR(50) NULL,
                                PermAddress NVARCHAR(500) NULL,
                                PresAddress NVARCHAR(500) NULL
                            );
                        END;

                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='RawData')
                        BEGIN
                            CREATE TABLE dbo.RawData (
                                LogID INT IDENTITY(1,1) PRIMARY KEY,
                                EmpID INT NOT NULL,
                                [Date] NVARCHAR(20) NOT NULL,
                                InTime NVARCHAR(20) NULL,
                                OutTime NVARCHAR(20) NULL,
                                InDateTime NVARCHAR(30) NULL,
                                OutDateTime NVARCHAR(30) NULL
                            );
                            CREATE NONCLUSTERED INDEX IX_RawData_EmpID_Date ON dbo.RawData (EmpID, [Date]);
                        END;

                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='ActivityLog')
                        BEGIN
                            CREATE TABLE dbo.ActivityLog (
                                LogID INT IDENTITY(1,1) PRIMARY KEY,
                                Description NVARCHAR(500) NULL,
                                LogTime DATETIME DEFAULT GETDATE()
                            );
                        END;";

                    using var cmd = new SqlCommand(ddl, conn);
                    cmd.ExecuteNonQuery();
                    _tablesEnsured = true;
                }
                catch
                {
                    // If DDL fails (e.g. read-only permissions), allow operation to continue
                }
            }
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
