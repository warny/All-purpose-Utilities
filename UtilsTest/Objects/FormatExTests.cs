using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Utils.Format;
using Utils.Randomization;

namespace UtilsTest.Objects
{
    [TestClass]
    public class StringFormatTests
    {
        [TestMethod]
        public void StringFormatTest()
        {
            var format = StringFormat.Create<Func<int, int, string>>("ceci est {var1} test de {var2,3:X2} formatage", "var1", "var2");

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
            Random r = new Random();

            var formatter = new CustomFormatter(CultureInfo.InvariantCulture);
            formatter.AddFormatter("fluc", (string s) => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s));
            formatter.AddFormatter("uc", (string s) => s.ToUpper());
            formatter.AddFormatter("lc", (string s) => s.ToLower());

            var result = new {
                Field1 = r.RandomString(5, 10),
                Field2 = r.Next(-100, 100),
                Field3 = new DateTime(r.Next(2000, 2100), 1, 1).AddDays(r.Next(0,365)),
                Field3_1 = r.RandomFrom(null, r.RandomString(5, 10)),
                Field4 = r.RandomString(5, 10)
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

            foreach (var field in fields.Select ((Field, Index) => (Field, Index)))
            {
                dataRecord.Setup(dr => dr[field.Index]).Returns(field.Field.Value);
                dataRecord.Setup(dr => dr.GetFieldType(field.Index)).Returns(field.Field.Type);
                dataRecord.Setup(dr => dr.GetName(field.Index)).Returns(field.Field.Name);
            }


            var dr = dataRecord.Object;

            var format = StringFormat.Create("{field1,-10} : {field2,8:X2} => {field3:yyyy-MM-dd} {field3_1} {{{field3_1:fluc}}} {field4}", formatter, CultureInfo.InvariantCulture, dr);
            var expected = string.Format(formatter, "{0,-10} : {1,8:X2} => {2:yyyy-MM-dd} {3} {{{3:fluc}}} {4}", dr[0], dr[1], dr[2], dr[3], dr[4]);

            Assert.AreEqual(expected, format(dr));
        }

        private struct SimpleHandler
        {
            private readonly StringBuilder _sb;
            public SimpleHandler(int literalLength, int formattedCount, StringBuilder sb)
            {
                _sb = sb;
            }
            public void AppendLiteral(string s) => _sb.Append(s);
            public void AppendFormatted<T>(T value) => _sb.Append(value);
            public override string ToString() => _sb.ToString();
        }

        [TestMethod]
        public void CustomHandlerTest()
        {
            var format = StringFormat.Create<Func<string, string>, SimpleHandler>("Value: {text}", "text");
            Assert.AreEqual("Value: hello", format("hello"));
        }
    }
}
