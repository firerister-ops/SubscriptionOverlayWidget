using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SubscriptionOverlayWidget.Services;

public sealed class DebugLogService
{
    private readonly string _logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SubscriptionOverlayWidget", "Logs");

    public string LogPath => Path.Combine(_logDirectory, "debug.log");

    public async Task WriteAsync(string message)
    {
        Directory.CreateDirectory(_logDirectory);
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
        await File.AppendAllTextAsync(LogPath, line, Encoding.UTF8);
    }

    public Task WriteExceptionAsync(string context, Exception exception)
    {
        return WriteAsync($"{context}{Environment.NewLine}{exception}");
    }
}
