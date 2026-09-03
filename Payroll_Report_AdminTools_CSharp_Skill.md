---
name: payroll-report-admintools-csharp-port
description: >
  Payroll Management সফটওয়্যারের "Report Tab" এবং "Admin Tools Tab"-এর সম্পূর্ণ বিজনেস
  লজিক (মূল Python/PySide6 + MS SQL Server অ্যাপ থেকে বিশ্লেষণ করা) C#/.NET (ADO.NET,
  Microsoft.Data.SqlClient) কোডে রূপান্তরিত করার জন্য একটি রেফারেন্স গাইড। এই ফাইলটি একটি
  AI Coding Agent-কে (Claude Code, Cursor, Copilot ইত্যাদি) দিয়ে C# Payroll সফটওয়্যার
  আপডেট/তৈরি করার প্রম্পট হিসেবে ব্যবহার করা যাবে।
---

# Payroll Management — Report Tab ও Admin Tools Tab: সম্পূর্ণ লজিক + C# কোড

> **এই ফাইলটি কীভাবে ব্যবহার করবেন:** এই সম্পূর্ণ ফাইলটি কপি করে আপনার AI Coding Agent-কে
> (Claude Code / Cursor / Copilot Chat ইত্যাদি) দিন এবং নিচে দেওয়া "Agent Instructions"
> অনুযায়ী কাজ করতে বলুন। এখানে প্রতিটি রিপোর্ট ও অ্যাডমিন টুলের **নিয়ম (business rule)**
> এবং তার **C# ইমপ্লিমেন্টেশন** দুটোই দেওয়া আছে, যাতে এজেন্ট অনুমান না করে হুবহু একই আচরণ
> কোড করতে পারে।

---

## 0. Agent Instructions (এজেন্টকে যা মেনে চলতে হবে)

1. এই ডকুমেন্টে বর্ণিত **প্রতিটি নিয়ম বাধ্যতামূলক** — বিশেষ করে:
   - Holiday > Friday > Attendance — এই অগ্রাধিকার ক্রম কখনও পরিবর্তন করা যাবে না।
   - Night Duty শিফটের ক্ষেত্রে In/Out টাইম AM/PM সোয়াপ লজিক এবং cross-midnight
     লুক-এহেড লজিক হুবহু বজায় রাখতে হবে।
   - সব SQL কোয়েরি **parameterized** (SqlParameter ব্যবহার করে) হতে হবে, কখনও string
     concatenation দিয়ে user input বসানো যাবে না (SQL Injection প্রতিরোধ)।
   - Exit/Resign হওয়া কর্মীকে exit date-এর পরের কোনো তারিখে "active" হিসেবে গণনা করা
     যাবে না (`IsEmployeeActiveOn` / `EmployeeActiveOnDateSql` মেনে চলতে হবে)।
2. ডাটাবেজ **স্কিমা অপরিবর্তিত** রাখা হয়েছে (টেবিল নাম, কলাম নাম হুবহু একই) — Section 1
   দেখুন। আপনার C# অ্যাপ একই MS SQL Server ডাটাবেজের সাথে কাজ করবে বলে ধরে নেওয়া হয়েছে।
3. প্রযুক্তি স্ট্যাক (ধরে নেওয়া হয়েছে, প্রয়োজনে প্রজেক্টের সাথে মিলিয়ে নিন):
   - .NET 8, `Microsoft.Data.SqlClient` (ADO.NET) — pyodbc-এর সমতুল্য।
   - UI ফ্রেমওয়ার্ক নির্বিশেষে (WinForms / WPF / ASP.NET Core / MAUI) — এখানে দেওয়া
     ক্লাসগুলো **UI-independent service layer**, যেকোনো UI-তে বসানো যাবে।
   - PDF এক্সপোর্টের জন্য উদাহরণ `QuestPDF` দিয়ে দেওয়া হয়েছে (মূল অ্যাপে Python
     `reportlab` ব্যবহৃত হয়েছিল); আপনার প্রজেক্টে অন্য লাইব্রেরি থাকলে শুধু রেন্ডারিং অংশ
     বদলে দিন, ডেটা/লজিক অংশ অপরিবর্তিত রাখুন।
4. প্রতিটি সেকশনে "Python মূল লজিক" সংক্ষেপে বলা আছে এবং তার নিচে "C# ইমপ্লিমেন্টেশন"।
   নতুন ফিচার যোগ করার সময় বিদ্যমান নিয়মগুলো না ভেঙে যোগ করুন।
5. তারিখ সবসময় `yyyy-MM-dd` স্ট্রিং ফরম্যাটে ডাটাবেজে সংরক্ষিত (NVARCHAR), DateTime অবজেক্ট
   নয় — এটি মূল স্কিমার সাথে সামঞ্জস্যপূর্ণ রাখতে C#-এও স্ট্রিং তুলনা ব্যবহার করা হয়েছে
   যেখানে মূল কোডে তা করা হয়েছিল।

---

## 1. Database Schema (অপরিবর্তিত — T-SQL)

```sql
CREATE TABLE EmployeeInfo (
    SL          INT IDENTITY(1,1) PRIMARY KEY,
    Name        NVARCHAR(200),
    EmpID       INT UNIQUE,
    Gender      NVARCHAR(20),
    Designation NVARCHAR(100),
    Section     NVARCHAR(100),
    Department  NVARCHAR(100),
    Shift       NVARCHAR(50),
    Category    NVARCHAR(50),
    Status      NVARCHAR(50),
    RocketAC    NVARCHAR(50),
    GrossWages  FLOAT,
    Religion    NVARCHAR(50),
    DOJ         NVARCHAR(20),
    FatherName  NVARCHAR(200),
    NID         NVARCHAR(50),
    PermAddress NVARCHAR(500),
    PresAddress NVARCHAR(500)
);

CREATE TABLE RawData (
    LogID    INT IDENTITY(1,1) PRIMARY KEY,
    EmpID    INT,
    Date     NVARCHAR(20),   -- yyyy-MM-dd
    InTime   NVARCHAR(20),   -- HH:mm:ss
    OutTime  NVARCHAR(20)    -- HH:mm:ss
);

CREATE TABLE ProcessedAttendance (
    ProcessedID   INT IDENTITY(1,1) PRIMARY KEY,
    EmpID         INT,
    Date          NVARCHAR(20),
    InTime        NVARCHAR(20),
    OutTime       NVARCHAR(20),
    TotalHours    NVARCHAR(20),
    LateStatus    NVARCHAR(10),   -- 'Yes' / 'No'
    OvertimeHours FLOAT DEFAULT 0.0,
    Shift         NVARCHAR(50),
    InDateTime    NVARCHAR(30),
    OutDateTime   NVARCHAR(30),
    DutyType      NVARCHAR(20),
    OvertimeFlag  NVARCHAR(20)
);

CREATE TABLE CompanyDetails (
    ID      INT PRIMARY KEY CHECK (ID = 1),
    Name    NVARCHAR(200),
    Address NVARCHAR(500),
    Number  NVARCHAR(50)
);

CREATE TABLE WeekEnd (
    DayName NVARCHAR(20) PRIMARY KEY   -- e.g. 'Saturday'
);

CREATE TABLE CompanyHoliday (
    HolidayID    INT IDENTITY(1,1) PRIMARY KEY,
    FestivalName NVARCHAR(200),
    FromDate     NVARCHAR(20),
    ToDate       NVARCHAR(20),
    Notes        NVARCHAR(500)
);

CREATE TABLE EmployeeExitInfo (
    ExitID    INT IDENTITY(1,1) PRIMARY KEY,
    EmpID     INT,
    ExitType  NVARCHAR(50),    -- e.g. 'Resign'
    ExitDate  NVARCHAR(20),
    Reason    NVARCHAR(1000),
    CreatedAt NVARCHAR(30) DEFAULT CONVERT(NVARCHAR(30), GETDATE(), 120),
    CONSTRAINT UQ_EmpExit UNIQUE (EmpID, ExitType),
    FOREIGN KEY (EmpID) REFERENCES EmployeeInfo(EmpID)
);

CREATE TABLE ShiftSchedule (            -- static "current" shift time per shift name
    ScheduleID INT IDENTITY(1,1) PRIMARY KEY,
    ShiftName  NVARCHAR(50) UNIQUE,
    DutyType   NVARCHAR(20),            -- 'Day' / 'Night'
    InTime     NVARCHAR(20),
    OutTime    NVARCHAR(20),
    Notes      NVARCHAR(200)
);

CREATE TABLE ShiftRotationSchedule (    -- per-date generated rotation (Day/Night alternates weekly)
    RotationID   INT IDENTITY(1,1) PRIMARY KEY,
    ScheduleDate NVARCHAR(20),
    ShiftName    NVARCHAR(50),
    DutyType     NVARCHAR(20),
    InDateTime   NVARCHAR(30),
    OutDateTime  NVARCHAR(30),
    IsOvertime   INT DEFAULT 0,
    CONSTRAINT UQ_ShiftRotation UNIQUE (ScheduleDate, ShiftName)
);

CREATE TABLE ShiftCycleConfig (
    CycleID    INT IDENTITY(1,1) PRIMARY KEY,
    ShiftName  NVARCHAR(50) UNIQUE,
    DutyType   NVARCHAR(20),    -- starting duty type
    StartDate  NVARCHAR(20),
    CreatedAt  NVARCHAR(30) DEFAULT CONVERT(NVARCHAR(30), GETDATE(), 120)
);
```

---

## 2. Common Infrastructure (C#)

```csharp
// DbHelper.cs — pyodbc get_connection()-এর সমতুল্য
using Microsoft.Data.SqlClient;
using System.Data;

namespace PayrollSystem.Data
{
    public static class DbHelper
    {
        // আপনার db_config.ini / appsettings.json থেকে কানেকশন স্ট্রিং লোড করুন
        public static string ConnectionString { get; set; } = "";

        public static SqlConnection GetConnection()
        {
            var conn = new SqlConnection(ConnectionString);
            conn.Open();
            return conn;
        }

        /// <summary>Parameterized SELECT চালিয়ে List&lt;object[]&gt; রিটার্ন করে (row-based)।</summary>
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
            // মূল অ্যাপে: সব কনফিগ ফিল্ড ভরা + একবার test connection পাস করা থাকলে true
            if (string.IsNullOrWhiteSpace(ConnectionString)) return false;
            try
            {
                using var conn = GetConnection();
                return true;
            }
            catch { return false; }
        }
    }
}
```

```csharp
// CompanyHelper.cs — CompanyDetails read
namespace PayrollSystem.Data
{
    public record CompanyDetails(string Name, string Address, string Number);

    public static class CompanyHelper
    {
        public static CompanyDetails GetCompanyDetails()
        {
            var rows = DbHelper.FetchRows(
                "SELECT Name, Address, Number FROM CompanyDetails WHERE ID = 1");
            if (rows.Count == 0)
                return new CompanyDetails("PAYROLL SYSTEM", "", "");

            var r = rows[0];
            return new CompanyDetails(
                r[0] as string ?? "PAYROLL SYSTEM",
                r[1] as string ?? "",
                r[2] as string ?? "");
        }
    }
}
```

```csharp
// EmployeeExitHelper.cs
// মূল লজিক: exit_date সেট থাকলে ওই তারিখের পরে কর্মীকে "active" ধরা হবে না।
namespace PayrollSystem.Data
{
    public static class EmployeeExitHelper
    {
        /// <summary>EmpID -> earliest ExitDate ("yyyy-MM-dd") ম্যাপ তৈরি করে।</summary>
        public static Dictionary<int, string> GetEmployeeExitDateMap()
        {
            var rows = DbHelper.FetchRows(@"
                SELECT EmpID, MIN(NULLIF(LTRIM(RTRIM(ExitDate)), ''))
                FROM EmployeeExitInfo
                WHERE NULLIF(LTRIM(RTRIM(ExitDate)), '') IS NOT NULL
                GROUP BY EmpID");

            var map = new Dictionary<int, string>();
            foreach (var r in rows)
            {
                if (r[0] == null || r[1] == null) continue;
                map[Convert.ToInt32(r[0])] = r[1].ToString()!;
            }
            return map;
        }

        /// <summary>date &lt;= exitDate হলে (বা exitDate না থাকলে) কর্মী active।</summary>
        public static bool IsEmployeeActiveOn(Dictionary<int, string> exitDates, int empId, string dateValue)
        {
            if (!exitDates.TryGetValue(empId, out var exitDate) || string.IsNullOrEmpty(exitDate))
                return true;
            if (string.IsNullOrEmpty(dateValue))
                return true;
            return string.CompareOrdinal(dateValue, exitDate) <= 0;
        }

        /// <summary>SQL-এ বসানোর জন্য "active on date" শর্ত (NOT EXISTS সাব-কোয়েরি)।</summary>
        public static string EmployeeActiveOnDateSql(string empAlias, string dateExpr) =>
            $@"NOT EXISTS (
                 SELECT 1 FROM EmployeeExitInfo x
                 WHERE x.EmpID = {empAlias}.EmpID
                   AND NULLIF(LTRIM(RTRIM(x.ExitDate)), '') IS NOT NULL
                   AND x.ExitDate < {dateExpr}
               )";
    }
}
```

---

## 3. Shift Logic (C#) — `ShiftLogic.cs`

**মূল নিয়ম (Python `shift_logic.py` থেকে):**

- দুই ধরনের শিফট থাকতে পারে: (ক) **A/B rotation shift** (স্বয়ংক্রিয়ভাবে সাপ্তাহিক Day↔Night
  পাল্টায়), (খ) **স্থির (static) শিফট** (`ShiftSchedule` টেবিলে নির্দিষ্ট In/Out টাইম)।
- সপ্তাহের সীমানা **শনিবার থেকে শুক্রবার** পর্যন্ত ধরা হয় (`SaturdayOfWeek`)।
- `ShiftCycleConfig`-এ শুরুর তারিখ ও শুরুর ডিউটি টাইপ (Day/Night) সেভ থাকে; সপ্তাহ
  ইনডেক্স জোড় হলে শুরুর ডিউটি টাইপ, বিজোড় হলে উল্টোটা।
- `ShiftRotationSchedule`-এ প্রতিটি তারিখ + শিফটের জন্য প্রি-জেনারেটেড রো থাকে (ডিফল্ট
  ৫২০ সপ্তাহ ≈ ১০ বছর, MERGE দিয়ে upsert করা হয়)।
