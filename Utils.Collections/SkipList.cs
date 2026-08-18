using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Utils.Collections;

/// <summary>
/// Represents a skip list, which is a probabilistic data structure that allows for fast search, insertion, and deletion operations.
/// </summary>
/// <typeparam name="T">The type of elements in the skip list.</typeparam>
/// <remarks>
/// Instance members are not thread-safe. Callers must synchronize access when the same instance is
/// shared between threads, including access through lookup operations such as <see cref="Contains"/>
/// and <see cref="TryGet"/>, because lookups may maintain the adaptive index.
/// </remarks>
public class SkipList<T> : ICollection<T>
{
    private readonly IComparer<T> comparer;

    /// <summary>
    /// Maximum number of nodes we traverse at a given level before forcing a new
    /// structure node to be created in the upper level (if the next node has no Up link).
    /// </summary>
    private readonly int _threshold;

    /// <summary>
    /// Points to the leftmost element in the top level.
    /// Once we get to the bottom level (following Sub links), we can traverse horizontally
    /// to enumerate all items in ascending order.
    /// </summary>
    private Element _firstElement;

    /// <summary>
    /// Points to the rightmost element in the top level.
    /// Once we get to the bottom level (following Sub links), we can traverse horizontally
    /// leftwards or do other operations if needed.
    /// </summary>
    private Element _lastElement;

    /// <summary>
    /// Tracks changes to the logical bottom-level content, excluding adaptive index maintenance.
    /// </summary>
    private int _version;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkipList{T}"/> class
    /// using the default comparer and a threshold of 10.
    /// </summary>
    public SkipList() : this(Comparer<T>.Default, 10) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SkipList{T}"/> class
    /// with the specified threshold.
    /// </summary>
    /// <param name="threshold">
    /// The maximum distance at a given level before forcing the creation of a structure node.
    /// Must be &gt;= 2.
    /// </param>
    public SkipList(int threshold) : this(Comparer<T>.Default, threshold) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SkipList{T}"/> class
    /// with the specified comparer and threshold.
    /// </summary>
    /// <param name="comparer">The comparer to use when comparing elements.</param>
    /// <param name="threshold">
    /// The maximum number of nodes to traverse at a given level before forcing
    /// the creation of a structure node. Must be &gt;= 2.
    /// </param>
    public SkipList(IComparer<T> comparer, int threshold = 10)
    {
        if (threshold < 2)
            throw new ArgumentOutOfRangeException(nameof(threshold), "Density must be between 0.001 and 0.5.");

        this.comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
        this._threshold = threshold;
    }

    /// <summary>
    /// Gets the number of elements contained in the skip list.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the skip list is read-only (always <see langword="false"/>).
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Adds an element to the skip list at the appropriate position.
    /// If the list is empty, the element becomes the first and last element.
    /// Otherwise, we locate the insertion point and insert accordingly.
    /// If the element is inserted before <see cref="_firstElement"/>, it becomes the new first.
    /// If it's inserted after <see cref="_lastElement"/>, it becomes the new last.
    /// Otherwise, it is inserted in between two existing nodes at the bottom level.
    /// A comparer-equal element is treated as an existing element and is not inserted again.
    /// </summary>
    /// <param name="item">The element to add.</param>
    /// <returns><see langword="true"/> if the element was inserted; otherwise, <see langword="false"/>.</returns>
    public bool Add(T item)
    {
        var newElement = new Element(item);
        if (_firstElement is null)
        {
            _firstElement = newElement;
            _lastElement = newElement;
            Count = 1;
            _version++;
            return true;
        }

        var (elementBefore, elementAfter) = FindElementPosition(item);
        if (elementBefore is not null && elementBefore == elementAfter)
        {
            return false;
        }

        if (elementBefore is null)
        {
            // add the new element before the first element
            elementAfter.InsertBefore(newElement);

            // make it the first element in the object hierarchy
            for (var levelElement = elementAfter?.Up; levelElement != null; levelElement = levelElement.Up)
            {
                newElement = newElement.CreateUp(null, levelElement);
            }
            elementAfter?.RemoveUp();
            _firstElement = newElement;
        }
        else if (elementAfter is null)
        {
            //add the new element after the last element
            elementBefore.InsertAfter(newElement);
            // make it the last element in the object hierarchy
            for (var levelElement = elementBefore?.Up; levelElement != null; levelElement = levelElement.Up)
            {
                newElement = newElement.CreateUp(levelElement, null);
            }
            elementBefore?.RemoveUp();
            _lastElement = newElement;
        }
        else
        {
            elementBefore.InsertAfter(newElement);
        }
        Count++;
        _version++;
        return true;
    }

