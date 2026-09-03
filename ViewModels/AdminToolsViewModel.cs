using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using PayrollManagement.AdminTools;
using PayrollManagement.Data;
using PayrollManagement.Shifts;

namespace PayrollManagement.ViewModels
{
    public class AdminToolsViewModel : INotifyPropertyChanged
    {
        // Company Details
        private string _companyName = "";
        public string CompanyName { get => _companyName; set { _companyName = value; OnPropertyChanged(); } }
        private string _companyAddress = "";
        public string CompanyAddress { get => _companyAddress; set { _companyAddress = value; OnPropertyChanged(); } }
        private string _companyNumber = "";
        public string CompanyNumber { get => _companyNumber; set { _companyNumber = value; OnPropertyChanged(); } }

        // WeekEnd
        public ObservableCollection<string> WeekEndList { get; } = new();
        private string _selectedWeekDay = "Saturday";
        public string SelectedWeekDay { get => _selectedWeekDay; set { _selectedWeekDay = value; OnPropertyChanged(); } }

        // Holiday
        public ObservableCollection<CompanyHolidayRow> Holidays { get; } = new();
        private string _festivalName = "";
        public string FestivalName { get => _festivalName; set { _festivalName = value; OnPropertyChanged(); } }
        private DateTime? _holidayFrom = DateTime.Today;
        public DateTime? HolidayFrom { get => _holidayFrom; set { _holidayFrom = value; OnPropertyChanged(); } }
        private DateTime? _holidayTo = DateTime.Today;
        public DateTime? HolidayTo { get => _holidayTo; set { _holidayTo = value; OnPropertyChanged(); } }
        private string _holidayNotes = "";
        public string HolidayNotes { get => _holidayNotes; set { _holidayNotes = value; OnPropertyChanged(); } }
        private int? _editingHolidayId;
        public int? EditingHolidayId { get => _editingHolidayId; set { _editingHolidayId = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEditingHoliday)); } }
        public bool IsEditingHoliday => EditingHolidayId.HasValue;
        private CompanyHolidayRow? _selectedHoliday;
        public CompanyHolidayRow? SelectedHoliday { get => _selectedHoliday; set { _selectedHoliday = value; OnPropertyChanged(); if (value != null) LoadHolidayForEdit(value); } }

        // Exit Info
        public ObservableCollection<ExitInfoRow> ExitRows { get; } = new();
        private string _exitType = "Resign";
        public string ExitType { get => _exitType; set { _exitType = value; OnPropertyChanged(); _ = LoadExitAsync(); } }
        private string _exitEmpId = "";
        public string ExitEmpId { get => _exitEmpId; set { _exitEmpId = value; OnPropertyChanged(); LookupExitName(); } }
        private string _exitEmpName = "";
        public string ExitEmpName { get => _exitEmpName; set { _exitEmpName = value; OnPropertyChanged(); } }
        private DateTime? _exitDate = DateTime.Today;
        public DateTime? ExitDate { get => _exitDate; set { _exitDate = value; OnPropertyChanged(); } }
        private string _exitReason = "";
        public string ExitReason { get => _exitReason; set { _exitReason = value; OnPropertyChanged(); } }
        private string _exitReasonType = "";
        public string ExitReasonType { get => _exitReasonType; set { _exitReasonType = value; OnPropertyChanged(); if (!string.IsNullOrEmpty(value)) ExitReason = value; } }

        // Shift Change
        public ObservableCollection<string> UnscheduledShifts { get; } = new();
        public ObservableCollection<ShiftScheduleRow> ShiftSchedules { get; } = new();
        private string _shiftName = "";
        public string ShiftName { get => _shiftName; set { _shiftName = value; OnPropertyChanged(); } }
        private string _shiftInTime = "08:00:00";
        public string ShiftInTime { get => _shiftInTime; set { _shiftInTime = value; OnPropertyChanged(); } }
        private string _shiftOutTime = "20:00:00";
        public string ShiftOutTime { get => _shiftOutTime; set { _shiftOutTime = value; OnPropertyChanged(); } }
        private string _shiftNotes = "";
        public string ShiftNotes { get => _shiftNotes; set { _shiftNotes = value; OnPropertyChanged(); } }
        private ShiftScheduleRow? _selectedShiftSchedule;
        public ShiftScheduleRow? SelectedShiftSchedule { get => _selectedShiftSchedule; set { _selectedShiftSchedule = value; OnPropertyChanged(); if (value != null) LoadShiftForEdit(value); } }

        // Shift Cycle
        public ObservableCollection<string> AllShifts { get; } = new();
        public ObservableCollection<ShiftCycleItem> ShiftCycleItems { get; } = new();
        private DateTime? _cycleStartDate = DateTime.Today;
        public DateTime? CycleStartDate { get => _cycleStartDate; set { _cycleStartDate = value; OnPropertyChanged(); } }
        public ObservableCollection<ShiftRotationRow> RotationRows { get; } = new();

        private string _statusMessage = "";
        public string StatusMessage { get => _statusMessage; set { _statusMessage = value; OnPropertyChanged(); } }

        public RelayCommand SaveCompanyCommand { get; }
        public RelayCommand AddWeekEndCommand { get; }
        public RelayCommand DeleteWeekEndCommand { get; }
        public RelayCommand SaveHolidayCommand { get; }
        public RelayCommand DeleteHolidayCommand { get; }
        public RelayCommand CancelHolidayEditCommand { get; }
        public RelayCommand SaveExitCommand { get; }
        public RelayCommand DeleteExitCommand { get; }
        public RelayCommand SaveShiftCommand { get; }
        public RelayCommand DeleteShiftCommand { get; }
        public RelayCommand GenerateCycleCommand { get; }
        public RelayCommand DeleteAllCycleCommand { get; }
        public RelayCommand RefreshAllCommand { get; }

        public AdminToolsViewModel()
        {
            SaveCompanyCommand = new RelayCommand(_ => SaveCompany());
            AddWeekEndCommand = new RelayCommand(_ => AddWeekEnd());
            DeleteWeekEndCommand = new RelayCommand(p => DeleteWeekEnd(p as string));
            SaveHolidayCommand = new RelayCommand(_ => SaveHoliday());
            DeleteHolidayCommand = new RelayCommand(p => DeleteHoliday(p));
            CancelHolidayEditCommand = new RelayCommand(_ => CancelHolidayEdit());
            SaveExitCommand = new RelayCommand(_ => SaveExit());
            DeleteExitCommand = new RelayCommand(p => DeleteExit(p));
            SaveShiftCommand = new RelayCommand(_ => SaveShift());
            DeleteShiftCommand = new RelayCommand(p => DeleteShift(p));
            GenerateCycleCommand = new RelayCommand(_ => GenerateCycle());
            DeleteAllCycleCommand = new RelayCommand(_ => DeleteAllCycle());
            RefreshAllCommand = new RelayCommand(_ => _ = LoadAllAsync());
            _ = LoadAllAsync();
        }

        private async Task LoadAllAsync()
        {
            await Task.Run(() =>
            {
                try { LoadCompany(); } catch { }
                try { LoadWeekEnd(); } catch { }
                try { LoadHolidays(); } catch { }
                try { LoadExitSync(); } catch { }
                try { LoadShiftSchedules(); } catch { }
                try { LoadShiftCycle(); } catch { }
            });
        }

        private void LoadCompany()
        {
            var c = CompanyDetailsService.Load();
            Application.Current?.Dispatcher.Invoke(() =>
            {
                CompanyName = c.Name; CompanyAddress = c.Address; CompanyNumber = c.Number;
            });
        }

        private void SaveCompany()
        {
            try
            {
                CompanyDetailsService.Save(new CompanyDetailsInput { Name = CompanyName, Address = CompanyAddress, Number = CompanyNumber });
                StatusMessage = "Company details saved.";
            }
            catch (Exception ex) { StatusMessage = ex.Message; MessageBox.Show(ex.Message, "Company Details", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void LoadWeekEnd()
        {
            var list = WeekEndService.List();
            Application.Current?.Dispatcher.Invoke(() => { WeekEndList.Clear(); foreach (var d in list) WeekEndList.Add(d); });
        }

        private void AddWeekEnd()
        {
            try { WeekEndService.Add(SelectedWeekDay); LoadWeekEnd(); StatusMessage = $"{SelectedWeekDay} added."; }
            catch (Exception ex) { MessageBox.Show(ex.Message, "WeekEnd", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void DeleteWeekEnd(string? day)
        {
            if (string.IsNullOrEmpty(day)) return;
            try { WeekEndService.Delete(day); LoadWeekEnd(); StatusMessage = $"{day} removed."; }
            catch (Exception ex) { MessageBox.Show(ex.Message, "WeekEnd", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void LoadHolidays()
        {
            var list = CompanyHolidayService.List();
            Application.Current?.Dispatcher.Invoke(() => { Holidays.Clear(); foreach (var h in list) Holidays.Add(h); });
        }

        private void LoadHolidayForEdit(CompanyHolidayRow row)
        {
            EditingHolidayId = row.HolidayId;
            FestivalName = row.FestivalName;
            if (DateTime.TryParse(row.FromDate, out var fd)) HolidayFrom = fd;
            if (DateTime.TryParse(row.ToDate, out var td)) HolidayTo = td;
            HolidayNotes = row.Notes;
        }

        private void CancelHolidayEdit()
        {
            EditingHolidayId = null; FestivalName = ""; HolidayNotes = ""; SelectedHoliday = null;
            HolidayFrom = DateTime.Today; HolidayTo = DateTime.Today;
        }

        private void SaveHoliday()
        {
            var input = new CompanyHolidayInput
            {
                HolidayId = EditingHolidayId,
                FestivalName = FestivalName,
                FromDate = HolidayFrom?.ToString("yyyy-MM-dd") ?? "",
                ToDate = HolidayTo?.ToString("yyyy-MM-dd") ?? "",
                Notes = HolidayNotes
            };
            var (ok, err) = CompanyHolidayService.Save(input);
            if (!ok) { MessageBox.Show(err, "Holiday", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            LoadHolidays(); CancelHolidayEdit(); StatusMessage = "Holiday saved.";
        }

        private void DeleteHoliday(object? param)
        {
            int id = 0;
            if (param is int i) id = i;
            else if (param is CompanyHolidayRow r) id = r.HolidayId;
            else if (SelectedHoliday != null) id = SelectedHoliday.HolidayId;
            else return;
            if (MessageBox.Show("Delete this holiday?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            CompanyHolidayService.Delete(id); LoadHolidays(); StatusMessage = "Holiday deleted.";
        }

        private void LoadExitSync()
        {
            var list = ExitInfoService.List(ExitType);
            Application.Current?.Dispatcher.Invoke(() => { ExitRows.Clear(); foreach (var r in list) ExitRows.Add(r); });
        }

        private async Task LoadExitAsync()
        {
            await Task.Run(() => LoadExitSync());
        }

        private void LookupExitName()
        {
            if (int.TryParse(ExitEmpId, out int id))
            {
                var name = ExitInfoService.LookupEmployeeName(id);
                ExitEmpName = name ?? "Employee not found";
            }
            else ExitEmpName = "";
        }

        private void SaveExit()
        {
            if (!int.TryParse(ExitEmpId, out int empId)) { MessageBox.Show("Enter valid EmpID", "Exit Info", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            var input = new ExitInfoInput { EmpId = empId, ExitType = ExitType, ExitDate = ExitDate?.ToString("yyyy-MM-dd") ?? "", Reason = ExitReason };
            var (ok, err) = ExitInfoService.Save(input);
            if (!ok) { MessageBox.Show(err, "Exit Info", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            LoadExitSync(); StatusMessage = "Exit info saved.";
        }

        private void DeleteExit(object? param)
        {
            int id = 0;
            if (param is int i) id = i;
            else if (param is ExitInfoRow r) id = r.ExitId;
            else return;
            if (MessageBox.Show("Delete this exit record?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            ExitInfoService.Delete(id); LoadExitSync(); StatusMessage = "Exit record deleted.";
        }

        private void LoadShiftSchedules()
        {
            try
            {
                var unscheduled = ShiftChangeService.UnscheduledShifts();
                var list = ShiftChangeService.List();
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    UnscheduledShifts.Clear(); foreach (var s in unscheduled) UnscheduledShifts.Add(s);
                    ShiftSchedules.Clear(); foreach (var s in list) ShiftSchedules.Add(s);
                });
            }
            catch { }
        }

        private void LoadShiftForEdit(ShiftScheduleRow row)
        {
            ShiftName = row.ShiftName; ShiftInTime = row.InTime; ShiftOutTime = row.OutTime; ShiftNotes = row.Notes;
        }

        private void SaveShift()
        {
            var (ok, err) = ShiftChangeService.Save(ShiftName, ShiftInTime, ShiftOutTime, ShiftNotes);
            if (!ok) { MessageBox.Show(err, "Shift", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            LoadShiftSchedules(); StatusMessage = "Shift saved.";
        }

        private void DeleteShift(object? param)
        {
            int id = 0;
            if (param is int i) id = i;
            else if (param is ShiftScheduleRow r) id = r.ScheduleId;
            else if (SelectedShiftSchedule != null) id = SelectedShiftSchedule.ScheduleId;
            else return;
            if (MessageBox.Show("Delete this shift schedule?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            ShiftChangeService.Delete(id); LoadShiftSchedules(); StatusMessage = "Shift deleted.";
        }

        private void LoadShiftCycle()
        {
            try
            {
                var all = ShiftCycleService.AllShiftsInEmployeeInfo();
                var configs = ShiftCycleService.ExistingConfigs();
                var rotations = ShiftCycleService.RecentRotationRows(200);
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    AllShifts.Clear(); foreach (var s in all) AllShifts.Add(s);
                    ShiftCycleItems.Clear();
                    foreach (var s in all)
                    {
                        configs.TryGetValue(s, out var cfg);
                        ShiftCycleItems.Add(new ShiftCycleItem
                        {
                            ShiftName = s,
                            IsSelected = cfg != null,
                            DutyType = cfg?.DutyType ?? "Day"
                        });
                    }
                    RotationRows.Clear(); foreach (var r in rotations) RotationRows.Add(r);
                });
            }
            catch { }
        }

        private void GenerateCycle()
        {
            var selected = ShiftCycleItems.Where(x => x.IsSelected).Select(x => new ShiftCycleSelection(x.ShiftName, x.DutyType)).ToList();
            if (selected.Count == 0) { MessageBox.Show("Please select at least one shift for the cycle.", "Shift Cycle", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            var start = CycleStartDate?.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd");
            var (ok, err, count) = ShiftCycleService.GenerateCycle(start, selected);
            if (!ok) { MessageBox.Show(err, "Shift Cycle", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            LoadShiftCycle(); StatusMessage = $"Cycle generated for {count} shift(s).";
            MessageBox.Show($"Shift cycle generated for {count} shift(s) (520 weeks).", "Shift Cycle", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DeleteAllCycle()
        {
            if (MessageBox.Show("Delete ALL rotation schedules and cycle configs?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            ShiftCycleService.DeleteAll(); LoadShiftCycle(); StatusMessage = "All cycles deleted.";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class ShiftCycleItem : INotifyPropertyChanged
    {
        private string _shiftName = "";
        public string ShiftName { get => _shiftName; set { _shiftName = value; OnPropertyChanged(); } }
        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
        private string _dutyType = "Day";
        public string DutyType { get => _dutyType; set { _dutyType = value; OnPropertyChanged(); } }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
