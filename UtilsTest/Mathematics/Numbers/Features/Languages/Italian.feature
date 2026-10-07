@NumberToString @IT
Feature: Italian number conversion

Background:
    Given I use the "IT" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | uno |
    | 3 | tre |
    | 11 | undici |
    | 20 | venti |
    | 100 | cento |
    | 1000 | mille |

# Treccani, Enciclopedia dell'Italiano, "numerali": compound cardinals below a million are written
# as one word; the final vowel of the tens is elided before uno and otto ("ventuno [e non
# *ventiuno]"); compounds of tre take the accent ("ventitré") although tre alone does not. The
# <Fusion> rules of the tens digits implement these junctions.
Scenario Outline: Compound cardinals are written as one word
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 21 | ventuno |
    | 22 | ventidue |
    | 23 | ventitré |
    | 25 | venticinque |
    | 28 | ventotto |
    | 29 | ventinove |
    | 31 | trentuno |
    | 33 | trentatré |
    | 38 | trentotto |
    | 43 | quarantatré |
    | 48 | quarantotto |
    | 58 | cinquantotto |
    | 68 | sessantotto |
    | 78 | settantotto |
    | 88 | ottantotto |
    | 98 | novantotto |
    | 99 | novantanove |

# Treccani: with cento followed by a ten or a unit, elision is uncommon before uno, otto and
# undici ("centouno", "centootto", "centoundici"), so those keep the vowel; before ottanta the
# hundreds elide ("centottanta"; also attested "trecentottanta-" in dictionary forms). The accent
# of a final tre also applies after the hundreds ("centotré", Treccani/Libreriamo).
Scenario Outline: Compound cardinals with hundreds
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 101 | centouno |
    | 103 | centotré |
    | 108 | centootto |
    | 111 | centoundici |
    | 123 | centoventitré |
    | 180 | centottanta |
    | 181 | centottantuno |
    | 183 | centottantatré |
    | 188 | centottantotto |
    | 280 | duecentottanta |
    | 281 | duecentottantuno |
    | 999 | novecentonovantanove |

# Treccani: mille becomes -mila in the plural and joins the multiplier in writing ("duemila"); with
# mille the elision is "decisamente da evitare" ("milleuno [non *milluno]"). The whole number below
# a million is one word, and a final tre keeps its accent ("milletré", "duemilatré", Libreriamo),
# while an inner tre does not carry it ("ventitremila"). These scale-level junctions are handled by
# onScale Replacements, not by <Fusion>, whose contract is intra-group. 21000 uses the regular,
# non-apocopated "ventunomila"; the apocopated "ventunmila" is also attested and not chosen.
Scenario Outline: Thousands are written as one word
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1001 | milleuno |
    | 1003 | milletré |
    | 1021 | milleventuno |
    | 1100 | millecento |
    | 2000 | duemila |
    | 2001 | duemilauno |
    | 2003 | duemilatré |
    | 3000 | tremila |
    | 21000 | ventunomila |
    | 23000 | ventitremila |
    | 28000 | ventottomila |
    | 100000 | centomila |
    | 123456 | centoventitremilaquattrocentocinquantasei |
    | 180180 | centottantamilacentottanta |
    | 999999 | novecentonovantanovemilanovecentonovantanove |

# NTS-14. Treccani, Enciclopedia dell'Italiano, "numerali [prontuario]": "Milione e miliardo sono
# nomi: hanno la forma plurale, sono preceduti da un altro cardinale [...] e, quando la cifra non è
# tonda al milione / miliardo, sono seguiti da un altro cardinale separato dalla congiunzione e"
# ("un milione, due milioni; un miliardo, due miliardi; 2.100.000 / due milioni e centomila").
# Treccani, "uno, numerali composti con [prontuario]": "I composti con milioni e miliardi ammettono
# solo la forma separata (un milione e uno, due milioni e uno ...; un miliardo e uno)"; compounds of
# uno stay invariable ("ventuno banchi"), hence "ventuno milioni". DICO (Università di Messina):
# "Milione e miliardo si scrivono sempre separati dalle altre cifre, con la congiunzione e". Treccani
# vocabolario: bilione = 10^12, trilione = 10^18; biliardo = 10^15 (Libreriamo, Wiktionary). Below a
# million the number stays soldered (duemilauno), unchanged by NTS-14.
Scenario Outline: Millions and above are separate nouns joined by e
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000000 | un milione |
    | 2000000 | due milioni |
    | 1000001 | un milione e uno |
    | 2000001 | due milioni e uno |
    | 2100000 | due milioni e centomila |
    | 1001000 | un milione e mille |
    | 1001001 | un milione e milleuno |
    | 1021000 | un milione e ventunomila |
    | 21000000 | ventuno milioni |
    | 31000000 | trentuno milioni |
    | 1000000000 | un miliardo |
    | 2000000000 | due miliardi |
    | 1000000001 | un miliardo e uno |
    | 1001000000 | un miliardo e un milione |
    | 1000000000000 | un bilione |
    | 2000000000000 | due bilioni |
    | 1000000000000000 | un biliardo |
    | 1000000000000000000 | un trilione |