    /// <inheritdoc />
    void ICollection<T>.Add(T item) => Add(item);

    /// <summary>
    /// Removes all elements from the skip list.
    /// </summary>
    public void Clear()
    {
        if (Count == 0)
        {
            return;
        }
        _firstElement = null;
        _lastElement = null;
        Count = 0;
        _version++;
    }

    /// <summary>
    /// Determines whether the skip list contains a specific element.
    /// </summary>
    /// <param name="item">The element to locate in the skip list.</param>
    /// <returns><see langword="true"/> if the element is found; otherwise, <see langword="false"/>.</returns>
    public bool Contains(T item)
    {
        var (elementBefore, elementAfter) = FindElementPosition(item);
        return elementBefore is not null && elementAfter is not null && elementBefore == elementAfter;
    }

    /// <summary>
    /// Searches for an element that compares equal to <paramref name="item"/> and returns
    /// the stored instance. This is useful when the comparer considers only a subset of
    /// the element's fields (e.g. a key), allowing the caller to retrieve the full stored
    /// object rather than just a membership check.
    /// </summary>
    /// <param name="item">The element to locate.</param>
    /// <param name="found">
    /// When this method returns <see langword="true"/>, contains the stored element that
    /// matched <paramref name="item"/>; otherwise the default value.
    /// </param>
    /// <returns><see langword="true"/> if a matching element was found; otherwise, <see langword="false"/>.</returns>
    public bool TryGet(T item, out T found)
    {
        var (elementBefore, elementAfter) = FindElementPosition(item);
        if (elementBefore is not null && elementBefore == elementAfter)
        {
            found = elementBefore.Value;
            return true;
        }
        found = default!;
        return false;
    }

    /// <summary>
    /// Copies the elements of the skip list to an array, starting at a particular array index.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in the destination array.</param>
    public void CopyTo(T[] array, int arrayIndex)
    {
        foreach (var item in this)
        {
            array[arrayIndex++] = item;
        }
    }

    /// <summary>
    /// Removes a specific element from the skip list.
    /// </summary>
    /// <param name="item">The element to remove.</param>
    /// <returns><see langword="true"/> if the element was successfully removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove(T item)
    {
        var (elementBefore, elementAfter) = FindElementPosition(item);
        if (elementBefore is null || elementBefore != elementAfter) return false;
        var element = elementBefore;

        if (element.Previous is null && element.Next is not null)
        {
            var next = element.Next;
            for (var levelElement = element?.Up; levelElement != null; levelElement = levelElement.Up)
            {
                next = next.CreateUp(levelElement, levelElement.Next);
            }
            _firstElement = next;
        }
        else if (element.Next is null && element.Previous is not null)
        {
            var previous = element.Previous;
            for (var levelElement = element?.Up; levelElement != null; levelElement = levelElement.Up)
            {
                previous = previous.CreateUp(levelElement.Previous, levelElement);
            }
            _lastElement = previous;
        }
        element.Remove();
        while (_firstElement?.Sub is not null && _lastElement?.Sub is not null && _firstElement.Next == _lastElement)
        {
            _firstElement = _firstElement.Sub;
            _lastElement = _lastElement.Sub;
            _firstElement.Up.Remove();
            _lastElement.Up.Remove();
        }
        Count--;
        _version++;
        if (Count == 0)
        {
            _firstElement = null;
            _lastElement = null;
        }
        return true;
    }

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => new Enumerator(this);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Signals that the observable state of an item stored by reference changed without changing its node.
    /// </summary>
    internal void NotifyItemChanged() => _version++;

