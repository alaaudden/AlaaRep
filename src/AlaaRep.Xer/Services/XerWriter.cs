using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AlaaRep.Xer.Models;

namespace AlaaRep.Xer.Services;

/// <summary>
/// Serializes a XerDocument back into the Primavera P6 XER file format.
/// Preserves %T, %F, %R, and %E structures with standard tab delimiters.
/// </summary>
public class XerWriter
{
    static XerWriter()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// Writes the document to the specified file path using Windows-1252 (or specified encoding).
    /// </summary>
    public static void WriteFile(XerDocument document, string filePath, Encoding? encoding = null)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Standard Primavera P6 export uses Windows-1252 / ANSI by default unless configured otherwise.
        var enc = encoding ?? Encoding.GetEncoding(1252);
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, enc);
        Write(document, writer);
        document.FilePath = filePath;
    }

    /// <summary>
    /// Asynchronously writes the document to file.
    /// </summary>
    public static async Task WriteFileAsync(XerDocument document, string filePath, Encoding? encoding = null)
    {
        await Task.Run(() => WriteFile(document, filePath, encoding)).ConfigureAwait(false);
    }

    /// <summary>
    /// Serializes document into an XER formatted string.
    /// </summary>
    public static string WriteToString(XerDocument document)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        using var sw = new StringWriter();
        Write(document, sw);
        return sw.ToString();
    }

    /// <summary>
    /// Writes the document to any TextWriter.
    /// </summary>
    public static void Write(XerDocument document, TextWriter writer)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        if (writer == null) throw new ArgumentNullException(nameof(writer));

        // 1. Header line
        var header = string.IsNullOrWhiteSpace(document.HeaderLine)
            ? $"ERMHDR\t8.3\t{DateTime.Now:yyyy-MM-dd}\tPROJECT"
            : document.HeaderLine;
        writer.WriteLine(header);

        // 2. Tables
        foreach (var table in document.Tables)
        {
            if (string.IsNullOrWhiteSpace(table.Name))
                continue;

            // %T <TableName>
            writer.WriteLine($"%T\t{table.Name}");

            // %F <Field1>\t<Field2>...
            if (table.Fields.Count > 0)
            {
                writer.WriteLine($"%F\t{string.Join("\t", table.Fields)}");
            }
            else
            {
                writer.WriteLine("%F");
            }

            // %R <Val1>\t<Val2>...
            foreach (var row in table.Rows)
            {
                var values = new string[table.Fields.Count];
                for (int i = 0; i < table.Fields.Count; i++)
                {
                    values[i] = row.GetValue(table.Fields[i], string.Empty);
                }
                writer.WriteLine($"%R\t{string.Join("\t", values)}");
            }
        }

        // 3. End of export marker
        writer.WriteLine("%E");
    }
}
