using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace Hyleus.Soundboard.Framework;
public static class Log {

    private static readonly string BaseDir = AppContext.BaseDirectory;
    private static readonly string LogDir = Path.Combine(BaseDir, "Logs");
    private static readonly string OldDir = Path.Combine(LogDir, "Old");
    private static readonly string LogFilePath = Path.Combine(LogDir, "log.txt");
    private static readonly object _lock = new();

    static Log() {
        Directory.CreateDirectory(LogDir);
        Directory.CreateDirectory(OldDir);
        // archive old logs
        RotateLogsIfNeeded();
        // clear log file
        File.Create(LogFilePath).Close();
    }

    private static void RotateLogsIfNeeded() {
        if (!File.Exists(LogFilePath))
            return;

        string timestamp = new FileInfo(LogFilePath).LastWriteTime.ToString("yyyy-MM-dd_HH-mm-ss.fff");
        string zipPath = Path.Combine(OldDir, $"log_{timestamp}.zip");
        if (File.Exists(zipPath))
            return; // already rotated

        using ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        zip.CreateEntryFromFile(LogFilePath, "log.txt");

        File.WriteAllText(LogFilePath, string.Empty);

        // keep only last 5 archives
        IEnumerable<FileInfo> oldZips = new DirectoryInfo(OldDir)
            .GetFiles("log_*.zip")
            .OrderByDescending(f => f.LastWriteTime)
            .Skip(5);

        foreach (FileInfo file in oldZips) {
            try {
                file.Delete();
            } catch { /* ignore */ }
        }
    }

    internal static void WriteDate() =>
        Console.Write($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");

    // holy inefficient
    internal static void BaseWrite(string level, ConsoleColor color, params object[] objects) {
        string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{Thread.CurrentThread.Name ?? $"Thread {Environment.CurrentManagedThreadId}"}/{level}] [Hyleus] - {string.Join(' ', objects)}";

        lock (_lock) {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write('[');
            Console.ForegroundColor = ConsoleColor.Magenta;
            WriteDate();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("] [");
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write(Thread.CurrentThread.Name ?? $"Thread {Environment.CurrentManagedThreadId}");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write('/');
            Console.ForegroundColor = color;
            Console.Write(level);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("] [");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("Hyleus");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("] - ");
            Console.ResetColor();
            Console.WriteLine(string.Join(' ', objects));

            // file logging
            File.AppendAllText(LogFilePath, logMessage + Environment.NewLine);
        }
    }

    public static void Info(params object[] objects) =>
        BaseWrite("INFO", ConsoleColor.Cyan, objects);

    public static void Warn(params object[] objects) =>
        BaseWrite("WARN", ConsoleColor.Yellow, objects);

    public static void Error(params object[] objects) =>
        BaseWrite("ERROR", ConsoleColor.DarkRed, objects);

    public static void Debug(params object[] objects) {
#if DEBUG
        BaseWrite("DEBUG", ConsoleColor.Green, objects);
#endif
    }
}
