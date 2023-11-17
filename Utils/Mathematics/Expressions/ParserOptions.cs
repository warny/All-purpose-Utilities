﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Expressions
{
    public class ParserOptions
    {
        public IReadOnlyDictionary<string, int> OperatorPriorityLevel { get; private set; }
        public IReadOnlyDictionary<Type, int> NumberTypeLevel { get; private set; }
        public IReadOnlyDictionary<string, Type> DefaultTypes { get; private set; }
        public IReadOnlyDictionary<string, Func<string, object>> NumberSuffixes { get; private set; }

        public ParserOptions()
        {
            OperatorPriorityLevel = new Dictionary<string, int>
            {
                { "(", 100 },
                { ")", 100 },
                { "[", 100 },
                { "]", 100 },
                { ".", 13 },
                { "?.", 13 },
                { "function()", 13 },
                { "index[]", 13 },
                { "++behind", 13 },
                { "--behind", 13 },
                { "new", 13 },
                { "typeof", 13 },
                { "checked", 13 },
                { "unchecked", 13 },
                { "->", 13 },
                { "++before", 12 },
                { "--before", 12 },
                { "+before", 12 },
                { "-before", 12 },
                { "!", 12 },
                { "~", 12 },
                { "convert()", 12 },
                { "sizeof", 12 },
                { "*", 11 },
                { "/", 11 },
                { "%", 11 },
                { "+", 10 },
                { "-", 10 },
                { "<<", 9 },
                { ">>", 9 },
                { ">", 8 },
                { "<", 8 },
                { ">=", 8 },
                { "<=", 8 },
                { "is", 8 },
                { "as", 8 },
                { "==", 7 },
                { "!=", 7 },
                { "&", 6 },
                { "^", 6 },
                { "|", 6 },
                { "&&", 5 },
                { "||", 5 },
                { "?", 5 },
                { "??", 4 },
                { "=", 4 },
                { "+=", 4 },
                { "-=", 4 },
                { "*=", 4 },
                { "/=", 4 },
                { "%=", 4 },
                { "&=", 4 },
                { "|=", 4 },
                { "^=", 4 },
                { ">>=", 4 },
                { "<<=", 4 }
            }.ToImmutableDictionary();

            NumberTypeLevel = new Dictionary<Type, int>
            {
                { typeof(byte), 1 },
                { typeof(short), 2 },
                { typeof(ushort), 3 },
                { typeof(int), 4 },
                { typeof(uint), 5 },
                { typeof(long), 6 },
                { typeof(ulong), 7 },
                { typeof(float), 8 },
                { typeof(double), 9 },
                { typeof(decimal), 10 }
            }.ToImmutableDictionary();

            DefaultTypes = new Dictionary<string, Type>
            {
                { "bool", typeof(bool) },
                { "byte", typeof(byte) },
                { "sbyte", typeof(sbyte) },
                { "char", typeof(char) },
                { "decimal", typeof(decimal) },
                { "double", typeof(double) },
                { "float", typeof(float) },
                { "int", typeof(int) },
                { "uint", typeof(uint) },
                { "long", typeof(long) },
                { "ulong", typeof(ulong) },
                { "object", typeof(object) },
                { "short", typeof(short) },
                { "ushort", typeof(ushort) },
                {  "string", typeof(string) },
            }.ToImmutableDictionary();

            NumberSuffixes = new Dictionary<string, Func<string, object>>(StringComparer.CurrentCultureIgnoreCase)
            {
                { "l", s => long.Parse(s) },
                { "m", s => decimal.Parse(s) },
                { "f", s => float.Parse(s) },
                { "d", s => double.Parse(s) },
            }.ToImmutableDictionary();
        }

    }
}
