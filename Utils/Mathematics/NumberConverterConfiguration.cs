using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Utils.Mathematics;

[XmlRoot]
public class Numbers
{
    [XmlElement(ElementName = "Language")]
    public List<LanguageType> Languages { get; set; }
}

// Correspond à l'élément 'Number' dans votre schéma
public class NumberType
{
    [XmlAttribute("value")]
    public long Value { get; set; }

    [XmlAttribute("string")]
    public string StringValue { get; set; }

    public override string ToString() => $"N : {Value} => {StringValue}";
}

// Correspond à l'élément 'NumberListType' dans votre schéma
[XmlRoot("NumberList")]
public class NumberListType
{
    [XmlElement("Number")]
    public List<NumberType> Numbers { get; set; }
}

public class DigitType
{
    [XmlAttribute("digit")]
    public long Digit { get; set; }

    [XmlAttribute("string")]
    public string StringValue { get; set; }

    [XmlAttribute("buildString")]
    public string BuildString { get; set; }

    public DigitType() { }

    public DigitType(int digit, string stringValue, string buildString = null)
    {
        Digit = digit;
        StringValue = stringValue;
        BuildString = buildString;
    }

    public static implicit operator DigitType(string[] values) {
        if (values.Length == 0) return null;
        var result = new DigitType();
        result.StringValue = values[0];
        if( values.Length > 1) result.BuildString = values[1];
        return result;
    }

    public override string ToString() => $"D : {Digit} => {StringValue}, {BuildString}";
}

public class DigitListType
{
    [XmlElement("Digit")]
    public List<DigitType> Digits { get; set; }
}

public class ReplacementsListType
{
    [XmlElement("Replacement")]
    public List<ReplacementType> Replacements { get; set; }
}
public class ReplacementType
{
    [XmlAttribute("oldValue")]
    public string OldValue { get; set; }

    [XmlAttribute("newValue")]
    public string NewValue { get; set; }
}

public class FractionType
{
    [XmlAttribute("digits")]
    public int Digits { get; set; }

    [XmlAttribute("string")]
    public string StringValue { get; set; }
}

public class FractionListType
{
    [XmlElement("Fraction")]
    public List<FractionType> Fractions { get; set; }
}

// Correspond à l'élément 'Language' dans votre schéma
public class LanguageType
{
    [XmlElement("Culture")]
    public List<string> Cultures { get; set; }

    [XmlAttribute("baseOn")]
    public string BaseOn { get; set; }

    [XmlAttribute("groupSize")]
    public int GroupSize { get; set; }

    [XmlAttribute("separator")]
    public string Separator { get; set; }

    [XmlAttribute("groupSeparator")]
    public string GroupSeparator { get; set; }

    [XmlAttribute("zero")]
    public string Zero { get; set; }

    [XmlAttribute("minus")]
    public string Minus { get; set; }

    [XmlAttribute("decimalSeparator")]
    public string DecimalSeparator { get; set; }

    [XmlAttribute("maxNumber")]
    public string MaxNumber { get; set; }

    [XmlElement(ElementName = "Groups")]
    public GroupsListType Groups { get; set; }

    [XmlElement(ElementName = "Exceptions")]
    public NumberListType Exceptions { get; set; }

    [XmlElement(ElementName = "NumberScale")]
    public NumberScaleType NumberScale { get; set; }

    [XmlElement(ElementName = "Replacements")]
    public ReplacementsListType Replacements { get; set; }

    [XmlElement(ElementName = "AdjustFunction")]
    public string AdjustFunction { get; set; }

    [XmlElement(ElementName = "Fractions")]
    public FractionListType Fractions { get; set; }
}

public class GroupType : DigitListType
{
    [XmlAttribute("level")]
    public int Level { get; set; }
}

public class GroupsListType
{
    [XmlElement("Group")]
    public List<GroupType> Groups { get; set; }
}

public class SuffixesType
{
    [XmlElement(ElementName = "Suffix")]
    public List<string> Values { get; set; }

}

public class StaticNamesType
{
    [XmlElement(ElementName = "Scale")]
    public List<NumberType> Scales { get; set; }
}

public class NumberScaleType
{
    [XmlAttribute("firstLetterUpperCase")]
    public bool FirstLetterUpperCase { get; set; }

    [XmlAttribute("voidGroup")]
    public string VoidGroup { get; set; }

    [XmlAttribute("groupSeparator")]
    public string GroupSeparator { get; set; }
    
    [XmlAttribute("startIndex")]
    public int StartIndex { get; set; } 

    [XmlElement(ElementName = "StaticNames")]
    public StaticNamesType StaticNames { get; set; }

    [XmlElement(ElementName = "Scale0Prefixes")]
    public DigitListType Scale0Prefixes { get; set; }
    [XmlElement(ElementName = "UnitsPrefixes")]
    public DigitListType UnitsPrefixes { get; set; }

    [XmlElement(ElementName = "TensPrefixes")]
    public DigitListType TensPrefixes { get; set; }

    [XmlElement(ElementName = "HundredsPrefixes")]
    public DigitListType HundredsPrefixes { get; set; }

    [XmlElement(ElementName = "Suffixes")]
    public SuffixesType Suffixes { get; set; }
}