Scenario Outline: Millions keep their noun form in the feminine
    Given I use the variants "gender=femminile"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000000 | un milione |
    | 1000001 | un milione e una |
    | 2000001 | due milioni e una |

Scenario Outline: Feminine cardinal numbers
    Given I use the variants "gender=femminile"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

# Treccani, vocabolario "ventuno": "ventuno ballerine" (the compound stays in -uno before a plural
# feminine noun; "ventun(o) ballerina sarebbe raro"), so only the standalone 1 becomes "una".
Examples:
    | number | expected |
    | 1 | una |
    | 21 | ventuno |
    | 22 | ventidue |
    | 31 | trentuno |

Scenario Outline: Ordinal numbers
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | primo |
    | 2 | secondo |
    | 3 | terzo |
    | 10 | decimo |
    | 11 | undicesimo |
    | 20 | ventesimo |
    | 100 | centesimo |

Scenario Outline: Feminine ordinal numbers
    Given I use the variants "gender=femminile"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | prima |
    | 2 | seconda |
    | 11 | undicesima |
    | 19 | diciannovesima |

# Treccani, vocabolario "diciannovesimo": the ordinal of diciannove drops its final vowel.
Scenario: Ordinal of nineteen
    When I convert the ordinal number 19
    Then the result is "diciannovesimo"

# NTS-13. Treccani, La grammatica italiana, "Aggettivi numerali ordinali": from 11 on, the ordinal
# is the cardinal without its final vowel + -esimo ("sedici ▶ sedicesimo", "ventiquattro ▶
# ventiquattresimo", "trentotto ▶ trentottesimo"); compounds of tre keep the -e ("ventitré ▶
# ventitreesimo", "trentatré ▶ trentatreesimo") and compounds of sei keep the -i ("ventisei ▶
# ventiseiesimo"). Treccani, vocabolario "ordinale": "undicesimo, dodicesimo, ... ventesimo,
# ventunesimo, ventiduesimo, ventitreesimo, ... novantanovesimo, centesimo, centunesimo". The
# prontuario of the Enciclopedia dell'Italiano lists "trentaseesimo / trentaseiesimo" and notes that
# only the second is used: -seiesimo is the canonical form.
Scenario Outline: Compound ordinals below one hundred
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 11 | undicesimo |
    | 12 | dodicesimo |
    | 16 | sedicesimo |
    | 17 | diciassettesimo |
    | 18 | diciottesimo |
    | 19 | diciannovesimo |
    | 20 | ventesimo |
    | 21 | ventunesimo |
    | 22 | ventiduesimo |
    | 23 | ventitreesimo |
    | 24 | ventiquattresimo |
    | 25 | venticinquesimo |
    | 26 | ventiseiesimo |
    | 27 | ventisettesimo |
    | 28 | ventottesimo |
    | 29 | ventinovesimo |
    | 30 | trentesimo |
    | 31 | trentunesimo |
    | 33 | trentatreesimo |
    | 36 | trentaseiesimo |
    | 38 | trentottesimo |
    | 90 | novantesimo |
    | 99 | novantanovesimo |

