using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using ClosedXML.Excel;
using PayrollManagement.Models;

namespace PayrollManagement.Data
{
    public class AttendanceRepository
    {
        // ================= Dashboard =================

        public async Task<DashboardSummary> GetDashboardSummaryAsync()
        {
            var summary = new DashboardSummary();

            using var conn = await DbConfig.GetOpenConnectionAsync();
            if (conn == null) throw new Exception("Failed to connect to the server. Please check if the server is running.");

            // 1. Total active employees from dbo.EmployeeInfo
            try
            {
                using var cmdEmp = new SqlCommand("SELECT COUNT(*) FROM dbo.EmployeeInfo WHERE Status = 'Active' OR Status IS NULL", conn);
                var empCount = await cmdEmp.ExecuteScalarAsync();
                summary.TotalEmployees = empCount != null && empCount != DBNull.Value ? Convert.ToInt32(empCount) : 0;
            }
            catch
            {
                try
                {
                    using var cmdEmpFallback = new SqlCommand("SELECT COUNT(*) FROM dbo.EmployeeInfo", conn);
                    summary.TotalEmployees = Convert.ToInt32(await cmdEmpFallback.ExecuteScalarAsync());
                }
                catch { }
            }

            // 2. Present / Absent from dbo.RawData
            try
            {
                var todayStr = DateTime.Today.ToString("yyyy-MM-dd");
                string? targetDate = null;

                // Check if today has data
                using (var cmdCheckToday = new SqlCommand("SELECT COUNT(*) FROM dbo.RawData WHERE [Date] = @d", conn))
                {
                    cmdCheckToday.Parameters.AddWithValue("@d", todayStr);
                    int todayCount = Convert.ToInt32(await cmdCheckToday.ExecuteScalarAsync());
                    if (todayCount > 0)
                    {
                        targetDate = todayStr;
                        summary.PresentToday = todayCount;
                    }
                }

                // If today has no records yet, use the latest recorded date
                if (targetDate == null)
                {
                    using var cmdLatestDate = new SqlCommand("SELECT MAX([Date]) FROM dbo.RawData WHERE [Date] IS NOT NULL AND [Date] <> ''", conn);
                    var maxDateObj = await cmdLatestDate.ExecuteScalarAsync();
                    if (maxDateObj != null && maxDateObj != DBNull.Value)
                    {
                        targetDate = maxDateObj.ToString();
                        using var cmdCount = new SqlCommand("SELECT COUNT(*) FROM dbo.RawData WHERE [Date] = @d", conn);
                        cmdCount.Parameters.AddWithValue("@d", targetDate);
                        summary.PresentToday = Convert.ToInt32(await cmdCount.ExecuteScalarAsync());
                    }
                }

                // Calculate Absent
                if (summary.TotalEmployees > 0)
                {
                    summary.AbsentToday = Math.Max(0, summary.TotalEmployees - summary.PresentToday);
                }
            }
            catch { }

            return summary;
        }

        public async Task<List<WeeklyAttendancePoint>> GetWeeklyTrendAsync()
        {
            var list = new List<WeeklyAttendancePoint>();
            using var conn = await DbConfig.GetOpenConnectionAsync();
            if (conn == null) throw new Exception("DB Connection failed");

            try
            {
                // Query the 7 most recent dates from dbo.RawData
                var sql = @"
                    SELECT TOP 7 [Date], COUNT(*) AS PresentCount
                    FROM dbo.RawData
                    WHERE [Date] IS NOT NULL AND [Date] <> ''
                    GROUP BY [Date]
                    ORDER BY [Date] DESC";

                using var cmd = new SqlCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var dateStr = reader.GetString(0);
                    var count = reader.GetInt32(1);
                    if (DateTime.TryParse(dateStr, out var parsedDate))
                    {
                        list.Add(new WeeklyAttendancePoint
                        {
                            AttendanceDate = parsedDate,
                            PresentCount = count
                        });
                    }
                }
            }
            catch { }

            // Ensure 7 points sorted chronologically
            if (list.Count == 0)
            {
                for (int i = 6; i >= 0; i--)
                {
                    list.Add(new WeeklyAttendancePoint
                    {
                        AttendanceDate = DateTime.Today.AddDays(-i),
                        PresentCount = 0
                    });
                }
            }

            return list.OrderBy(x => x.AttendanceDate).ToList();
        }

