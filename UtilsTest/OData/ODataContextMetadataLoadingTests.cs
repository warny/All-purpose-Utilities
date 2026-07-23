using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.OData;
using Utils.OData.Metadatas;

namespace UtilsTest.OData;

/// <summary>
/// Unit tests for <see cref="ODataContext"/> asynchronous, bounded, injectable metadata loading
/// (audit items 14, 15, 16, 29, 33, 34). Network-specific behaviour (item 30 redirects) is covered
/// by the functional tests; these tests exercise the stream and size-policy paths without I/O.
/// </summary>
[TestClass]
public class ODataContextMetadataLoadingTests
{
    private const string SampleEdmx =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
        "<edmx:Edmx Version=\"4.0\" xmlns:edmx=\"http://docs.oasis-open.org/odata/ns/edmx\">" +
        "<edmx:DataServices>" +
        "<Schema Namespace=\"SampleModel\" xmlns=\"http://docs.oasis-open.org/odata/ns/edm\">" +
        "<EntityType Name=\"Product\">" +
        "<Key><PropertyRef Name=\"Id\" /></Key>" +
        "<Property Name=\"Id\" Type=\"Edm.Int32\" />" +
        "<Property Name=\"Name\" Type=\"Edm.String\" />" +
        "</EntityType>" +
        "</Schema>" +
        "</edmx:DataServices>" +
        "</edmx:Edmx>";

    private static MemoryStream EdmxStream()
        => new(Encoding.UTF8.GetBytes(SampleEdmx));

    // -----------------------------------------------------------------------
    // Item 34: async stream loading returns parsed metadata
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task LoadMetadataFromStreamAsync_SeekableStream_ParsesMetadata()
    {
        using MemoryStream stream = EdmxStream();
        var metadata = await ODataContext.LoadMetadataFromStreamAsync(stream);
        Assert.IsNotNull(metadata);
        Assert.IsNotNull(metadata.DataServices);
    }

    [TestMethod]
    public async Task LoadMetadataFromStreamAsync_NonSeekableStream_ParsesMetadata()
    {
        // A forward-only stream forces the bounded buffered copy path (item 15).
        using MemoryStream backing = EdmxStream();
        using var forwardOnly = new ForwardOnlyStream(backing);
        var metadata = await ODataContext.LoadMetadataFromStreamAsync(forwardOnly);
        Assert.IsNotNull(metadata);
    }

    [TestMethod]
    public async Task LoadMetadataFromStreamAsync_NullStream_ThrowsArgumentNull()
    {
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(
            async () => await ODataContext.LoadMetadataFromStreamAsync(null!));
    }

