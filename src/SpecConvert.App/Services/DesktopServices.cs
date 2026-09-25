using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace SpecConvert.App.Services;

public interface IDesktopServices
{
    string? SelectInput();
    string? SelectOutput(string suggested);
    void OpenFile(string path);
}
public sealed class DesktopServices : IDesktopServices
{
    public string? SelectInput()
    {
        var dialog = new OpenFileDialog { Filter = "Книги Excel (*.xlsx)|*.xlsx", CheckFileExists = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
    public string? SelectOutput(string suggested)
    {
        var dialog = new SaveFileDialog { Filter = "Книги Excel (*.xlsx)|*.xlsx", DefaultExt = ".xlsx", AddExtension = true, FileName = Path.GetFileName(suggested), OverwritePrompt = true };
        if (Directory.Exists(Path.GetDirectoryName(suggested))) dialog.InitialDirectory = Path.GetDirectoryName(suggested);
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
    public void OpenFile(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
}