# Treccani, vocabolario "centesimo": "centesimo primo (o, più com., centunesimo), centesimo secondo
# (o, più com., centoduesimo)"; vocabolario "ordinale": "centesimo, centunesimo". The cardinal
# keeps "centouno" (NTS-10) but the ordinal follows the attested "centunesimo"; the -ouno → -un
# stem is applied to every hundred (duecentunesimo) by analogy. Otherwise the hundreds keep their
# vowels as in the cardinal (DICO, Università di Messina: "le vocali si mantengono al di sopra di
# cento: centoduesimo, centoundicesimo"), and the tre/sei rules apply to the final constituent.
Scenario Outline: Compound ordinals with hundreds
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 100 | centesimo |
    | 101 | centunesimo |
    | 102 | centoduesimo |
    | 103 | centotreesimo |
    | 106 | centoseiesimo |
    | 111 | centoundicesimo |
    | 120 | centoventesimo |
    | 180 | centottantesimo |
    | 183 | centottantatreesimo |
    | 186 | centottantaseiesimo |
    | 200 | duecentesimo |
    | 201 | duecentunesimo |
    | 999 | novecentonovantanovesimo |

# After a hundred, dieci keeps its lexical ordinal decimo instead of the mechanical "centodiecesimo",
# which no consulted source attests. Vocabolario degli Accademici della Crusca, 5th ed., vol. 2
# p. 753, s.v. "centesimo" § III: "Centodecimo, Centundicesimo, Centododicesimo ec."; Wiktionary,
# Appendix:Italian numbers: "centodecimo", "duecentodecimo". Treccani ("ordinale") prefers the
# analytic "centesimo decimo" (separate spelling), which is not modelled, like "millesimo primo".
Scenario Outline: Hundreds followed by ten keep the lexical decimo
    Given I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 110 | | centodecimo |
    | 210 | | duecentodecimo |
    | 310 | | trecentodecimo |
    | 910 | | novecentodecimo |
    | 110 | gender=femminile | centodecima |
    | 210 | gender=femminile | duecentodecima |
    | 910 | gender=femminile | novecentodecima |

# Thousands. Treccani, vocabolario "ordinale": "millesimo, duemillesimo, ... diecimillesimo";
# vocabolario "centomillesimo": the ordinal of centomila. Round thousands therefore turn the plural
# -mila into mill- before -esimo. Between 1001 and 1999 the official forms are the synthetic
# "milleunesimo, milleduesimo ecc." (DICO, Università di Messina; Accademia della Crusca for
# milleunesimo), the analytic "millesimo primo" being a formal alternative (Treccani "ordinale").
Scenario Outline: Ordinals of thousands
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | millesimo |
    | 1001 | milleunesimo |
    | 1002 | milleduesimo |
    | 1003 | milletreesimo |
    | 1100 | millecentesimo |
    | 1999 | millenovecentonovantanovesimo |
    | 2000 | duemillesimo |
    | 3000 | tremillesimo |
    | 10000 | diecimillesimo |
    | 23000 | ventitremillesimo |
    | 100000 | centomillesimo |
    | 999000 | novecentonovantanovemillesimo |

# The stem rules are shared by both genders; the feminine variant only changes the suffix.
Scenario Outline: Feminine compound ordinals
    Given I use the variants "gender=femminile"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 21 | ventunesima |
    | 23 | ventitreesima |
    | 26 | ventiseiesima |
    | 101 | centunesima |
    | 180 | centottantesima |
    | 1001 | milleunesima |
    | 2000 | duemillesima |

Scenario: Negative compound ordinal keeps the existing sign policy
    When I convert the ordinal number -21
    Then the result is "meno ventunesimo"

# NTS-15. Beside the synthetic type, Treccani (vocabolario "ordinale") describes ordinals formed by
# juxtaposing the ordinal of the thousand, hundred or ten with the ordinal of the rest; it is "pressoché
# l'unico" type where the synthetic one would sound uneuphonious, with the separate spelling preferred:
# "millesimo primo, millesimo secondo, millesimo decimo". For 1010 the synthetic form would be the
# unattested "millediecesimo", so the analytic "millesimo decimo" is used (both parts agree in gender).
# Treccani (vocabolario "centomillesimo") gives the ordinals following centomillesimo as soldered
# juxtapositions, "centomillesimoprimo, centomillesimosecondo, centomillesimoterzo, ecc.", and warns
# that the synthetic "un centomiladuesimo" is a partitive (a fraction), not the ordinal. Only these
# attested families are composed (OrdinalComposition on the numeric split, never on the cardinal
# text); 1110-1910 and the other non-round thousands are deliberately rejected (see below).
Scenario Outline: Analytic compound ordinals after thousands
    Given I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1010 | | millesimo decimo |
    | 1010 | gender=femminile | millesima decima |
    | 100001 | | centomillesimoprimo |
    | 100002 | | centomillesimosecondo |
    | 100003 | | centomillesimoterzo |
    | 100009 | | centomillesimonono |
    | 100001 | gender=femminile | centomillesimaprima |
    | 100003 | gender=femminile | centomillesimaterza |