    // -----------------------------------------------------------------------
    // Item 15 / 33: consistent size cap enforced for caller-provided streams
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task LoadMetadataFromStreamAsync_SeekableStreamOverLimit_Throws()
    {
        using MemoryStream stream = EdmxStream();
        var options = new ODataMetadataOptions { MaxMetadataBytes = 8 };
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await ODataContext.LoadMetadataFromStreamAsync(stream, options));
    }

    [TestMethod]
    public async Task LoadMetadataFromStreamAsync_SeekableStreamAtOffset_ReadsFromCurrentPosition()
    {
        // Arrange: prepend 100 bytes of junk before the EDMX document.
        // The deserializer must read from the current position, not from the beginning.
        const int junkLength = 100;
        byte[] edmxBytes = Encoding.UTF8.GetBytes(SampleEdmx);
        using var stream = new MemoryStream();
        stream.Write(new byte[junkLength], 0, junkLength);
        stream.Write(edmxBytes, 0, edmxBytes.Length);
        stream.Position = junkLength;  // Skip the junk prefix

        var metadata = await ODataContext.LoadMetadataFromStreamAsync(stream);
        Assert.IsNotNull(metadata);
        Assert.IsNotNull(metadata.DataServices);
    }

    [TestMethod]
    public async Task LoadMetadataFromStreamAsync_SeekableStreamAtOffset_SizeLimitAppliesToRemainingBytes()
    {
        // The size limit must be compared against (Length - Position), not total Length.
        // With a 1 000-byte junk prefix and MaxMetadataBytes set between the EDMX size and
        // the total size, the load must succeed when started at the correct offset.
        const int junkLength = 1_000;
        byte[] edmxBytes = Encoding.UTF8.GetBytes(SampleEdmx);
        using var stream = new MemoryStream();
        stream.Write(new byte[junkLength], 0, junkLength);
        stream.Write(edmxBytes, 0, edmxBytes.Length);
        stream.Position = junkLength;

        // Limit is smaller than the full stream but larger than the EDMX portion alone.
        var options = new ODataMetadataOptions { MaxMetadataBytes = junkLength + edmxBytes.Length - 1 };
        var metadata = await ODataContext.LoadMetadataFromStreamAsync(stream, options);
        Assert.IsNotNull(metadata);
    }

    [TestMethod]
    public async Task LoadMetadataFromStreamAsync_NonSeekableStreamOverLimit_Throws()
    {
        using MemoryStream backing = EdmxStream();
        using var forwardOnly = new ForwardOnlyStream(backing);
        var options = new ODataMetadataOptions { MaxMetadataBytes = 8 };
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await ODataContext.LoadMetadataFromStreamAsync(forwardOnly, options));
    }

    // -----------------------------------------------------------------------
    // Item 14 / 29: async file loading path and CreateAsync factory
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task LoadMetadataAsync_FilePath_ParsesMetadata()
    {
        string path = Path.Combine(Path.GetTempPath(), $"odata-metadata-{Guid.NewGuid():N}.edmx");
        await File.WriteAllTextAsync(path, SampleEdmx, Encoding.UTF8);
        try
        {
            var metadata = await ODataContext.LoadMetadataAsync(path);
            Assert.IsNotNull(metadata);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task LoadMetadataAsync_MissingFile_ThrowsInvalidOperation()
    {
        string path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.edmx");
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await ODataContext.LoadMetadataAsync(path));
    }

    [TestMethod]
    public async Task LoadMetadataAsync_NullOrWhitespace_ThrowsArgument()
    {
        await Assert.ThrowsExceptionAsync<ArgumentException>(
            async () => await ODataContext.LoadMetadataAsync("   "));
    }

    [TestMethod]
    public async Task CreateAsync_FromStream_InvokesFactoryWithParsedMetadata()
    {
        using MemoryStream stream = EdmxStream();
        // The Edmx type is duplicated across Utils.OData and Utils.OData.Generators (both
        // referenced by this test project), so the factory receives it as an inferred parameter
        // and the concrete context is built from a stream to avoid naming the ambiguous type.
        int factoryInvocations = 0;
        TestContext context = await ODataContext.CreateAsync(
            stream,
            _ =>
            {
                factoryInvocations++;
                return new TestContext(EdmxStream());
            });
        Assert.AreEqual(1, factoryInvocations);
        Assert.IsNotNull(context);
        Assert.IsNotNull(context.Metadata);
        Assert.IsNotNull(context.Metadata.DataServices);
    }

    // -----------------------------------------------------------------------
    // ODataMetadataOptions validation
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ODataMetadataOptions_NonPositiveMaxBytes_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new ODataMetadataOptions { MaxMetadataBytes = 0 });
    }

    [TestMethod]
    public void ODataMetadataOptions_Default_HasTenMebibyteCapAndNoCrossOriginRedirect()
    {
        Assert.AreEqual(10 * 1024 * 1024, ODataMetadataOptions.Default.MaxMetadataBytes);
        Assert.IsFalse(ODataMetadataOptions.Default.AllowCrossOriginRedirect);
    }

    /// <summary>Minimal concrete context used to exercise construction from a metadata stream.</summary>
    private sealed class TestContext : ODataContext
    {
        public TestContext(Stream edmxStream) : base(edmxStream) { }
    }

    /// <summary>A forward-only, non-seekable stream wrapper used to exercise the buffered copy path.</summary>
    private sealed class ForwardOnlyStream : Stream
    {
        private readonly Stream _inner;

        public ForwardOnlyStream(Stream inner) => _inner = inner;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
