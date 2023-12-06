using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Mathematics
{
    public partial class NumberToStringConverter
    {
        public static NumberToStringConverter FrenchFranceNumbers()
            => new NumberToStringConverter(
                group: 3,
                separator: " ",
                zero: "zéro",
                minus: "moins *",
                groups: new Dictionary<int, Dictionary<long, string[]>>()
                {
                    { 1 ,
                        new ()
                        {
                            { 0, [ "" ] },
                            { 1, [ "et un"] },
                            { 2, [ "deux"] },
                            { 3, [ "trois"] },
                            { 4, [ "quatre"] },
                            { 5, [ "cinq"] },
                            { 6, [ "six" ] },
                            { 7, [ "sept" ] },
                            { 8, [ "huit"] },
                            { 9, [ "neuf"] }
                        }
                    },
                    { 2,
                        new () {
                            { 0, [ "", "*" ] },
                            { 1, [ "dix", "dix *" ] },
                            { 2, [ "vingt", "vingt *" ] },
                            { 3, [ "trente", "trente *"] },
                            { 4, [ "quarante", "quarante *" ] },
                            { 5, [ "cinquante", "cinquante *" ] },
                            { 6, [ "soixante", "soixante *" ] },
                            { 7, [ "soixante dix", "soixante dix *" ] },
                            { 8, [ "quatre vingt", "quatre vingt *" ] },
                            { 9, [ "quatre vingt dix", "quatre vingt dix *"] }
                        }
                    },
                    { 3,
                        new ()
                        {
                            { 0, [ ""             , "*"             ]},
                            { 1, [ "cent"         , "cent *"        ]},
                            { 2, [ "deux cents"   , "deux cent *"   ]},
                            { 3, [ "trois cents"  , "trois cent *"  ]},
                            { 4, [ "quatre cents" , "quatre cent *" ]},
                            { 5, [ "cinq cents"   , "cinq cent *"   ]},
                            { 6, [ "six cents"    , "six cent *"    ]},
                            { 7, [ "sept cents"   , "sept cent *"   ]},
                            { 8, [ "huit cents"   , "huit cent *"   ]},
                            { 9, [ "neuf cents"   , "neuf cent *"   ]}
                        }
                    }
                },
                scale: new NumberScale(["","mille"],["on(s)", "ard(s)"]),
                replacements: new Dictionary<string, string>()
		        {
			        { "un mille", "mille" }
                },
                exceptions: new Dictionary<long, string>()
                {
                    { 1, "un" },
                    { 11, "onze" },
                    { 12, "douze" },
                    { 13, "treize" },
                    { 14, "quatorze" },
                    { 15, "quinze" },
                    { 16, "seize" },
                    { 71, "soixante onze" },
                    { 72, "soixante douze" },
                    { 73, "soixante treize" },
                    { 74, "soixante quatorze" },
                    { 75, "soixante quinze" },
                    { 76, "soixante seize" },
                    { 77, "soixante dix sept" },
                    { 78, "soixante dix huit" },
                    { 79, "soixante dix neuf" },
                    { 91, "quatre-vingt onze" },
                    { 92, "quatre-vingt douze" },
                    { 93, "quatre-vingt treize" },
                    { 94, "quatre-vingt quatorze" },
                    { 95, "quatre-vingt quinze" },
                    { 96, "quatre-vingt seize" },
                    { 97, "quatre-vingt dix sept" },
                    { 98, "quatre-vingt dix huit" },
                    { 99, "quatre-vingt dix neuf" },
                }
            );

    }
}