- শুক্রবার রাতের শিফট (Night শিফটের In টাইম) হলে `IsOvertime = 1` মার্ক হয়।

```csharp
using System.Globalization;

namespace PayrollSystem.Shifts
{
    public enum DutyType { Day, Night }

    public static class ShiftLogic
    {
        public const string DateFormat = "yyyy-MM-dd";
        public const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        // ডিফল্ট টেমপ্লেট — কাস্টম টাইম না থাকলে এগুলো ব্যবহৃত হয়
        public static readonly TimeSpan DayIn = new(8, 0, 0);
        public static readonly TimeSpan DayOut = new(20, 0, 0);
        public static readonly TimeSpan NightIn = new(20, 0, 0);
        public static readonly TimeSpan NightOut = new(8, 0, 0);

        public static DateTime ParseDate(string value) =>
            DateTime.ParseExact(value.Trim(), DateFormat, CultureInfo.InvariantCulture);

        public static TimeSpan ParseTime(string value)
        {
            string[] formats = { "HH:mm:ss", "HH:mm", "hh:mm:ss tt", "hh:mm tt" };
            foreach (var fmt in formats)
            {
                if (DateTime.TryParseExact(value.Trim(), fmt, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var dt))
                    return dt.TimeOfDay;
            }
            throw new FormatException($"Invalid time: {value}");
        }

        /// <summary>শুধু "A" / "B" / "Shift A" / "Shift B" সমর্থিত।</summary>
        public static string NormalizeShiftName(string shiftName)
        {
            var text = shiftName.Trim().ToUpperInvariant().Replace("SHIFT", "").Trim();
            if (text != "A" && text != "B")
                throw new ArgumentException("Only Shift A and Shift B are supported for auto-rotation");
            return $"Shift {text}";
        }

        public static bool IsAbShift(string shiftName)
        {
            try { NormalizeShiftName(shiftName); return true; }
            catch (ArgumentException) { return false; }
        }

        public static string OppositeShift(string shiftName)
        {
            var normalized = NormalizeShiftName(shiftName);
            return normalized == "Shift A" ? "Shift B" : "Shift A";
        }

        /// <summary>ওই তারিখ যে সপ্তাহে পড়ে (শনি-শুক্র সপ্তাহ) তার শনিবার তারিখ রিটার্ন করে।</summary>
        public static DateTime SaturdayOfWeek(DateTime workDate)
        {
            // .NET: Sunday=0 ... Saturday=6  (Python: Mon=0..Sun=6, Sat=5)
            int dow = (int)workDate.DayOfWeek; // Sun=0..Sat=6
            int daysSinceSaturday = ((dow - 6) % 7 + 7) % 7;
            return workDate.Date.AddDays(-daysSinceSaturday);
        }

        public static (DateTime inDt, DateTime outDt) CombineShiftDateTime(
            DateTime workDate, TimeSpan inTime, TimeSpan outTime)
        {
            var inDt = workDate.Date + inTime;
            var outDt = workDate.Date + outTime;
            if (outDt <= inDt) outDt = outDt.AddDays(1); // রাত পার হয়ে গেলে পরদিন হিসেবে গণ্য
            return (inDt, outDt);
        }

        public static double CalculateHours(string inDateTimeStr, string outDateTimeStr)
        {
            var inDt = DateTime.ParseExact(inDateTimeStr, DateTimeFormat, CultureInfo.InvariantCulture);
            var outDt = DateTime.ParseExact(outDateTimeStr, DateTimeFormat, CultureInfo.InvariantCulture);
            return (outDt - inDt).TotalHours;
        }

        public static (string inDateTime, string outDateTime) BuildAttendanceDatetimes(
            string workDate, string inTime, string outTime)
        {
            var (inDt, outDt) = CombineShiftDateTime(ParseDate(workDate), ParseTime(inTime), ParseTime(outTime));
            return (inDt.ToString(DateTimeFormat), outDt.ToString(DateTimeFormat));
        }

        public static string DutyTypeForTimes(string inTime, string outTime)
        {
            var inT = ParseTime(inTime);
            var outT = ParseTime(outTime);
            return outT <= inT ? "Night" : "Day";
        }

        public static bool IsFridayNightShift(DateTime? inDt) =>
            inDt.HasValue && inDt.Value.DayOfWeek == DayOfWeek.Friday && inDt.Value.TimeOfDay == NightIn;

        // ---------------- Cycle config (ShiftCycleConfig টেবিল) ----------------

        public record CycleConfig(string DutyType, string StartDate);

        public static Dictionary<string, CycleConfig> GetCycleConfigs()
        {
            var rows = DbHelper.FetchRows("SELECT ShiftName, DutyType, StartDate FROM ShiftCycleConfig");
            var configs = new Dictionary<string, CycleConfig>();
            foreach (var r in rows)
            {
                var name = (r[0] as string ?? "").Trim();
                if (name == "") continue;
                configs[name] = new CycleConfig(
                    (r[1] as string ?? "Day").Trim(),
                    (r[2] as string ?? "").Trim());
            }
            return configs;
        }

        public static void SaveCycleConfig(string shiftName, string dutyType, string startDate) =>
            DbHelper.ExecuteNonQuery(@"
                MERGE ShiftCycleConfig AS target
                USING (VALUES (@p0,@p1,@p2)) AS source (ShiftName, DutyType, StartDate)
                ON target.ShiftName = source.ShiftName
                WHEN MATCHED THEN
                    UPDATE SET DutyType = source.DutyType, StartDate = source.StartDate
                WHEN NOT MATCHED THEN
                    INSERT (ShiftName, DutyType, StartDate)
                    VALUES (source.ShiftName, source.DutyType, source.StartDate);",
                ("@p0", shiftName), ("@p1", dutyType), ("@p2", startDate));

        public static void DeleteCycleConfig(string shiftName) =>
            DbHelper.ExecuteNonQuery("DELETE FROM ShiftCycleConfig WHERE ShiftName = @p0", ("@p0", shiftName));

        public static void ClearCycleConfigs() =>
            DbHelper.ExecuteNonQuery("DELETE FROM ShiftCycleConfig");

        public static bool IsShiftInCycle(string shiftName)
        {
            var normalized = shiftName.Trim().ToUpperInvariant().Replace(" ", "");
            return GetCycleConfigs().Keys.Any(k => k.Trim().ToUpperInvariant().Replace(" ", "") == normalized);
        }

        /// <summary>
        /// শিফট সাইকেল অনুযায়ী নির্দিষ্ট তারিখে শিফটটি Day নাকি Night তা বের করে।
        /// সাইকেলে না থাকলে null রিটার্ন করে।
        /// </summary>
        public static string? GetDutyTypeForDate(string shiftName, string workDateStr,
            Dictionary<string, CycleConfig>? cycleConfigs = null)
        {
            if (string.IsNullOrWhiteSpace(shiftName)) return null;
            cycleConfigs ??= GetCycleConfigs();

            var normalized = shiftName.Trim().ToUpperInvariant().Replace(" ", "");
            var cfg = cycleConfigs.FirstOrDefault(kv =>
                kv.Key.Trim().ToUpperInvariant().Replace(" ", "") == normalized).Value;
            if (cfg is null || string.IsNullOrEmpty(cfg.StartDate)) return null;

            var workDate = ParseDate(workDateStr);
            var startDate = ParseDate(cfg.StartDate);

            var satOfWorkWeek = SaturdayOfWeek(workDate);
            var satOfStartWeek = SaturdayOfWeek(startDate);
            if (satOfWorkWeek < satOfStartWeek) return null; // সাইকেল শুরুর আগে

            int weekIndex = (int)(satOfWorkWeek - satOfStartWeek).TotalDays / 7;
            bool isStartingDay = cfg.DutyType == "Day";
            bool isDay = weekIndex % 2 == 0 ? isStartingDay : !isStartingDay;
            return isDay ? "Day" : "Night";
        }

        // ---------------- Static shift schedule (ShiftSchedule টেবিল) ----------------

        public record StaticShift(string DutyType, string InTime, string OutTime);

        public static StaticShift? GetStaticShiftSchedule(string shiftName)
        {
            var rows = DbHelper.FetchRows(@"
                SELECT DutyType, InTime, OutTime FROM ShiftSchedule
                WHERE UPPER(REPLACE(ShiftName, ' ', '')) = UPPER(REPLACE(@p0, ' ', ''))",
                ("@p0", shiftName.Trim()));
            if (rows.Count == 0) return null;
            var r = rows[0];
            return new StaticShift(r[0] as string ?? "", r[1] as string ?? "", r[2] as string ?? "");
        }

        public static void SaveShiftTime(string shiftName, string inTime, string outTime, string notes)
        {
            shiftName = shiftName.Trim();
            if (string.IsNullOrEmpty(inTime) || string.IsNullOrEmpty(outTime)) return;

            var inT = ParseTime(inTime).ToString(@"hh\:mm\:ss");
            var outT = ParseTime(outTime).ToString(@"hh\:mm\:ss");
            var dutyType = DutyTypeForTimes(inT, outT);

            DbHelper.ExecuteNonQuery(@"
                MERGE ShiftSchedule AS target
                USING (VALUES (@p0,@p1,@p2,@p3,@p4))
                      AS source (ShiftName, DutyType, InTime, OutTime, Notes)
                ON target.ShiftName = source.ShiftName
                WHEN MATCHED THEN
                    UPDATE SET DutyType=source.DutyType, InTime=source.InTime,
                               OutTime=source.OutTime, Notes=source.Notes
                WHEN NOT MATCHED THEN
                    INSERT (ShiftName, DutyType, InTime, OutTime, Notes)
                    VALUES (source.ShiftName, source.DutyType, source.InTime,
                            source.OutTime, source.Notes);",
                ("@p0", shiftName), ("@p1", dutyType), ("@p2", inT), ("@p3", outT), ("@p4", notes));
        }

        /// <summary>নির্দিষ্ট তারিখে একটি নির্দিষ্ট শিফটের সময়সূচি (rotation থেকে)।</summary>
        public record RotationRow(string DutyType, string InDateTime, string OutDateTime, bool IsOvertime);

        public static RotationRow? GetShiftScheduleForDate(string shiftName, string workDateStr)
        {
            var rows = DbHelper.FetchRows(@"
                SELECT DutyType, InDateTime, OutDateTime, IsOvertime
                FROM ShiftRotationSchedule
                WHERE UPPER(REPLACE(ShiftName, ' ', '')) = UPPER(REPLACE(@p0, ' ', ''))
                  AND ScheduleDate = @p1",
                ("@p0", shiftName.Trim()), ("@p1", ParseDate(workDateStr).ToString(DateFormat)));
            if (rows.Count == 0) return null;
            var r = rows[0];
            return new RotationRow(r[0] as string ?? "", r[1] as string ?? "", r[2] as string ?? "",
                Convert.ToInt32(r[3]) == 1);
        }

        /// <summary>
        /// একটি শিফট (এবং A/B হলে তার opposite শিফট)-এর জন্য rotation schedule জেনারেট করে।
        /// শনিবার থেকে শুরু, প্রতি সপ্তাহে Day/Night পাল্টায়, ডিফল্ট ৫২০ সপ্তাহ (~১০ বছর)।
        /// </summary>
        public static void SetShiftSchedule(string shiftName, string startDateStr, bool isDayShift, int weeks = 520)
        {
            var firstDate = ParseDate(startDateStr);
            var alignedStart = SaturdayOfWeek(firstDate);

            (string name, bool startingIsDay, StaticShift? custom)[] shiftsToGenerate;
            try
            {
                var primary = NormalizeShiftName(shiftName);
                var secondary = OppositeShift(primary);
                shiftsToGenerate = new[]
                {
                    (primary, isDayShift, GetStaticShiftSchedule(primary)),
                    (secondary, !isDayShift, GetStaticShiftSchedule(secondary)),
                };
            }
            catch (ArgumentException)
            {
                shiftsToGenerate = new[] { (shiftName.Trim(), isDayShift, GetStaticShiftSchedule(shiftName)) };
            }

            var rows = new List<(string date, string name, string duty, string inDt, string outDt, int ot)>();
            for (int dayOffset = 0; dayOffset < weeks * 7; dayOffset++)
            {
                var workDate = alignedStart.AddDays(dayOffset);
                int weekIndex = dayOffset / 7;
                foreach (var (name, startingIsDay, custom) in shiftsToGenerate)
                {
                    bool isDay = weekIndex % 2 == 0 ? startingIsDay : !startingIsDay;

                    TimeSpan inTime, outTime;
                    string dutyType;
                    if (custom is not null && !string.IsNullOrEmpty(custom.InTime) && !string.IsNullOrEmpty(custom.OutTime))
                    {
                        inTime = ParseTime(custom.InTime);
                        outTime = ParseTime(custom.OutTime);
                        dutyType = DutyTypeForTimes(custom.InTime, custom.OutTime);
                    }
                    else
                    {
                        inTime = isDay ? DayIn : NightIn;
                        outTime = isDay ? DayOut : NightOut;
                        dutyType = isDay ? "Day" : "Night";
                    }

                    var (inDt, outDt) = CombineShiftDateTime(workDate, inTime, outTime);
                    int isOvertime = IsFridayNightShift(inDt) ? 1 : 0;

                    rows.Add((workDate.ToString(DateFormat), NormalizeOrOriginal(name), dutyType,
                        inDt.ToString(DateTimeFormat), outDt.ToString(DateTimeFormat), isOvertime));
                }
            }

            using var conn = DbHelper.GetConnection();
            using var tx = conn.BeginTransaction();
            foreach (var r in rows)
            {
                using var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                    MERGE ShiftRotationSchedule AS target
                    USING (VALUES (@p0,@p1,@p2,@p3,@p4,@p5))
                          AS source (ScheduleDate, ShiftName, DutyType, InDateTime, OutDateTime, IsOvertime)
                    ON target.ScheduleDate = source.ScheduleDate
                       AND target.ShiftName = source.ShiftName
                    WHEN MATCHED THEN
                        UPDATE SET DutyType=source.DutyType, InDateTime=source.InDateTime,
                                   OutDateTime=source.OutDateTime, IsOvertime=source.IsOvertime
                    WHEN NOT MATCHED THEN
                        INSERT (ScheduleDate, ShiftName, DutyType, InDateTime, OutDateTime, IsOvertime)
                        VALUES (source.ScheduleDate, source.ShiftName, source.DutyType,
                                source.InDateTime, source.OutDateTime, source.IsOvertime);", conn, tx);
                cmd.Parameters.AddWithValue("@p0", r.date);
                cmd.Parameters.AddWithValue("@p1", r.name);
                cmd.Parameters.AddWithValue("@p2", r.duty);
                cmd.Parameters.AddWithValue("@p3", r.inDt);
                cmd.Parameters.AddWithValue("@p4", r.outDt);
                cmd.Parameters.AddWithValue("@p5", r.ot);
                cmd.ExecuteNonQuery();
            }

            // সর্বশেষ জেনারেট করা রো দিয়ে ShiftSchedule (static) টেবিল আপডেট
            foreach (var (name, _, _) in shiftsToGenerate)
            {
                var last = rows.LastOrDefault(x => x.name == NormalizeOrOriginal(name));
                if (last.name is null) continue;
                var inTimeOnly = last.inDt[^8..];
                var outTimeOnly = last.outDt[^8..];
                var notes = last.ot == 1 ? "Friday night OT" : "";
                using var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                    MERGE ShiftSchedule AS target
                    USING (VALUES (@p0,@p1,@p2,@p3,@p4))
                          AS source (ShiftName, DutyType, InTime, OutTime, Notes)
                    ON target.ShiftName = source.ShiftName
                    WHEN MATCHED THEN
                        UPDATE SET DutyType=source.DutyType, InTime=source.InTime,
                                   OutTime=source.OutTime, Notes=source.Notes
                    WHEN NOT MATCHED THEN
                        INSERT (ShiftName, DutyType, InTime, OutTime, Notes)
                        VALUES (source.ShiftName, source.DutyType, source.InTime,
                                source.OutTime, source.Notes);", conn, tx);
                cmd.Parameters.AddWithValue("@p0", name);
                cmd.Parameters.AddWithValue("@p1", last.duty);
                cmd.Parameters.AddWithValue("@p2", inTimeOnly);
                cmd.Parameters.AddWithValue("@p3", outTimeOnly);
                cmd.Parameters.AddWithValue("@p4", notes);
                cmd.ExecuteNonQuery();
            }

            tx.Commit();

            static string NormalizeOrOriginal(string n)
            {
                try { return NormalizeShiftName(n); } catch { return n.Trim(); }
            }
        }

        /// <summary>ShiftCycleConfig-এ থাকা সব শিফটের জন্য rotation regenerate করে।</summary>
        public static void PopulateAllShiftSchedules(int weeks = 520)
        {
            foreach (var (shiftName, cfg) in GetCycleConfigs())
            {
                if (string.IsNullOrEmpty(cfg.StartDate)) continue;
                SetShiftSchedule(shiftName, cfg.StartDate, cfg.DutyType == "Day", weeks);
            }
        }
    }
}
```

