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

# Values outside the validated domain fail closed instead of being produced by a mechanical suffix.
# Zero: NTS-12 decision unchanged (Treccani attests "zeresimo" only in special, mathematical uses).
# Non-round thousands above 1999: no source establishes a canonical form, and Treccani
# ("centomillesimo") gives the analytic "centomillesimoprimo" for 100001 rather than a synthetic
# derivation (TODO NTS-15). Millions and above: the cardinals are known wrong (TODO NTS-14).
Scenario Outline: Ordinals outside the validated domain are rejected
    Given I use the variants "<variants>"
    When I attempt to convert the ordinal number <number>
    Then conversion is rejected because no ordinal form is available

Examples:
    | number | variants |
    | 0 | |
    | 0 | gender=femminile |
    | 2001 | |
    | 2021 | |
    | 21001 | |
    | 100001 | |
    | 999999 | |
    | 2001 | gender=femminile |
    | 1000000 | |
    | 2000000 | |
    | 1000000000 | |

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