    /// <summary>
    /// Creates a deterministic, read-only signature of the adaptive topology for diagnostics and tests.
    /// </summary>
    /// <returns>A signature containing the bottom-level positions represented at each level.</returns>
    internal string GetStructureSignature()
    {
        if (_firstElement is null)
        {
            return "empty";
        }

        Element? bottomFirst = GetBottomFirstElement();
        Dictionary<Element, int> bottomPositions = new(ReferenceEqualityComparer.Instance);
        int position = 0;
        for (Element? node = bottomFirst; node is not null; node = node.Next)
        {
            bottomPositions.Add(node, position++);
        }

        StringBuilder signature = new();
        int level = 0;
        for (Element first = _firstElement; first is not null; first = first.Sub)
        {
            if (level++ > 0)
            {
                signature.Append('|');
            }
            for (Element node = first; node is not null; node = node.Next)
            {
                Element bottom = node;
                while (bottom.Sub is not null)
                {
                    bottom = bottom.Sub;
                }
                if (node != first)
                {
                    signature.Append(',');
                }
                signature.Append(bottomPositions[bottom]);
            }
        }
        return signature.ToString();
    }

    /// <summary>
    /// Validates the complete linked structure without changing it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The skip list violates a structural invariant.</exception>
    internal void ValidateInvariants()
    {
        if (Count < 0)
        {
            throw new InvalidOperationException("Count cannot be negative.");
        }

        if (Count == 0)
        {
            if (_firstElement is not null || _lastElement is not null)
            {
                throw new InvalidOperationException("An empty skip list must have null root boundaries.");
            }
            return;
        }

        if (_firstElement is null || _lastElement is null)
        {
            throw new InvalidOperationException("A non-empty skip list must have both root boundaries.");
        }
        if (_firstElement.Previous is not null)
        {
            throw new InvalidOperationException("Top-level first node has a Previous link.");
        }
        if (_lastElement.Next is not null)
        {
            throw new InvalidOperationException("Top-level last node has a Next link.");
        }

        Element first = _firstElement;
        Element last = _lastElement;
        HashSet<Element> upperLevel = null;
        HashSet<Element> allReachableNodes = new(ReferenceEqualityComparer.Instance);
        int depth = 0;

        while (true)
        {
            HashSet<Element> currentLevel = ValidateLevel(first, last, upperLevel, allReachableNodes, depth);
            if (first.Sub is null || last.Sub is null)
            {
                if (first.Sub is not null || last.Sub is not null)
                {
                    throw new InvalidOperationException("Left and right boundary depths differ.");
                }

                if (currentLevel.Any(node => node.Sub is not null))
                {
                    throw new InvalidOperationException("Bottom-level nodes must not have Sub links.");
                }
                if (currentLevel.Count != Count)
                {
                    throw new InvalidOperationException($"Bottom-level node count {currentLevel.Count} does not match Count {Count}.");
                }
                return;
            }

            if (currentLevel.Any(node => node.Sub is null))
            {
                throw new InvalidOperationException("An upper-level node has no Sub node.");
            }

            upperLevel = currentLevel;
            first = first.Sub;
            last = last.Sub;
            depth++;
        }
    }

