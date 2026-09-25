using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SpecConvert.App.Services;
using SpecConvert.Core.Models;
using SpecConvert.Core.Services;

namespace SpecConvert.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IDesktopServices desktop;
    private string inputPath = "", outputPath = "", log = "Выберите исходный Excel-файл.", summary = "", status = "Готово", savedPath = "";
    private bool busy;
    public MainViewModel(IDesktopServices desktop)
    {
        this.desktop = desktop;
        BrowseInputCommand = new RelayCommand(() => { var p = desktop.SelectInput(); if (p is not null) { InputPath = p; OutputPath = Path.Combine(Path.GetDirectoryName(p)!, Path.GetFileNameWithoutExtension(p) + " — результат.xlsx"); } }, () => !IsBusy);
        BrowseOutputCommand = new RelayCommand(() => { var p = desktop.SelectOutput(OutputPath); if (p is not null) OutputPath = p; }, () => !IsBusy);
        ConvertCommand = new RelayCommand(async () => await ConvertAsync(), () => !IsBusy && File.Exists(InputPath));
        ExportCommand = new RelayCommand(async () => await ExportAsync(), () => !IsBusy && Rows.Count > 0);
        OpenCommand = new RelayCommand(() => { try { desktop.OpenFile(savedPath); } catch (Exception ex) { ReportError(ex); } }, () => !IsBusy && File.Exists(savedPath));
    }
    public ObservableCollection<SpecificationItem> Rows { get; } = [];
    public string InputPath { get => inputPath; set { if (inputPath == value) return; Set(ref inputPath,value); savedPath = ""; Rows.Clear(); Summary = ""; } }
    public string OutputPath { get => outputPath; set => Set(ref outputPath,value); }
    public string Log { get => log; private set => Set(ref log,value); }
    public string Summary { get => summary; private set => Set(ref summary,value); }
    public string Status { get => status; private set => Set(ref status,value); }
    public bool IsBusy { get => busy; private set { Set(ref busy,value); Changed(nameof(CanEdit)); CommandManager.InvalidateRequerySuggested(); } }
    public bool CanEdit => !IsBusy;
    public ICommand BrowseInputCommand { get; }
    public ICommand BrowseOutputCommand { get; }
    public ICommand ConvertCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand OpenCommand { get; }
    public string LogPath { get; private set; } = "";

    public async Task ConvertAsync()
    {
        IsBusy = true; Rows.Clear(); savedPath = ""; Summary = ""; LogPath = "";
        Log = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} — {InputPath}";
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpecConvert", "Logs");
            Directory.CreateDirectory(dir); LogPath = Path.Combine(dir,$"conversion-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.log");
            var progress = new Progress<string>(s => Status = s);
            var result = await Task.Run(() => new SpecificationParser().Parse(new ExcelReader().Read(InputPath),progress));
            foreach (var row in result.Rows) Rows.Add(row);
            Summary = result.Summary;
            Log += Environment.NewLine + string.Join(Environment.NewLine,result.Log);
            Status = result.ItemCount == 0 ? "Позиции не найдены. Проверьте журнал." : "Результат готов к проверке. Исправьте значения и нажмите «Сохранить XLSX».";
        }
        catch (Exception ex) { ReportError(ex); }
        finally { PersistLog(); IsBusy = false; }
    }

    public async Task ExportAsync()
    {
        string? target = desktop.SelectOutput(OutputPath);
        if (target is null) return;
        IsBusy = true;
        try
        {
            if (string.Equals(Path.GetFullPath(target),Path.GetFullPath(InputPath),StringComparison.OrdinalIgnoreCase))
                throw new IOException("Выберите другое имя: исходный файл нельзя перезаписывать.");
            OutputPath = target;
            var snapshot = Rows.ToArray();
            await Task.Run(() => new ExcelExporter().Export(target,snapshot));
            savedPath = target; Status = "Файл сохранен."; Log += $"\n{DateTime.Now:HH:mm:ss} Сохранено: {target}";
        }
        catch (Exception ex) { ReportError(ex); }
        finally { PersistLog(); IsBusy = false; }
    }
    private void ReportError(Exception ex)
    {
        Status = ex switch
        {
            UnauthorizedAccessException => "Нет доступа к файлу или каталогу. Выберите доступный путь.",
            IOException => "Не удалось прочитать или сохранить файл. Проверьте путь и закройте файл в Excel. " + ex.Message,
            _ => "Не удалось обработать книгу. Проверьте формат и целостность файла. " + ex.Message
        };
        Log += $"\n{DateTime.Now:HH:mm:ss} ОШИБКА: {ex}";
    }
    private void PersistLog()
    {
        try { if (LogPath.Length > 0) File.WriteAllText(LogPath,Log); }
        catch (Exception ex) { Log += "\nНе удалось записать журнал: " + ex.Message; }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this,new(name));
    private void Set<T>(ref T field,T value,[CallerMemberName] string? name = null) { field=value; Changed(name); }
}
