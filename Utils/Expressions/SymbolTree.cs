using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Expressions
{
    public class SymbolTree : IEnumerable<string>
    {
        internal IDictionary<char, SymbolLeaf> SubItems { get; } = new Dictionary<char, SymbolLeaf>();
        public SymbolTree() { }

        public void AddRange(params string[] symbols) => AddRange((IEnumerable<string>)symbols);
        public void AddRange(IEnumerable<string> symbols)
        {
            foreach (string symbol in symbols) Add(symbol);
        }

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

        public void Clear() => SubItems.Clear();
        public SymbolLeaf this[char character] => SubItems[character];
        internal bool TryGetValue(char c, out SymbolLeaf leaf) => SubItems.TryGetValue(c, out leaf);

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

        public IEnumerator<string> GetEnumerator() => Values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => Values.GetEnumerator();

        public IEnumerable<string> Values => SubItems.SelectMany(i => i.Value.Values);

    }

    public class SymbolLeaf
    {
        public char Character { get; }
        public string Value { get; internal set; }
        public bool IsFinal => SubItems is null || !SubItems.Any();
        internal IDictionary <char, SymbolLeaf> SubItems { get; } = new Dictionary<char, SymbolLeaf>();
        public bool TryFindNext(char c, out SymbolLeaf leaf) => SubItems.TryGetValue(c, out leaf);

        public IEnumerable<string> Values 
            => Value is not null 
            ? SubItems.SelectMany(i=>i.Value.Values).Prepend(Value)
            : SubItems.SelectMany(i => i.Value.Values);

        internal SymbolLeaf(string value, int index)
        {
            Character = value[index];
        }


    }

}