    /// <summary>
    /// Validates one horizontal level and its links to the level immediately above it.
    /// </summary>
    /// <param name="first">The expected left boundary for the level.</param>
    /// <param name="last">The expected right boundary for the level.</param>
    /// <param name="upperLevel">The reachable nodes in the level immediately above, or <see langword="null"/> for the top.</param>
    /// <param name="allReachableNodes">The identity set of nodes already reached from the root boundaries.</param>
    /// <param name="depth">The zero-based depth used in diagnostic messages.</param>
    /// <returns>The identity set containing every node in the validated level.</returns>
    private HashSet<Element> ValidateLevel(Element first, Element last, HashSet<Element> upperLevel,
        HashSet<Element> allReachableNodes, int depth)
    {
        if (first.Previous is not null || last.Next is not null)
        {
            throw new InvalidOperationException($"Level {depth} boundary nodes are not horizontally bounded.");
        }

        HashSet<Element> level = new(ReferenceEqualityComparer.Instance);
        Element previous = null;
        for (Element node = first; node is not null; node = node.Next)
        {
            if (!level.Add(node))
            {
                throw new InvalidOperationException($"Horizontal cycle detected at level {depth}.");
            }
            if (!allReachableNodes.Add(node))
            {
                throw new InvalidOperationException("A node is reachable at more than one structural level.");
            }
            if (node.Next == node || node.Previous == node)
            {
                throw new InvalidOperationException($"A horizontal self-reference exists at level {depth}.");
            }
            if (node.Previous != previous || (previous is not null && previous.Next != node))
            {
                throw new InvalidOperationException($"Horizontal Next/Previous reciprocity is broken at level {depth}.");
            }
            if (previous is not null)
            {
                int comparison = comparer.Compare(previous.Value, node.Value);
                if (comparison == 0)
                {
                    throw new InvalidOperationException($"Comparer-equal duplicate detected at level {depth}.");
                }
                if (comparison > 0)
                {
                    throw new InvalidOperationException($"Level {depth} is not strictly ordered.");
                }
            }

            ValidateVerticalLinks(node, upperLevel, depth);
            previous = node;
        }

        if (previous != last)
        {
            throw new InvalidOperationException($"Right boundary is not reachable from the left boundary at level {depth}.");
        }
        if (upperLevel is not null && upperLevel.Any(node => !level.Contains(node.Sub)))
        {
            throw new InvalidOperationException($"An upper node points outside the reachable level {depth}.");
        }
        return level;
    }

    /// <summary>
    /// Validates a node's vertical reciprocity, reachability, and comparer-defined identity.
    /// </summary>
    /// <param name="node">The node whose vertical links are validated.</param>
    /// <param name="upperLevel">The reachable nodes in the level immediately above, or <see langword="null"/> for the top.</param>
    /// <param name="depth">The zero-based depth used in diagnostic messages.</param>
    private void ValidateVerticalLinks(Element node, HashSet<Element> upperLevel, int depth)
    {
        if (node.Up == node || node.Sub == node)
        {
            throw new InvalidOperationException($"A vertical self-reference exists at level {depth}.");
        }
        if (upperLevel is null)
        {
            if (node.Up is not null)
            {
                throw new InvalidOperationException("A top-level node has an Up link.");
            }
        }
        else if (node.Up is not null && (!upperLevel.Contains(node.Up) || node.Up.Sub != node))
        {
            throw new InvalidOperationException($"Vertical Up/Sub reciprocity or reachability is broken at level {depth}.");
        }
        if (node.Sub is not null)
        {
            if (node.Sub.Up != node)
            {
                throw new InvalidOperationException($"Vertical Sub/Up reciprocity is broken at level {depth}.");
            }
            if (comparer.Compare(node.Value, node.Sub.Value) != 0)
            {
                throw new InvalidOperationException($"A vertical tower contains comparer-distinct values at level {depth}.");
            }
        }
    }

    /// <summary>
    /// Finds the position where 'value' should be inserted:
    /// (ElementBefore, ElementAfter). If they are the same, it means we've found
    /// a match for 'value'. If 'ElementBefore' is null =&gt; insertion is at the front.
    /// If 'ElementAfter' is null =&gt; insertion is at the end.
    /// </summary>
    private (Element ElementBefore, Element ElementAfter) FindElementPosition(T value)
    {
        Element startElement = _firstElement;
        Element endElement = _lastElement;
        List<MaintenancePlan>? maintenancePlans = null;

        while (true)
        {
            (Element before, Element after, MaintenancePlan? plan) = FindElementPositionAtLevel(startElement, endElement, value);
            if (plan is not null)
            {
                maintenancePlans ??= [];
                maintenancePlans.Add(plan);
            }
            startElement = before;
            endElement = after;
            if (startElement?.Sub == null && endElement?.Sub == null)
            {
                ApplyMaintenancePlans(maintenancePlans);
                return (startElement, endElement);
            }
            startElement = startElement?.Sub;
            endElement = endElement?.Sub;
        }

    }

