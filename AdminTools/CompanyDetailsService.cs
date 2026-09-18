using PayrollManagement.Data;

namespace PayrollManagement.AdminTools
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

        public static void Save(CompanyDetailsInput input)
        {
            EnsureTable();
            DbHelper.ExecuteNonQuery(@"
                MERGE dbo.CompanyDetails AS target
                USING (VALUES (1, @p0, @p1, @p2)) AS source (ID, Name, Address, Number)
                ON target.ID = source.ID
                WHEN MATCHED THEN
                    UPDATE SET Name = source.Name, Address = source.Address, Number = source.Number
                WHEN NOT MATCHED THEN
                    INSERT (ID, Name, Address, Number) VALUES (source.ID, source.Name, source.Address, source.Number);",
                ("@p0", input.Name.Trim()), ("@p1", input.Address.Trim()), ("@p2", input.Number.Trim()));
        }

        public static void EnsureTable()
        {
            try
            {
                DbHelper.ExecuteNonQuery(@"
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
                    END;");
            }
            catch { }
        }
    }
}