---

## 4. Report Tab — সম্পূর্ণ বিজনেস লজিক

### 4.1 রিপোর্টের তালিকা (৭টি বাটন)

| রিপোর্ট | বর্ণনা |
|---|---|
| Attendance Report | নির্বাচিত তারিখ-রেঞ্জে প্রতিদিন প্রতি কর্মীর In/Out টাইম (Absent/Holiday/Friday সহ) |
| Absent Report | যেসব কর্মী কর্মদিবসে অনুপস্থিত ছিল (weekend/holiday বাদে) |
| Present Report | নির্দিষ্ট দিন/রেঞ্জে যাদের In বা Out টাইম আছে |
| Late Report | Grace মিনিটের বেশি দেরিতে ঢুকেছে এমন রেকর্ড |
| Early Report | নির্ধারিত সময়ের অনেক আগে ঢুকেছে এমন রেকর্ড |
| Duty Duration Report | প্রতিদিন কর্মীর মোট ডিউটি সময় (ঘণ্টা:মিনিট) |
| Monthly Summary Report | পুরো মাসের ম্যাট্রিক্স (প্রতিদিন P/A/WL/WP মার্ক + টোটাল) |

### 4.2 কমন ফিল্টার

সব রিপোর্টে থাকে: Employee ID, Section, Designation, Category, Shift (dependent
dropdown — একটার ভ্যালু বাছলে বাকিগুলোর অপশন filter হয়ে যায়), এবং Date/Date-Range/Month।

```csharp
namespace PayrollSystem.Reports
{
    public class ReportFilters
    {
        public string EmpId { get; set; } = "";
        public string Date { get; set; } = "";
        public string FromDate { get; set; } = "";
        public string ToDate { get; set; } = "";
        public string Month { get; set; } = "";       // "yyyy-MM"
        public int GraceMinutes { get; set; } = 0;
        public string Section { get; set; } = "All";
        public string Designation { get; set; } = "All";
        public string Category { get; set; } = "All";
        public string Shift { get; set; } = "All";
    }
}
```

```csharp
// ReportQueryBuilder.cs
namespace PayrollSystem.Reports
{
    public static class ReportQueryBuilder
    {
        private static readonly HashSet<string> AllowedFilterFields =
            new() { "Section", "Designation", "Category", "Shift", "Department" };

        /// <summary>একটি ফিল্টার ফিল্ডের ডিসটিংক্ট ভ্যালু (dropdown-এর জন্য), অন্য ফিল্টারগুলো দিয়ে narrow করা।</summary>
        public static List<string> FilteredValues(string fieldName, ReportFilters filters)
        {
            if (!AllowedFilterFields.Contains(fieldName)) return new();

            var where = new List<string> { $"{fieldName} IS NOT NULL", $"TRIM({fieldName}) != ''" };
            var parameters = new List<(string, object)>();
            var map = new (string col, string val)[]
            {
                ("Section", filters.Section), ("Designation", filters.Designation),
                ("Category", filters.Category), ("Shift", filters.Shift),
            };
            int i = 0;
            foreach (var (col, val) in map)
            {
                if (col == fieldName) continue;
                if (val != "All")
                {
                    where.Add($"{col} = @p{i}");
                    parameters.Add(($"@p{i}", val));
                    i++;
                }
            }

            var sql = $@"SELECT DISTINCT {fieldName} FROM EmployeeInfo
                         WHERE {string.Join(" AND ", where)} ORDER BY {fieldName}";
            return DbHelper.FetchRows(sql, parameters.ToArray())
                .Select(r => r[0]?.ToString() ?? "").ToList();
        }

        /// <summary>এমপ্লয়ি WHERE ক্লজ + প্যারামিটার তৈরি করে (EmpID/Section/Designation/Category/Shift)।</summary>
        public static (List<string> where, List<(string, object)> parameters) EmployeeFilterSql(
            ReportFilters filters, string alias = "e")
        {
            var where = new List<string> { "1 = 1" };
            var parameters = new List<(string, object)>();
            int i = 0;

            if (!string.IsNullOrEmpty(filters.EmpId))
            {
                where.Add($"{alias}.EmpID = @p{i}");
                parameters.Add(($"@p{i}", filters.EmpId));
                i++;
            }
            var fields = new (string col, string val)[]
            {
                ("Section", filters.Section), ("Designation", filters.Designation),
                ("Category", filters.Category), ("Shift", filters.Shift),
            };
            foreach (var (col, val) in fields)
            {
                if (val != "All")
                {
                    where.Add($"{alias}.{col} = @p{i}");
                    parameters.Add(($"@p{i}", val));
                    i++;
                }
            }
            return (where, parameters);
        }

        public static List<string> DateRangeValues(string startDate, string endDate)
        {
            var start = ShiftLogic.ParseDate(startDate);
            var end = ShiftLogic.ParseDate(endDate);
            var dates = new List<string>();
            for (var d = start; d <= end; d = d.AddDays(1))
                dates.Add(d.ToString("yyyy-MM-dd"));
            return dates;
        }
    }
}
```

### 4.3 Weekend / Holiday হেল্পার

**নিয়ম:** `WeekEnd` টেবিলে যেসব বার (যেমন Saturday) সেট করা আছে + **শুক্রবার সবসময়
non-absent দিন** (কোম্পানির সাপ্তাহিক ছুটি না হলেও শুক্রবার অনুপস্থিত ধরা হয় না — শুক্রবারের
জন্য আলাদা "__FRIDAY__" মার্কার আছে)।

```csharp
namespace PayrollSystem.Reports
{
    public static class CalendarHelper
    {
        public static HashSet<string> WeekendDays() =>
            DbHelper.FetchRows("SELECT DayName FROM WeekEnd")
                .Select(r => (r[0] as string ?? "").Trim())
                .Where(d => d != "")
                .ToHashSet();

        /// <summary>Weekend days + Friday — "absent" গণনা থেকে বাদ দেওয়ার জন্য।</summary>
        public static HashSet<string> NonAbsentDays()
        {
            var days = WeekendDays();
            days.Add("Friday");
            return days;
        }

        public static bool IsNonAbsentDate(string dateValue, HashSet<string> nonAbsentDays)
        {
            if (string.IsNullOrEmpty(dateValue)) return false;
            if (!DateTime.TryParseExact(dateValue, "yyyy-MM-dd", null,
                    System.Globalization.DateTimeStyles.None, out var dt)) return false;
            return nonAbsentDays.Contains(dt.DayOfWeek.ToString());
        }

        /// <summary>date -> festival name ম্যাপ (from/to রেঞ্জ প্রতিটি দিনে expand করে)।</summary>
        public static Dictionary<string, string> CompanyHolidayMap(string? startDate = null, string? endDate = null)
        {
            var rows = DbHelper.FetchRows(@"
                SELECT FestivalName, FromDate, ToDate FROM CompanyHoliday
                WHERE NULLIF(LTRIM(RTRIM(FromDate)), '') IS NOT NULL");

            var holidays = new Dictionary<string, string>();
            foreach (var r in rows)
            {
                var festival = (r[0] as string) ?? "Company Holiday";
                var from = (r[1] as string ?? "").Trim();
                var to = (r[2] as string ?? from).Trim();
                if (to == "") to = from;
                if (from == "") continue;

                try
                {
                    foreach (var d in ReportQueryBuilder.DateRangeValues(from, to))
                    {
                        if (startDate != null && string.CompareOrdinal(d, startDate) < 0) continue;
                        if (endDate != null && string.CompareOrdinal(d, endDate) > 0) continue;
                        holidays[d] = string.IsNullOrEmpty(festival) ? "Company Holiday" : festival;
                    }
                }
                catch (FormatException) { continue; }
            }
            return holidays;
        }

        public const string FridayMarker = "__FRIDAY__";
        public const string AbsentMarker = "__ABSENT__";
        public const string HolidayMarkerPrefix = "__HOLIDAY__:";

        public static string HolidayMarker(string? festivalName) =>
            $"{HolidayMarkerPrefix}{(string.IsNullOrEmpty(festivalName) ? "Company Holiday" : festivalName)}";
    }
}
```

### 4.4 Time-formatting হেল্পার (রিপোর্টের জন্য)

```csharp
namespace PayrollSystem.Reports
{
    public static class ReportTimeHelper
    {
        public static string FormatReportTime(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            string[] formats = { "HH:mm:ss", "HH:mm", "yyyy-MM-dd HH:mm:ss" };
            foreach (var fmt in formats)
                if (DateTime.TryParseExact(value.Trim(), fmt, null,
                        System.Globalization.DateTimeStyles.None, out var dt))
                    return dt.ToString("hh:mm tt");
            return value.Trim();
        }

        public static string FormatShiftTime(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            string[] formats = { "HH:mm:ss", "HH:mm", "hh:mm:ss tt", "hh:mm tt" };
            foreach (var fmt in formats)
                if (DateTime.TryParseExact(value.Trim(), fmt, null,
                        System.Globalization.DateTimeStyles.None, out var dt))
                    return dt.ToString("hh:mm tt");
            return value.Trim();
        }

        public static bool? IsAmTime(string? timeStr)
        {
            if (string.IsNullOrEmpty(timeStr)) return null;
            var text = timeStr.Trim();
            text = text.Contains(' ') ? text.Split(' ')[^1] : text;
            string[] formats = { "HH:mm:ss", "HH:mm" };
            foreach (var fmt in formats)
                if (DateTime.TryParseExact(text, fmt, null,
                        System.Globalization.DateTimeStyles.None, out var dt))
                    return dt.Hour < 12;
            return null;
        }

        public static string FormatDuration(TimeSpan? duration)
        {
            if (duration is null) return "0:00";
            var totalSeconds = (int)Math.Abs(duration.Value.TotalSeconds);
            var minutes = totalSeconds / 60;
            var hours = minutes / 60;
            minutes %= 60;
            return $"{hours}:{minutes:D2}";
        }

        public static string FormatHoursDuration(double hoursValue)
        {
            var totalMinutes = (int)Math.Round(hoursValue * 60);
            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;
            return $"{hours}:{minutes:D2}";
        }

        /// <summary>
        /// Night Duty হলে In/Out কলামে AM/PM সোয়াপ করে দেখানো হয়:
        ///   Day duty:   InTime ← সকালের এন্ট্রি,  OutTime ← সন্ধ্যার এক্সিট
        ///   Night duty: InTime ← সন্ধ্যার এন্ট্রি, OutTime ← সকালের এক্সিট
        /// </summary>
        public static (string inDisplay, string outDisplay) AttendanceTimesForReport(
            string? inTimeRaw, string? outTimeRaw, string? dutyType, string dateValue)
        {
            var inFmt = FormatReportTime(inTimeRaw);
            var outFmt = FormatReportTime(outTimeRaw);

            if (dutyType != "Night") return (inFmt, outFmt);

            var inIsAm = IsAmTime(inTimeRaw);
            var outIsAm = IsAmTime(outTimeRaw);

            if (!string.IsNullOrEmpty(inTimeRaw) && !string.IsNullOrEmpty(outTimeRaw))
            {
                if (inIsAm == true && outIsAm == false) return (outFmt, inFmt);
                return (inFmt, outFmt);
            }

            if (!string.IsNullOrEmpty(inTimeRaw) && inIsAm == false) return (inFmt, "");
            if (!string.IsNullOrEmpty(inTimeRaw) && inIsAm == true) return ("", inFmt);
            if (!string.IsNullOrEmpty(outTimeRaw) && outIsAm == false) return (outFmt, "");
            if (!string.IsNullOrEmpty(outTimeRaw) && outIsAm == true) return ("", outFmt);

            return (inFmt, outFmt);
        }
    }
}
```

