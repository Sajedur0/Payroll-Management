using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PayrollManagement.Reports;

public class ReportExportSettingsViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> PaperSizes { get; } =
        new ObservableCollection<string>(ReportPaperSizes.Names);

    public ObservableCollection<string> Orientations { get; } =
        new ObservableCollection<string> { "Portrait", "Landscape" };

    private string _paperSize = "A4";
    public string PaperSize
    {
        get => _paperSize;
        set { _paperSize = value; OnPropertyChanged(); }
    }

    private string _orientation;
    public string Orientation
    {
        get => _orientation;
        set { _orientation = value; OnPropertyChanged(); }
    }

    // Bound to Window.Title ("{Export|Print} Settings") and the accept button ("Export"|"Print").
    // Mirrors Python: dialog.setWindowTitle(f"{action_label} Settings") + action_button text.
    private string _dialogTitle = "Export Settings";
    public string DialogTitle
    {
        get => _dialogTitle;
        set { _dialogTitle = value; OnPropertyChanged(); }
    }

    private string _actionLabel = "Export";
    public string ActionLabel
    {
        get => _actionLabel;
        set { _actionLabel = value; OnPropertyChanged(); }
    }

    private bool _repeatCompanyHeader = true;
    public bool RepeatCompanyHeader
    {
        get => _repeatCompanyHeader;
        set { _repeatCompanyHeader = value; OnPropertyChanged(); }
    }

    private bool _separateReport;
    public bool SeparateReport
    {
        get => _separateReport;
        set { _separateReport = value; OnPropertyChanged(); }
    }

    public ReportExportSettingsViewModel(string defaultOrientation, bool splitByEmployeeDefault)
    {
        _orientation = defaultOrientation == "Landscape" || defaultOrientation == "Portrait"
            ? defaultOrientation
            : "Portrait";
        _separateReport = splitByEmployeeDefault;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
