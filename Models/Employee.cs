using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PayrollManagement.Models
{
    public class Employee : INotifyPropertyChanged
    {
        private int _sl;
        public int SL { get => _sl; set { _sl = value; OnPropertyChanged(); OnPropertyChanged(nameof(EmployeeId)); } }

        private string _name = "";
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); OnPropertyChanged(nameof(FullName)); } }

        private int? _empID;
        public int? EmpID { get => _empID; set { _empID = value; OnPropertyChanged(); OnPropertyChanged(nameof(EmployeeCode)); } }

        private string? _gender;
        public string? Gender { get => _gender; set { _gender = value; OnPropertyChanged(); } }

        private string? _designation;
        public string? Designation { get => _designation; set { _designation = value; OnPropertyChanged(); } }

        private string? _section;
        public string? Section { get => _section; set { _section = value; OnPropertyChanged(); } }

        private string? _department;
        public string? Department { get => _department; set { _department = value; OnPropertyChanged(); } }

        private string? _shift;
        public string? Shift { get => _shift; set { _shift = value; OnPropertyChanged(); } }

        private string? _category;
        public string? Category { get => _category; set { _category = value; OnPropertyChanged(); } }

        private string? _status = "Active";
        public string? Status { get => _status; set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsActive)); } }

        private string? _rocketAC;
        public string? RocketAC { get => _rocketAC; set { _rocketAC = value; OnPropertyChanged(); } }

        private double? _grossWages;
        public double? GrossWages { get => _grossWages; set { _grossWages = value; OnPropertyChanged(); OnPropertyChanged(nameof(GrossWagesDisplay)); } }

        private string? _religion;
        public string? Religion { get => _religion; set { _religion = value; OnPropertyChanged(); } }

        private string? _doj;
        public string? DOJ { get => _doj; set { _doj = value; OnPropertyChanged(); } }

        private string? _fatherName;
        public string? FatherName { get => _fatherName; set { _fatherName = value; OnPropertyChanged(); } }

        private string? _nid;
        public string? NID { get => _nid; set { _nid = value; OnPropertyChanged(); } }

        private string? _permAddress;
        public string? PermAddress { get => _permAddress; set { _permAddress = value; OnPropertyChanged(); } }

        private string? _presAddress;
        public string? PresAddress { get => _presAddress; set { _presAddress = value; OnPropertyChanged(); } }

        // Helper aliases & displays
        public int EmployeeId => SL;
        public string EmployeeCode => EmpID?.ToString() ?? "";
        public string FullName => Name;
        public bool IsActive => string.Equals(Status, "Active", StringComparison.OrdinalIgnoreCase);
        public string GrossWagesDisplay => GrossWages.HasValue ? GrossWages.Value.ToString("N2") : "-";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