### 4.5 Expected Shift-In-Time (Late/Early রিপোর্টের জন্য)

```csharp
namespace PayrollSystem.Reports
{
    public static class ShiftExpectationHelper
    {
        /// <summary>
        /// শিফটের প্রত্যাশিত In Time বের করে: প্রথমে rotation schedule (তারিখ-ভিত্তিক),
        /// না পেলে static ShiftSchedule ফলব্যাক হিসেবে ব্যবহার হয়।
        /// </summary>
        public static TimeSpan? GetExpectedShiftInTime(string? shiftName, string dateValue)
        {
            if (string.IsNullOrWhiteSpace(shiftName)) return null;
            shiftName = shiftName.Trim();

            try
            {
                var schedule = ShiftLogic.GetShiftScheduleForDate(shiftName, dateValue);
                if (schedule is not null && !string.IsNullOrEmpty(schedule.InDateTime))
                {
                    var inPart = schedule.InDateTime.Contains(' ')
                        ? schedule.InDateTime.Split(' ', 2)[1] : schedule.InDateTime;
                    return ShiftLogic.ParseTime(inPart);
                }
            }
            catch { /* ফলব্যাকে যাবে */ }

            try
            {
                var staticShift = ShiftLogic.GetStaticShiftSchedule(shiftName);
                if (staticShift is not null && !string.IsNullOrEmpty(staticShift.InTime))
                    return ShiftLogic.ParseTime(staticShift.InTime.Trim());
            }
            catch { }

            return null;
        }

        /// <summary>একজন কর্মীর নির্দিষ্ট তারিখে Day/Night ডিউটি টাইপ (cache সহ)।</summary>
        public static string? EmployeeShiftDutyType(int empId, string dateValue,
            Dictionary<int, string>? empShiftCache = null)
        {
            empShiftCache ??= new();
            if (!empShiftCache.TryGetValue(empId, out var shiftName))
            {
                var rows = DbHelper.FetchRows(
                    "SELECT Shift FROM EmployeeInfo WHERE EmpID = @p0", ("@p0", empId));
                shiftName = rows.Count > 0 ? (rows[0][0] as string ?? "").Trim() : "";
                empShiftCache[empId] = shiftName;
            }
            if (string.IsNullOrEmpty(shiftName)) return null;
            return ShiftLogic.GetDutyTypeForDate(shiftName, dateValue);
        }
    }
}
```

### 4.6 Report Row Model

```csharp
namespace PayrollSystem.Reports
{
    public class ReportResult
    {
        public List<string> Columns { get; set; } = new();
        public List<object?[]> Rows { get; set; } = new();
    }
}
```

### 4.7 Attendance Report (হুবহু মূল Python লজিক)

**অগ্রাধিকার ক্রম প্রতিটি (কর্মী, তারিখ)-এর জন্য:**
1. **Holiday** — Company Holiday হলে সবার আগে দেখানো হয় (উপস্থিত থাকলেও)।
2. **Friday** —
   - Night duty (cycle-এ থাকা শিফট): যদি শুক্রবার রাতের রেকর্ডে PM ইন-টাইম থাকে (অর্থাৎ
     শুক্রবার রাতে ডিউটি শুরু হয়েছে) তাহলে সেটাকে normal attendance হিসেবে দেখানো হয়,
     নাহলে "__FRIDAY__" মার্কার।
   - Day duty / cycle-এ নেই: সবসময় "__FRIDAY__" মার্কার (Day শিফটের কেউ শুক্রবার কাজ করে
     না ধরা হয়)।
3. **সাধারণ attendance** — RawData-তে রেকর্ড থাকলে তা, নাহলে (weekend/Friday না হলে)
   "__ABSENT__" মার্কার।

**Night duty cross-midnight লজিক:** রাতের শিফটে হাজিরা রাত ৮টায় শুরু, পরদিন সকাল ৮টায়
শেষ — তাই আজকের OutTime না থাকলে **পরের দিনের রেকর্ড** থেকে AM টাইম খুঁজে সেটাকে আজকের
OutTime হিসেবে ধরা হয়। এই কারণে end_date-এর ১ দিন পরের ডেটাও fetch করতে হয়
("extended_end")। একইভাবে, শুক্রবারে যদি শুধু AM-only ইন-টাইম রেকর্ড থাকে (কোনো OutTime
ছাড়া) সেটা আসলে বৃহস্পতিবার রাতের শিফটের আউট-পাঞ্চ — তাই শুক্রবারের নিজস্ব রেকর্ড হিসেবে
স্কিপ করা হয় (এটা বৃহস্পতিবারের রো-তেই যোগ হয়ে গেছে)।

```csharp
namespace PayrollSystem.Reports
{
    public static class AttendanceReport
    {
        public static ReportResult Fetch(ReportFilters filters)
        {
            var (employeeWhere, employeeParams) = ReportQueryBuilder.EmployeeFilterSql(filters);
            var exitDates = EmployeeExitHelper.GetEmployeeExitDateMap();

            string startDate, endDate;
            if (!string.IsNullOrEmpty(filters.FromDate) && !string.IsNullOrEmpty(filters.ToDate))
            {
                startDate = filters.FromDate; endDate = filters.ToDate;
            }
            else if (!string.IsNullOrEmpty(filters.Date))
            {
                startDate = endDate = filters.Date;
            }
            else
            {
                (startDate, endDate) = AttendanceDateBounds();
            }

            var holidayMap = CalendarHelper.CompanyHolidayMap(startDate, endDate);
            var columns = new List<string> { "EmpID", "Name", "Section", "Designation", "Category", "Date", "In Time", "Out Time" };
            var rows = new List<object?[]>();

            var empQuery = $@"SELECT e.EmpID, e.Name, e.Section, e.Designation, e.Category
                               FROM EmployeeInfo e WHERE {string.Join(" AND ", employeeWhere)}
                               ORDER BY e.EmpID";
            var employees = DbHelper.FetchRows(empQuery, employeeParams.ToArray());

            var empShiftCache = new Dictionary<int, string>();
            foreach (var emp in employees)
            {
                var empId = Convert.ToInt32(emp[0]);
                if (!empShiftCache.ContainsKey(empId))
                {
                    var r = DbHelper.FetchRows("SELECT Shift FROM EmployeeInfo WHERE EmpID = @p0", ("@p0", empId));
                    empShiftCache[empId] = r.Count > 0 ? (r[0][0] as string ?? "").Trim() : "";
                }
            }

            var extendedEnd = ShiftLogic.ParseDate(endDate).AddDays(1).ToString("yyyy-MM-dd");

            var attendanceQuery = $@"
                SELECT e.EmpID, e.Name, e.Section, e.Designation, e.Category,
                       r.Date, r.InTime, r.OutTime
                FROM RawData r
                JOIN EmployeeInfo e ON e.EmpID = r.EmpID
                WHERE {string.Join(" AND ", employeeWhere)}
                  AND {EmployeeExitHelper.EmployeeActiveOnDateSql("e", "r.Date")}
                  AND r.Date BETWEEN @dStart AND @dEnd
                ORDER BY r.Date, e.EmpID";
            var allRawParams = employeeParams.Append(("@dStart", (object)startDate))
                                              .Append(("@dEnd", (object)extendedEnd)).ToArray();
            var allRawRows = DbHelper.FetchRows(attendanceQuery, allRawParams);

            // (empId, date) -> (inTimeRaw, outTimeRaw) — cross-midnight lookahead-এর জন্য
            var rawTimesMap = new Dictionary<(string, string), (string?, string?)>();
            foreach (var r in allRawRows)
                rawTimesMap[(r[0]!.ToString()!, r[5]!.ToString()!)] = (r[6] as string, r[7] as string);

            var attendanceMap = new Dictionary<(string, string), object?[]>();
            foreach (var raw in allRawRows)
            {
                var empId = raw[0]!; var name = raw[1]; var section = raw[2]; var designation = raw[3];
                var category = raw[4]; var dateValue = raw[5]!.ToString()!;
                var inTimeRaw = raw[6] as string; var outTimeRaw = raw[7] as string;

                if (string.CompareOrdinal(dateValue, endDate) > 0) continue; // শুধু lookahead-এর জন্য fetch করা

                var empIdKey = empId.ToString()!;
                var dutyType = ShiftExpectationHelper.EmployeeShiftDutyType(
                    Convert.ToInt32(empId), dateValue, empShiftCache);

                if (dutyType == "Night")
                {
                    if (string.IsNullOrEmpty(outTimeRaw))
                    {
                        var nextDate = ShiftLogic.ParseDate(dateValue).AddDays(1).ToString("yyyy-MM-dd");
                        rawTimesMap.TryGetValue((empIdKey, nextDate), out var nextTimes);
                        foreach (var candidate in new[] { nextTimes.Item1, nextTimes.Item2 })
                        {
                            if (string.IsNullOrEmpty(candidate)) continue;
                            try
                            {
                                var tStr = candidate!.Trim();
                                tStr = tStr.Contains(' ') ? tStr.Split(' ')[^1] : tStr;
                                var t = ShiftLogic.ParseTime(tStr);
                                if (t.Hours < 12) { outTimeRaw = candidate; break; }
                            }
                            catch { }
                        }
                    }

                    var rowDay = ShiftLogic.ParseDate(dateValue).DayOfWeek;
                    if (rowDay == DayOfWeek.Friday && !string.IsNullOrEmpty(inTimeRaw) && string.IsNullOrEmpty(outTimeRaw))
                    {
                        try
                        {
                            var tStr = inTimeRaw!.Trim();
                            tStr = tStr.Contains(' ') ? tStr.Split(' ')[^1] : tStr;
                            var t = ShiftLogic.ParseTime(tStr);
                            if (t.Hours < 12) continue; // বৃহস্পতিবারের আউট-পাঞ্চ, শুক্রবারে দেখানো হবে না
                        }
                        catch { }
                    }
                }

                var (inDisplay, outDisplay) = ReportTimeHelper.AttendanceTimesForReport(
                    inTimeRaw, outTimeRaw, dutyType, dateValue);
                attendanceMap[(empIdKey, dateValue)] = new object?[]
                    { empId, name, section, designation, category, dateValue, inDisplay, outDisplay };
            }

            var nonAbsentDays = CalendarHelper.NonAbsentDays();
            foreach (var dateValue in ReportQueryBuilder.DateRangeValues(startDate, endDate))
            {
                var dayName = ShiftLogic.ParseDate(dateValue).DayOfWeek.ToString();
                foreach (var emp in employees)
                {
                    var empId = Convert.ToInt32(emp[0]);
                    var name = emp[1]; var section = emp[2]; var designation = emp[3]; var category = emp[4];
                    if (!EmployeeExitHelper.IsEmployeeActiveOn(exitDates, empId, dateValue)) continue;
                    var key = (empId.ToString(), dateValue);

                    // 1) Holiday — সর্বোচ্চ অগ্রাধিকার
                    if (holidayMap.TryGetValue(dateValue, out var festival))
                    {
                        rows.Add(new object?[] { empId, name, section, designation, category, dateValue,
                            CalendarHelper.HolidayMarker(festival), "" });
                        continue;
                    }

                    // 2) Friday — attendance map চেক করার আগেই
                    if (dayName == "Friday")
                    {
                        var dutyType = ShiftExpectationHelper.EmployeeShiftDutyType(empId, dateValue, empShiftCache);
                        if (dutyType == "Night")
                        {
                            if (attendanceMap.TryGetValue(key, out var fridayRow))
                            {
                                var friInFmt = fridayRow[6]?.ToString() ?? "";
                                if (friInFmt.ToUpperInvariant().Contains("PM"))
                                {
                                    rows.Add(fridayRow);
                                    continue;
                                }
                            }
                            rows.Add(new object?[] { empId, name, section, designation, category, dateValue,
                                CalendarHelper.FridayMarker, "" });
                        }
                        else if (dutyType == "Day")
                        {
                            rows.Add(new object?[] { empId, name, section, designation, category, dateValue,
                                CalendarHelper.FridayMarker, "" });
                        }
                        else
                        {
                            if (attendanceMap.TryGetValue(key, out var row)) rows.Add(row);
                            else rows.Add(new object?[] { empId, name, section, designation, category, dateValue,
                                CalendarHelper.FridayMarker, "" });
                        }
                        continue;
                    }

                    // 3) সাধারণ attendance
                    if (attendanceMap.TryGetValue(key, out var attRow)) rows.Add(attRow);
                    else if (!CalendarHelper.IsNonAbsentDate(dateValue, nonAbsentDays))
                        rows.Add(new object?[] { empId, name, section, designation, category, dateValue,
                            CalendarHelper.AbsentMarker, "" });
                }
            }

            rows = rows.OrderBy(r => r[5]!.ToString())
                       .ThenBy(r => NumericSortValue(r[0]))
                       .ToList();
            return new ReportResult { Columns = columns, Rows = rows };
        }

        private static double NumericSortValue(object? value) =>
            double.TryParse(value?.ToString(), out var d) ? d : double.PositiveInfinity;

        private static (string, string) AttendanceDateBounds()
        {
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            var rows = DbHelper.FetchRows(
                "SELECT MIN(Date), MAX(Date) FROM RawData WHERE Date IS NOT NULL AND TRIM(Date) != ''");
            if (rows.Count == 0 || rows[0][0] is null) return (today, today);
            return (rows[0][0]!.ToString()!, rows[0][1]?.ToString() ?? today);
        }
    }
}
```

### 4.8 Absent Report

**নিয়ম:** শুধুমাত্র (weekend/Friday নয়, holiday নয়) এমন প্রতিটি তারিখে যেসব কর্মীর
`RawData`-তে কোনো রেকর্ড নেই (LEFT JOIN ... LogID IS NULL) তারাই তালিকায় আসবে।

