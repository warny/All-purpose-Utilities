using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Utils.Collections;

/// <summary>
/// Represents a prefix tree (trie) structure for storing and retrieving symbols (strings) efficiently.
/// </summary>
public sealed class SymbolTree : IEnumerable<string>
{
	/// <summary>
	/// Internal dictionary mapping characters to <see cref="SymbolLeaf"/> at the root level.
	/// </summary>
	internal Dictionary<char, SymbolLeaf> SubItems { get; } = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="SymbolTree"/> class.
	/// </summary>
	public SymbolTree() { }

	/// <summary>
	/// Adds multiple symbols to the tree.
	/// </summary>
	/// <param name="symbols">An array of symbols to add.</param>
	public void AddRange(params string[] symbols)
		=> AddRange((IEnumerable<string>)symbols);

	/// <summary>
	/// Adds multiple symbols to the tree.
	/// </summary>
	/// <param name="symbols">A sequence of symbols to add.</param>
	public void AddRange(IEnumerable<string> symbols)
	{
		foreach (var symbol in symbols)
		{
			Add(symbol);
		}
	}

	/// <summary>
	/// Adds a single symbol to the tree.
	/// </summary>
	/// <param name="symbol">The symbol to add.</param>
	public void Add(string symbol)
	{
		if (string.IsNullOrEmpty(symbol))
			return;

		var subItems = SubItems;
		int length = symbol.Length;

		for (int i = 0; i < length; i++)
		{
			char c = symbol[i];

			// Attempt to retrieve existing leaf; otherwise create a new one
			if (!subItems.TryGetValue(c, out var leaf))
			{
				leaf = new SymbolLeaf(c);
				subItems[c] = leaf;
			}

			// Move deeper into the tree
			subItems = leaf.SubItems;

			// Mark the end of the symbol
			if (i == length - 1)
			{
				leaf.Value = symbol;
			}
		}
	}

	/// <summary>
	/// Removes all symbols from the tree.
	/// </summary>
	public void Clear()
		=> SubItems.Clear();

	/// <summary>
	/// Returns the <see cref="SymbolLeaf"/> associated with the specified character
	/// at the root level. Throws if the character is not present.
	/// </summary>
	/// <param name="character">The character to retrieve.</param>
	/// <returns>A <see cref="SymbolLeaf"/> instance.</returns>
	/// <exception cref="KeyNotFoundException">Thrown if the character is not found.</exception>
	public SymbolLeaf this[char character]
		=> SubItems[character];

	/// <summary>
	/// Tries to retrieve the <see cref="SymbolLeaf"/> for the given character
	/// at the root level.
	/// </summary>
	/// <param name="c">The character to look up.</param>
	/// <param name="leaf">When this method returns, contains the <see cref="SymbolLeaf"/> if found; otherwise, null.</param>
	/// <returns><see langword="true"/> if a leaf is found; <see langword="false"/> otherwise.</returns>
	public bool TryGetValue(char c, out SymbolLeaf leaf)
		=> SubItems.TryGetValue(c, out leaf);

	/// <summary>
	/// Determines whether a given symbol is contained in the tree.
	/// </summary>
	/// <param name="symbol">The symbol to check for.</param>
	/// <returns><see langword="true"/> if the symbol is found; <see langword="false"/> otherwise.</returns>
	public bool Contains(string symbol)
	{
		if (string.IsNullOrEmpty(symbol))
			return false;

		var subItems = SubItems;
		int length = symbol.Length;

		for (int i = 0; i < length; i++)
		{
			char c = symbol[i];

			if (!subItems.TryGetValue(c, out var leaf))
				return false;

			// Move deeper into the tree
			subItems = leaf.SubItems;

			// Check if this node marks the end of the symbol
			if (i == length - 1 && leaf.Value is not null)
				return true;
		}

		return false;
	}

	/// <summary>
	/// Returns an enumerator that iterates through all symbols in the tree.
	/// </summary>
	/// <returns>An enumerator of strings.</returns>
	public IEnumerator<string> GetEnumerator()
		=> Values.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator()
		=> GetEnumerator();

	/// <summary>
	/// Gets an enumerable collection of all symbols stored in the tree.
	/// </summary>
	public IEnumerable<string> Values
		=> SubItems.Values.SelectMany(leaf => leaf.Values);
}

/// <summary>
/// Represents a node (leaf) within the <see cref="SymbolTree"/>.
/// Stores one character and potentially references child nodes.
/// </summary>
public sealed class SymbolLeaf
{
	/// <summary>
	/// The character this leaf node represents.
	/// </summary>
	public char Character { get; }

	/// <summary>
	/// The full symbol if this leaf node terminates a string, otherwise <see langword="null"/>.
	/// </summary>
	public string Value { get; internal set; }

	/// <summary>
	/// A dictionary of child leaf nodes, keyed by character.
	/// </summary>
	internal Dictionary<char, SymbolLeaf> SubItems { get; } = new();

	/// <summary>
	/// Indicates whether this leaf has no children.
	/// </summary>
	public bool IsFinal => SubItems.Count == 0;

	/// <summary>
	/// Retrieves all symbols rooted at this node.
	/// This includes this node's symbol (if it terminates one) plus
	/// all symbols in child nodes.
	/// </summary>
	public IEnumerable<string> Values
		=> (Value is not null
			? [Value]
			: Enumerable.Empty<string>())
		   .Concat(SubItems.Values.SelectMany(child => child.Values));

	/// <summary>
	/// Initializes a new <see cref="SymbolLeaf"/> with the specified character.
	/// </summary>
	/// <param name="character">The character for which this leaf is responsible.</param>
	internal SymbolLeaf(char character)
	{
		Character = character;
	}

	/// <summary>
	/// Attempts to find a child leaf matching the specified character.
	/// </summary>
	/// <param name="c">The character to find.</param>
	/// <param name="leaf">When this method returns, contains the <see cref="SymbolLeaf"/> if found; otherwise, null.</param>
	/// <returns><see langword="true"/> if a child leaf is found, <see langword="false"/> otherwise.</returns>
	public bool TryFindNext(char c, out SymbolLeaf leaf)
		=> SubItems.TryGetValue(c, out leaf);
}
