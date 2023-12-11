using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Utils.Collections;

namespace Utils.Expressions
{
    public class Tokenizer
    {
        public string Content { get; }
        public SymbolTree Symbols { get; }
        public char[] WhiteSpaces { get; }

        public IEnumerable<TryReadToken> TokenReaders { get; }
        public IEnumerable<StringTransformer> StringTransformers { get; }

        public ITokenizerPosition Position => position;

        private TokenizerPosition position;

        public string DefineString { get; private set; }

        public Tokenizer(string content, IBuilder builder)
        {
            ResetPosition();
            Symbols = [.. builder.Symbols];
            WhiteSpaces = builder.SpaceSymbols;
            TokenReaders = builder.TokenReaders;
            StringTransformers = builder.StringTransformers;
            this.Content = content;
        }

        public string ReadToken(bool isIgnoreWhiteSpace = true)
        {
            if (Read(true, isIgnoreWhiteSpace))
            {
                var token = this.Content.Substring(this.Position.Index, this.Position.Length);
                foreach (var stringTransformer in StringTransformers)
                {
                    if (stringTransformer(token, out string result))
                    {
                        DefineString = result;
                        break;
                    }
                }
                return token;
            }
            return null;
        }

        public bool ReadSymbol(string symbol, bool throwExceptionIfError = true)
        {
            if (this.position.Index + this.position.Length >= this.Content.Length) return false;
            // Skip whitespace characters
            while (char.IsWhiteSpace(this.Content[this.position.Index + this.position.Length]))
            {
                this.position.Length++;
            }

            if (throwExceptionIfError)
            {
                // Check if the next part of the content matches the expected symbol, and if not, throw an exception
                ParseException.Assert(this.Content.Substring(this.position.Index + this.position.Length, symbol.Length), symbol, this.position.Index);
            }
            else if (this.position.Index + this.position.Length + symbol.Length >= this.Content.Length)
            {
                return false;
            }
            else if (this.Content.Substring(this.position.Index + this.position.Length, symbol.Length) != symbol)
            {
                return false;
            }

            // Move the reading position to the end of the symbol
            this.position.Index += this.position.Length;
            this.position.Length = symbol.Length;
            return true;
        }

        public string PeekToken()
        {
            PushPosition();
            string str = ReadToken(true);
            PopPosition();
            return str;
        }

        #region private methods
        private bool Read(bool isBuildDefineString, bool isIgnoreWhiteSpace)
        {
            // Advance the cursor and reset the length to 1
            this.position.Index += this.position.Length;
            this.position.Length = 1;

            // If the cursor has reached the end of the content, reset it to the beginning and return false
            if (this.position.Index == this.Content.Length)
            {
                this.position.Index = 0;
                return false;
            }

            // If 'isIgnoreWhiteSpace' is true and the current character is a white space, skip it and continue
            if (isIgnoreWhiteSpace && WhiteSpaces.Contains(this.Content[this.position.Index]))
            {
                return Read(isBuildDefineString, isIgnoreWhiteSpace);
            }

            // Get the current character
            char c = this.Content[this.position.Index];

            foreach (var tokenReader in TokenReaders)
            {
                if(tokenReader(this.Content, this.position.Index, out var tokenLength))
                {
                    this.position.Length = tokenLength;
                    return true;
                }
            }

            // Get the next character
            char nextInner;
            if (!TryGetNextChar(false, out nextInner))
            {
                // If the end of the content is reached, return true
                return true;
            }

            if (Symbols.TryGetValue(c, out SymbolLeaf leaf))
            {
                SymbolLeaf currentLeaf = leaf;
                while (!leaf.IsFinal)
                {
                    if (!TryGetNextChar(false, out nextInner)) break;
                    if (!leaf.TryFindNext(nextInner, out leaf)) break;
                    if (leaf.Value is not null) currentLeaf = leaf;
                }
                if (currentLeaf.Value is not null)
                {
                    position.Length = currentLeaf.Value.Length;
                    return true;
                }
            }

            throw new ParseUnknownException(c.ToString(), this.position.Index);
        }

        private bool TryGetNextChar(bool ignoreWhiteSpace, out char cNext)
        {
            cNext = '\0';
            for (int i = 0; i < int.MaxValue; i++)
            {
                if (this.position.Index + this.position.Length + i >= this.Content.Length)
                {
                    return false;
                }
                cNext = this.Content[this.position.Index + this.position.Length];
                if ((!ignoreWhiteSpace) || (!char.IsWhiteSpace(cNext)))
                {
                    break;
                }
            }
            return true;
        }

        #endregion


        #region Code Parser Position

        private Stack<TokenizerPosition> SavedPositions { get; } = new Stack<TokenizerPosition>();

        public void PushPosition()
        {
            SavedPositions.Push(new TokenizerPosition(this.position.Index, this.position.Length));
        }

        public void PopPosition()
        {
            var myPosition = SavedPositions.Pop();
            this.position = myPosition;
        }

        public void DiscardPosition()
        {
            SavedPositions.Pop();
        }

        public void ResetPosition()
        {
            SavedPositions.Clear();
            position = new TokenizerPosition(0, 0);
        }

        private class TokenizerPosition(int index, int length) : ITokenizerPosition
        {
            public int Index { get; set; } = index;
            public int Length { get; set; } = length;

            public override string ToString() => $"Index: {Index}, Length: {Length}";
        }

        #endregion
    }

    public interface ITokenizerPosition
    {
        public int Index { get; }
        public int Length { get; }
    }
}