```csharp
namespace PayrollSystem.Reports
{
    public static class AbsentReport
    {
        public static ReportResult Fetch(ReportFilters filters)
        {
            var (employeeWhere, employeeParams) = ReportQueryBuilder.EmployeeFilterSql(filters);
            var columns = new List<string> { "EmpID", "Name", "Section", "Designation", "Category", "Date" };
            var rows = new List<object?[]>();
            var nonAbsentDays = CalendarHelper.NonAbsentDays();
            var holidayMap = CalendarHelper.CompanyHolidayMap(filters.FromDate, filters.ToDate);

            foreach (var dateValue in ReportQueryBuilder.DateRangeValues(filters.FromDate, filters.ToDate))
            {
                if (holidayMap.ContainsKey(dateValue) || CalendarHelper.IsNonAbsentDate(dateValue, nonAbsentDays))
                    continue;

                var query = $@"
                    SELECT e.EmpID, e.Name, e.Section, e.Designation, e.Category, @dv
                    FROM EmployeeInfo e
                    LEFT JOIN RawData r ON r.EmpID = e.EmpID AND r.Date = @dv2
                    WHERE {string.Join(" AND ", employeeWhere)}
                      AND {EmployeeExitHelper.EmployeeActiveOnDateSql("e", "@dv3")}
                      AND r.LogID IS NULL
                    ORDER BY e.EmpID";

                var parameters = employeeParams
                    .Append(("@dv", (object)dateValue))
                    .Append(("@dv2", (object)dateValue))
                    .Append(("@dv3", (object)dateValue)).ToArray();

                rows.AddRange(DbHelper.FetchRows(query, parameters));
            }
            return new ReportResult { Columns = columns, Rows = rows };
        }
    }
}
```

> **নোট:** উপরের কোয়েরিতে `@dv`/`@dv2`/`@dv3` একই মান ভিন্ন প্যারামিটার নামে — কারণ
> `EmployeeActiveOnDateSql` স্ট্রিং-ইন্টারপোলেটেড এক্সপ্রেশন, তাই সবগুলোকে আলাদা প্যারামিটার
> রাখা নিরাপদ। চাইলে C#-এ একই `SqlParameter` তিনবার re-use করেও লেখা যায়।

### 4.9 Present Report

```csharp
namespace PayrollSystem.Reports
{
    public static class PresentReport
    {
        public static ReportResult Fetch(ReportFilters filters)
        {
            var (employeeWhere, employeeParams) = ReportQueryBuilder.EmployeeFilterSql(filters);
            var where = new List<string>(employeeWhere);
            var parameters = new List<(string, object)>(employeeParams);
            int i = employeeParams.Count;

            string startDate, endDate;
            if (!string.IsNullOrEmpty(filters.FromDate) && !string.IsNullOrEmpty(filters.ToDate))
            {
                where.Add($"r.Date BETWEEN @p{i} AND @p{i + 1}");
                parameters.Add(($"@p{i}", filters.FromDate));
                parameters.Add(($"@p{i + 1}", filters.ToDate));
                startDate = filters.FromDate; endDate = filters.ToDate;
                i += 2;
            }
            else
            {
                where.Add($"r.Date = @p{i}");
                parameters.Add(($"@p{i}", filters.Date));
                startDate = endDate = filters.Date;
                i++;
            }
            where.Add("(COALESCE(r.InTime, '') != '' OR COALESCE(r.OutTime, '') != '')");
            where.Add(EmployeeExitHelper.EmployeeActiveOnDateSql("e", "r.Date"));

            var query = $@"
                SELECT e.EmpID, e.Name, e.Section, e.Designation, e.Category,
                       r.Date, r.InTime, r.OutTime
                FROM RawData r JOIN EmployeeInfo e ON e.EmpID = r.EmpID
                WHERE {string.Join(" AND ", where)}
                ORDER BY r.Date, e.EmpID";

            var holidayMap = CalendarHelper.CompanyHolidayMap(startDate, endDate);
            var rows = new List<object?[]>();
            foreach (var r in DbHelper.FetchRows(query, parameters.ToArray()))
            {
                var rowValues = (object?[])r.Clone();
                var dateValue = rowValues[5]!.ToString()!;
                if (holidayMap.TryGetValue(dateValue, out var festival))
                {
                    rowValues[6] = CalendarHelper.HolidayMarker(festival);
                    rowValues[7] = "";
                }
                else
                {
                    rowValues[6] = ReportTimeHelper.FormatReportTime(rowValues[6] as string);
                    rowValues[7] = ReportTimeHelper.FormatReportTime(rowValues[7] as string);
                }
                rows.Add(rowValues);
            }

            // Holiday-তে যেসব একটিভ কর্মীর কোনো রেকর্ডই আসেনি (RawData-তে কিছু নেই),
            // তাদেরও Holiday মার্কার-সহ Present তালিকায় যোগ করা হয়।
            if (holidayMap.Count > 0)
            {
                var empQuery = $@"SELECT e.EmpID, e.Name, e.Section, e.Designation, e.Category
                                   FROM EmployeeInfo e WHERE {string.Join(" AND ", employeeWhere)}
                                   ORDER BY e.EmpID";
                var employees = DbHelper.FetchRows(empQuery, employeeParams.ToArray());
                var exitDates = EmployeeExitHelper.GetEmployeeExitDateMap();
                var existingKeys = rows.Select(r => (r[0]!.ToString(), r[5]!.ToString())).ToHashSet();

                foreach (var dateValue in ReportQueryBuilder.DateRangeValues(startDate, endDate))
                {
                    if (!holidayMap.TryGetValue(dateValue, out var festival)) continue;
                    foreach (var emp in employees)
                    {
                        var empId = Convert.ToInt32(emp[0]);
                        if (!EmployeeExitHelper.IsEmployeeActiveOn(exitDates, empId, dateValue)) continue;
                        var key = (empId.ToString(), dateValue);
                        if (existingKeys.Contains(key)) continue;
                        rows.Add(new object?[] { emp[0], emp[1], emp[2], emp[3], emp[4], dateValue,
                            CalendarHelper.HolidayMarker(festival), "" });
                        existingKeys.Add(key);
                    }
                }
                rows = rows.OrderBy(r => r[5]!.ToString())
                           .ThenBy(r => double.TryParse(r[0]?.ToString(), out var d) ? d : double.PositiveInfinity)
                           .ToList();
            }

            return new ReportResult
            {
                Columns = new() { "EmpID", "Name", "Section", "Designation", "Category", "Date", "In Time", "Out Time" },
                Rows = rows,
            };
        }
    }
}
```

### 4.10 Late Report ও Early Report

**নিয়ম:**
- প্রতিটি রেকর্ডের জন্য প্রত্যাশিত In Time (`GetExpectedShiftInTime`) বের করা হয়; না
  পাওয়া গেলে সেই রেকর্ড স্কিপ।
- **Late:** `actualIn > expectedIn` এবং `(actualIn - expectedIn) > graceMinutes`
  হলে তালিকায় আসবে; Status কলামে দেরির পরিমাণ (H:MM)।
- **Early:** থ্রেশহোল্ড = `expectedIn - graceMinutes`; `actualIn < threshold` হলে
  তালিকায় আসবে — তবে থ্রেশহোল্ড PM (দুপুরের পরে) অথচ actualIn AM হলে বাদ (রাতের শিফটে
  ভুল পজিটিভ এড়াতে)। Status কলামে কত আগে ঢুকেছে (H:MM)।
- Access/Allowed-In কলামে থ্রেশহোল্ড টাইম ফরম্যাট করে দেখানো হয়।

```csharp
namespace PayrollSystem.Reports
{
    public enum LateEarlyType { Late, Early }

    public static class LateEarlyReport
    {
        public static ReportResult Fetch(LateEarlyType type, ReportFilters filters)
        {
            var accessColumn = type == LateEarlyType.Late ? "Access" : "Allowed In";
            var columns = new List<string>
                { "EmpID", "Name", "Section", "Designation", "Date", "Shift", accessColumn, "In Time", "Out Time", "Status" };
            var rows = new List<object?[]>();

            var (employeeWhere, employeeParams) = ReportQueryBuilder.EmployeeFilterSql(filters);
            var where = new List<string>(employeeWhere);
            var parameters = new List<(string, object)>(employeeParams);
            int i = employeeParams.Count;

            if (!string.IsNullOrEmpty(filters.FromDate) && !string.IsNullOrEmpty(filters.ToDate))
            {
                where.Add($"r.Date BETWEEN @p{i} AND @p{i + 1}");
                parameters.Add(($"@p{i}", filters.FromDate)); parameters.Add(($"@p{i + 1}", filters.ToDate));
                i += 2;
            }
            else if (!string.IsNullOrEmpty(filters.Date))
            {
                where.Add($"r.Date = @p{i}");
                parameters.Add(($"@p{i}", filters.Date));
                i++;
            }
            where.Add(EmployeeExitHelper.EmployeeActiveOnDateSql("e", "r.Date"));

            var query = $@"
                SELECT e.EmpID, e.Name, e.Section, e.Designation,
                       r.Date, e.Shift, r.InTime, r.OutTime
                FROM RawData r JOIN EmployeeInfo e ON e.EmpID = r.EmpID
                WHERE {string.Join(" AND ", where)}
                ORDER BY r.Date, e.EmpID";

            int graceMinutes = filters.GraceMinutes;

            foreach (var r in DbHelper.FetchRows(query, parameters.ToArray()))
            {
                var empId = r[0]; var name = r[1]; var section = r[2]; var designation = r[3];
                var dateValue = r[4]!.ToString()!; var shiftName = r[5] as string;
                var inTimeRaw = r[6] as string; var outTimeRaw = r[7] as string;
                if (string.IsNullOrEmpty(inTimeRaw)) continue;

                TimeSpan actualIn;
                try { actualIn = ShiftLogic.ParseTime(inTimeRaw); } catch { continue; }

                var expectedIn = ShiftExpectationHelper.GetExpectedShiftInTime(shiftName, dateValue);
                if (expectedIn is null) continue;

                var reportDate = ShiftLogic.ParseDate(dateValue);
                var expectedDt = reportDate + expectedIn.Value;
                var thresholdDt = type == LateEarlyType.Early
                    ? expectedDt.AddMinutes(-graceMinutes)
                    : expectedDt;
                var thresholdTime = thresholdDt.TimeOfDay;
                var actualInDt = reportDate + actualIn;

                string? status = null;
                if (type == LateEarlyType.Late && actualInDt > expectedDt)
                {
                    var delta = actualInDt - expectedDt;
                    if (delta <= TimeSpan.FromMinutes(graceMinutes)) continue;
                    status = ReportTimeHelper.FormatDuration(delta);
                }
                else if (type == LateEarlyType.Early && actualInDt < thresholdDt)
                {
                    bool thresholdIsPm = thresholdTime.Hours >= 12;
                    bool actualIsPm = actualIn.Hours >= 12;
                    if (thresholdIsPm && !actualIsPm) continue;
                    var delta = thresholdDt - actualInDt;
                    status = ReportTimeHelper.FormatDuration(delta);
                }
                if (status is null) continue;

                rows.Add(new object?[]
                {
                    empId, name, section, designation, dateValue, shiftName ?? "",
                    ReportTimeHelper.FormatShiftTime(thresholdTime.ToString(@"hh\:mm\:ss")),
                    ReportTimeHelper.FormatReportTime(inTimeRaw),
                    ReportTimeHelper.FormatReportTime(outTimeRaw),
                    status,
                });
            }
            return new ReportResult { Columns = columns, Rows = rows };
        }
    }
}
```

### 4.11 Duty Duration Report

**নিয়ম:** প্রতিদিনের জন্য In/Out টাইম থেকে ঘণ্টা হিসাব (`CalculateHours`), ফলাফল
`H:MM` ফরম্যাটে। Night duty-র cross-midnight ও Friday-skip লজিক Attendance Report-এর
মতোই প্রযোজ্য (উপরে ৪.৭ দেখুন)।

