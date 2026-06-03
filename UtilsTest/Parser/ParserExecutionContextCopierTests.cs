using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies the shallow structural copy semantics of <see cref="ParserExecutionContextCopier{TContext}"/>.
/// </summary>
[TestClass]
public class ParserExecutionContextCopierTests
{
    /// <summary>
    /// Verifies that simple private fields are copied and then evolve independently from the source instance.
    /// </summary>
    [TestMethod]
    public void CopyCopiesSimplePrivateFields()
    {
        SimpleContext source = new();
        source.SetState(5, "source", true);

        SimpleContext copy = ParserExecutionContextCopier<SimpleContext>.Copy(source, static () => new SimpleContext());
        copy.SetState(7, "copy", false);

        Assert.AreEqual(5, source.Count);
        Assert.AreEqual("source", source.Name);
        Assert.IsTrue(source.Enabled);
        Assert.AreEqual(7, copy.Count);
        Assert.AreEqual("copy", copy.Name);
        Assert.IsFalse(copy.Enabled);
    }

    /// <summary>
    /// Verifies that private fields with no public setter are still copied by the compiled field delegate.
    /// </summary>
    [TestMethod]
    public void CopyCopiesPrivateFields()
    {
        PrivateFieldContext source = PrivateFieldContext.Create("secret");

        PrivateFieldContext copy = ParserExecutionContextCopier<PrivateFieldContext>.Copy(source, static () => new PrivateFieldContext());

        Assert.AreEqual("secret", copy.Secret);
    }

    /// <summary>
    /// Verifies that static fields are not copied or assigned by <see cref="ParserExecutionContextCopier{TContext}.CopyTo"/>.
    /// </summary>
    [TestMethod]
    public void CopyToDoesNotCopyStaticFields()
    {
        StaticFieldContext.StaticCount = 123;
        StaticFieldContext source = new(1);
        StaticFieldContext target = new(9);

        ParserExecutionContextCopier<StaticFieldContext>.CopyTo(source, target);

        Assert.AreEqual(1, target.InstanceCount);
        Assert.AreEqual(123, StaticFieldContext.StaticCount);
    }

    /// <summary>
    /// Verifies that field-like event backing fields are skipped rather than copied as context state.
    /// </summary>
    [TestMethod]
    public void CopyDoesNotCopyEventBackingFields()
    {
        EventContext source = new(4);
        int notifications = 0;
        source.Changed += () => notifications++;

        EventContext copy = ParserExecutionContextCopier<EventContext>.Copy(source, static () => new EventContext());
        copy.RaiseChanged();
        source.RaiseChanged();

        Assert.AreEqual(4, copy.Count);
        Assert.AreEqual(1, notifications);
    }

