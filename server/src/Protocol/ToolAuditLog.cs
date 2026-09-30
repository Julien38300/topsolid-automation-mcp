using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TopSolidMcpServer.Protocol
{
    /// <summary>
    /// Append-only audit journal for MCP tool calls. One JSONL line per invocation,
    /// written next to the server executable so an incident can be reconstructed
    /// afterwards (T-1062, point 4). Writing here must NEVER break a tool call:
    /// every failure is swallowed on purpose.
    /// Format: {"timestamp":"2026-09-29T08:00:00.000Z","tool":"...","args":"...","error":false,"duration_ms":12}
    /// </summary>
    public static class ToolAuditLog
    {
        private const int MaxArgsLength = 500;
        private const string LogFileName = "tool_audit.jsonl";
        private static readonly object WriteLock = new object();

        /// <summary>
        /// Appends one audit line for a tool call. Silent on any failure: audit is
        /// best-effort by design and must never surface an error to the MCP client.
        /// </summary>
        /// <param name="toolName">Registered tool name.</param>
        /// <param name="argumentsJson">Raw JSON arguments (already compacted by the caller).</param>
        /// <param name="isError">True when the call was reported as a tool error.</param>
        /// <param name="durationMs">Wall-clock duration of the handler, in milliseconds.</param>
        public static void Write(string toolName, string argumentsJson, bool isError, long durationMs)
        {
            try
            {
                string line = "{\"timestamp\":\"" + DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'") +
                              "\",\"tool\":\"" + EscapeJson(toolName) +
                              "\",\"args\":\"" + EscapeJson(Truncate(argumentsJson)) +
                              "\",\"error\":" + (isError ? "true" : "false") +
                              ",\"duration_ms\":" + durationMs + "}\n";

                lock (WriteLock)
                {
                    File.AppendAllText(LogFilePath(), line, Encoding.UTF8);
                }
            }
            catch
            {
                // Swallowed on purpose: audit must never break tool execution.
            }
        }

        /// <summary>Audit file lives next to the running assembly (i.e. the exe directory).</summary>
        private static string LogFilePath()
        {
            try
            {
                string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(assemblyLocation))
                {
                    return Path.Combine(Path.GetDirectoryName(assemblyLocation), LogFileName);
                }
            }
            catch
            {
                // Fall through to the working directory.
            }
            return LogFileName;
        }

        private static string Truncate(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            // Cut on char boundaries so a multi-byte character is not split in half.
            var sb = new StringBuilder();
            int count = 0;
            foreach (char c in value)
            {
                if (count >= MaxArgsLength - 3)
                {
                    sb.Append("...");
                    break;
                }
                sb.Append(c);
                count++;
            }
            return sb.ToString();
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u" + ((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}