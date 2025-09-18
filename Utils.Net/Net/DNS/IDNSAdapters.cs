namespace Utils.Net.DNS
{
    /// <summary>
    /// Describes a component capable of reading DNS headers from a transport-specific source.
    /// </summary>
    /// <typeparam name="T">Type of the input data supported by the reader.</typeparam>
    public interface IDNSReader<T>
    {
        /// <summary>
        /// Parses DNS data from the supplied input.
        /// </summary>
        /// <param name="datas">The input instance to read from.</param>
        /// <returns>The DNS header extracted from the input.</returns>
        DNSHeader Read(T datas);
    }

    /// <summary>
    /// Represents a DNS serializer targeting a specific transport type.
    /// </summary>
    /// <typeparam name="T">The output format produced by the writer.</typeparam>
    public interface IDNSWriter<T>
    {
        /// <summary>
        /// Serializes the provided DNS header into the target transport representation.
        /// </summary>
        /// <param name="header">The DNS header to serialize.</param>
        /// <returns>The serialized output.</returns>
        T Write(DNSHeader header);
    }

}
