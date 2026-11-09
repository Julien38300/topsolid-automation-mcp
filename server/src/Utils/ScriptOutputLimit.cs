using System;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// Caps the text returned by topsolid_execute_script and topsolid_modify_script. A cut is
    /// announced on the FIRST line, before any data: the old trailing marker came after a
    /// truncated JSON payload, which callers parsed and failed on without seeing it.
    /// </summary>
    public static class ScriptOutputLimit
    {
        /// <summary>Environment variable that overrides <see cref="DefaultMaxChars"/>.</summary>
        public const string EnvVarName = "TOPSOLID_MCP_SCRIPT_MAX_OUTPUT_CHARS";

        /// <summary>Default cap, in characters (about 8 000 tokens).</summary>
        public const int DefaultMaxChars = 30000;

        /// <summary>Smallest accepted override, so a typo cannot hide every result.</summary>
        public const int MinMaxChars = 1000;

        /// <summary>Cap read from <see cref="EnvVarName"/>, or the default when unset or invalid.</summary>
        public static int ResolveMaxChars(string rawValue)
        {
            int value;
            if (!string.IsNullOrWhiteSpace(rawValue) && int.TryParse(rawValue.Trim(), out value) && value >= MinMaxChars)
                return value;
            return DefaultMaxChars;
        }

        /// <summary>Cap from the environment of the running server.</summary>
        public static int CurrentMaxChars()
        {
            return ResolveMaxChars(Environment.GetEnvironmentVariable(EnvVarName));
        }

        /// <summary>
        /// Returns <paramref name="output"/> unchanged when it fits, otherwise a header line that
        /// states the cut, the first <paramref name="maxChars"/> characters, and a closing marker.
        /// </summary>
        public static string Apply(string output, int maxChars)
        {
            if (string.IsNullOrEmpty(output) || output.Length <= maxChars)
                return output;

            int omitted = output.Length - maxChars;
            return "[OUTPUT TRUNCATED: first " + maxChars + " of " + output.Length + " characters shown. " +
                "The text below is incomplete, so structured data such as JSON will not parse. " +
                "Return less text, write large results to a file and return its path, or raise " +
                EnvVarName + ".]\n" +
                output.Substring(0, maxChars) +
                "\n... [" + omitted + " characters omitted]";
        }
    }
}
