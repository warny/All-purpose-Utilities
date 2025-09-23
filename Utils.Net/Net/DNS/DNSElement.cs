namespace Utils.Net.DNS
{
    /// <summary>
    /// Represents the abstract base class for all DNS elements within this library.
    /// </summary>
    /// <remarks>
    /// Derived classes should extend this class to implement specific DNS record types or
    /// additional functionality. The <see cref="DNSType"/> property can be overridden to
    /// provide a more descriptive or protocol-specific type identifier if necessary.
    /// </remarks>
    public abstract class DNSElement
    {
        /// <summary>
        /// Gets the DNS type name of the current instance. By default, it returns the
        /// runtime type name obtained from <see cref="object.GetType"/>.
        /// </summary>
        /// <remarks>
        /// Override this property in derived classes to provide a customized or
        /// protocol-oriented DNS record type name.
        /// </remarks>
        public virtual string DNSType => GetType().Name;
    }
}
