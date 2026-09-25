using System.Windows;
using System.Windows.Controls;
using SpecConvert.App.Services;
using SpecConvert.App.ViewModels;
namespace SpecConvert.App.Views;
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(new DesktopServices());
        // Commit the current editor before focus moves to an export button.
        PreviewGrid.LostKeyboardFocus += (_, _) => { PreviewGrid.CommitEdit(DataGridEditingUnit.Cell,true); PreviewGrid.CommitEdit(DataGridEditingUnit.Row,true); };
    }
}
