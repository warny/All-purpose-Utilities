using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies parser execution-context structural hashing semantics.
/// </summary>
[TestClass]
public class ParserExecutionContextHasherTests
{
    /// <summary>
    /// Verifies that identical generated-context-shaped instances produce identical state keys.
    /// </summary>
    [TestMethod]
    public void GetKey_IdenticalContexts_ProduceSameKey()
    {
        var left = CreateContext();
        var right = CreateContext();

        Assert.AreEqual(ParserExecutionContextHasher<HashingContext>.GetKey(left), ParserExecutionContextHasher<HashingContext>.GetKey(right));
    }

    /// <summary>
    /// Verifies that changing a simple field changes the state key.
    /// </summary>
    [TestMethod]
    public void GetKey_SimpleFieldChange_ChangesKey()
    {
        var context = CreateContext();
        var original = ParserExecutionContextHasher<HashingContext>.GetKey(context);

        context.Count++;

        Assert.AreNotEqual(original, ParserExecutionContextHasher<HashingContext>.GetKey(context));
    }

    /// <summary>
    /// Verifies that changing an ordered collection changes the state key.
    /// </summary>
    [TestMethod]
    public void GetKey_CollectionChange_ChangesKey()
    {
        var context = CreateContext();
        var original = ParserExecutionContextHasher<HashingContext>.GetKey(context);

        context.Items.Add("c");

        Assert.AreNotEqual(original, ParserExecutionContextHasher<HashingContext>.GetKey(context));
    }

    /// <summary>
    /// Verifies that dictionary insertion order does not affect the state key.
    /// </summary>
    [TestMethod]
    public void GetKey_DictionaryOrder_DoesNotChangeKey()
    {
        var left = new DictionaryContext
        {
            Values = new Dictionary<string, int>
            {
                ["first"] = 1,
                ["second"] = 2
            }
        };
        var right = new DictionaryContext
        {
            Values = new Dictionary<string, int>
            {
                ["second"] = 2,
                ["first"] = 1
            }
        };

        Assert.AreEqual(ParserExecutionContextHasher<DictionaryContext>.GetKey(left), ParserExecutionContextHasher<DictionaryContext>.GetKey(right));
    }

    /// <summary>
    /// Verifies that set insertion order does not affect the state key.
    /// </summary>
    [TestMethod]
    public void GetKey_SetOrder_DoesNotChangeKey()
    {
        var left = new SetContext { Values = ["first", "second"] };
        var right = new SetContext { Values = ["second", "first"] };

        Assert.AreEqual(ParserExecutionContextHasher<SetContext>.GetKey(left), ParserExecutionContextHasher<SetContext>.GetKey(right));
    }

    /// <summary>
    /// Verifies that explicit user hashable objects are supported.
    /// </summary>
    [TestMethod]
    public void GetKey_ExplicitHashableObject_IsSupported()
    {
        var left = new HashableObjectContext { Value = new SupportedUserValue(42) };
        var right = new HashableObjectContext { Value = new SupportedUserValue(42) };
        var changed = new HashableObjectContext { Value = new SupportedUserValue(43) };

        Assert.AreEqual(ParserExecutionContextHasher<HashableObjectContext>.GetKey(left), ParserExecutionContextHasher<HashableObjectContext>.GetKey(right));
        Assert.AreNotEqual(ParserExecutionContextHasher<HashableObjectContext>.GetKey(left), ParserExecutionContextHasher<HashableObjectContext>.GetKey(changed));
    }

    /// <summary>
    /// Verifies that unsupported complex user objects fail explicitly instead of using reference identity.
    /// </summary>
    [TestMethod]
    public void GetKey_UnsupportedComplexObject_ThrowsExplicitException()
    {
        var context = new UnsupportedObjectContext { Value = new UnsupportedUserValue() };

        var exception = Assert.ThrowsException<InvalidOperationException>(() => ParserExecutionContextHasher<UnsupportedObjectContext>.GetKey(context));

        StringAssert.Contains(exception.Message, typeof(UnsupportedUserValue).FullName!);
        StringAssert.Contains(exception.Message, nameof(UnsupportedObjectContext.Value));
        StringAssert.Contains(exception.Message, nameof(IParserExecutionStateHashable));
    }

    /// <summary>
    /// Creates a sample context with scalar, collection, dictionary, set, and auto-property state.
    /// </summary>
    /// <returns>A populated context.</returns>
    private static HashingContext CreateContext()
    {
        return new HashingContext
        {
            Count = 1,
            Enabled = true,
            Name = "state",
            Items = ["a", "b"],
            Numbers = [1, 2],
            Map = new Dictionary<string, int> { ["one"] = 1 },
            Set = ["x", "y"],
            AutoPropertyValue = Guid.Parse("d7b36b93-3c9b-4dd2-bf77-7434dbb753d4")
        };
    }

    /// <summary>
    /// Sample context with supported scalar and collection fields.
    /// </summary>
    private sealed class HashingContext
    {
        /// <summary>Mutable integer state.</summary>
        public int Count;
        /// <summary>Mutable boolean state.</summary>
        public bool Enabled;
        /// <summary>Mutable string state.</summary>
        public string? Name;
        /// <summary>Mutable ordered list state.</summary>
        public List<string> Items = [];
        /// <summary>Mutable array state.</summary>
        public int[] Numbers = [];
        /// <summary>Mutable dictionary state.</summary>
        public Dictionary<string, int> Map = [];
        /// <summary>Mutable set state.</summary>
        public HashSet<string> Set = [];
        /// <summary>Auto-property state represented by a compiler backing field.</summary>
        public Guid AutoPropertyValue { get; set; }
        /// <summary>Event backing field that must be ignored.</summary>
        public event EventHandler? Changed;
        /// <summary>Static state that must be ignored.</summary>
        public static int StaticValue = 0;

        /// <summary>
        /// Raises the ignored event so the compiler keeps the event field observable in metadata.
        /// </summary>
        public void RaiseChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Sample context containing a dictionary.
    /// </summary>
    private sealed class DictionaryContext
    {
        /// <summary>Dictionary state whose enumeration order must not affect hashing.</summary>
        public Dictionary<string, int> Values = [];
    }

    /// <summary>
    /// Sample context containing a set.
    /// </summary>
    private sealed class SetContext
    {
        /// <summary>Set state whose enumeration order must not affect hashing.</summary>
        public HashSet<string> Values = [];
    }

    /// <summary>
    /// Sample context containing an explicit hashable user object.
    /// </summary>
    private sealed class HashableObjectContext
    {
        /// <summary>User object state.</summary>
        public SupportedUserValue? Value;
    }

    /// <summary>
    /// Sample context containing an unsupported complex user object.
    /// </summary>
    private sealed class UnsupportedObjectContext
    {
        /// <summary>Unsupported user object state.</summary>
        public UnsupportedUserValue? Value;
    }

    /// <summary>
    /// Supported complex user value with explicit parser execution-state hashing.
    /// </summary>
    /// <param name="Number">State value.</param>
    private sealed record SupportedUserValue(int Number) : IParserExecutionStateHashable
    {
        /// <summary>
        /// Computes a deterministic hash for the user value.
        /// </summary>
        /// <returns>The user state hash.</returns>
        public ulong GetParserExecutionStateHash()
        {
            return (ulong)Number;
        }
    }

    /// <summary>
    /// Unsupported complex user value used to verify explicit failure.
    /// </summary>
    private sealed class UnsupportedUserValue
    {
        /// <summary>Mutable user data that would need an explicit hash contract.</summary>
        public int Number = 1;
    }
}
