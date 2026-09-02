using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
            if (sender is DatePicker dp && !dp.IsDropDownOpen)
            {
                dp.IsDropDownOpen = true;
                e.Handled = true;
            }
        }
    }
}
