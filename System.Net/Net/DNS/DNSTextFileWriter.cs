using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Utils.Net.DNS;

/// <summary>
/// Provides helpers to write DNS records or headers to zone text files.
/// </summary>
public class DNSTextFileWriter : IDNSWriter<string>
{
    private readonly string path;
    private readonly bool append;

    /// <summary>
    /// Initializes a new instance of the <see cref="DNSTextFileWriter"/> class.
    /// </summary>
    /// <param name="path">Destination file path.</param>
    /// <param name="append">If true, records are appended to the file.</param>
    public DNSTextFileWriter(string path, bool append = false)
    {
        this.path = path;
        this.append = append;
    }
    /// <summary>
    /// Writes the given records to a file in standard zone file format.
    /// </summary>
    /// <param name="path">Destination file path.</param>
    /// <param name="records">The DNS records to write.</param>
    /// <param name="append">If true, records are appended to the file.</param>
    public static void WriteRecords(string path, IEnumerable<DNSResponseRecord> records, bool append = false)
    {
        using var writer = new StreamWriter(path, append, Encoding.UTF8);
        foreach (var r in records)
            writer.WriteLine(DNSText.ToText(r));
    }

    /// <summary>
    /// Writes the specified records using the path provided at construction.
    /// </summary>
    /// <param name="records">Records to write.</param>
    public void WriteRecords(IEnumerable<DNSResponseRecord> records)
    {
        WriteRecords(path, records, append);
    }

    /// <summary>
    /// Writes all records contained in a DNS header to a file.
    /// </summary>
    /// <param name="path">Destination file path.</param>
    /// <param name="header">The DNS header with the records to write.</param>
    public static void WriteHeader(string path, DNSHeader header)
    {
        using var writer = new StreamWriter(path, false, Encoding.UTF8);
        writer.Write(DNSText.Default.Write(header));
    }

    /// <inheritdoc />
    public string Write(DNSHeader header)
    {
        WriteHeader(path, header);
        return path;
    }
}
