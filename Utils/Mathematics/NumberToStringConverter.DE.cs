using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Utils.Mathematics
{
    public partial class NumberToStringConverter
    {
        public static NumberToStringConverter GermanNumbers { get; }
        = new NumberToStringConverter(
                group: 3,
                separator: " ",
                groupSeparator: "",
                zero: "null",
                minus: "minus *",
                groups: new Dictionary<int, Dictionary<long, string[]>>()
                {
                    { 1 ,
                        new ()
                        {
                            { 0, [ "" ] },
                            { 1, [ "ein"] },
                            { 2, [ "zwei"] },
                            { 3, [ "drei"] },
                            { 4, [ "vier"] },
                            { 5, [ "fünf"] },
                            { 6, [ "sechs" ] },
                            { 7, [ "sieben" ] },
                            { 8, [ "acht"] },
                            { 9, [ "neun"] }
                        }
                    },
                    { 2,
                        new () {
                            { 0, [ "", "*" ] },
                            { 1, [ "zehn", "*zehn" ] },
                            { 2, [ "zwanzig", "*undzwanzig"] },
                            { 3, [ "dreizig", "*unddreizig"] },
                            { 4, [ "vierzig", "*undvierzig"] },
                            { 5, [ "fünfzig", "*undfünfzig"] },
                            { 6, [ "sechzig", "*undsechzig"] },
                            { 7, [ "siebzig", "*undsiebzig"] },
                            { 8, [ "achtzich", "*undachtzich"] },
                            { 9, [ "neunzig", "*undneunzig"] }
                        }
                    },
                    { 3,
                        new ()
                        {
                            { 0, [""             , "*"             ]},
                            { 1, ["einhundert"         , "einhundert*"        ]},
                            { 2, ["zweihundert", "zweihundert*"   ]},
                            { 3, ["dreihundert", "dreihundert*"  ]},
                            { 4, ["vierhundert", "vierhundert*" ]},
                            { 5, ["fünfhundert", "fünfhundert*"   ]},
                            { 6, ["sechshundert", "sechshundert*"    ]},
                            { 7, ["siebenhundert", "siebenhundert*"   ]},
                            { 8, ["achthundert", "achthundert*"   ]},
                            { 9, ["neunhundert", "neunhundert*"   ]}
                        }
                    }
                },
                scale: new NumberScale(["", "tausend"], ["on(en)", "arde(n)"], firstLetterUppercase: true)
                {
                    TensPrefixes = new string[] {
                        "",
                        "(n)dezi",
                        "(ms)vingti",
                        "(ns)triginta",
                        "(ns)quadraginta",
                        "(ns)quinquaginta",
                        "(n)sexaginta",
                        "(n)septuaginta",
                        "(mxs)octoginta",
                        "nonaginta"
                    }.ToImmutableArray()
                },
                replacements: null,
                exceptions: new Dictionary<long, string>()
                {
                    { 11, "elf" },
                    { 12, "zwölf" },
                    { 16, "sechzehn" },
                    { 17, "siebzehn" },
                },
                adjustFunction: (s=> {
                    s = Regex.Replace(s, @"\bein (?<l>[A-Z])", "eine ${l}");
                    return s.EndsWith("ein") ? s + "s" : s;
                })
            );

    }
}
