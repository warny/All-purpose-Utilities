using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Objects;

public class CustomFormatter : NullFormatter
{

    private readonly IDictionary<Type, IDictionary <string, Func<object, IFormatProvider, string>>> typeFormatters = new Dictionary<Type, IDictionary<string, Func<object, IFormatProvider, string>>>();

    public CustomFormatter() { }
    public CustomFormatter (CultureInfo CultureInfo) : base (CultureInfo) { }

    public void AddFormatter<T>(string format, Func<T, string> formatter)
    {
        if (!typeFormatters.TryGetValue(typeof(T), out var formatters))
        {
            formatters = new Dictionary<string, Func<object, IFormatProvider, string>>();
        }
        formatters[format] = (object o, IFormatProvider formatProvider) => formatter((T)o);
    }

    public void AddFormatter<T>(string format, Func<T, IFormatProvider, string> formatter)
    {
        if (!typeFormatters.TryGetValue(typeof(T), out var formatters))
        {
            formatters = new Dictionary<string, Func<object, IFormatProvider, string>>();
        }
        formatters[format] = (object o, IFormatProvider formatProvider) => formatter((T)o, formatProvider);
    }

    public override string Format(string format, object arg, IFormatProvider formatProvider)
    {
        formatProvider ??= CultureInfo;
        if (arg is null) return "";
        if (typeFormatters.TryGetValue(arg.GetType(), out var formatters) && formatters.TryGetValue(format, out var formatter))
        {
            return formatter(arg, formatProvider);
        }
        return base.Format(format, arg, formatProvider);
    }

}


public class NullFormatter : IFormatProvider, ICustomFormatter
{
public static NullFormatter Default { get; } = new NullFormatter();

public CultureInfo CultureInfo { get; }

public NullFormatter() : this (CultureInfo.CurrentCulture) { }
public NullFormatter(CultureInfo cultureInfo)
{
    CultureInfo = cultureInfo;
}

public object GetFormat(Type formatType)
{
    if (formatType == typeof(ICustomFormatter))
        return this;
    else
        return null;
}

public virtual string Format(string format, object arg, IFormatProvider formatProvider) => arg switch
{
    IFormattable formattable => formattable.ToString(format, formatProvider ?? CultureInfo),
    _ => arg?.ToString() ?? ""
};


}