        public async Task<List<ActivityLogItem>> GetRecentActivitiesAsync(int top = 5)
        {
            var list = new List<ActivityLogItem>();
            using var conn = await DbConfig.GetOpenConnectionAsync();
            if (conn == null) return list;

            try
            {
                var sql = $@"
                    SELECT TOP ({top})
                        r.LogID,
                        ISNULL(e.Name, 'Employee ' + CAST(r.EmpID AS NVARCHAR)) + ' (EmpID: ' + CAST(r.EmpID AS NVARCHAR) + ') - ' +
                        CASE 
                            WHEN r.OutTime IS NOT NULL AND r.OutTime <> '' THEN 'Out: ' + r.OutTime + ' (In: ' + ISNULL(r.InTime, '-') + ')'
                            ELSE 'In: ' + ISNULL(r.InTime, '-')
                        END AS Description,
                        ISNULL(TRY_CAST(r.InDateTime AS DATETIME), TRY_CAST(r.Date AS DATETIME)) AS LogTime
                    FROM dbo.RawData r
                    LEFT JOIN dbo.EmployeeInfo e ON r.EmpID = e.EmpID
                    ORDER BY r.LogID DESC";

                using var cmd = new SqlCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new ActivityLogItem
                    {
                        LogId = reader.GetInt32(0),
                        Description = reader.IsDBNull(1) ? "Attendance logged" : reader.GetString(1),
                        LogTime = reader.IsDBNull(2) ? DateTime.Now : reader.GetDateTime(2)
                    });
                }
            }
            catch { }

            return list;
        }

        // ================= Attendance Log =================

        public async Task<List<AttendanceRecord>> GetAttendanceLogAsync(DateTime? fromDate = null, DateTime? toDate = null, string? search = null)
        {
            var list = new List<AttendanceRecord>();
            using var conn = await DbConfig.GetOpenConnectionAsync();
            if (conn == null) throw new Exception("DB Connection failed");

            // Try stored procedure first
            try
            {
                using var cmd = new SqlCommand("dbo.sp_GetAttendanceLog", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@FromDate", (object?)fromDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ToDate", (object?)toDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Search", (object?)search ?? DBNull.Value);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(MapAttendanceLog(reader));
                }
                return list;
            }
            catch { /* fallback to direct query */ }

            var sql = @"
                SELECT a.AttendanceId, e.EmployeeId, e.EmployeeCode, e.FullName, e.Department,
                       a.AttendanceDate, a.CheckIn, a.CheckOut, a.Status, a.WorkHours
                FROM dbo.Attendance a
                JOIN dbo.Employees e ON a.EmployeeId = e.EmployeeId
                WHERE (@from IS NULL OR a.AttendanceDate >= @from)
                  AND (@to IS NULL OR a.AttendanceDate <= @to)
                  AND (@search IS NULL OR e.FullName LIKE '%' + @search + '%' OR e.EmployeeCode LIKE '%' + @search + '%')
                ORDER BY a.AttendanceDate DESC";

            using var cmd2 = new SqlCommand(sql, conn);
            cmd2.Parameters.AddWithValue("@from", (object?)fromDate ?? DBNull.Value);
            cmd2.Parameters.AddWithValue("@to", (object?)toDate ?? DBNull.Value);
            cmd2.Parameters.AddWithValue("@search", (object?)search ?? DBNull.Value);

            using var reader2 = await cmd2.ExecuteReaderAsync();
            while (await reader2.ReadAsync())
            {
                list.Add(new AttendanceRecord
                {
                    AttendanceId = reader2.GetInt32(0),
                    EmployeeId = reader2.GetInt32(1),
                    EmployeeCode = reader2.GetString(2),
                    FullName = reader2.GetString(3),
                    Department = reader2.GetString(4),
                    AttendanceDate = reader2.GetDateTime(5),
                    CheckIn = reader2.IsDBNull(6) ? null : reader2.GetTimeSpan(6),
                    CheckOut = reader2.IsDBNull(7) ? null : reader2.GetTimeSpan(7),
                    Status = reader2.GetString(8),
                    WorkHours = reader2.IsDBNull(9) ? null : reader2.GetDecimal(9)
                });
            }
            return list;
        }

        private AttendanceRecord MapAttendanceLog(SqlDataReader r)
        {
            // sp_GetAttendanceLog returns: AttendanceId, EmployeeCode, FullName, Department, AttendanceDate, CheckIn, CheckOut, Status, WorkHours
            return new AttendanceRecord
            {
                AttendanceId = r.GetInt32(0),
                EmployeeCode = r.GetString(1),
                FullName = r.GetString(2),
                Department = r.GetString(3),
                AttendanceDate = r.GetDateTime(4),
                CheckIn = r.IsDBNull(5) ? null : r.GetTimeSpan(5),
                CheckOut = r.IsDBNull(6) ? null : r.GetTimeSpan(6),
                Status = r.GetString(7),
                WorkHours = r.IsDBNull(8) ? null : r.GetDecimal(8)
            };
        }

        // ================= Employees (dbo.EmployeeInfo) =================
        public async Task<List<Employee>> GetEmployeesAsync(string? search = null)
        {
            var list = new List<Employee>();
            using var conn = await DbConfig.GetOpenConnectionAsync();
            if (conn == null) throw new Exception("Failed to connect to the server.");

            var sql = @"
                SELECT 
                    SL, Name, EmpID, Gender, Designation, Section, Department, 
                    Shift, Category, Status, RocketAC, GrossWages, Religion, 
                    DOJ, FatherName, NID, PermAddress, PresAddress
                FROM dbo.EmployeeInfo
                WHERE (@s IS NULL 
                    OR Name LIKE '%' + @s + '%' 
                    OR CAST(EmpID AS NVARCHAR) LIKE '%' + @s + '%' 
                    OR Department LIKE '%' + @s + '%' 
                    OR Designation LIKE '%' + @s + '%' 
                    OR Section LIKE '%' + @s + '%'
                    OR NID LIKE '%' + @s + '%'
                    OR RocketAC LIKE '%' + @s + '%')
                ORDER BY SL DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@s", (object?)search ?? DBNull.Value);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new Employee
                {
                    SL = reader.GetInt32(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    EmpID = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                    Gender = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Designation = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Section = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Department = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Shift = reader.IsDBNull(7) ? null : reader.GetString(7),
                    Category = reader.IsDBNull(8) ? null : reader.GetString(8),
                    Status = reader.IsDBNull(9) ? null : reader.GetString(9),
                    RocketAC = reader.IsDBNull(10) ? null : reader.GetString(10),
                    GrossWages = reader.IsDBNull(11) ? null : reader.GetDouble(11),
                    Religion = reader.IsDBNull(12) ? null : reader.GetString(12),
                    DOJ = reader.IsDBNull(13) ? null : reader.GetString(13),
                    FatherName = reader.IsDBNull(14) ? null : reader.GetString(14),
                    NID = reader.IsDBNull(15) ? null : reader.GetString(15),
                    PermAddress = reader.IsDBNull(16) ? null : reader.GetString(16),
                    PresAddress = reader.IsDBNull(17) ? null : reader.GetString(17)
                });
            }
            return list;
        }

        public async Task<int> AddEmployeeAsync(Employee emp)
        {
            using var conn = await DbConfig.GetOpenConnectionAsync();
            if (conn == null) throw new Exception("Failed to connect to the server.");

            var sql = @"
                INSERT INTO dbo.EmployeeInfo (
                    Name, EmpID, Gender, Designation, Section, Department,
                    Shift, Category, Status, RocketAC, GrossWages, Religion,
                    DOJ, FatherName, NID, PermAddress, PresAddress
                )
                VALUES (
                    @Name, @EmpID, @Gender, @Designation, @Section, @Department,
                    @Shift, @Category, @Status, @RocketAC, @GrossWages, @Religion,
                    @DOJ, @FatherName, @NID, @PermAddress, @PresAddress
                );
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Name", (object?)emp.Name?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EmpID", (object?)emp.EmpID ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Gender", (object?)emp.Gender?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Designation", (object?)emp.Designation?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Section", (object?)emp.Section?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Department", (object?)emp.Department?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Shift", (object?)emp.Shift?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Category", (object?)emp.Category?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", (object?)emp.Status?.Trim() ?? "Active");
            cmd.Parameters.AddWithValue("@RocketAC", (object?)emp.RocketAC?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@GrossWages", (object?)emp.GrossWages ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Religion", (object?)emp.Religion?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DOJ", (object?)emp.DOJ?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FatherName", (object?)emp.FatherName?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@NID", (object?)emp.NID?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PermAddress", (object?)emp.PermAddress?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PresAddress", (object?)emp.PresAddress?.Trim() ?? DBNull.Value);

            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task UpdateEmployeeAsync(Employee emp)
        {
            using var conn = await DbConfig.GetOpenConnectionAsync();
            if (conn == null) throw new Exception("Failed to connect to the server.");

            var sql = @"
                UPDATE dbo.EmployeeInfo SET 
                    Name = @Name,
                    EmpID = @EmpID,
                    Gender = @Gender,
                    Designation = @Designation,
                    Section = @Section,
                    Department = @Department,
                    Shift = @Shift,
                    Category = @Category,
                    Status = @Status,
                    RocketAC = @RocketAC,
                    GrossWages = @GrossWages,
                    Religion = @Religion,
                    DOJ = @DOJ,
                    FatherName = @FatherName,
                    NID = @NID,
                    PermAddress = @PermAddress,
                    PresAddress = @PresAddress
                WHERE SL = @SL";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@SL", emp.SL);
            cmd.Parameters.AddWithValue("@Name", (object?)emp.Name?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EmpID", (object?)emp.EmpID ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Gender", (object?)emp.Gender?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Designation", (object?)emp.Designation?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Section", (object?)emp.Section?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Department", (object?)emp.Department?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Shift", (object?)emp.Shift?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Category", (object?)emp.Category?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", (object?)emp.Status?.Trim() ?? "Active");
            cmd.Parameters.AddWithValue("@RocketAC", (object?)emp.RocketAC?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@GrossWages", (object?)emp.GrossWages ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Religion", (object?)emp.Religion?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DOJ", (object?)emp.DOJ?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FatherName", (object?)emp.FatherName?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@NID", (object?)emp.NID?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PermAddress", (object?)emp.PermAddress?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PresAddress", (object?)emp.PresAddress?.Trim() ?? DBNull.Value);

            int rows = await cmd.ExecuteNonQueryAsync();
            if (rows == 0) throw new Exception("Employee not found or not updated");
        }

        public async Task DeleteEmployeeAsync(int sl)
        {
            using var conn = await DbConfig.GetOpenConnectionAsync();
            if (conn == null) throw new Exception("Failed to connect to the server.");

            using var cmd = new SqlCommand("DELETE FROM dbo.EmployeeInfo WHERE SL = @SL", conn);
            cmd.Parameters.AddWithValue("@SL", sl);
            int rows = await cmd.ExecuteNonQueryAsync();
            if (rows == 0) throw new Exception("Employee not found");
        }

        /// <summary>
        /// Reads employee data from Excel file (.xlsx / .xls) and Upserts (Insert / Update) into dbo.EmployeeInfo
        /// </summary>
        public async Task<(int inserted, int updated)> ImportEmployeesFromExcelAsync(string filePath)
        {
            var employees = new List<Employee>();

            using (var workbook = new XLWorkbook(filePath))
            {
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null) throw new Exception("No worksheet found in the Excel file.");

                var rows = worksheet.RangeUsed()?.RowsUsed()?.ToList();
                if (rows == null || rows.Count < 2) throw new Exception("Insufficient data in Excel file (header and at least 1 data row required).");

                var headerRow = rows[0];
                var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var cell in headerRow.Cells())
                {
                    var headerText = cell.GetString().Trim().Replace(" ", "").Replace("_", "").Replace("-", "");
                    if (!string.IsNullOrEmpty(headerText) && !colMap.ContainsKey(headerText))
                    {
                        colMap[headerText] = cell.Address.ColumnNumber;
                    }
                }

                string GetVal(IXLRangeRow row, params string[] names)
                {
                    foreach (var name in names)
                    {
                        var clean = name.Replace(" ", "").Replace("_", "").Replace("-", "");
                        if (colMap.TryGetValue(clean, out int colIdx))
                        {
                            var cell = row.Cell(colIdx);
                            if (cell.DataType == XLDataType.DateTime)
                                return cell.GetDateTime().ToString("yyyy-MM-dd");
                            return cell.GetString().Trim();
                        }
                    }
                    return "";
                }

                for (int i = 1; i < rows.Count; i++)
                {
                    var r = rows[i];
                    var empIdStr = GetVal(r, "EmpID", "EmployeeID", "EmpId", "ID", "Code", "EmployeeCode", "CardNo");
                    var name = GetVal(r, "Name", "FullName", "EmployeeName", "EmpName");

                    if (string.IsNullOrWhiteSpace(empIdStr) && string.IsNullOrWhiteSpace(name))
                        continue;

                    if (!int.TryParse(empIdStr, out int empId))
                        continue;

                    var emp = new Employee
                    {
                        EmpID = empId,
                        Name = string.IsNullOrWhiteSpace(name) ? $"Employee {empId}" : name,
                        Gender = GetVal(r, "Gender", "Sex"),
                        Designation = GetVal(r, "Designation", "Desig", "Post"),
                        Section = GetVal(r, "Section", "Sec"),
                        Department = GetVal(r, "Department", "Dept"),
                        Shift = GetVal(r, "Shift"),
                        Category = GetVal(r, "Category", "Cat"),
                        Status = GetVal(r, "Status"),
                        RocketAC = GetVal(r, "RocketAC", "Rocket", "Account", "BankAC", "RocketNo"),
                        Religion = GetVal(r, "Religion"),
                        DOJ = GetVal(r, "DOJ", "JoinDate", "JoiningDate", "DateOfJoining"),
                        FatherName = GetVal(r, "FatherName", "Father", "FathersName"),
                        NID = GetVal(r, "NID", "NationalID", "NIDNo"),
                        PermAddress = GetVal(r, "PermAddress", "PermanentAddress", "PermAddr"),
                        PresAddress = GetVal(r, "PresAddress", "PresentAddress", "PresAddr")
                    };

                    var grossWagesStr = GetVal(r, "GrossWages", "Gross", "Salary", "Wages", "GrossSalary");
                    if (double.TryParse(grossWagesStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double gw) ||
                        double.TryParse(grossWagesStr, out gw))
                    {
                        emp.GrossWages = gw;
                    }

                    if (string.IsNullOrWhiteSpace(emp.Status))
                        emp.Status = "Active";

                    employees.Add(emp);
                }
            }

            if (employees.Count == 0)
                throw new Exception("No valid employee data (with correct EmpID) found in the Excel file.");

            using var conn = await DbConfig.GetOpenConnectionAsync();
            if (conn == null) throw new Exception("Failed to connect to the server.");

            int inserted = 0;
            int updated = 0;

            foreach (var emp in employees)
            {
                var checkSql = "SELECT SL FROM dbo.EmployeeInfo WHERE EmpID = @EmpID";
                int? existingSL = null;
                using (var checkCmd = new SqlCommand(checkSql, conn))
                {
                    checkCmd.Parameters.AddWithValue("@EmpID", emp.EmpID);
                    var result = await checkCmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                        existingSL = Convert.ToInt32(result);
                }

                if (existingSL.HasValue)
                {
                    emp.SL = existingSL.Value;
                    await UpdateEmployeeAsync(emp);
                    updated++;
                }
                else
                {
                    await AddEmployeeAsync(emp);
                    inserted++;
                }
            }

            return (inserted, updated);
        }

        // ================= FACEIDDB → RawData Load =================

        /// <summary>
        /// Reads data from FACEIDDB.dbo.KQZ_Card + KQZ_Employee
        /// and Inserts/Updates into AttendanceDB.dbo.RawData with progress reporting
        /// </summary>
        public async Task<int> LoadAttendanceFromFaceIdDbAsync(DateTime? fromDate = null, DateTime? toDate = null, IProgress<AttendanceLoadProgress>? progress = null)
        {
            progress?.Report(new AttendanceLoadProgress { Processed = 0, Total = 0, Message = "Connecting to data source..." });

            // Step 1: Read data from FACEIDDB
            using var faceConn = await DbConfig.GetFaceIdDbConnectionAsync();
            if (faceConn == null)
                throw new Exception("Failed to connect to the external data source. Please check if the server is running.");

            var sql = @"
                SELECT 
                    e.EmployeeCode AS EmpID,
                    CONVERT(VARCHAR(10), c.CardTime, 120) AS [Date],
                    CONVERT(VARCHAR(8), MIN(c.CardTime), 108) AS InTime,
                    CASE 
                        WHEN MIN(c.CardTime) = MAX(c.CardTime) THEN NULL 
                        ELSE CONVERT(VARCHAR(8), MAX(c.CardTime), 108) 
                    END AS OutTime,
                    CONVERT(VARCHAR(19), MIN(c.CardTime), 120) AS InDateTime,
                    CASE 
                        WHEN MIN(c.CardTime) = MAX(c.CardTime) THEN NULL 
                        ELSE CONVERT(VARCHAR(19), MAX(c.CardTime), 120) 
                    END AS OutDateTime
                FROM dbo.KQZ_Card c
                INNER JOIN dbo.KQZ_Employee e ON c.EmployeeID = e.EmployeeID
                WHERE (@FromDate IS NULL OR c.CardTime >= @FromDate)
                  AND (@ToDate IS NULL OR c.CardTime < DATEADD(DAY, 1, @ToDate))
                  AND e.EmployeeCode IS NOT NULL AND e.EmployeeCode <> ''
                GROUP BY e.EmployeeCode, CONVERT(VARCHAR(10), c.CardTime, 120)
                ORDER BY CONVERT(VARCHAR(10), c.CardTime, 120), e.EmployeeCode";

            var records = new List<RawAttendanceData>();

            using (var cmd = new SqlCommand(sql, faceConn))
            {
                cmd.Parameters.AddWithValue("@FromDate", (object?)fromDate?.Date ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ToDate", (object?)toDate?.Date ?? DBNull.Value);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var record = new RawAttendanceData
                    {
                        EmpID = reader.IsDBNull(0) ? "" : reader.GetString(0).Trim(),
                        Date = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        InTime = reader.IsDBNull(2) ? null : reader.GetString(2),
                        OutTime = reader.IsDBNull(3) ? null : reader.GetString(3),
                        InDateTime = reader.IsDBNull(4) ? null : reader.GetString(4),
                        OutDateTime = reader.IsDBNull(5) ? null : reader.GetString(5)
                    };

                    records.Add(record);
                }
            }

            faceConn.Close();

            int total = records.Count;
            if (total == 0)
            {
                progress?.Report(new AttendanceLoadProgress { Processed = 0, Total = 0, Message = "No records found in selected date range." });
                return 0;
            }

            progress?.Report(new AttendanceLoadProgress { Processed = 0, Total = total, Message = $"Found {total} records. Saving..." });

            // Step 2: Save to AttendanceDB.dbo.RawData
            using var attConn = await DbConfig.GetOpenConnectionAsync();
            if (attConn == null)
                throw new Exception("Failed to connect to the server.");

            // Create RawData table and index if they do not exist
            await EnsureRawDataTableAsync(attConn);

            int batchSize = 250;
            int processed = 0;

            using (var tx = attConn.BeginTransaction())
            {
                try
                {
                    using (var cmdCreateTemp = new SqlCommand(@"
                        CREATE TABLE #TempRawData (
                            EmpID INT NOT NULL,
                            [Date] NVARCHAR(20) NOT NULL,
                            InTime NVARCHAR(20) NULL,
                            OutTime NVARCHAR(20) NULL,
                            InDateTime NVARCHAR(30) NULL,
                            OutDateTime NVARCHAR(30) NULL
                        );", attConn, tx))
                    {
                        await cmdCreateTemp.ExecuteNonQueryAsync();
                    }

                    for (int i = 0; i < total; i += batchSize)
                    {
                        var batch = records.Skip(i).Take(batchSize).ToList();

                        // Clear temp table
                        using (var cmdTruncate = new SqlCommand("TRUNCATE TABLE #TempRawData;", attConn, tx))
                        {
                            await cmdTruncate.ExecuteNonQueryAsync();
                        }

                        var dt = new DataTable();
                        dt.Columns.Add("EmpID", typeof(int));
                        dt.Columns.Add("Date", typeof(string));
                        dt.Columns.Add("InTime", typeof(string));
                        dt.Columns.Add("OutTime", typeof(string));
                        dt.Columns.Add("InDateTime", typeof(string));
                        dt.Columns.Add("OutDateTime", typeof(string));

                        foreach (var rec in batch)
                        {
                            if (int.TryParse(rec.EmpID, out int empIdNum))
                            {
                                dt.Rows.Add(
                                    empIdNum,
                                    rec.Date,
                                    (object?)rec.InTime ?? DBNull.Value,
                                    (object?)rec.OutTime ?? DBNull.Value,
                                    (object?)rec.InDateTime ?? DBNull.Value,
                                    (object?)rec.OutDateTime ?? DBNull.Value
                                );
                            }
                        }

                        using (var bulk = new SqlBulkCopy(attConn, SqlBulkCopyOptions.Default, tx))
                        {
                            bulk.DestinationTableName = "#TempRawData";
                            await bulk.WriteToServerAsync(dt);
                        }

                        var mergeSql = @"
                            MERGE dbo.RawData AS target
                            USING #TempRawData AS source
                            ON target.EmpID = source.EmpID AND target.[Date] = source.[Date]
                            WHEN MATCHED THEN
                                UPDATE SET 
                                    target.InTime = source.InTime,
                                    target.OutTime = source.OutTime,
                                    target.InDateTime = source.InDateTime,
                                    target.OutDateTime = source.OutDateTime
                            WHEN NOT MATCHED THEN
                                INSERT (EmpID, [Date], InTime, OutTime, InDateTime, OutDateTime)
                                VALUES (source.EmpID, source.[Date], source.InTime, source.OutTime, source.InDateTime, source.OutDateTime);";

                        using (var cmdMerge = new SqlCommand(mergeSql, attConn, tx))
                        {
                            await cmdMerge.ExecuteNonQueryAsync();
                        }

                        processed += batch.Count;
                        progress?.Report(new AttendanceLoadProgress
                        {
                            Processed = processed,
                            Total = total,
                            Message = $"Loaded {processed} of {total} records ({total - processed} remaining)..."
                        });

                        await Task.Delay(15); // Smooth UI progress rendering
                    }

                    // Activity Log (if table exists)
                    try
                    {
                        using var logCmd = new SqlCommand(@"
                            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='ActivityLog')
                            BEGIN
                                INSERT INTO dbo.ActivityLog (Description, LogTime) VALUES (@desc, GETDATE())
                            END", attConn, tx);
                        logCmd.Parameters.AddWithValue("@desc",
                            $"{total} attendance records loaded ({fromDate?.ToString("yyyy-MM-dd") ?? "All"} ~ {toDate?.ToString("yyyy-MM-dd") ?? "All"})");
                        await logCmd.ExecuteNonQueryAsync();
                    }
                    catch { /* Ignore */ }

                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }

            return total;
        }

        /// <summary>
        /// Creates RawData table if it does not exist
        /// </summary>
        private async Task EnsureRawDataTableAsync(SqlConnection conn)
        {
            var sql = @"
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
                END
                ELSE IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RawData_EmpID_Date' AND object_id = OBJECT_ID('dbo.RawData'))
                BEGIN
                    CREATE NONCLUSTERED INDEX IX_RawData_EmpID_Date ON dbo.RawData (EmpID, [Date]);
                END";

            using var cmd = new SqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Reads data from RawData table (for displaying in DataGrid)
        /// </summary>
        public async Task<List<RawAttendanceData>> GetRawDataAsync(DateTime? fromDate = null, DateTime? toDate = null, string? search = null)
        {
            var list = new List<RawAttendanceData>();
            using var conn = await DbConfig.GetOpenConnectionAsync();
            if (conn == null) throw new Exception("Failed to connect to the server.");

            await EnsureRawDataTableAsync(conn);

            var sql = @"
                SELECT LogID, EmpID, [Date], InTime, OutTime, InDateTime, OutDateTime
                FROM dbo.RawData
                WHERE (@fromStr IS NULL OR [Date] >= @fromStr)
                  AND (@toStr IS NULL OR [Date] <= @toStr)
                  AND (@search IS NULL OR CAST(EmpID AS NVARCHAR) LIKE '%' + @search + '%')
                ORDER BY [Date] DESC, EmpID ASC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@fromStr", (object?)fromDate?.ToString("yyyy-MM-dd") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@toStr", (object?)toDate?.ToString("yyyy-MM-dd") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@search", (object?)search ?? DBNull.Value);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new RawAttendanceData
                {
                    LogID = reader.GetInt32(0),
                    EmpID = reader.IsDBNull(1) ? "" : reader.GetValue(1).ToString() ?? "",
                    Date = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    InTime = reader.IsDBNull(3) ? null : reader.GetString(3),
                    OutTime = reader.IsDBNull(4) ? null : reader.GetString(4),
                    InDateTime = reader.IsDBNull(5) ? null : reader.GetString(5),
                    OutDateTime = reader.IsDBNull(6) ? null : reader.GetString(6)
                });
            }
            return list;
        }
    }
}


