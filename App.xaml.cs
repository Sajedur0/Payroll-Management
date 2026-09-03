using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace PayrollManagement
{
    public partial class App : Application
    {
        private void ComboBox_PreviewMouseDown_OpenDropDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ComboBox cb && !cb.IsDropDownOpen)
            {
                cb.IsDropDownOpen = true;
                // For non-editable, prevent default toggle that would close it immediately
                if (!cb.IsEditable)
                    e.Handled = true;
            }
        }

        private void DatePicker_PreviewMouseDown_OpenPicker(object sender, MouseButtonEventArgs e)
        {
            // Do not open on preview down; handle on mouse up to avoid immediate close
        }

        private void DatePicker_PreviewMouseUp_OpenPicker(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DatePicker dp || dp.IsDropDownOpen) return;

            // If click originated from the inner drop-down Button (calendar icon) or
            // from the Calendar popup itself, let the native DatePicker logic handle it.
            // Otherwise our global "click anywhere opens" would intercept and mark
            // the event as handled, which prevents CalendarDayButton selection from committing.
            if (e.OriginalSource is DependencyObject src)
            {
                DependencyObject? cur = src;
                while (cur != null && cur != dp)
                {
                    if (cur is Button || cur is Calendar || cur is CalendarItem || cur is CalendarDayButton)
                        return;
                    // Walk up visual tree; if VisualTreeHelper fails (e.g., inside Button template),
                    // try to check templated parent
                    DependencyObject? parent = null;
                    try { parent = VisualTreeHelper.GetParent(cur); } catch { parent = null; }
                    if (parent == null && cur is FrameworkElement fe)
                        parent = fe.TemplatedParent as DependencyObject;
                    cur = parent;
                }
            }

            dp.IsDropDownOpen = true;
            // IMPORTANT: Do NOT set e.Handled = true here.
            // Marking PreviewMouseUp as handled breaks the Calendar's MouseUp that
            // commits SelectedDate when picking a date after opening via the icon.
        }
    }
}