```csharp
namespace PayrollSystem.Reports
{
    public static class DutyDurationReport
    {
        public static ReportResult Fetch(ReportFilters filters)
        {
            var (employeeWhere, employeeParams) = ReportQueryBuilder.EmployeeFilterSql(filters);
            var columns = new List<string> { "EmpID", "Name", "Section", "Designation", "Date", "In Time", "Out Time", "Duration" };
            var rows = new List<object?[]>();

            string startDate = !string.IsNullOrEmpty(filters.FromDate) ? filters.FromDate : filters.Date;
            string endDate = !string.IsNullOrEmpty(filters.ToDate) ? filters.ToDate : filters.Date;
            var holidayMap = CalendarHelper.CompanyHolidayMap(startDate, endDate);
            var exitDates = EmployeeExitHelper.GetEmployeeExitDateMap();

            var empQuery = $@"SELECT e.EmpID, e.Name, e.Section, e.Designation
                               FROM EmployeeInfo e WHERE {string.Join(" AND ", employeeWhere)}
                               ORDER BY e.EmpID";
            var employees = DbHelper.FetchRows(empQuery, employeeParams.ToArray());
            var empShiftCache = new Dictionary<int, string>();

            var extendedEnd = ShiftLogic.ParseDate(endDate).AddDays(1).ToString("yyyy-MM-dd");
            var attendanceQuery = $@"
                SELECT e.EmpID, e.Name, e.Section, e.Designation,
                       r.Date, r.InTime, r.OutTime
                FROM RawData r JOIN EmployeeInfo e ON e.EmpID = r.EmpID
                WHERE {string.Join(" AND ", employeeWhere)}
                  AND {EmployeeExitHelper.EmployeeActiveOnDateSql("e", "r.Date")}
                  AND r.Date BETWEEN @dStart AND @dEnd
                ORDER BY r.Date, e.EmpID";
            var allRows = DbHelper.FetchRows(attendanceQuery,
                employeeParams.Append(("@dStart", (object)startDate)).Append(("@dEnd", (object)extendedEnd)).ToArray());

            var rawTimesMap = new Dictionary<(string, string), (string?, string?)>();
            foreach (var r in allRows)
                rawTimesMap[(r[0]!.ToString()!, r[4]!.ToString()!)] = (r[5] as string, r[6] as string);

            var attendanceMap = new Dictionary<(string, string), object?[]>();
            foreach (var row in allRows)
            {
                var empId = row[0]!; var name = row[1]; var section = row[2]; var designation = row[3];
                var dateValue = row[4]!.ToString()!;
                var inTimeRaw = row[5] as string; var outTimeRaw = row[6] as string;
                if (string.CompareOrdinal(dateValue, endDate) > 0) continue;

                var empIdKey = empId.ToString()!;
                var dutyType = ShiftExpectationHelper.EmployeeShiftDutyType(Convert.ToInt32(empId), dateValue, empShiftCache);

                if (dutyType == "Night")
                {
                    if (string.IsNullOrEmpty(outTimeRaw))
                    {
                        var nextDate = ShiftLogic.ParseDate(dateValue).AddDays(1).ToString("yyyy-MM-dd");
                        rawTimesMap.TryGetValue((empIdKey, nextDate), out var nextTimes);
                        foreach (var candidate in new[] { nextTimes.Item1, nextTimes.Item2 })
                        {
                            if (string.IsNullOrEmpty(candidate)) continue;
                            try
                            {
                                var tStr = candidate!.Trim();
                                tStr = tStr.Contains(' ') ? tStr.Split(' ')[^1] : tStr;
                                if (ShiftLogic.ParseTime(tStr).Hours < 12) { outTimeRaw = candidate; break; }
                            }
                            catch { }
                        }
                    }
                    var rowDay = ShiftLogic.ParseDate(dateValue).DayOfWeek;
                    if (rowDay == DayOfWeek.Friday && !string.IsNullOrEmpty(inTimeRaw) && string.IsNullOrEmpty(outTimeRaw))
                    {
                        try
                        {
                            var tStr = inTimeRaw!.Trim();
                            tStr = tStr.Contains(' ') ? tStr.Split(' ')[^1] : tStr;
                            if (ShiftLogic.ParseTime(tStr).Hours < 12) continue;
                        }
                        catch { }
                    }
                }

                string duration = "";
                if (!string.IsNullOrEmpty(inTimeRaw) && !string.IsNullOrEmpty(outTimeRaw))
                {
                    try
                    {
                        var (inDt, outDt) = ShiftLogic.BuildAttendanceDatetimes(dateValue, inTimeRaw, outTimeRaw);
                        duration = ReportTimeHelper.FormatHoursDuration(ShiftLogic.CalculateHours(inDt, outDt));
                    }
                    catch { duration = ""; }
                }

                var (inDisplay, outDisplay) = ReportTimeHelper.AttendanceTimesForReport(inTimeRaw, outTimeRaw, dutyType, dateValue);
                attendanceMap[(empIdKey, dateValue)] = new object?[] { empId, name, section, designation, dateValue, inDisplay, outDisplay, duration };
            }

            var nonAbsentDays = CalendarHelper.NonAbsentDays();
            foreach (var dateValue in ReportQueryBuilder.DateRangeValues(startDate, endDate))
            {
                var dayName = ShiftLogic.ParseDate(dateValue).DayOfWeek.ToString();
                foreach (var emp in employees)
                {
                    var empId = Convert.ToInt32(emp[0]);
                    var name = emp[1]; var section = emp[2]; var designation = emp[3];
                    if (!EmployeeExitHelper.IsEmployeeActiveOn(exitDates, empId, dateValue)) continue;
                    var key = (empId.ToString(), dateValue);

                    if (holidayMap.TryGetValue(dateValue, out var festival))
                    {
                        rows.Add(new object?[] { empId, name, section, designation, dateValue,
                            CalendarHelper.HolidayMarker(festival), "", "" });
                        continue;
                    }

                    if (dayName == "Friday")
                    {
                        var dutyType = ShiftExpectationHelper.EmployeeShiftDutyType(empId, dateValue, empShiftCache);
                        if (dutyType == "Night")
                        {
                            if (attendanceMap.TryGetValue(key, out var fridayRow) &&
                                (fridayRow[5]?.ToString() ?? "").ToUpperInvariant().Contains("PM"))
                            {
                                rows.Add(fridayRow);
                                continue;
                            }
                        }
                        rows.Add(new object?[] { empId, name, section, designation, dateValue,
                            CalendarHelper.FridayMarker, "", "" });
                        continue;
                    }

                    if (attendanceMap.TryGetValue(key, out var row)) rows.Add(row);
                    else if (!CalendarHelper.IsNonAbsentDate(dateValue, nonAbsentDays))
                        rows.Add(new object?[] { empId, name, section, designation, dateValue,
                            CalendarHelper.AbsentMarker, "", "" });
                }
            }

            rows = rows.OrderBy(r => r[4]!.ToString())
                       .ThenBy(r => double.TryParse(r[0]?.ToString(), out var d) ? d : double.PositiveInfinity)
                       .ToList();
            return new ReportResult { Columns = columns, Rows = rows };
        }
    }
}
```

### 4.12 Monthly Summary Report

**নিয়ম (মার্ক লজিক, প্রতিদিনের জন্য):**

| শর্ত | মার্ক |
|---|---|
| Exit date-এর পরের দিন (inactive) | (blank) |
| Company Holiday | **P** |
| শুক্রবার + Night duty শিফট + সেদিন উপস্থিত | **WP** |
| শুক্রবার + Night duty শিফট + অনুপস্থিত | **WL** (weekend count +1) |
| শুক্রবার/Weekend day (non-Friday-night-case) | **WL** (আসল WeekEnd টেবিলের বার হলে weekend count +1) |
| সাধারণ কর্মদিবসে উপস্থিত | **P** |
| সাধারণ কর্মদিবসে অনুপস্থিত | **A** |

কলাম: `EmpID, Name, Section, Designation, 01, 02, ... (মাসের প্রতিটি দিন), Present, WL, Total P, Total A`
যেখানে `Present = P/WP গণনা`, `WL = weekend/Friday leave গণনা`, `Total P = Present + WL`,
`Total A = A গণনা`।

```csharp
namespace PayrollSystem.Reports
{
    public static class MonthlySummaryReport
    {
        public static ReportResult Fetch(ReportFilters filters)
        {
            var parts = filters.Month.Split('-');
            int year = int.Parse(parts[0]), month = int.Parse(parts[1]);
            int lastDay = DateTime.DaysInMonth(year, month);

            var (employeeWhere, employeeParams) = ReportQueryBuilder.EmployeeFilterSql(filters);
            var employeeQuery = $@"SELECT e.EmpID, e.Name, e.Section, e.Designation
                                    FROM EmployeeInfo e WHERE {string.Join(" AND ", employeeWhere)}
                                    ORDER BY e.EmpID";
            var employees = DbHelper.FetchRows(employeeQuery, employeeParams.ToArray());

            var startDate = $"{year:D4}-{month:D2}-01";
            var endDate = $"{year:D4}-{month:D2}-{lastDay:D2}";
            var holidayMap = CalendarHelper.CompanyHolidayMap(startDate, endDate);

            var attendanceQuery = $@"
                SELECT e.EmpID, r.Date FROM RawData r
                JOIN EmployeeInfo e ON e.EmpID = r.EmpID
                WHERE {string.Join(" AND ", employeeWhere)}
                  AND {EmployeeExitHelper.EmployeeActiveOnDateSql("e", "r.Date")}
                  AND r.Date BETWEEN @dStart AND @dEnd";
            var attendanceRows = DbHelper.FetchRows(attendanceQuery,
                employeeParams.Append(("@dStart", (object)startDate)).Append(("@dEnd", (object)endDate)).ToArray());

            var presentDates = new Dictionary<int, HashSet<string>>();
            foreach (var r in attendanceRows)
            {
                var empId = Convert.ToInt32(r[0]);
                if (!presentDates.TryGetValue(empId, out var set))
                    presentDates[empId] = set = new HashSet<string>();
                set.Add(r[1]!.ToString()!);
            }

            var weekendDays = CalendarHelper.WeekendDays();
            var nonAbsentDays = CalendarHelper.NonAbsentDays();
            var dayColumns = Enumerable.Range(1, lastDay).Select(d => d.ToString("D2")).ToList();
            var columns = new List<string> { "EmpID", "Name", "Section", "Designation" }
                .Concat(dayColumns).Concat(new[] { "Present", "WL", "Total P", "Total A" }).ToList();

            var exitDates = EmployeeExitHelper.GetEmployeeExitDateMap();
            var rows = new List<object?[]>();

            foreach (var emp in employees)
            {
                var empId = Convert.ToInt32(emp[0]);
                var name = emp[1]; var section = emp[2]; var designation = emp[3];
                var empPresentDates = presentDates.TryGetValue(empId, out var s) ? s : new HashSet<string>();

                var marks = new List<string>();
                int presentCount = 0, weekendCount = 0, activeDayCount = 0;

                for (int day = 1; day <= lastDay; day++)
                {
                    var dateValue = $"{year:D4}-{month:D2}-{day:D2}";
                    if (!EmployeeExitHelper.IsEmployeeActiveOn(exitDates, empId, dateValue))
                    {
                        marks.Add("");
                        continue;
                    }
                    activeDayCount++;
                    var dayName = ShiftLogic.ParseDate(dateValue).DayOfWeek.ToString();
                    string mark;

                    if (holidayMap.ContainsKey(dateValue))
                    {
                        mark = "P";
                    }
                    else if (nonAbsentDays.Contains(dayName))
                    {
                        if (dayName == "Friday")
                        {
                            var dutyType = ShiftExpectationHelper.EmployeeShiftDutyType(empId, dateValue);
                            if (dutyType == "Night") { mark = "WL"; weekendCount++; }
                            else if (empPresentDates.Contains(dateValue)) mark = "WP";
                            else { mark = "WL"; weekendCount++; }
                        }
                        else
                        {
                            if (weekendDays.Contains(dayName)) weekendCount++;
                            mark = "WL";
                        }
                    }
                    else if (empPresentDates.Contains(dateValue))
                    {
                        mark = "P";
                    }
                    else
                    {
                        mark = "A";
                    }

                    if (mark is "P" or "WP") presentCount++;
                    marks.Add(mark);
                }

                if (activeDayCount == 0) continue;
                int totalAbsent = marks.Count(m => m == "A");

                var row = new List<object?> { empId, name, section, designation };
                row.AddRange(marks);
                row.Add(presentCount); row.Add(weekendCount);
                row.Add(weekendCount + presentCount); row.Add(totalAbsent);
                rows.Add(row.ToArray());
            }

            return new ReportResult { Columns = columns, Rows = rows };
        }
    }
}
```

### 4.13 Filter Summary ও Export Filename

```csharp
namespace PayrollSystem.Reports
{
    public static class ReportNamingHelper
    {
        public static string FilterSummary(ReportFilters filters, string? reportType = null)
        {
            if (!string.IsNullOrEmpty(filters.FromDate) && !string.IsNullOrEmpty(filters.ToDate))
                return $"Date: {DisplayDate(filters.FromDate)} - {DisplayDate(filters.ToDate)}";

            if (reportType == "monthly" && !string.IsNullOrEmpty(filters.Month))
            {
                if (DateTime.TryParseExact(filters.Month, "yyyy-MM", null,
                        System.Globalization.DateTimeStyles.None, out var dt))
                    return $"Month: {dt:MMMM - yyyy}";
                return $"Month: {filters.Month}";
            }

            if (!string.IsNullOrEmpty(filters.Date))
                return $"Date: {DisplayDate(filters.Date)}";
            return "All records";
        }

        public static string DisplayDate(string dateValue)
        {
            if (DateTime.TryParseExact(dateValue, "yyyy-MM-dd", null,
                    System.Globalization.DateTimeStyles.None, out var dt))
                return dt.ToString("dd/MM/yyyy");
            return dateValue ?? "";
        }

        public static string SanitizeFilenameValue(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return System.Text.RegularExpressions.Regex.Replace(value.Trim(), "[<>:\"/\\\\|?*]", "");
        }

        /// <summary>এক্সপোর্ট ডিফল্ট ফাইলনেম: "Title - ID=.. - Section=.. - Date=.._to_.."</summary>
        public static string ExportDefaultFilename(string reportTitle, ReportFilters filters)
        {
            var parts = new List<string> { SanitizeFilenameValue(reportTitle) };

            if (!string.IsNullOrEmpty(filters.EmpId))
                parts.Add($"ID={SanitizeFilenameValue(filters.EmpId)}");

            foreach (var (label, value) in new[]
                     { ("Section", filters.Section), ("Designation", filters.Designation),
                       ("Category", filters.Category), ("Shift", filters.Shift) })
            {
                if (value != "All" && !string.IsNullOrEmpty(value))
                    parts.Add($"{label}={SanitizeFilenameValue(value)}");
            }

            if (reportTitle == "Late Report") parts.Add($"LateMoreThan={filters.GraceMinutes}min");
            else if (reportTitle == "Early Report") parts.Add($"Grace={filters.GraceMinutes}min");

            if (!string.IsNullOrEmpty(filters.FromDate) && !string.IsNullOrEmpty(filters.ToDate))
                parts.Add($"Date={SanitizeFilenameValue(filters.FromDate)}_to_{SanitizeFilenameValue(filters.ToDate)}");
            else if (!string.IsNullOrEmpty(filters.Date))
                parts.Add($"Date={SanitizeFilenameValue(filters.Date)}");

            if (parts.Count == 1) parts.Add("All");
            return string.Join(" - ", parts);
        }
    }
}
```

### 4.14 PDF Export — কাঠামো (QuestPDF দিয়ে উদাহরণ)

মূল Python কোড `reportlab` ব্যবহার করেছে; নিচে একই **নিয়ম** (কোম্পানি হেডার, রিপোর্ট
টাইটেল/ফিল্টার সাবটাইটেল, প্রতি-পেজে হেডার রিপিট অপশন, Absent/Holiday/Friday সেল হাইলাইট
মার্জ, employee-wise আলাদা রিপোর্ট অপশন, পেজ নম্বরিং) `QuestPDF`-এ বাস্তবায়িত করা হলো।
আপনার প্রজেক্টে যদি iTextSharp/PdfSharp থাকে, শুধু রেন্ডারিং API বদলে দিন — গঠন
(কী কী দেখাতে হবে, কী ক্রমে) অপরিবর্তিত রাখুন।

```csharp
// PDF রেন্ডারিং অপশন — মূল অ্যাপের "Export/Print Settings" ডায়ালগের সমতুল্য
namespace PayrollSystem.Reports
{
    public class ReportPdfOptions
    {
        public string PaperName { get; set; } = "A4";
        public bool Landscape { get; set; } = false;
        public bool RepeatCompanyHeaderOnEveryPage { get; set; } = true;
        public bool SeparateReportPerEmployee { get; set; } = false;
    }
}
```

