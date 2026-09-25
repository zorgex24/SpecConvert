using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SpecConvert.App.ViewModels;
using SpecConvert.App.Views;
using SpecConvert.App.Services;
using Xunit;

namespace SpecConvert.App.Tests;
public sealed class WindowTests
{
    [Fact] public void WindowLoadsAndGridCommitsManualEdit()
    {
        Exception? failure=null;
        var thread=new Thread(()=>
        {
            try
            {
                var window=new MainWindow();
                var vm=Assert.IsType<MainViewModel>(window.DataContext);
                vm.Rows.Add(new(){ Name="Исходное имя",Quantity=1d });
                var grid=Assert.IsType<DataGrid>(window.FindName("PreviewGrid"));
                Assert.Equal(9,grid.Columns.Count); Assert.False(grid.IsReadOnly);
                var binding=Assert.IsType<Binding>(((DataGridTextColumn)grid.Columns[1]).Binding);
                var editor=new TextBox { DataContext=vm.Rows[0] };
                editor.SetBinding(TextBox.TextProperty,binding);
                editor.Text="Ручная правка"; editor.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Assert.Equal("Ручная правка",vm.Rows[0].Name);
                window.Close();
            }
            catch(Exception ex) { failure=ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)),"WPF smoke test timed out");
        Assert.Null(failure);
    }
    [Fact] public void ChangingInputInvalidatesOldPreview()
    {
        var vm=new MainViewModel(new NoDialogs()); vm.Rows.Add(new(){Name="Old"}); vm.InputPath="new.xlsx";
        Assert.Empty(vm.Rows); Assert.False(vm.ExportCommand.CanExecute(null));
    }
    private sealed class NoDialogs : IDesktopServices
    {
        public string? SelectInput()=>null;
        public string? SelectOutput(string suggested)=>null;
        public void OpenFile(string path) { }
    }
}
