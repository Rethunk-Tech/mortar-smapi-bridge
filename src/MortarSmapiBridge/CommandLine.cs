using System.Security.Cryptography;
using System.Text;

namespace MortarSmapiBridge;

/// <summary>Parsing and authentication helpers for the bridge protocol. Independent of SMAPI so tests need no game.</summary>
internal static class CommandLine
{
    /// <summary>Split a console line into a command name and arguments. Whitespace separates tokens, double quotes group, and a backslash escapes the next character.</summary>
    public static bool TryParse(string? line, out string name, out string[] args, out string error)
    {
        name = "";
        args = [];
        error = "";

        var tokens = new List<string>();
        var current = new StringBuilder();
        bool inToken = false, inQuotes = false, escaped = false;

        foreach (char c in line ?? "")
        {
            if (escaped)
            {
                current.Append(c);
                escaped = false;
            }
            else if (c == '\\')
            {
                escaped = true;
                inToken = true;
            }
            else if (c == '"')
            {
                inQuotes = !inQuotes;
                inToken = true;
            }
            else if (!inQuotes && char.IsWhiteSpace(c))
            {
                if (inToken)
                    tokens.Add(current.ToString());
                current.Clear();
                inToken = false;
            }
            else
            {
                current.Append(c);
                inToken = true;
            }
        }

        if (inQuotes || escaped)
        {
            error = "unterminated quote or escape";
            return false;
        }
        if (inToken)
            tokens.Add(current.ToString());
        if (tokens.Count == 0 || tokens[0].Length == 0)
        {
            error = "empty command";
            return false;
        }

        name = tokens[0];
        args = tokens.Skip(1).ToArray();
        return true;
    }

    /// <summary>Compare tokens without leaking the matching prefix length through timing.</summary>
    public static bool TokenMatches(string expected, string? provided)
    {
        return provided != null
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided));
    }
}