```csharp
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PayrollSystem.Reports
{
    public static class ReportPdfBuilder
    {
        public static void Build(string filePath, string reportTitle, string reportFilters,
            ReportResult result, ReportPdfOptions options)
        {
            var company = CompanyHelper.GetCompanyDetails();

            // Holiday/Friday/Absent মার্কারযুক্ত রো আলাদাভাবে হাইলাইট + মার্জ করে দেখানো হবে
            var groups = options.SeparateReportPerEmployee
                ? GroupByEmployee(result)
                : new List<List<object?[]>> { result.Rows };

            QuestPDF.Settings.License = LicenseType.Community;
            Document.Create(container =>
            {
                foreach (var (group, idx) in groups.Select((g, i) => (g, i)))
                {
                    container.Page(page =>
                    {
                        page.Size(options.PaperName == "A4"
                            ? (options.Landscape ? PageSizes.A4.Landscape() : PageSizes.A4)
                            : PageSizes.A4);
                        page.Margin(30);

                        if (options.RepeatCompanyHeaderOnEveryPage)
                        {
                            page.Header().Column(col =>
                            {
                                col.Item().AlignCenter().Text(company.Name).Bold().FontSize(16);
                                if (!string.IsNullOrWhiteSpace(company.Address))
                                    col.Item().AlignCenter().Text(company.Address).FontSize(9);
                                if (!string.IsNullOrWhiteSpace(company.Number))
                                    col.Item().AlignCenter().Text($"Number: {company.Number}").FontSize(9);
                                col.Item().AlignCenter().Text(reportTitle).Bold().FontSize(12);
                                col.Item().AlignCenter().Text(reportFilters).FontSize(9);
                            });
                        }

                        page.Content().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                foreach (var _ in result.Columns) cols.RelativeColumn();
                            });

                            table.Header(header =>
                            {
                                foreach (var col in result.Columns)
                                    header.Cell().Background(Colors.Grey.Lighten3).Padding(3)
                                        .Text(col).Bold().FontSize(9);
                            });

                            foreach (var row in group)
                            {
                                // Holiday / Friday / Absent মার্কার হলে ইন-টাইম কলাম মার্জ করে
                                // ধূসর ব্যাকগ্রাউন্ডে দেখানো হয় (মূল অ্যাপের "merged cell" আচরণ)
                                var inTimeIdx = result.Columns.IndexOf("In Time");
                                var mergedText = inTimeIdx >= 0
                                    ? MergedCellText(row[inTimeIdx]?.ToString())
                                    : null;

                                for (int c = 0; c < result.Columns.Count; c++)
                                {
                                    if (mergedText != null && c == inTimeIdx)
                                    {
                                        table.Cell().ColumnSpan((uint)(result.Columns.Count - c))
                                            .Background(Colors.Grey.Lighten2).Padding(3)
                                            .AlignCenter().Text(mergedText).FontSize(9);
                                        break;
                                    }
                                    table.Cell().Padding(3).AlignCenter()
                                        .Text(row[c]?.ToString() ?? "").FontSize(9);
                                }
                            }
                        });

                        page.Footer().AlignCenter().Text(t =>
                        {
                            t.CurrentPageNumber(); t.Span(" / "); t.TotalPages();
                        });
                    });

                    if (idx < groups.Count - 1) { /* নতুন পেজ পরের group-এর জন্য */ }
                }
            }).GeneratePdf(filePath);
        }

        private static string? MergedCellText(string? value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            if (value == CalendarHelper.FridayMarker) return "Friday";
            if (value == CalendarHelper.AbsentMarker) return "Absent";
            if (value.StartsWith(CalendarHelper.HolidayMarkerPrefix))
                return value.Substring(CalendarHelper.HolidayMarkerPrefix.Length);
            return null;
        }

        private static List<List<object?[]>> GroupByEmployee(ReportResult result)
        {
            var empIdIdx = result.Columns.IndexOf("EmpID");
            return result.Rows.GroupBy(r => r[empIdIdx]?.ToString())
                .Select(g => g.ToList()).ToList();
        }
    }
}
```

---

## 5. Admin Tools Tab — সম্পূর্ণ বিজনেস লজিক

Admin Tools-এ ৬টি ট্যাব: **Company Details, Week End, Company Holiday, Resign Info,
Shift Change, Shift Cycle**।

### 5.1 Company Details Tab

```csharp
namespace PayrollSystem.AdminTools
{
    public class CompanyDetailsInput
    {
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public string Number { get; set; } = "";
    }

    public static class CompanyDetailsService
    {
        public static CompanyDetails Load() => CompanyHelper.GetCompanyDetails();

        public static void Save(CompanyDetailsInput input) =>
            DbHelper.ExecuteNonQuery(
                "UPDATE CompanyDetails SET Name = @p0, Address = @p1, Number = @p2 WHERE ID = 1",
                ("@p0", input.Name.Trim()), ("@p1", input.Address.Trim()), ("@p2", input.Number.Trim()));
    }
}
```

### 5.2 Week End Tab

**নিয়ম:** কোম্পানির সাপ্তাহিক ছুটির বার(গুলো) `WeekEnd` টেবিলে রাখা হয় (একাধিক দিন হতে
পারে); যোগ করার সময় ডুপ্লিকেট চেক করা হয় (`IF NOT EXISTS`)।

```csharp
namespace PayrollSystem.AdminTools
{
    public static class WeekEndService
    {
        public static readonly string[] AllDays =
            { "Saturday", "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" };

        public static List<string> List() =>
            DbHelper.FetchRows("SELECT DayName FROM WeekEnd ORDER BY DayName")
                .Select(r => r[0]!.ToString()!).ToList();

        public static void Add(string dayName) =>
            DbHelper.ExecuteNonQuery(
                "IF NOT EXISTS (SELECT 1 FROM WeekEnd WHERE DayName = @p0) INSERT INTO WeekEnd (DayName) VALUES (@p1)",
                ("@p0", dayName), ("@p1", dayName));

        public static void Delete(string dayName) =>
            DbHelper.ExecuteNonQuery("DELETE FROM WeekEnd WHERE DayName = @p0", ("@p0", dayName));
    }
}
```

### 5.3 Company Holiday Tab

**নিয়ম:** ফেস্টিভ্যাল নাম আবশ্যক; From Date, To Date-এর পরে হতে পারবে না। Add/Update
একই ফর্মে (`selectedHolidayId` থাকলে UPDATE, না থাকলে INSERT)।

```csharp
namespace PayrollSystem.AdminTools
{
    public record CompanyHolidayRow(int HolidayId, string FestivalName, string FromDate, string ToDate, string Notes);

    public class CompanyHolidayInput
    {
        public int? HolidayId { get; set; }        // null হলে নতুন, সেট থাকলে আপডেট
        public string FestivalName { get; set; } = "";
        public string FromDate { get; set; } = "";  // yyyy-MM-dd
        public string ToDate { get; set; } = "";
        public string Notes { get; set; } = "";
    }

    public static class CompanyHolidayService
    {
        public static List<CompanyHolidayRow> List() =>
            DbHelper.FetchRows(
                "SELECT HolidayID, FestivalName, FromDate, ToDate, Notes FROM CompanyHoliday ORDER BY FromDate")
            .Select(r => new CompanyHolidayRow(
                Convert.ToInt32(r[0]), r[1]?.ToString() ?? "", r[2]?.ToString() ?? "",
                r[3]?.ToString() ?? "", r[4]?.ToString() ?? "")).ToList();

        /// <returns>(success, errorMessage)</returns>
        public static (bool ok, string? error) Save(CompanyHolidayInput input)
        {
            if (string.IsNullOrWhiteSpace(input.FestivalName))
                return (false, "Festival name is required");
            if (string.CompareOrdinal(input.FromDate, input.ToDate) > 0)
                return (false, "From date cannot be after To date");

            if (input.HolidayId.HasValue)
            {
                DbHelper.ExecuteNonQuery(
                    "UPDATE CompanyHoliday SET FestivalName=@p0, FromDate=@p1, ToDate=@p2, Notes=@p3 WHERE HolidayID=@p4",
                    ("@p0", input.FestivalName.Trim()), ("@p1", input.FromDate), ("@p2", input.ToDate),
                    ("@p3", input.Notes.Trim()), ("@p4", input.HolidayId.Value));
            }
            else
            {
                DbHelper.ExecuteNonQuery(
                    "INSERT INTO CompanyHoliday (FestivalName, FromDate, ToDate, Notes) VALUES (@p0,@p1,@p2,@p3)",
                    ("@p0", input.FestivalName.Trim()), ("@p1", input.FromDate), ("@p2", input.ToDate),
                    ("@p3", input.Notes.Trim()));
            }
            return (true, null);
        }

        public static void Delete(int holidayId) =>
            DbHelper.ExecuteNonQuery("DELETE FROM CompanyHoliday WHERE HolidayID = @p0", ("@p0", holidayId));
    }
}
```

### 5.4 Resign / Exit Info Tab

**নিয়ম:**
- EmpID দিলে সাথে সাথে (টাইপ করার সময়ই) নাম অটো-লোড হয়; না পাওয়া গেলে "Employee not
  found"।
- Reason Type ড্রপডাউন থেকে একটা বাছলে Reason টেক্সটবক্সে সেটা বসে যায় (প্রি-ফিল, তবে
  এডিটযোগ্য)।
- সেভ করার সময় `(EmpID, ExitType)` জোড়া ইউনিক — MERGE দিয়ে upsert (নতুন হলে insert,
  আগে থেকে থাকলে update)।
- এই তথ্যই সারা অ্যাপে "কর্মী active কি না" যাচাইয়ের ভিত্তি (Section 2-এর
  `EmployeeExitHelper` দেখুন)।

```csharp
namespace PayrollSystem.AdminTools
{
    public static class ExitReasons
    {
        public static readonly string[] CommonResignReasons =
        {
            "Better career opportunity", "Personal reason", "Family reason", "Relocation",
            "Higher education", "Health issue", "Mutual agreement", "End of contract",
            "Unsatisfactory performance", "Regular absenteeism", "Disciplinary issue",
            "Violation of company policy", "Misconduct", "Workforce reduction",
        };
    }

    public record ExitInfoRow(int ExitId, int EmpId, string Name, string ExitType, string ExitDate, string Reason);

    public class ExitInfoInput
    {
        public int EmpId { get; set; }
        public string ExitType { get; set; } = "Resign";
        public string ExitDate { get; set; } = "";
        public string Reason { get; set; } = "";
    }

    public static class ExitInfoService
    {
        public static List<ExitInfoRow> List(string exitType) =>
            DbHelper.FetchRows(@"
                SELECT x.ExitID, x.EmpID, e.Name, x.ExitType, x.ExitDate, x.Reason
                FROM EmployeeExitInfo x
                LEFT JOIN EmployeeInfo e ON e.EmpID = x.EmpID
                WHERE x.ExitType = @p0
                ORDER BY x.CreatedAt DESC", ("@p0", exitType))
            .Select(r => new ExitInfoRow(Convert.ToInt32(r[0]), Convert.ToInt32(r[1]),
                r[2]?.ToString() ?? "", r[3]?.ToString() ?? "", r[4]?.ToString() ?? "",
                r[5]?.ToString() ?? "")).ToList();

        public static string? LookupEmployeeName(int empId)
        {
            var rows = DbHelper.FetchRows("SELECT Name FROM EmployeeInfo WHERE EmpID = @p0", ("@p0", empId));
            return rows.Count > 0 ? rows[0][0]?.ToString() : null;
        }

        /// <returns>(success, errorMessage)</returns>
        public static (bool ok, string? error) Save(ExitInfoInput input)
        {
            var name = LookupEmployeeName(input.EmpId);
            if (name is null) return (false, "Employee not found");

            DbHelper.ExecuteNonQuery(@"
                MERGE EmployeeExitInfo AS target
                USING (VALUES (@p0,@p1,@p2,@p3)) AS source (EmpID, ExitType, ExitDate, Reason)
                ON target.EmpID = source.EmpID AND target.ExitType = source.ExitType
                WHEN MATCHED THEN
                    UPDATE SET ExitDate=source.ExitDate, Reason=source.Reason
                WHEN NOT MATCHED THEN
                    INSERT (EmpID, ExitType, ExitDate, Reason)
                    VALUES (source.EmpID, source.ExitType, source.ExitDate, source.Reason);",
                ("@p0", input.EmpId), ("@p1", input.ExitType), ("@p2", input.ExitDate), ("@p3", input.Reason.Trim()));
            return (true, null);
        }

        public static void Delete(int exitId) =>
            DbHelper.ExecuteNonQuery("DELETE FROM EmployeeExitInfo WHERE ExitID = @p0", ("@p0", exitId));
    }
}
```

### 5.5 Shift Change Tab (Static Shift Schedule ম্যানেজমেন্ট)

**নিয়ম:**
- শিফট ড্রপডাউনে শুধু সেই শিফটগুলো দেখানো হয় যেগুলো `EmployeeInfo.Shift`-এ ব্যবহৃত
  হয়েছে **কিন্তু** এখনও `ShiftSchedule`-এ সময় সেট করা হয়নি (যাতে ডুপ্লিকেট এন্ট্রি না হয়)।
- In/Out টাইম দুটোর একটাও খালি রাখা যাবে না।
- `Out <= In` হলে Duty Type স্বয়ংক্রিয়ভাবে "Night" ধরা হয় (`DutyTypeForTimes`), নাহলে
  "Day"।
- সেভ MERGE দিয়ে upsert (ShiftName ইউনিক)।
- ডিলিটের পর যদি টেবিল খালি হয়ে যায়, IDENTITY কলাম রিসিড করা হয় (`DBCC CHECKIDENT`)।

