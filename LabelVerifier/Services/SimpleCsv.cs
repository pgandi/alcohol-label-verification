using System.Text;

namespace LabelVerifier.Services;

/// <summary>
/// Minimal CSV reader/writer good enough for the batch manifest. Handles quoted
/// fields and escaped quotes ("") but intentionally stays dependency-free.
/// </summary>
public static class SimpleCsv
{
    /// <summary>Parses CSV text into rows of string cells.</summary>
    public static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else inQuotes = false;
                }
                else cell.Append(c);
            }
            else
            {
                switch (c)
                {
                    case '"':
                        inQuotes = true;
                        break;
                    case ',':
                        row.Add(cell.ToString());
                        cell.Clear();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        row.Add(cell.ToString());
                        cell.Clear();
                        rows.Add(row);
                        row = new List<string>();
                        break;
                    default:
                        cell.Append(c);
                        break;
                }
            }
        }

        // Flush the final cell/row if the file doesn't end in a newline.
        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }

        // Drop fully-empty trailing rows.
        rows.RemoveAll(r => r.Count == 1 && string.IsNullOrWhiteSpace(r[0]));
        return rows;
    }

    /// <summary>Quotes a single cell for CSV output.</summary>
    public static string Escape(string? value)
    {
        value ??= "";
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    public static string Row(params string?[] cells)
        => string.Join(",", cells.Select(Escape));
}
