using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Mathematics
{
    public partial class NumberToStringConverter
    {
        public static NumberToStringConverter EnglishAmericanNumbers()
            => new NumberToStringConverter(
                group: 3,
                separator: " ",
                zero: "zero",
                minus: "minus *",
                groups: new Dictionary<int, Dictionary<long, string[]>>()
                {
                    { 1 ,
                        new ()
                        {
                            { 0, [ "" ] },
                            { 1, [ "one"] },
                            { 2, [ "two"] },
                            { 3, [ "three"] },
                            { 4, [ "four"] },
                            { 5, [ "five"] },
                            { 6, [ "six" ] },
                            { 7, [ "seven" ] },
                            { 8, [ "height"] },
                            { 9, [ "nine"] }
                        }
                    },
                    { 2,
                        new () {
                            { 0, [ "", "*" ] },
                            { 1, [ "ten", "ten *" ] },
                            { 2, [ "twenty", "twenty *"] },
                            { 3, [ "thirty", "thirty *"] },
                            { 4, [ "forty", "forty *" ] },
                            { 5, [ "fifty", "fifty *" ] },
                            { 6, [ "sixty", "sixty *"] },
                            { 7, [ "seventy", "seventy *"] },
                            { 8, [ "eighty", "eighty *"] },
                            { 9, [ "ninety", "ninety *"] }
                        }
                    },
                    { 3,
                        new ()
                        {
                            { 0, [""             , "*"             ]},
                            { 1, ["one hundred"         , "one hundred and *"        ]},
                            { 2, ["two hundreds", "two hundreds and *"   ]},
                            { 3, ["three hundreds", "three hundreds and *"  ]},
                            { 4, ["four hundreds", "four hundreds and *" ]},
                            { 5, ["five hundreds", "five hundreds and *"   ]},
                            { 6, ["six hundreds", "six hundreds and *"    ]},
                            { 7, ["seven hundreds", "seven hundreds and *"   ]},
                            { 8, ["height hundreds", "height hundreds and *"   ]},
                            { 9, ["nine hundreds", "nine hundreds and *"   ]}
                        }
                    }
                },
                scale: new NumberScale(["", "thousand(s)"], ["on(s)"]),
                replacements: new Dictionary<string, string>() { },
                exceptions: new Dictionary<long, string>()
                {
                    { 11, "eleven" },
                    { 12, "twelve" },
                    { 13, "thirteen" },
                    { 14, "fourteen" },
                    { 15, "fifteen" },
                    { 16, "sixteen" },
                    { 17, "seventeen" },
                    { 18, "eighteen" },
                    { 19, "nineteen" },
                }
            );

    }
}