# NTS-17. The ordinal of a round scale value is formed on the scale noun, not on the cardinal "un
# milione" (whose mechanical ordinal "un milionesimo" is the fraction "one millionth"). Treccani has
# the entries "milionesimo", "miliardesimo", "bilionesimo" (vocabolario) and "decimilionesimo (o
# diecimilionesimo)"; the CNR press release on the .it registry writes "il duemilionesimo indirizzo
# web" and "il milionesimo dominio". A multiplier above one is the cardinal soldered to the ordinal
# of the noun, as for the thousands ("duemillesimo", "diecimillesimo", Treccani "ordinale"): the
# project writes "diecimilionesimo", transparent on the cardinal dieci (Treccani also accepts
# "decimilionesimo"). Like "ventitremila"/"ventitremillesimo", an inner tre loses its accent.
Scenario Outline: Round million-scale ordinals
    Given I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1000000 | | milionesimo |
    | 2000000 | | duemilionesimo |
    | 3000000 | | tremilionesimo |
    | 10000000 | | diecimilionesimo |
    | 21000000 | | ventunomilionesimo |
    | 23000000 | | ventitremilionesimo |
    | 100000000 | | centomilionesimo |
    | 999000000 | | novecentonovantanovemilionesimo |
    | 1000000000 | | miliardesimo |
    | 2000000000 | | duemiliardesimo |
    | 1000000000000 | | bilionesimo |
    | 2000000000000 | | duebilionesimo |
    | 1000000 | gender=femminile | milionesima |
    | 2000000 | gender=femminile | duemilionesima |
    | 10000000 | gender=femminile | diecimilionesima |
    | 1000000000 | gender=femminile | miliardesima |

# NTS-18. The ordinals of biliardo (10^15) and trilione (10^18) are sourced: Ministero delle
# Infrastrutture e dei Trasporti, ADN table of multiples and submultiples ("Biliardesimo" 10^-15,
# "Trilionesimo" 10^-18); Nuovo De Mauro "trilionesimo" (agg. num. ord., "che in una serie ordinata
# occupa il posto corrispondente al trilione"); GDLI "trilionesimo". The forms with a multiplier above
# one ("duebiliardesimo", "duetrilionesimo") are not individually attested: they result from the same
# productive rule already retained for "duemillesimo", "duemilionesimo", "duemiliardesimo" and
# "duebilionesimo". Every round multiple of trilione that fits a long is at most 9 × 10^18, so the
# multiplier is a single digit and tre is unaccented ("tretrilionesimo"); compounds of uno keep the
# invariable "ventuno" of the plural nouns ("ventuno biliardi" → "ventunobiliardesimo").
Scenario Outline: Round biliardo and trilione ordinals
    Given I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1000000000000000 | | biliardesimo |
    | 2000000000000000 | | duebiliardesimo |
    | 3000000000000000 | | trebiliardesimo |
    | 21000000000000000 | | ventunobiliardesimo |
    | 23000000000000000 | | ventitrebiliardesimo |
    | 999000000000000000 | | novecentonovantanovebiliardesimo |
    | 1000000000000000000 | | trilionesimo |
    | 2000000000000000000 | | duetrilionesimo |
    | 3000000000000000000 | | tretrilionesimo |
    | 9000000000000000000 | | novetrilionesimo |
    | 1000000000000000 | gender=femminile | biliardesima |
    | 2000000000000000 | gender=femminile | duebiliardesima |
    | 1000000000000000000 | gender=femminile | trilionesima |
    | 2000000000000000000 | gender=femminile | duetrilionesima |