    /// <summary>
    /// Finds, at the current level (from 'startElement' to 'endElement'),
    /// the two nodes that sandwich 'value'. If 'value' matches one node's Value,
    /// that node is returned in both 'ElementBefore' and 'ElementAfter'.
    /// 
    /// Along the way, if we traverse more than 'threshold' nodes
    /// without encountering a skip node, we create a new skip node in the upper level.
    /// </summary>
    private (Element ElementBefore, Element ElementAfter, MaintenancePlan? Plan) FindElementPositionAtLevel(Element startElement, Element endElement, T value)
    {
        if (startElement == null) return (startElement, endElement, null);

        Element currentElement;
        Element previousElement = startElement?.Previous;
        int counter = 0;
        List<Element> candidates = null;

        for (currentElement = startElement; currentElement != null; currentElement = currentElement.Next)
        {
            if (currentElement.Up is not null) counter = 0;
            if (counter > _threshold && currentElement.Next is not null && currentElement.Next?.Up is null)
            {
                candidates ??= [];
                candidates.Add(currentElement);
                counter = 0;
            }
            int comparison = comparer.Compare(value, currentElement.Value);
            if (comparison == 0) return (currentElement, currentElement, CreateMaintenancePlan());
            if (comparison < 0)
            {
                return (currentElement.Previous, currentElement, CreateMaintenancePlan());
            }
            previousElement = currentElement;
            counter++;
        }
        return (previousElement, null, CreateMaintenancePlan());

        // Materializes the deferred promotion candidates discovered at the current level.
        MaintenancePlan? CreateMaintenancePlan()
            => candidates is null ? null : new MaintenancePlan(startElement, endElement, candidates);
    }

    /// <summary>
    /// Commits adaptive promotions only after every comparison required by the lookup has succeeded.
    /// </summary>
    /// <param name="plans">The ordered per-level plans, or <see langword="null"/> when no maintenance is required.</param>
    private void ApplyMaintenancePlans(List<MaintenancePlan>? plans)
    {
        if (plans is null)
        {
            return;
        }

        foreach (MaintenancePlan plan in plans)
        {
            Element lastUpperNode = plan.StartElement.Up;
            foreach (Element candidate in plan.Candidates)
            {
                lastUpperNode = CreateNewSkipNode(plan.StartElement, plan.EndElement, candidate, lastUpperNode);
            }
        }
    }

    private Element CreateNewSkipNode(Element startElement, Element endElement, Element currentElement, Element lastUpperNode)
    {
        Element previousUp, nextUp;

        if (startElement.Up is null)
        {
            // create new upper level
            previousUp = _firstElement.CreateUp(null, null);
            _firstElement = previousUp;
            nextUp = _lastElement.CreateUp(null, null);
            _lastElement = nextUp;
            _firstElement.InsertAfter(_lastElement);
        }
        else
        {
            // use lastUpperNode as left anchor to avoid overwriting links from previously created skip nodes
            previousUp = lastUpperNode ?? startElement.Up;
            nextUp = endElement.Up;
        }
        return currentElement.CreateUp(previousUp, nextUp);
    }

    /// <summary>
    /// Gets the first node at the content-bearing bottom level.
    /// </summary>
    /// <returns>The first bottom-level node, or <see langword="null"/> for an empty list.</returns>
    private Element? GetBottomFirstElement()
    {
        var element = _firstElement;
        while (element?.Sub is not null) element = element.Sub;
        return element;
    }

    /// <summary>
    /// Describes the promotions discovered while traversing one structural level.
    /// </summary>
    private sealed class MaintenancePlan
    {
        /// <summary>Initializes a per-level adaptive maintenance plan.</summary>
        /// <param name="startElement">The left search boundary at this level.</param>
        /// <param name="endElement">The right search boundary at this level.</param>
        /// <param name="candidates">Nodes to promote in traversal order.</param>
        public MaintenancePlan(Element startElement, Element endElement, List<Element> candidates)
        {
            StartElement = startElement;
            EndElement = endElement;
            Candidates = candidates;
        }

