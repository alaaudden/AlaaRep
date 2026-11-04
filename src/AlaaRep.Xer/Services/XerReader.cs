using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AlaaRep.Xer.Models;

namespace AlaaRep.Xer.Services;

/// <summary>
/// Parser for Primavera P6 XER files.
/// Automatically handles encoding detection (UTF-8, Windows-1252, Latin1) and resilient parsing.
/// </summary>
public class XerReader
{
    static XerReader()
    {
        // Register CodePagesEncodingProvider to support Windows-1252 on .NET Core/.NET 8+
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// Reads and parses an XER file from the specified file path.
    /// </summary>
    public static XerDocument ReadFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentNullException(nameof(filePath));
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"XER file not found: {filePath}", filePath);

        var content = ReadTextWithEncodingFallbacks(filePath);
        var doc = ParseContent(content);
        doc.FilePath = filePath;
        return doc;
    }

    /// <summary>
    /// Asynchronously reads and parses an XER file.
    /// </summary>
    public static async Task<XerDocument> ReadFileAsync(string filePath)
    {
        return await Task.Run(() => ReadFile(filePath)).ConfigureAwait(false);
    }

    /// <summary>
    /// Parses XER content from a string.
    /// </summary>
    public static XerDocument ParseContent(string content)
    {
        var doc = new XerDocument();
        if (string.IsNullOrEmpty(content))
            return doc;

        using var reader = new StringReader(content);
        return Parse(reader, doc);
    }

    /// <summary>
    /// Parses XER content from a TextReader.
    /// </summary>
    public static XerDocument Parse(TextReader reader, XerDocument? existingDoc = null)
    {
        var doc = existingDoc ?? new XerDocument();
        XerTable? currentTable = null;

        string? line;
        bool isFirstLine = true;

        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Header line (e.g. ERMHDR...)
            if (isFirstLine && (line.StartsWith("ERMHDR") || !line.StartsWith("%")))
            {
                doc.HeaderLine = line.Trim();
                isFirstLine = false;
                continue;
            }
            isFirstLine = false;

            // End marker
            if (line.StartsWith("%E"))
            {
                break;
            }

            // Table declaration (%T <TableName>)
            if (line.StartsWith("%T\t") || line.StartsWith("%T "))
            {
                var tableName = line[2..].Trim();
                currentTable = doc.GetOrCreateTable(tableName);
                continue;
            }

            // Fields declaration (%F <Field1>\t<Field2>...)
            if (line.StartsWith("%F\t") || line.StartsWith("%F "))
            {
                if (currentTable == null)
                    continue;

                var rawFields = line[2..].TrimStart('\t', ' ');
                var fieldNames = rawFields.Split('\t');
                currentTable.Fields.Clear();
                foreach (var fn in fieldNames)
                {
                    currentTable.Fields.Add(fn.Trim());
                }
                continue;
            }

            // Record row (%R <Val1>\t<Val2>...)
            if (line.StartsWith("%R\t") || line.StartsWith("%R "))
            {
                if (currentTable == null)
                    continue;

                var rawRecord = line[2..].TrimStart('\t', ' ');
                // In XER format, values are tab-separated. We preserve trailing empty tokens.
                var values = rawRecord.Split('\t');

                var rowValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < currentTable.Fields.Count; i++)
                {
                    var fieldName = currentTable.Fields[i];
                    var val = (i < values.Length) ? values[i] : string.Empty;
                    rowValues[fieldName] = val;
                }

                currentTable.Rows.Add(new XerRow(rowValues));
                continue;
            }
        }

        return doc;
    }

    /// <summary>
    /// Detects encoding or tries common XER encodings (UTF-8, Windows-1252, Latin1).
    /// </summary>
    private static string ReadTextWithEncodingFallbacks(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);

        // Check UTF-8 BOM
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        // Try UTF-8 with strict throw
        try
        {
            var strictUtf8 = new UTF8Encoding(false, true);
            return strictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            // Not valid UTF-8, try Windows-1252 (very common in P6 exports on Windows)
            try
            {
                var win1252 = Encoding.GetEncoding(1252);
                return win1252.GetString(bytes);
            }
            catch
            {
                // Fallback to ISO-8859-1 (Latin1)
                return Encoding.Latin1.GetString(bytes);
            }
        }
    }
}