# Values outside the supported domain fail closed instead of being produced by a mechanical suffix.
# These are deliberate linguistic limitations, not pending technical work: the engine could compose
# them (<OrdinalComposition>), but no consulted source establishes a canonical form or spelling.
# Zero: NTS-12 decision unchanged (Treccani attests "zeresimo" only in special, mathematical uses).
# 1110-1910: two analytic splits are possible ("millesimo centodecimo" or "millecentesimo decimo")
# and no source selects one. The non-round thousands above 1999 outside 100001-100009: Treccani
# attests the juxtaposition only on millesimo and centomillesimo, never "duemillesimo primo", and
# treats the synthetic form as partitive. The non-round values from a million: the only proposals
# ("milionesimoprimo", "unmilioneunesimo") are self-declared virtual extrapolations.
Scenario Outline: Ordinals outside the validated domain are rejected
    Given I use the variants "<variants>"
    When I attempt to convert the ordinal number <number>
    Then conversion is rejected because no ordinal form is available

Examples:
    | number | variants |
    | 0 | |
    | 0 | gender=femminile |
    | 1110 | |
    | 1210 | |
    | 1910 | |
    | 1110 | gender=femminile |
    | 2001 | |
    | 2010 | |
    | 2021 | |
    | 21001 | |
    | 100010 | |
    | 100011 | |
    | 100100 | |
    | 999999 | |
    | 2001 | gender=femminile |
    | 1000001 | |
    | 1001000 | |
    | 2000001 | |
    | 1000000001 | |
    | 2000001 | gender=femminile |
    | 1000000000001000 | |
    | 1000000000000001 | |
    | 2000000000000001 | gender=femminile |
    | 1000000000000000001 | |
    | 1001000000000000000 | |
    | 9000000000000000001 | |
    | 9223372036854775807 | |
    | 9223372036854775807 | gender=femminile |

Scenario Outline: Decimal numbers
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | uno virgola cinque |
    | 12.34 | dodici virgola tre quattro |

Scenario Outline: Masculine ordinal numbers
    Given I use the "IT" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1000 |  | millesimo |

Scenario Outline: Additional feminine ordinal numbers
    Given I use the "IT" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 20 | gender=femminile | ventesima |
    | 1000 | gender=femminile | millesima |

Scenario Outline: Caller-defined currency wording
    Given I use this currency definition
        | property         | value      |
        | unit singular    | euro       |
        | unit plural      | euro       |
        | subunit singular | centesimo  |
        | subunit plural   | centesimi  |
        | connector        | e          |
    When I convert the currency amount <amount>
    Then the result is "<expected>"

Examples:
    | amount | expected                            |
    | 1      | uno euro                            |
    | 2      | due euro                            |
    | 1.50   | uno euro e cinquanta centesimi      |

Scenario: Ordinal conversion is supported
    Given I use the "IT" number converter
    Then the converter supports ordinal conversion

Scenario: Fraction connector wording
    Given I use the "IT" number converter
    When I convert the fraction 3/2 through both public fraction APIs
    Then both fraction results are "tre su due"

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "IT" number converter
    Then the converter supports clock-time conversion

# Accademia della Crusca: "l'una e mezzo" (also "e mezza"), "le due meno un quarto", and "le otto
# meno venti" / "meno dieci" for the minutes before the hour. The article is elided for one
# ("l'una") and plural otherwise ("le due"), selected through displayHourRange; {hour} is forced to
# the feminine numeral. Five-minute clock since the compound cardinals are orthographic (NTS-10):
# minutes after the hour are added with "e" ("le due e venticinque"), minutes before the following
# hour are subtracted with "meno".
Scenario Outline: Idiomatic Italian clock times
    Given I use the "IT" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | l'una |
    | 01:05 | l'una e cinque |
    | 01:15 | l'una e un quarto |
    | 01:25 | l'una e venticinque |
    | 01:30 | l'una e mezzo |
    | 01:35 | le due meno venticinque |
    | 01:45 | le due meno un quarto |
    | 01:55 | le due meno cinque |
    | 02:00 | le due |
    | 02:10 | le due e dieci |
    | 02:20 | le due e venti |
    | 02:40 | le tre meno venti |
    | 02:50 | le tre meno dieci |
    | 12:45 | l'una meno un quarto |
    | 12:50 | l'una meno dieci |
    | 13:30 | l'una e mezzo |
