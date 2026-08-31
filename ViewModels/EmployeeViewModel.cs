using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.Win32;
using PayrollManagement.Data;
using PayrollManagement.Models;
using PayrollManagement.Views;

namespace PayrollManagement.ViewModels
{
    public class EmployeeViewModel : INotifyPropertyChanged
    {
        private readonly AttendanceRepository _repo = new();

        // ===== Employee List (Directly from Database) =====
        private ObservableCollection<Employee> _employees = new();
        public ObservableCollection<Employee> Employees
        {
            get => _employees;
            set { _employees = value; OnPropertyChanged(); }
        }

        private Employee? _selectedEmployee;
        public Employee? SelectedEmployee
        {
            get => _selectedEmployee;
            set
            {
                _selectedEmployee = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedEmployee));
            }
        }

        public bool HasSelectedEmployee => SelectedEmployee != null;

        private string? _searchText;
        public string? SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); }
        }

        // ===== Form Dialog Properties =====
        private int _sl;
        public int SL { get => _sl; set { _sl = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEditMode)); } }

        private string _name = "";
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

        private string _empIDText = "";
        public string EmpIDText
        {
            get => _empIDText;
            set { _empIDText = value; OnPropertyChanged(); }
        }

        private string? _gender = "Male";
        public string? Gender { get => _gender; set { _gender = value; OnPropertyChanged(); } }

        private string? _designation;
        public string? Designation { get => _designation; set { _designation = value; OnPropertyChanged(); } }

        private string? _section;
        public string? Section { get => _section; set { _section = value; OnPropertyChanged(); } }

        private string? _department;
        public string? Department { get => _department; set { _department = value; OnPropertyChanged(); } }

        private string? _shift = "G";
        public string? Shift { get => _shift; set { _shift = value; OnPropertyChanged(); } }

        private string? _category = "Worker";
        public string? Category { get => _category; set { _category = value; OnPropertyChanged(); } }

        private string? _status = "Active";
        public string? Status { get => _status; set { _status = value; OnPropertyChanged(); } }

        private string? _rocketAC;
        public string? RocketAC { get => _rocketAC; set { _rocketAC = value; OnPropertyChanged(); } }

        private string _grossWagesText = "";
        public string GrossWagesText
        {
            get => _grossWagesText;
            set { _grossWagesText = value; OnPropertyChanged(); }
        }

        private string? _religion = "Islam";
        public string? Religion { get => _religion; set { _religion = value; OnPropertyChanged(); } }

        private DateTime? _dojDate = DateTime.Today;
        public DateTime? DOJDate
        {
            get => _dojDate;
            set { _dojDate = value; OnPropertyChanged(); }
        }

        private string? _fatherName;
        public string? FatherName { get => _fatherName; set { _fatherName = value; OnPropertyChanged(); } }

        private string? _nid;
        public string? NID { get => _nid; set { _nid = value; OnPropertyChanged(); } }

        private string? _permAddress;
        public string? PermAddress { get => _permAddress; set { _permAddress = value; OnPropertyChanged(); } }

        private string? _presAddress;
        public string? PresAddress { get => _presAddress; set { _presAddress = value; OnPropertyChanged(); } }

        public bool IsEditMode => SL > 0;

        // Modal Dialog Display Properties
        public string FormTitle => IsEditMode ? $"✏️ Edit Employee (SL: {SL}, EmpID: {EmpIDText})" : "➕ Add New Employee";
        public string FormModeSubtitle => IsEditMode ? "Edit / Update Mode" : "New Entry Mode";
        public string SubmitButtonText => IsEditMode ? "💾 Update Employee" : "💾 Save Employee";

        private string? _formErrorMessage;
        public string? FormErrorMessage
        {
            get => _formErrorMessage;
            set { _formErrorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasFormError)); }
        }
        public bool HasFormError => !string.IsNullOrEmpty(FormErrorMessage);

        // ===== Dropdown Options =====
        public List<string> GenderList { get; } = new() { "Male", "Female" };
        public List<string> ShiftList { get; } = new() { "G", "O", "A", "B", "C", "H", "S" };
        public List<string> CategoryList { get; } = new() { "Worker", "Staff", "Officer" };
        public List<string> StatusList { get; } = new() { "Active", "resigned", "Inactive" };
        public List<string> ReligionList { get; } = new() { "Islam", "Hindu", "Christian", "Buddhist" };
        public List<string> DepartmentList { get; } = new()
        {
            "Design", "Dyeing", "F & A", "Finishing", "HR & Admin",
            "Maintenance", "Marketing", "Pretreatment", "Printing",
            "R & D", "Regent", "Stentering", "Store", "Weaving",
            "Accounts", "IT", "Security"
        };

        // ===== Main View Status / Feedback =====
        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }

        private string? _errorMessage;
        public string? ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
        }
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        private string? _successMessage;
        public string? SuccessMessage
        {
            get => _successMessage;
            set { _successMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSuccess)); }
        }
        public bool HasSuccess => !string.IsNullOrEmpty(SuccessMessage);

        // ===== Commands =====
        public RelayCommand OpenAddEmployeeCommand { get; }
        public RelayCommand OpenEditEmployeeCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand ImportExcelCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand SearchCommand { get; }

        public EmployeeViewModel()
        {
            OpenAddEmployeeCommand = new RelayCommand(_ => OpenAddEmployeeDialog());
            OpenEditEmployeeCommand = new RelayCommand(_ => OpenEditEmployeeDialog(), _ => HasSelectedEmployee);
            DeleteCommand = new RelayCommand(async _ => await DeleteAsync(), _ => HasSelectedEmployee);
            ImportExcelCommand = new RelayCommand(async _ => await ImportExcelAsync());
            RefreshCommand = new RelayCommand(async _ => await LoadAsync());
            SearchCommand = new RelayCommand(async _ => await LoadAsync());

            _ = LoadAsync();
        }

        public async Task LoadAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            ErrorMessage = null;
            try
            {
                var list = await _repo.GetEmployeesAsync(string.IsNullOrWhiteSpace(SearchText) ? null : SearchText);
                Employees = new ObservableCollection<Employee>(list);
                SuccessMessage = $"{list.Count} employees loaded";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
                Employees = new ObservableCollection<Employee>();
            }
            finally { IsLoading = false; }
        }

        // ===== Open Dialogs =====
        private void OpenAddEmployeeDialog()
        {
            ClearForm();
            OnPropertyChanged(nameof(FormTitle));
            OnPropertyChanged(nameof(FormModeSubtitle));
            OnPropertyChanged(nameof(SubmitButtonText));

            var win = new EmployeeFormWindow(this)
            {
                Owner = Application.Current.MainWindow
            };
            bool? result = win.ShowDialog();
            if (result == true)
            {
                _ = LoadAsync();
            }
        }

        private void OpenEditEmployeeDialog()
        {
            if (SelectedEmployee == null)
            {
                ErrorMessage = "Please select an employee from the list to edit.";
                return;
            }

            LoadFormFromSelected();
            OnPropertyChanged(nameof(FormTitle));
            OnPropertyChanged(nameof(FormModeSubtitle));
            OnPropertyChanged(nameof(SubmitButtonText));

            var win = new EmployeeFormWindow(this)
            {
                Owner = Application.Current.MainWindow
            };
            bool? result = win.ShowDialog();
            if (result == true)
            {
                _ = LoadAsync();
            }
        }

        private void LoadFormFromSelected()
        {
            if (SelectedEmployee == null) return;
            SL = SelectedEmployee.SL;
            Name = SelectedEmployee.Name;
            EmpIDText = SelectedEmployee.EmpID?.ToString() ?? "";
            Gender = string.IsNullOrWhiteSpace(SelectedEmployee.Gender) ? "Male" : SelectedEmployee.Gender;
            Designation = SelectedEmployee.Designation;
            Section = SelectedEmployee.Section;
            Department = SelectedEmployee.Department;
            Shift = string.IsNullOrWhiteSpace(SelectedEmployee.Shift) ? "G" : SelectedEmployee.Shift;
            Category = string.IsNullOrWhiteSpace(SelectedEmployee.Category) ? "Worker" : SelectedEmployee.Category;
            Status = string.IsNullOrWhiteSpace(SelectedEmployee.Status) ? "Active" : SelectedEmployee.Status;
            RocketAC = SelectedEmployee.RocketAC;
            GrossWagesText = SelectedEmployee.GrossWages.HasValue ? SelectedEmployee.GrossWages.Value.ToString(CultureInfo.InvariantCulture) : "";
            Religion = string.IsNullOrWhiteSpace(SelectedEmployee.Religion) ? "Islam" : SelectedEmployee.Religion;

            if (DateTime.TryParse(SelectedEmployee.DOJ, out var parsedDate))
                DOJDate = parsedDate;
            else
                DOJDate = null;

            FatherName = SelectedEmployee.FatherName;
            NID = SelectedEmployee.NID;
            PermAddress = SelectedEmployee.PermAddress;
            PresAddress = SelectedEmployee.PresAddress;

            FormErrorMessage = null;
        }

        private void ClearForm()
        {
            SL = 0;
            Name = "";
            EmpIDText = "";
            Gender = "Male";
            Designation = "";
            Section = "";
            Department = "";
            Shift = "G";
            Category = "Worker";
            Status = "Active";
            RocketAC = "";
            GrossWagesText = "";
            Religion = "Islam";
            DOJDate = DateTime.Today;
            FatherName = "";
            NID = "";
            PermAddress = "";
            PresAddress = "";
            FormErrorMessage = null;
        }

        private bool ValidateForm(out int empId, out double? grossWages, out string dojStr, out string error)
        {
            empId = 0;
            grossWages = null;
            dojStr = "";
            error = "";

            if (string.IsNullOrWhiteSpace(Name))
            {
                error = "Name is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(EmpIDText))
            {
                error = "EmpID is required.";
                return false;
            }

            if (!int.TryParse(EmpIDText.Trim(), out empId))
            {
                error = "EmpID must be a valid integer (e.g., 10444).";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(GrossWagesText))
            {
                if (double.TryParse(GrossWagesText.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var gw) ||
                    double.TryParse(GrossWagesText.Trim(), out gw))
                {
                    grossWages = gw;
                }
                else
                {
                    error = "GrossWages must be a valid number (e.g., 27000.0).";
                    return false;
                }
            }

            if (DOJDate.HasValue)
            {
                dojStr = DOJDate.Value.ToString("yyyy-MM-dd");
            }

            return true;
        }

        public async Task<bool> ExecuteSaveFromDialogAsync()
        {
            FormErrorMessage = null;
            if (!ValidateForm(out int empId, out double? grossWages, out string dojStr, out string verr))
            {
                FormErrorMessage = verr;
                return false;
            }

            try
            {
                var emp = new Employee
                {
                    Name = Name.Trim(),
                    EmpID = empId,
                    Gender = Gender?.Trim(),
                    Designation = Designation?.Trim(),
                    Section = Section?.Trim(),
                    Department = Department?.Trim(),
                    Shift = Shift?.Trim(),
                    Category = Category?.Trim(),
                    Status = Status?.Trim() ?? "Active",
                    RocketAC = RocketAC?.Trim(),
                    GrossWages = grossWages,
                    Religion = Religion?.Trim(),
                    DOJ = dojStr,
                    FatherName = FatherName?.Trim(),
                    NID = NID?.Trim(),
                    PermAddress = PermAddress?.Trim(),
                    PresAddress = PresAddress?.Trim()
                };

                int newSL = await _repo.AddEmployeeAsync(emp);
                SuccessMessage = $"✓ New employee added successfully! SL={newSL}, EmpID={empId}, Name={emp.Name}";
                return true;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("UNIQUE") || ex.Message.Contains("duplicate") || ex.Message.Contains("UQ__Employee__AF2DBA78"))
                    FormErrorMessage = $"EmpID '{empId}' already exists. Please use a different EmpID.";
                else
                    FormErrorMessage = $"Save failed: {ex.Message}";
                return false;
            }
        }

        public async Task<bool> ExecuteUpdateFromDialogAsync()
        {
            FormErrorMessage = null;
            if (SL == 0)
            {
                FormErrorMessage = "Valid SL not found for update.";
                return false;
            }

            if (!ValidateForm(out int empId, out double? grossWages, out string dojStr, out string verr))
            {
                FormErrorMessage = verr;
                return false;
            }

            try
            {
                var emp = new Employee
                {
                    SL = SL,
                    Name = Name.Trim(),
                    EmpID = empId,
                    Gender = Gender?.Trim(),
                    Designation = Designation?.Trim(),
                    Section = Section?.Trim(),
                    Department = Department?.Trim(),
                    Shift = Shift?.Trim(),
                    Category = Category?.Trim(),
                    Status = Status?.Trim() ?? "Active",
                    RocketAC = RocketAC?.Trim(),
                    GrossWages = grossWages,
                    Religion = Religion?.Trim(),
                    DOJ = dojStr,
                    FatherName = FatherName?.Trim(),
                    NID = NID?.Trim(),
                    PermAddress = PermAddress?.Trim(),
                    PresAddress = PresAddress?.Trim()
                };

                await _repo.UpdateEmployeeAsync(emp);
                SuccessMessage = $"✓ Employee updated successfully! SL={SL}, EmpID={empId}, Name={emp.Name}";
                return true;
            }
            catch (Exception ex)
            {
                FormErrorMessage = $"Update failed: {ex.Message}";
                return false;
            }
        }

        public async Task DeleteAsync()
        {
            if (SelectedEmployee == null)
            {
                ErrorMessage = "Please select an employee from the list to delete.";
                return;
            }

            var emp = SelectedEmployee;
            var result = MessageBox.Show($"SL: {emp.SL}\nEmpID: {emp.EmpID}\nName: {emp.Name}\nDepartment: {emp.Department}\n\nAre you sure you want to permanently delete this employee?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            IsLoading = true;
            try
            {
                await _repo.DeleteEmployeeAsync(emp.SL);
                SuccessMessage = $"✓ Employee (SL={emp.SL}, EmpID={emp.EmpID}, Name={emp.Name}) has been deleted.";
                SelectedEmployee = null;
                await LoadAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Delete failed: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        public async Task ImportExcelAsync()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|All Files (*.*)|*.*",
                Title = "Select Employee Excel File to Import"
            };

            if (openFileDialog.ShowDialog() != true)
                return;

            IsLoading = true;
            ErrorMessage = null;
            SuccessMessage = null;

            try
            {
                var (inserted, updated) = await _repo.ImportEmployeesFromExcelAsync(openFileDialog.FileName);
                SuccessMessage = $"✓ Successfully loaded {inserted + updated} employees from Excel! (New: {inserted}, Updated: {updated})";
                await LoadAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Excel Import Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}

