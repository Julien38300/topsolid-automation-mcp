using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// File logger for the server: redirects the stderr stream ([MCP-INFO]/[MCP-WARN]/
    /// [MCP-FATAL] lines) to %LOCALAPPDATA%\TopSolidMcp\logs\server.log so that:
    ///   - the tray's "Report a bug" flow can attach recent lines to the GitHub issue
    ///     (GitHubFeedbackLogic.BuildBody embeds them, scrubbed);
    ///   - a headless install (scheduled task, console hidden or absent) keeps a
    ///     diagnostic trail — before v1.8.1 those lines went to a console window that
    ///     closed at logoff, or to nowhere at all when no console was attached.
    ///
    /// Write path is a single locked append: one line at a time, thread-safe via a
    /// lock. Rotation: when the file exceeds <see cref="MaxBytes"/>, it is renamed
    /// to server.log.old (previous .old is deleted) and a fresh log is started, so
    /// the disk footprint is bounded at ~2x MaxBytes.
    ///
    /// Pure decision logic (ShouldRotate, FormatLine, ScrubLine) is kept static and
    /// side-effect-free: it is source-linked into TopSolidMcp.Http.Tests (ServerLogTests).
    /// </summary>
    public static class ServerLog
    {
        /// <summary>Upper bound before rotation. Small on purpose: only the recent
        /// past matters for debugging, and the log is meant to be pasted into issues.</summary>
        public const long MaxBytes = 512 * 1024;

        private static readonly object Gate = new object();
        private static string _path;

        /// <summary>Log file path (creates the directory). Null when the local app
        /// data dir is unreachable — every write then becomes a no-op, never a crash.</summary>
        public static string GetPath()
        {
            if (_path != null) return _path;
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TopSolidMcp", "logs");
                Directory.CreateDirectory(dir);
                _path = Path.Combine(dir, "server.log");
            }
            catch
            {
                _path = null;
            }
            return _path;
        }

        /// <summary>Installs the redirection: Console.Error is replaced by a writer
        /// that mirrors every line to the log file AND to the original stderr (so a
        /// debug run in a console keeps showing the lines). Idempotent.</summary>
        public static void Setup()
        {
            if (_path == null) GetPath();
            if (_path == null) return;
            lock (Gate)
            {
                if (_installed) return;
                _installed = true;
            }
            var original = Console.Error;
            var tee = new TeeWriter(original, WriteLine);
            Console.SetError(tee);
            WriteLine("[MCP-LOG] stderr redirected to " + _path);
        }
        private static bool _installed;

        /// <summary>Appends one line to the log (rotation-checked). Public for the
        /// crash handler, which writes outside the redirected stream on purpose.</summary>
        public static void WriteLine(string line)
        {
            try
            {
                string path = GetPath();
                if (path == null) return;
                lock (Gate)
                {
                    if (ShouldRotate(path, out long size))
                        Rotate(path);
                    File.AppendAllText(path, FormatLine(line), Encoding.UTF8);
                }
            }
            catch
            {
                // Logging must never take the server down (no disk, locked file...).
            }
        }

        /// <summary>True when the current log has grown past MaxBytes.</summary>
        public static bool ShouldRotate(string path, out long size)
        {
            size = 0;
            try
            {
                var fi = new FileInfo(path);
                if (fi.Exists) size = fi.Length;
            }
            catch { }
            return size > MaxBytes;
        }

        /// <summary>Renames log -> log.old (deleting the previous .old).</summary>
        public static void Rotate(string path)
        {
            string old = path + ".old";
            try { if (File.Exists(old)) File.Delete(old); } catch { }
            try { if (File.Exists(path)) File.Move(path, old); } catch { }
        }

        /// <summary>Prefixes the line with a UTC timestamp: log correlation across
        /// machines (LY458 is UTC+2, a client machine may be elsewhere).</summary>
        public static string FormatLine(string line)
        {
            return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + (line ?? "") + "\r\n";
        }

        /// <summary>Removes the API key from a line before it can reach the log
        /// (the log feeds GitHub issues — defense in depth with GitHubFeedbackLogic.ScrubKey).</summary>
        public static string ScrubLine(string line, string key)
        {
            if (string.IsNullOrEmpty(line) || string.IsNullOrEmpty(key)) return line;
            return line.Replace(key, "***");
        }

        /// <summary>Reads the last <paramref name="count"/> lines (bounded: reads at
        /// most the whole file, keeps a 4000-line sliding window in memory).</summary>
        public static IList<string> ReadLastLines(string path, int count)
        {
            var lines = new List<string>();
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fs))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lines.Add(line);
                        if (lines.Count > 4000) lines.RemoveAt(0);
                    }
                }
            }
            catch { }
            return lines.Count <= count ? lines : lines.GetRange(lines.Count - count, count);
        }

        /// <summary>Writer that forwards to the original console AND the log.</summary>
        private sealed class TeeWriter : TextWriter
        {
            private readonly TextWriter _console;
            private readonly Action<string> _log;
            public TeeWriter(TextWriter console, Action<string> log)
            { _console = console; _log = log; }
            public override Encoding Encoding { get { return _console != null ? _console.Encoding : Encoding.UTF8; } }
            public override void WriteLine(string value)
            {
                try { if (_console != null) _console.WriteLine(value); } catch { }
                try { _log(value); } catch { }
            }
        }
    }

    /// <summary>
    /// Crash reporting: AppDomain.UnhandledException + Application.ThreadException
    /// write the exception + last log lines to crash-YYYYMMDD-HHMMSS.txt next to the
    /// log, then the next "Report a bug" from the tray attaches the newest crash
    /// file automatically. Pure builder (BuildCrashReport) is unit-tested; the
    /// handler registration stays in Program.cs.
    /// </summary>
    public static class CrashReport
    {
        /// <summary>Formats the crash report: header, exception chain, then the tail
        /// of the log for context. Key is scrubbed from every line.</summary>
        public static string BuildCrashReport(Exception ex, string version, IList<string> recentLogLines, string keyToScrub)
        {
            var sb = new StringBuilder();
            sb.AppendLine("TopSolidMcpServer crash report");
            sb.AppendLine("Version: " + version);
            sb.AppendLine("UTC: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine();
            Exception cur = ex;
            int depth = 0;
            while (cur != null && depth < 5)
            {
                sb.AppendLine(depth == 0 ? "Exception:" : "Inner exception " + depth + ":");
                sb.AppendLine("  " + cur.GetType().FullName + ": " + cur.Message);
                if (!string.IsNullOrEmpty(cur.StackTrace))
                    sb.AppendLine(cur.StackTrace);
                cur = cur.InnerException;
                depth++;
            }
            if (recentLogLines != null && recentLogLines.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Last log lines:");
                foreach (string raw in recentLogLines)
                {
                    string safe = ServerLog.ScrubLine(raw, keyToScrub);
                    if (!string.IsNullOrEmpty(safe)) sb.AppendLine("  " + safe);
                }
            }
            return sb.ToString();
        }

        /// <summary>Crash file path for a given instant (next to server.log).</summary>
        public static string GetCrashPath(DateTime utcNow)
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TopSolidMcp", "logs");
            return Path.Combine(dir, "crash-" + utcNow.ToString("yyyyMMdd-HHmmss") + ".txt");
        }
    }
}