        /// <summary>Gets the left search boundary.</summary>
        public Element StartElement { get; }

        /// <summary>Gets the right search boundary.</summary>
        public Element EndElement { get; }

        /// <summary>Gets nodes to promote in traversal order.</summary>
        public List<Element> Candidates { get; }
    }

    /// <summary>
    /// Enumerates bottom-level content and fails fast when observable content changes.
    /// </summary>
    private sealed class Enumerator : IEnumerator<T>
    {
        private readonly SkipList<T> _owner;
        private readonly int _capturedVersion;
        private Element? _next;
        private T _current = default!;
        private bool _hasCurrent;

        /// <summary>Initializes an enumerator and captures the owner's current content version.</summary>
        /// <param name="owner">The list to enumerate.</param>
        public Enumerator(SkipList<T> owner)
        {
            _owner = owner;
            _capturedVersion = owner._version;
            _next = owner.GetBottomFirstElement();
        }

        /// <inheritdoc />
        public T Current => _hasCurrent ? _current : throw new InvalidOperationException("The enumerator is not positioned on an element.");

        /// <inheritdoc />
        object IEnumerator.Current => Current!;

        /// <inheritdoc />
        public bool MoveNext()
        {
            EnsureVersion();
            if (_next is null)
            {
                _hasCurrent = false;
                _current = default!;
                return false;
            }
            _current = _next.Value;
            _next = _next.Next;
            _hasCurrent = true;
            return true;
        }

        /// <inheritdoc />
        public void Reset()
        {
            EnsureVersion();
            _next = _owner.GetBottomFirstElement();
            _current = default!;
            _hasCurrent = false;
        }

        /// <inheritdoc />
        public void Dispose() { }

        /// <summary>Throws when the owner's observable content changed after this enumerator was created.</summary>
        private void EnsureVersion()
        {
            if (_capturedVersion != _owner._version)
            {
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
            }
        }
    }

    /// <summary>
    /// Represents a node in the skip list, with horizontal links (Previous, Next)
    /// and vertical links (Up, Sub).
    /// </summary>
    private sealed class Element
    {
        public T Value { get; }
        public Element Next { get; private set; } = null;
        public Element Previous { get; private set; } = null;
        public Element Sub { get; private set; } = null;
        public Element Up { get; private set; } = null;

        public Element(T value)
        {
            this.Value = value;
        }

        private Element(T value, Element sub)
        {
            this.Value = value;
            this.Sub = sub;
        }

        public void Remove()
        {
            Up?.Remove();
            var previous = Previous;
            var next = Next;
            if (Previous is not null) Previous.Next = next;
            if (Next is not null) Next.Previous = previous;
            if (Sub is not null) Sub.Up = null;
        }

        public void RemoveUp()
        {
            if (Previous is not null && Next is not null)
            {
                Up?.Remove();
            }
        }

        public void InsertAfter(Element element)
        {
            element.Next = this.Next;
            if (element.Next is not null) element.Next.Previous = element;
            this.Next = element;
            element.Previous = this;
        }

        public void InsertBefore(Element element)
        {
            element.Previous = this.Previous;
            if (element.Previous is not null) element.Previous.Next = element;
            this.Previous = element;
            element.Next = this;
        }

        public Element CreateUp(Element elementBefore, Element elementAfter)
        {
            if (Up is not null) return Up;
            Element element = new(Value, this);
            this.Up = element;
            element.Previous = elementBefore;
            if (elementBefore is not null) elementBefore.Next = element;
            element.Next = elementAfter;
            if (elementAfter is not null) elementAfter.Previous = element;
            return element;
        }

        public override string ToString() => $"{{ Value = {Value}, Up = {(Up == null ? "null" : Up.Value)}, Sub = {(Sub == null ? "null" : Sub.Value)}, Prev = {(Previous == null ? "null" : Previous.Value)}, Next = {(Next == null ? "null" : Next.Value)}}}";
    }
}
