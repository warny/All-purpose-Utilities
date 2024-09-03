using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Utils.Collections;

/// <summary>
/// Represents a tree structure that stores symbols (strings) in a way that allows for efficient prefix searching.
/// </summary>
public class SymbolTree : IEnumerable<string>
{
	/// <summary>
	/// Gets the dictionary of character to <see cref="SymbolLeaf"/> mappings for the current level of the tree.
	/// </summary>
	internal IDictionary<char, SymbolLeaf> SubItems { get; } = new Dictionary<char, SymbolLeaf>();

	/// <summary>
	/// Initializes a new instance of the <see cref="SymbolTree"/> class.
	/// </summary>
	public SymbolTree() { }

	/// <summary>
	/// Adds a range of symbols (strings) to the tree.
	/// </summary>
	/// <param name="symbols">The symbols to add.</param>
	public void AddRange(params string[] symbols) => AddRange((IEnumerable<string>)symbols);

	/// <summary>
	/// Adds a range of symbols (strings) to the tree.
	/// </summary>
	/// <param name="symbols">The symbols to add.</param>
	public void AddRange(IEnumerable<string> symbols)
	{
		foreach (string symbol in symbols) Add(symbol);
	}

	/// <summary>
	/// Adds a single symbol (string) to the tree.
	/// </summary>
	/// <param name="symbol">The symbol to add.</param>
	public void Add(string symbol)
	{
		var subItems = SubItems;
		for (int index = 0; index < symbol.Length; index++)
		{
			if (!subItems.TryGetValue(symbol[index], out SymbolLeaf leaf))
			{
				leaf = new SymbolLeaf(symbol, index);
				subItems[leaf.Character] = leaf;
			}
			subItems = leaf.SubItems;
			if (symbol.Length == index + 1) leaf.Value = symbol;
		}
	}

	/// <summary>
	/// Removes all symbols from the tree.
	/// </summary>
	public void Clear() => SubItems.Clear();

	/// <summary>
	/// Gets the <see cref="SymbolLeaf"/> associated with the specified character at the current level of the tree.
	/// </summary>
	/// <param name="character">The character whose <see cref="SymbolLeaf"/> to get.</param>
	/// <returns>The <see cref="SymbolLeaf"/> associated with the specified character.</returns>
	public SymbolLeaf this[char character] => SubItems[character];

	/// <summary>
	/// Attempts to get the <see cref="SymbolLeaf"/> associated with the specified character.
	/// </summary>
	/// <param name="c">The character to search for.</param>
	/// <param name="leaf">When this method returns, contains the <see cref="SymbolLeaf"/> associated with the specified character, if found; otherwise, null.</param>
	/// <returns><see langword="true"/> if the <see cref="SymbolLeaf"/> was found; otherwise, <see langword="false"/>.</returns>
	internal bool TryGetValue(char c, out SymbolLeaf leaf) => SubItems.TryGetValue(c, out leaf);

	/// <summary>
	/// Determines whether the tree contains the specified symbol.
	/// </summary>
	/// <param name="symbol">The symbol to search for.</param>
	/// <returns><see langword="true"/> if the tree contains the specified symbol; otherwise, <see langword="false"/>.</returns>
	public bool Contains(string symbol)
	{
		var subItems = SubItems;
		SymbolLeaf leaf = null;
		for (int index = 0; index < symbol.Length; index++)
		{
			if (!subItems.TryGetValue(symbol[index], out leaf)) return false;
			subItems = leaf.SubItems;
		}
		return leaf?.Value is not null;
	}

	/// <summary>
	/// Returns an enumerator that iterates through all the symbols stored in the tree.
	/// </summary>
	/// <returns>An enumerator for the symbols in the tree.</returns>
	public IEnumerator<string> GetEnumerator() => Values.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	/// <summary>
	/// Gets all the symbols stored in the tree.
	/// </summary>
	public IEnumerable<string> Values => SubItems.SelectMany(i => i.Value.Values);
}

/// <summary>
/// Represents a leaf node in the <see cref="SymbolTree"/>, which contains a single character and may have child nodes.
/// </summary>
public class SymbolLeaf
{
	/// <summary>
	/// Gets the character represented by this leaf node.
	/// </summary>
	public char Character { get; }

	/// <summary>
	/// Gets or sets the complete symbol (string) if this leaf node represents the end of a symbol.
	/// </summary>
	public string Value { get; internal set; }

	/// <summary>
	/// Gets a value indicating whether this leaf node is a terminal node (i.e., it has no children).
	/// </summary>
	public bool IsFinal => SubItems is null || !SubItems.Any();

	/// <summary>
	/// Gets the dictionary of character to <see cref="SymbolLeaf"/> mappings for the child nodes.
	/// </summary>
	internal IDictionary<char, SymbolLeaf> SubItems { get; } = new Dictionary<char, SymbolLeaf>();

	/// <summary>
	/// Attempts to find the next <see cref="SymbolLeaf"/> in the sequence, based on the specified character.
	/// </summary>
	/// <param name="c">The character to search for.</param>
	/// <param name="leaf">When this method returns, contains the <see cref="SymbolLeaf"/> associated with the specified character, if found; otherwise, null.</param>
	/// <returns><see langword="true"/> if the next <see cref="SymbolLeaf"/> was found; otherwise, <see langword="false"/>.</returns>
	public bool TryFindNext(char c, out SymbolLeaf leaf) => SubItems.TryGetValue(c, out leaf);

	/// <summary>
	/// Gets all the symbols stored in the subtree rooted at this leaf node.
	/// </summary>
	public IEnumerable<string> Values
		=> Value is not null
			? SubItems.SelectMany(i => i.Value.Values).Prepend(Value)
			: SubItems.SelectMany(i => i.Value.Values);

	/// <summary>
	/// Initializes a new instance of the <see cref="SymbolLeaf"/> class with the specified value and character index.
	/// </summary>
	/// <param name="value">The symbol (string) associated with this leaf node.</param>
	/// <param name="index">The index of the character in the symbol.</param>
	internal SymbolLeaf(string value, int index)
	{
		Character = value[index];
	}
}
