using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Utils.Expressions.CSyntax.Runtime;
using Utils.Format;

namespace UtilsTest.Objects
{
    [TestClass]
    public class StringFormatTests
    {
        [TestMethod]
        public void StringFormatTest()
        {
            IStringFormatBuilder builder = new StringFormatBuilder(new CSyntaxExpressionCompiler());
            var format = builder.Create<Func<int, int, string>>("ceci est {var1} test de {var2,3:X2} formatage", "var1", "var2");

            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    Assert.AreEqual($"ceci est {i} test de {j,3:X2} formatage", format(i, j));
                }
            }
        }

        [TestMethod]
        public void StringFormatFromIDataRecordTest()
        {
            var formatter = new CustomFormatter(CultureInfo.InvariantCulture);
            formatter.AddFormatter("fluc", (string s) => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s));
            formatter.AddFormatter("uc", (string s) => s.ToUpperInvariant());
            formatter.AddFormatter("lc", (string s) => s.ToLowerInvariant());

            var result = new
            {
                Field1 = "alpha",
                Field2 = 42,
                Field3 = new DateTime(2026, 7, 19),
                Field3_1 = "mixed case",
                Field4 = "omega"
            };


            var fields = new (Type Type, string Name, Func<object> Value)[] {
                (typeof(string), "field1", () => result.Field1),
                (typeof(int), "field2", () => result.Field2),
                (typeof(DateTime), "field3", () => result.Field3),
                (typeof(string), "field3_1", () => result.Field3_1),
                (typeof(string), "field4", () => result.Field4),
            };

            Mock<IDataRecord> dataRecord = new Mock<IDataRecord>();
            dataRecord.Setup(dr => dr.FieldCount).Returns(fields.Count);

            foreach (var field in fields.Select((Field, Index) => (Field, Index)))
            {
                dataRecord.Setup(dr => dr[field.Index]).Returns(field.Field.Value);
                dataRecord.Setup(dr => dr.GetFieldType(field.Index)).Returns(field.Field.Type);
                dataRecord.Setup(dr => dr.GetName(field.Index)).Returns(field.Field.Name);
            }


            var dr = dataRecord.Object;

            IStringFormatBuilder builder = new StringFormatBuilder(new CSyntaxExpressionCompiler());
            var format = builder.Create("{field1,-10} : {field2,8:X2} => {field3:yyyy-MM-dd} {field3_1} {{{field3_1:fluc}}} {field4}", formatter, CultureInfo.InvariantCulture, dr);
            var expected = string.Format(formatter, "{0,-10} : {1,8:X2} => {2:yyyy-MM-dd} {3} {{{3:fluc}}} {4}", dr[0], dr[1], dr[2], dr[3], dr[4]);

            Assert.AreEqual(expected, format(dr));
        }

        /// <summary>
        /// Test interpolated string handler that appends all output to an injected <see cref="StringBuilder"/>.
        /// </summary>
        private struct SimpleHandler
        {
            private readonly StringBuilder _sb;

            /// <summary>
            /// Initializes a new instance of the <see cref="SimpleHandler"/> struct.
            /// </summary>
            /// <param name="literalLength">The literal length requested by interpolated-string construction.</param>
            /// <param name="formattedCount">The formatted value count requested by interpolated-string construction.</param>
            /// <param name="sb">The builder that receives literal and formatted output.</param>
            public SimpleHandler(int literalLength, int formattedCount, StringBuilder sb)
            {
                _sb = sb;
            }

            /// <summary>
            /// Appends a literal segment to the backing builder.
            /// </summary>
            /// <param name="s">The literal segment to append.</param>
            public void AppendLiteral(string s) => _sb.Append(s);

            /// <summary>
            /// Appends a formatted value to the backing builder.
            /// </summary>
            /// <typeparam name="T">The formatted value type.</typeparam>
            /// <param name="value">The formatted value to append.</param>
            public void AppendFormatted<T>(T value) => _sb.Append(value);

            /// <summary>
            /// Returns the accumulated formatted text.
            /// </summary>
            /// <returns>The accumulated formatted text.</returns>
            public override string ToString() => _sb.ToString();
        }

        [TestMethod]
        public void CustomHandlerTest()
        {
            IStringFormatBuilder builder = new StringFormatBuilder(new CSyntaxExpressionCompiler());
            var format = builder.Create<Func<string, string>, SimpleHandler>("Value: {text}", "text");
            Assert.AreEqual("Value: hello", format("hello"));
        }
    }
}
