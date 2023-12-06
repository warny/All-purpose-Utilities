using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Mathematics
{
    public partial class NumberToStringConverter
    {
        public static NumberToStringConverter EnglishNumbers { get; }
        = new NumberToStringConverter(
                group: 3,
                separator: " ",
                groupSeparator: ",",
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
                            { 2, [ "twenty", "twenty-*"] },
                            { 3, [ "thirty", "thirty-*"] },
                            { 4, [ "forty", "forty-*" ] },
                            { 5, [ "fifty", "fifty-*" ] },
                            { 6, [ "sixty", "sixty-*"] },
                            { 7, [ "seventy", "seventy-*"] },
                            { 8, [ "eighty", "eighty-*"] },
                            { 9, [ "ninety", "ninety-*"] }
                        }
                    },
                    { 3,
                        new ()
                        {
                            { 0, [""             , "*"             ]},
                            { 1, ["one hundred"         , "one hundred and *"        ]},
                            { 2, ["two hundred", "two hundred and *"   ]},
                            { 3, ["three hundred", "three hundred and *"  ]},
                            { 4, ["four hundred", "four hundred and *" ]},
                            { 5, ["five hundred", "five hundred and *"   ]},
                            { 6, ["six hundred", "six hundred and *"    ]},
                            { 7, ["seven hundred", "seven hundred and *"   ]},
                            { 8, ["height hundred", "height hundred and *"   ]},
                            { 9, ["nine hundred", "nine hundred and *"   ]}
                        }
                    }
                },
                scale: new NumberScale(["", "thousand"], ["on"]),
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
                },
                adjustFunction: null
            );

    }
}