    /// <summary>
    /// Verifies that array fields are cloned and can be mutated independently after copying.
    /// </summary>
    [TestMethod]
    public void CopyClonesArrays()
    {
        ArrayContext source = new([1, 2, 3]);

        ArrayContext copy = ParserExecutionContextCopier<ArrayContext>.Copy(source, static () => new ArrayContext());
        copy.Values![0] = 42;

        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, source.Values);
        CollectionAssert.AreEqual(new[] { 42, 2, 3 }, copy.Values);
        Assert.AreNotSame(source.Values, copy.Values);
    }

    /// <summary>
    /// Verifies that list fields are copied into new list instances with the same elements.
    /// </summary>
    [TestMethod]
    public void CopyClonesLists()
    {
        ListContext source = new(["a", "b"]);

        ListContext copy = ParserExecutionContextCopier<ListContext>.Copy(source, static () => new ListContext());
        copy.Items!.Add("c");

        CollectionAssert.AreEqual(new[] { "a", "b" }, source.Items);
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, copy.Items);
        Assert.AreNotSame(source.Items, copy.Items);
    }

    /// <summary>
    /// Verifies that dictionary fields are copied into new dictionary instances with the same key/value pairs.
    /// </summary>
    [TestMethod]
    public void CopyClonesDictionaries()
    {
        DictionaryContext source = new(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 });

        DictionaryContext copy = ParserExecutionContextCopier<DictionaryContext>.Copy(source, static () => new DictionaryContext());
        copy.Map!["c"] = 3;

        CollectionAssert.AreEquivalent(new[] { "a", "b" }, source.Map!.Keys.ToArray());
        CollectionAssert.AreEquivalent(new[] { 1, 2 }, source.Map.Values.ToArray());
        CollectionAssert.AreEquivalent(new[] { "a", "b", "c" }, copy.Map.Keys.ToArray());
        CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, copy.Map.Values.ToArray());
        Assert.AreNotSame(source.Map, copy.Map);
    }

    /// <summary>
    /// Verifies that hash-set fields are copied into new hash-set instances with the same values.
    /// </summary>
    [TestMethod]
    public void CopyClonesHashSets()
    {
        HashSetContext source = new(["a", "b"]);

        HashSetContext copy = ParserExecutionContextCopier<HashSetContext>.Copy(source, static () => new HashSetContext());
        copy.Set!.Add("c");

        CollectionAssert.AreEquivalent(new[] { "a", "b" }, source.Set.ToArray());
        CollectionAssert.AreEquivalent(new[] { "a", "b", "c" }, copy.Set.ToArray());
        Assert.AreNotSame(source.Set, copy.Set);
    }

    /// <summary>
    /// Verifies that null collection fields remain null after copying.
    /// </summary>
    [TestMethod]
    public void CopyPreservesNullCollections()
    {
        NullCollectionsContext source = new();

        NullCollectionsContext copy = ParserExecutionContextCopier<NullCollectionsContext>.Copy(source, static () => new NullCollectionsContext());

        Assert.IsNull(copy.Values);
        Assert.IsNull(copy.Items);
        Assert.IsNull(copy.Map);
        Assert.IsNull(copy.Set);
    }

    /// <summary>
    /// Verifies that unrecognized reference fields are copied by reference.
    /// </summary>
    [TestMethod]
    public void CopyCopiesUnknownReferencesByReference()
    {
        object service = new();
        ReferenceContext source = new(service);

        ReferenceContext copy = ParserExecutionContextCopier<ReferenceContext>.Copy(source, static () => new ReferenceContext());

        Assert.AreSame(service, copy.Service);
        Assert.AreSame(source.Service, copy.Service);
    }

    /// <summary>
    /// Verifies that <see cref="ParserExecutionContextCopier{TContext}.CopyTo"/> replaces existing target state with source state.
    /// </summary>
    [TestMethod]
    public void CopyToReplacesTargetState()
    {
        CompositeContext source = new(3, [1, 2], ["a"], new Dictionary<string, int> { ["x"] = 1 }, ["set"]);
        CompositeContext target = new(9, [9], ["old"], new Dictionary<string, int> { ["old"] = 9 }, ["old"]);

        ParserExecutionContextCopier<CompositeContext>.CopyTo(source, target);
        target.Values![0] = 42;
        target.Items!.Add("b");
        target.Map!["y"] = 2;
        target.Set!.Add("more");

        Assert.AreEqual(3, target.Count);
        CollectionAssert.AreEqual(new[] { 1, 2 }, source.Values);
        CollectionAssert.AreEqual(new[] { 42, 2 }, target.Values);
        CollectionAssert.AreEqual(new[] { "a" }, source.Items);
        CollectionAssert.AreEqual(new[] { "a", "b" }, target.Items);
        CollectionAssert.AreEquivalent(new[] { "x" }, source.Map!.Keys.ToArray());
        CollectionAssert.AreEquivalent(new[] { "x", "y" }, target.Map.Keys.ToArray());
        CollectionAssert.AreEquivalent(new[] { "set" }, source.Set!.ToArray());
        CollectionAssert.AreEquivalent(new[] { "set", "more" }, target.Set.ToArray());
        Assert.AreNotSame(source.Values, target.Values);
        Assert.AreNotSame(source.Items, target.Items);
        Assert.AreNotSame(source.Map, target.Map);
        Assert.AreNotSame(source.Set, target.Set);
    }

    /// <summary>
    /// Verifies repeated copies keep producing correct values while using the cached compiled delegate indirectly.
    /// </summary>
    [TestMethod]
    public void CopyCanBeCalledRepeatedlyForSameContextType()
    {
        SimpleContext source = new();
        source.SetState(1, "first", true);

        SimpleContext first = ParserExecutionContextCopier<SimpleContext>.Copy(source, static () => new SimpleContext());
        source.SetState(2, "second", false);
        SimpleContext second = ParserExecutionContextCopier<SimpleContext>.Copy(source, static () => new SimpleContext());

        Assert.AreEqual(1, first.Count);
        Assert.AreEqual("first", first.Name);
        Assert.IsTrue(first.Enabled);
        Assert.AreEqual(2, second.Count);
        Assert.AreEqual("second", second.Name);
        Assert.IsFalse(second.Enabled);
    }

    /// <summary>
    /// Verifies null argument validation for <see cref="ParserExecutionContextCopier{TContext}.Copy"/> and <see cref="ParserExecutionContextCopier{TContext}.CopyTo"/>.
    /// </summary>
    [TestMethod]
    public void CopyAndCopyToRejectNullArguments()
    {
        SimpleContext source = new();
        SimpleContext target = new();

        Assert.ThrowsException<ArgumentNullException>(() => ParserExecutionContextCopier<SimpleContext>.Copy(null!, static () => new SimpleContext()));
        Assert.ThrowsException<ArgumentNullException>(() => ParserExecutionContextCopier<SimpleContext>.Copy(source, null!));
        Assert.ThrowsException<ArgumentNullException>(() => ParserExecutionContextCopier<SimpleContext>.CopyTo(null!, target));
        Assert.ThrowsException<ArgumentNullException>(() => ParserExecutionContextCopier<SimpleContext>.CopyTo(source, null!));
    }

    /// <summary>
    /// Verifies that readonly instance fields fail with an explicit configuration exception.
    /// </summary>
    [TestMethod]
    public void CopyRejectsReadonlyInstanceFields()
    {
        InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
            () => ParserExecutionContextCopier<ReadonlyContext>.Copy(new ReadonlyContext(1), static () => new ReadonlyContext(0)));

        StringAssert.Contains(exception.Message, "Readonly instance field '_count'");
        StringAssert.Contains(exception.Message, "cannot be copied by ParserExecutionContextCopier<T>");
    }

    /// <summary>
    /// Verifies that a context shaped like a generated partial class can be copied after private methods mutate its state.
    /// </summary>
    [TestMethod]
    public void CopySupportsPartialLikeGeneratedContextShape()
    {
        PartialLikeExecutionContext source = new();
        object reference = new();
        source.ExecuteAction("first", reference);
        source.ExecuteAction("second", reference);

        PartialLikeExecutionContext copy = ParserExecutionContextCopier<PartialLikeExecutionContext>.Copy(source, static () => new PartialLikeExecutionContext());
        copy.ExecuteAction("copy", reference);

        Assert.AreEqual(2, source.ActionCount);
        Assert.AreEqual(3, copy.ActionCount);
        CollectionAssert.AreEqual(new[] { "first", "second" }, source.Actions);
        CollectionAssert.AreEqual(new[] { "first", "second", "copy" }, copy.Actions);
        Assert.AreNotSame(source.Actions, copy.Actions);
        Assert.AreSame(source.LastService, copy.LastService);
    }

    /// <summary>
    /// Holds simple mutable state for copy tests.
    /// </summary>
    private sealed class SimpleContext
    {
        /// <summary>
        /// Stores a numeric counter.
        /// </summary>
        private int _count;

        /// <summary>
        /// Stores an optional name.
        /// </summary>
        private string? _name;

        /// <summary>
        /// Stores an enabled flag.
        /// </summary>
        private bool _enabled;

        /// <summary>
        /// Gets the current counter value.
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// Gets the current name value.
        /// </summary>
        public string? Name => _name;

        /// <summary>
        /// Gets a value indicating whether the context is enabled.
        /// </summary>
        public bool Enabled => _enabled;

        /// <summary>
        /// Sets all simple state fields.
        /// </summary>
        /// <param name="count">The counter value.</param>
        /// <param name="name">The name value.</param>
        /// <param name="enabled">The enabled flag.</param>
        public void SetState(int count, string? name, bool enabled)
        {
            _count = count;
            _name = name;
            _enabled = enabled;
        }
    }

    /// <summary>
    /// Holds private state that can only be changed through a factory helper.
    /// </summary>
    private sealed class PrivateFieldContext
    {
        /// <summary>
        /// Stores private text copied by the runtime copier.
        /// </summary>
        private string? _secret;

        /// <summary>
        /// Gets the copied secret value.
        /// </summary>
        public string? Secret => _secret;

        /// <summary>
        /// Creates a context with a secret value.
        /// </summary>
        /// <param name="secret">The secret value.</param>
        /// <returns>A context initialized with the supplied value.</returns>
        public static PrivateFieldContext Create(string secret)
        {
            return new PrivateFieldContext { _secret = secret };
        }
    }

    /// <summary>
    /// Holds both instance and static fields to verify static-field exclusion.
    /// </summary>
    private sealed class StaticFieldContext
    {
        /// <summary>
        /// Stores process-wide state that must not be copied.
        /// </summary>
        public static int StaticCount;

        /// <summary>
        /// Stores instance state that should be copied.
        /// </summary>
        private int _instanceCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="StaticFieldContext"/> class.
        /// </summary>
        /// <param name="instanceCount">The instance counter value.</param>
        public StaticFieldContext(int instanceCount)
        {
            _instanceCount = instanceCount;
        }

        /// <summary>
        /// Gets the instance counter value.
        /// </summary>
        public int InstanceCount => _instanceCount;
    }

    /// <summary>
    /// Holds an event field to verify event backing-field exclusion.
    /// </summary>
    private sealed class EventContext
    {
        /// <summary>
        /// Stores instance state that should still be copied.
        /// </summary>
        private int _count;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventContext"/> class.
        /// </summary>
        public EventContext()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventContext"/> class.
        /// </summary>
        /// <param name="count">The count value.</param>
        public EventContext(int count)
        {
            _count = count;
        }

        /// <summary>
        /// Occurs when the context is raised by a test helper.
        /// </summary>
        public event Action? Changed;

        /// <summary>
        /// Gets the copied count value.
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// Raises the event for subscription-copy verification.
        /// </summary>
        public void RaiseChanged()
        {
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Holds an array field for clone tests.
    /// </summary>
    private sealed class ArrayContext
    {
        /// <summary>
        /// Stores array values.
        /// </summary>
        private int[]? _values;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArrayContext"/> class.
        /// </summary>
        public ArrayContext()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArrayContext"/> class.
        /// </summary>
        /// <param name="values">The array values.</param>
        public ArrayContext(int[]? values)
        {
            _values = values;
        }

        /// <summary>
        /// Gets the array values.
        /// </summary>
        public int[]? Values => _values;
    }

    /// <summary>
    /// Holds a list field for clone tests.
    /// </summary>
    private sealed class ListContext
    {
        /// <summary>
        /// Stores list items.
        /// </summary>
        private List<string>? _items;

        /// <summary>
        /// Initializes a new instance of the <see cref="ListContext"/> class.
        /// </summary>
        public ListContext()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ListContext"/> class.
        /// </summary>
        /// <param name="items">The list items.</param>
        public ListContext(List<string>? items)
        {
            _items = items;
        }

        /// <summary>
        /// Gets the list items.
        /// </summary>
        public List<string>? Items => _items;
    }

    /// <summary>
    /// Holds a dictionary field for clone tests.
    /// </summary>
    private sealed class DictionaryContext
    {
        /// <summary>
        /// Stores dictionary entries.
        /// </summary>
        private Dictionary<string, int>? _map;

        /// <summary>
        /// Initializes a new instance of the <see cref="DictionaryContext"/> class.
        /// </summary>
        public DictionaryContext()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DictionaryContext"/> class.
        /// </summary>
        /// <param name="map">The dictionary entries.</param>
        public DictionaryContext(Dictionary<string, int>? map)
        {
            _map = map;
        }

        /// <summary>
        /// Gets the dictionary entries.
        /// </summary>
        public Dictionary<string, int>? Map => _map;
    }

    /// <summary>
    /// Holds a hash set field for clone tests.
    /// </summary>
    private sealed class HashSetContext
    {
        /// <summary>
        /// Stores set values.
        /// </summary>
        private HashSet<string>? _set;

        /// <summary>
        /// Initializes a new instance of the <see cref="HashSetContext"/> class.
        /// </summary>
        public HashSetContext()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HashSetContext"/> class.
        /// </summary>
        /// <param name="set">The set values.</param>
        public HashSetContext(HashSet<string>? set)
        {
            _set = set;
        }

        /// <summary>
        /// Gets the set values.
        /// </summary>
        public HashSet<string>? Set => _set;
    }

    /// <summary>
    /// Holds null collection fields.
    /// </summary>
    private sealed class NullCollectionsContext
    {
        /// <summary>
        /// Stores optional array values.
        /// </summary>
        private int[]? _values = null;

        /// <summary>
        /// Stores optional list items.
        /// </summary>
        private List<string>? _items = null;

        /// <summary>
        /// Stores optional dictionary entries.
        /// </summary>
        private Dictionary<string, int>? _map = null;

        /// <summary>
        /// Stores optional set values.
        /// </summary>
        private HashSet<string>? _set = null;

        /// <summary>
        /// Gets the optional array values.
        /// </summary>
        public int[]? Values => _values;

        /// <summary>
        /// Gets the optional list items.
        /// </summary>
        public List<string>? Items => _items;

        /// <summary>
        /// Gets the optional dictionary entries.
        /// </summary>
        public Dictionary<string, int>? Map => _map;

        /// <summary>
        /// Gets the optional set values.
        /// </summary>
        public HashSet<string>? Set => _set;
    }

    /// <summary>
    /// Holds an unknown reference field.
    /// </summary>
    private sealed class ReferenceContext
    {
        /// <summary>
        /// Stores an unrecognized reference value.
        /// </summary>
        private object? _service;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReferenceContext"/> class.
        /// </summary>
        public ReferenceContext()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReferenceContext"/> class.
        /// </summary>
        /// <param name="service">The reference value.</param>
        public ReferenceContext(object? service)
        {
            _service = service;
        }

        /// <summary>
        /// Gets the reference value.
        /// </summary>
        public object? Service => _service;
    }

    /// <summary>
    /// Holds combined fields used to verify target-state replacement.
    /// </summary>
    private sealed class CompositeContext
    {
        /// <summary>
        /// Stores a count value.
        /// </summary>
        private int _count;

        /// <summary>
        /// Stores array values.
        /// </summary>
        private int[]? _values;

        /// <summary>
        /// Stores list items.
        /// </summary>
        private List<string>? _items;

        /// <summary>
        /// Stores dictionary entries.
        /// </summary>
        private Dictionary<string, int>? _map;

        /// <summary>
        /// Stores set values.
        /// </summary>
        private HashSet<string>? _set;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeContext"/> class.
        /// </summary>
        /// <param name="count">The count value.</param>
        /// <param name="values">The array values.</param>
        /// <param name="items">The list items.</param>
        /// <param name="map">The dictionary entries.</param>
        /// <param name="set">The set values.</param>
        public CompositeContext(int count, int[]? values, List<string>? items, Dictionary<string, int>? map, HashSet<string>? set)
        {
            _count = count;
            _values = values;
            _items = items;
            _map = map;
            _set = set;
        }

        /// <summary>
        /// Gets the count value.
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// Gets the array values.
        /// </summary>
        public int[]? Values => _values;

        /// <summary>
        /// Gets the list items.
        /// </summary>
        public List<string>? Items => _items;

        /// <summary>
        /// Gets the dictionary entries.
        /// </summary>
        public Dictionary<string, int>? Map => _map;

        /// <summary>
        /// Gets the set values.
        /// </summary>
        public HashSet<string>? Set => _set;
    }

    /// <summary>
    /// Holds a readonly field that must be rejected explicitly.
    /// </summary>
    private sealed class ReadonlyContext
    {
        /// <summary>
        /// Stores readonly state that cannot be assigned by the copier.
        /// </summary>
        private readonly int _count;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReadonlyContext"/> class.
        /// </summary>
        /// <param name="count">The count value.</param>
        public ReadonlyContext(int count)
        {
            _count = count;
        }

        /// <summary>
        /// Gets the count value.
        /// </summary>
        public int Count => _count;
    }

    /// <summary>
    /// Mimics a generated execution context with private state and private hook helpers.
    /// </summary>
    private sealed class PartialLikeExecutionContext
    {
        /// <summary>
        /// Stores the number of executed actions.
        /// </summary>
        private int _actionCount;

        /// <summary>
        /// Stores the action names.
        /// </summary>
        private List<string>? _actions;

        /// <summary>
        /// Stores a caller-owned reference copied by reference.
        /// </summary>
        private object? _lastService;

        /// <summary>
        /// Gets the number of executed actions.
        /// </summary>
        public int ActionCount => _actionCount;

        /// <summary>
        /// Gets the action names.
        /// </summary>
        public List<string>? Actions => _actions;

        /// <summary>
        /// Gets the last caller-owned reference.
        /// </summary>
        public object? LastService => _lastService;

        /// <summary>
        /// Executes a generated-style action that mutates private context state.
        /// </summary>
        /// <param name="name">The action name to record.</param>
        /// <param name="service">The caller-owned reference to retain.</param>
        public void ExecuteAction(string name, object service)
        {
            RecordAction(name);
            _lastService = service;
        }

        /// <summary>
        /// Records an action name in private context state.
        /// </summary>
        /// <param name="name">The action name to record.</param>
        private void RecordAction(string name)
        {
            _actions ??= [];
            _actions.Add(name);
            _actionCount++;
        }
    }
}