```csharp
namespace PayrollSystem.AdminTools
{
    public record ShiftScheduleRow(int ScheduleId, string ShiftName, string DutyType, string InTime, string OutTime, string Notes);

    public static class ShiftChangeService
    {
        /// <summary>যেসব শিফট EmployeeInfo-তে আছে কিন্তু এখনও ShiftSchedule-এ টাইম সেট হয়নি।</summary>
        public static List<string> UnscheduledShifts() =>
            DbHelper.FetchRows(@"
                SELECT DISTINCT Shift FROM EmployeeInfo
                WHERE Shift IS NOT NULL AND TRIM(Shift) <> ''
                  AND Shift NOT IN (
                    SELECT ShiftName FROM ShiftSchedule
                    WHERE ShiftName IS NOT NULL AND TRIM(ShiftName) <> ''
                  )
                ORDER BY 1")
            .Select(r => r[0]!.ToString()!).ToList();

        public static List<ShiftScheduleRow> List() =>
            DbHelper.FetchRows(
                "SELECT ScheduleID, ShiftName, DutyType, InTime, OutTime, Notes FROM ShiftSchedule ORDER BY ShiftName")
            .Select(r => new ShiftScheduleRow(Convert.ToInt32(r[0]), r[1]?.ToString() ?? "",
                r[2]?.ToString() ?? "", r[3]?.ToString() ?? "", r[4]?.ToString() ?? "", r[5]?.ToString() ?? ""))
            .ToList();

        /// <returns>(success, errorMessage)</returns>
        public static (bool ok, string? error) Save(string shiftName, string inTime, string outTime, string notes)
        {
            shiftName = shiftName.Trim();
            if (string.IsNullOrEmpty(shiftName)) return (false, "No shift found in Employee Info");
            if (inTime == "00:00:00") inTime = "";
            if (outTime == "00:00:00") outTime = "";
            if (string.IsNullOrEmpty(inTime) && string.IsNullOrEmpty(outTime))
                return (false, "In Time and Out Time cannot both be blank");

            try
            {
                ShiftLogic.SaveShiftTime(shiftName, inTime, outTime, notes.Trim());
                return (true, null);
            }
            catch (Exception e)
            {
                return (false, $"Failed to save shift time: {e.Message}");
            }
        }

        public static void Delete(int scheduleId)
        {
            DbHelper.ExecuteNonQuery("DELETE FROM ShiftSchedule WHERE ScheduleID = @p0", ("@p0", scheduleId));
            var count = Convert.ToInt32(DbHelper.ExecuteScalar("SELECT COUNT(*) FROM ShiftSchedule"));
            if (count == 0)
                DbHelper.ExecuteNonQuery("DBCC CHECKIDENT ('ShiftSchedule', RESEED, 0)");
        }
    }
}
```

### 5.6 Shift Cycle Tab

**নিয়ম:**
- `EmployeeInfo`-এ ব্যবহৃত সব ইউনিক শিফটের তালিকা দেখানো হয়; প্রতিটির পাশে
  চেকবক্স (সাইকেলে আছে কিনা) + রেডিও (Day/Night — সাইকেল শুরুর ডিউটি টাইপ)।
- অন্তত একটা শিফট চেক করা থাকতেই হবে "Generate" করার জন্য।
- Generate করলে প্রতিটি নির্বাচিত শিফটের জন্য: (১) `ShiftCycleConfig` আপসার্ট করে, (২)
  `SetShiftSchedule` কল করে ৫২০ সপ্তাহের rotation জেনারেট করে (শনিবার-সারিবদ্ধ)।
- "Delete All" করলে `ShiftRotationSchedule`-এর সব রো এবং `ShiftCycleConfig`-এর সব
  কনফিগারেশন মুছে যায় (নিশ্চিতকরণ ডায়ালগসহ)।

```csharp
namespace PayrollSystem.AdminTools
{
    public record ShiftCycleSelection(string ShiftName, string DutyType); // "Day" or "Night"

    public record ShiftRotationRow(string ScheduleDate, string ShiftName, string DutyType,
        string InDateTime, string OutDateTime, bool IsOvertime);

    public static class ShiftCycleService
    {
        public static List<string> AllShiftsInEmployeeInfo() =>
            DbHelper.FetchRows(@"
                SELECT DISTINCT Shift FROM EmployeeInfo
                WHERE Shift IS NOT NULL AND TRIM(Shift) <> '' ORDER BY Shift")
            .Select(r => r[0]!.ToString()!.Trim()).ToList();

        public static Dictionary<string, ShiftLogic.CycleConfig> ExistingConfigs() =>
            ShiftLogic.GetCycleConfigs();

        public static List<ShiftRotationRow> RecentRotationRows(int top = 500) =>
            DbHelper.FetchRows($@"
                SELECT TOP {top} ScheduleDate, ShiftName, DutyType, InDateTime, OutDateTime, IsOvertime
                FROM ShiftRotationSchedule ORDER BY ScheduleDate DESC, ShiftName")
            .Select(r => new ShiftRotationRow(r[0]?.ToString() ?? "", r[1]?.ToString() ?? "",
                r[2]?.ToString() ?? "", r[3]?.ToString() ?? "", r[4]?.ToString() ?? "",
                Convert.ToInt32(r[5]) == 1)).ToList();

        /// <returns>(success, errorMessage, generatedCount)</returns>
        public static (bool ok, string? error, int count) GenerateCycle(
            string startDate, List<ShiftCycleSelection> selectedShifts)
        {
            if (selectedShifts.Count == 0)
                return (false, "Please select at least one shift for the cycle.", 0);

            try
            {
                foreach (var sel in selectedShifts)
                {
                    ShiftLogic.SaveCycleConfig(sel.ShiftName, sel.DutyType, startDate);
                    ShiftLogic.SetShiftSchedule(sel.ShiftName, startDate, sel.DutyType == "Day", weeks: 520);
                }
                return (true, null, selectedShifts.Count);
            }
            catch (Exception e)
            {
                return (false, $"Failed to generate shift cycle: {e.Message}", 0);
            }
        }

        /// <summary>সব rotation + cycle config মুছে ফেলে (কনফার্মেশনের পরে UI থেকে কল করুন)।</summary>
        public static void DeleteAll()
        {
            DbHelper.ExecuteNonQuery("DELETE FROM ShiftRotationSchedule");
            ShiftLogic.ClearCycleConfigs();
        }
    }
}
```

---

## 6. Cross-cutting নিয়ম (দুই ট্যাবেই প্রযোজ্য) — সংক্ষিপ্ত চেকলিস্ট

- [ ] সব ডেট স্ট্রিং `yyyy-MM-dd`, সব টাইম স্ট্রিং `HH:mm:ss` — কখনও কালচার-নির্ভর
      ফরম্যাট (যেমন `MM/dd/yyyy`) দিয়ে ডাটাবেজে সেভ/তুলনা করবেন না।
- [ ] `EmployeeInfo.Shift` খালি/নাল হলে সেই কর্মীর জন্য কোনো Day/Night সাইকেল নিয়ম
      প্রযোজ্য নয় (duty type = null) — Attendance/Duty Duration রিপোর্টে সরাসরি
      raw In/Out দেখানো হবে, Friday special-case প্রযোজ্য হবে না যদি cycle-এ না থাকে।
- [ ] Exit/Resign তারিখের **পরের দিন** থেকে কর্মীকে সব রিপোর্ট ও স্যালারি হিসাব থেকে বাদ
      দিতে হবে (`x.ExitDate < date` অর্থাৎ ExitDate নিজেই শেষ active দিন)।
- [ ] Grace minutes (Late/Early) ঋণাত্মক (negative) দেওয়া যাবে না — UI-তে
      `int` validator (>=0) রাখুন, মূল অ্যাপে `QIntValidator(0, 999, ...)`।
- [ ] Company Holiday From/To তারিখ রেঞ্জ সাপেক্ষে প্রতিটি দিনকে আলাদা করে
      হলিডে-ম্যাপে expand করতে হবে (একটানা একাধিক দিনের ছুটি সমর্থনের জন্য)।
- [ ] `ShiftRotationSchedule`/`ShiftCycleConfig` টেবিলদুটো অন্তত একবার
      `ensure_shift_tables`-এর C# সমতুল্য (মাইগ্রেশন স্ক্রিপ্ট/EF মাইগ্রেশন) দিয়ে তৈরি
      করে নিতে হবে যদি আগে থেকে না থাকে (Section 1-এর CREATE TABLE দেখুন)।

---

## 7. পরিশিষ্ট: Salary হিসাব (রেফারেন্সের জন্য, Report Tab-এর অংশ না হলেও সম্পর্কিত)

`GrossWages ÷ মাসের কর্মদিবস (weekend+holiday বাদে) = daily wage`;
`daily wage ÷ 8 = hourly rate`; Absent deduction = `absentDays × dailyWage`;
Late deduction = `lateDays × dailyWage × 0.5`; Overtime pay = `overtimeHours × hourlyRate × 1.5`;
`Net Pay = GrossWages - (AbsentDeduction + LateDeduction) + OvertimePay`.

```csharp
namespace PayrollSystem.Payroll
{
    public class SalaryResult
    {
        public int EmpId; public string Name = ""; public string Designation = ""; public string Section = "";
        public string Category = ""; public double GrossWages; public string Month = "";
        public int TotalWorkingDays; public int PresentDays; public int AbsentDays; public int LateDays;
        public double TotalOvertimeHours; public double DailyWage; public double AbsentDeduction;
        public double LateDeduction; public double TotalDeductions; public double OvertimePay; public double NetPay;
    }

    public static class SalaryService
    {
        public static SalaryResult? CalculateSalary(int empId, string month) // month = "yyyy-MM"
        {
            var empRows = DbHelper.FetchRows("SELECT * FROM EmployeeInfo WHERE EmpID = @p0", ("@p0", empId));
            if (empRows.Count == 0) return null;
            var emp = empRows[0];
            double grossWages = emp[10] is double gw ? gw : Convert.ToDouble(emp[10] ?? 0.0); // GrossWages কলাম ইনডেক্স স্কিমা অনুযায়ী মিলিয়ে নিন

            var parts = month.Split('-');
            int year = int.Parse(parts[0]), monthNumber = int.Parse(parts[1]);
            int lastDay = DateTime.DaysInMonth(year, monthNumber);

            var exitDates = EmployeeExitHelper.GetEmployeeExitDateMap();
            if (!EmployeeExitHelper.IsEmployeeActiveOn(exitDates, empId, $"{year:D4}-{monthNumber:D2}-01"))
                return null;

            var weekendDays = CalendarHelper.WeekendDays();
            var holidayMap = CalendarHelper.CompanyHolidayMap();

            var workingDates = new List<string>();
            for (int day = 1; day <= lastDay; day++)
            {
                var dateValue = $"{year:D4}-{monthNumber:D2}-{day:D2}";
                if (!EmployeeExitHelper.IsEmployeeActiveOn(exitDates, empId, dateValue)) continue;
                var dayName = new DateTime(year, monthNumber, day).DayOfWeek.ToString();
                if (!weekendDays.Contains(dayName) && !holidayMap.ContainsKey(dateValue))
                    workingDates.Add(dateValue);
            }

            int totalWorkingDays = workingDates.Count;
            double dailyWage = totalWorkingDays > 0 ? grossWages / totalWorkingDays : 0;
            double hourlyRate = dailyWage > 0 ? dailyWage / 8 : 0;

            var records = DbHelper.FetchRows(@"
                SELECT * FROM ProcessedAttendance
                WHERE EmpID = @p0 AND Date LIKE @p1
                  AND NOT EXISTS (
                      SELECT 1 FROM EmployeeExitInfo x
                      WHERE x.EmpID = ProcessedAttendance.EmpID
                        AND NULLIF(LTRIM(RTRIM(x.ExitDate)), '') IS NOT NULL
                        AND x.ExitDate < ProcessedAttendance.Date
                  )", ("@p0", empId), ("@p1", $"{month}%"));

            var workingSet = workingDates.ToHashSet();
            var presentDates = records.Select(r => r[2]?.ToString() ?? "")
                                       .Where(d => workingSet.Contains(d)).ToHashSet();
            foreach (var h in holidayMap.Keys) if (workingSet.Contains(h)) presentDates.Add(h);

            int presentDays = presentDates.Count;
            int absentDays = Math.Max(totalWorkingDays - presentDays, 0);
            int lateDays = records.Count(r => (r[6]?.ToString() ?? "") == "Yes");
            double totalOvertimeHours = records.Sum(r => r[7] is null ? 0.0 : Convert.ToDouble(r[7]));

            double absentDeduction = absentDays * dailyWage;
            double lateDeduction = lateDays * (dailyWage * 0.5);
            double totalDeductions = absentDeduction + lateDeduction;
            double overtimePay = totalOvertimeHours * hourlyRate * 1.5;
            double netPay = grossWages - totalDeductions + overtimePay;

            return new SalaryResult
            {
                EmpId = empId, Name = emp[1]?.ToString() ?? "", Designation = emp[4]?.ToString() ?? "",
                Section = emp[5]?.ToString() ?? "", Category = emp[8]?.ToString() ?? "", GrossWages = grossWages,
                Month = month, TotalWorkingDays = totalWorkingDays, PresentDays = presentDays, AbsentDays = absentDays,
                LateDays = lateDays, TotalOvertimeHours = totalOvertimeHours, DailyWage = dailyWage,
                AbsentDeduction = absentDeduction, LateDeduction = lateDeduction, TotalDeductions = totalDeductions,
                OvertimePay = overtimePay, NetPay = netPay,
            };
        }
    }
}
```

> **সতর্কতা:** `EmployeeInfo` টেবিলের কলাম-ইনডেক্স (`emp[10]`, `emp[1]` ইত্যাদি)
> `SELECT *`-এর কলাম-অর্ডারের উপর নির্ভরশীল — Section 1-এর CREATE TABLE-এর অর্ডার
> অনুযায়ী `GrossWages` হলো ১১তম কলাম (0-based index 10)। যদি আপনার C# প্রজেক্টে
> Entity/DTO ম্যাপিং ব্যবহার করেন (যেমন Dapper `QuerySingle<EmployeeInfo>`), তাহলে
> ইনডেক্সের বদলে নাম দিয়ে অ্যাক্সেস করুন — এটাই সবচেয়ে নিরাপদ, এজেন্টকে এইভাবে রিফ্যাক্টর
     করার পরামর্শ দেওয়া হলো।

---

## 8. পরবর্তী ধাপে এজেন্টকে যা করতে বলবেন

আপনি এই পুরো ফাইলটা এজেন্টকে দেওয়ার পর নিচের মতো একটা প্রম্পট দিতে পারেন:

> "উপরের `SKILL.md`-এ বর্ণিত সব লজিক অনুযায়ী আমার C# প্রজেক্টে
> `PayrollSystem.Reports` এবং `PayrollSystem.AdminTools` নেমস্পেসে ক্লাসগুলো তৈরি/
> আপডেট করো। প্রথমে Section 2-3 (কমন ইনফ্রাস্ট্রাকচার ও শিফট লজিক) implement করো,
> তারপর Section 4 (Report Tab) এবং Section 5 (Admin Tools Tab)। প্রতিটি রিপোর্টের
> জন্য ইউনিট টেস্ট লেখো যাতে Holiday > Friday > Attendance অগ্রাধিকার এবং Night-shift
> cross-midnight লজিক ঠিকমতো কাজ করে তা যাচাই করা যায়। UI framework: [আপনার ফ্রেমওয়ার্ক
> এখানে লিখুন — WinForms/WPF/Blazor/ASP.NET Core MVC]।"
