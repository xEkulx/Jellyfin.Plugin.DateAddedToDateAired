using System;
using System.IO;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.DateAddedToDateAired;

/// <summary>Writes a concise, plugin-only activity log beside Jellyfin's server logs.</summary>
public sealed class PluginActivityLog
{
    private readonly string _logDirectory;
    private readonly object _writeLock = new();

    public PluginActivityLog(IApplicationPaths applicationPaths) => _logDirectory = applicationPaths.LogDirectoryPath;

    public void Information(string message) => Write("INF", message);

    public void Warning(string message) => Write("WRN", message);

    private void Write(string level, string message)
    {
        try
        {
            lock (_writeLock)
            {
                Directory.CreateDirectory(_logDirectory);
                var path = Path.Combine(_logDirectory, $"DateAddedToDateAired_{DateTime.UtcNow:yyyyMMdd}.log");
                File.AppendAllText(path, $"[{DateTimeOffset.Now:O}] [{level}] {message}{Environment.NewLine}");
            }
        }
        catch (IOException)
        {
            // Never let optional diagnostic logging interrupt Jellyfin library processing.
        }
        catch (UnauthorizedAccessException)
        {
            // Jellyfin's normal ILogger output remains available when the log directory is read-only.
        }
    }
}
