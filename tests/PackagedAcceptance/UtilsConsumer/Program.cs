using System.Globalization;
using System.Text;
using Utils.Dates;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
if (Encoding.GetEncoding(1252).GetBytes("é").Length != 1)
{
    throw new InvalidOperationException("The code-pages dependency is not operational.");
}

DateTime start = new(2023, 3, 15);
if (start.Calculate("FM+1J", new CultureInfo("fr-FR")) != new DateTime(2023, 4, 1))
{
    throw new InvalidOperationException("The embedded French DateFormula configuration was not loaded correctly.");
}
if (start.Calculate("EM+1D", new CultureInfo("en-US")) != new DateTime(2023, 4, 1))
{
    throw new InvalidOperationException("The embedded English DateFormula configuration was not loaded correctly.");
}

Span<int> values = stackalloc int[] { 1, 2, 3 };
if (values.Length != 3 || Array.Empty<int>().Length != 0)
{
    throw new InvalidOperationException("Representative array and Span operations failed.");
}
Console.WriteLine("omy.Utils packaged consumer passed.");
