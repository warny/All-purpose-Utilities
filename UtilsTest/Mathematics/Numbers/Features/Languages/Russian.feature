@NumberToString @RU
Feature: Russian number conversion

Background:
    Given I use the "RU" number converter

# NTS-30C: the generated names (триллион, квадриллион, …) decline like миллион (ru.wiktionary "триллион").
Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | один |
    | 2 | два |
    | 11 | одиннадцать |
    | 20 | двадцать |
    | 100 | сто |
    | 1000 | тысяча |
    | 1000000 | один миллион |
    | 2000000 | два миллиона |
    | 1000000000 | один миллиард |
    | 1000000000000 | один триллион |
    | 1000000000000000 | один квадриллион |
    | 1000000000000000000 | один квинтиллион |
    | 2000000000000 | два триллиона |
    | 5000000000000 | пять триллионов |
    | 22000000000000 | двадцать два триллиона |

# NTS-27: the scale nouns agree with their multiplier: тысяча is a feminine noun (одна тысяча, две
# тысячи), and тысяча, миллион, миллиард take the singular after 1 (but not 11), the genitive singular
# after 2-4 (but not 12-14) and the genitive plural otherwise (Unicode CLDR plural rules, ru: one/few/many;
# ru.wiktionary "тысяча": две тысячи, пять тысяч). 1000 stays "тысяча", as before.
Scenario Outline: NTS-27 Russian scale nouns agree with their multiplier
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | тысяча |
    | 1001 | тысяча один |
    | 2000 | две тысячи |
    | 2001 | две тысячи один |
    | 3000 | три тысячи |
    | 4000 | четыре тысячи |
    | 5000 | пять тысяч |
    | 11000 | одиннадцать тысяч |
    | 12000 | двенадцать тысяч |
    | 21000 | двадцать одна тысяча |
    | 22000 | двадцать две тысячи |
    | 24000 | двадцать четыре тысячи |
    | 25000 | двадцать пять тысяч |
    | 101000 | сто одна тысяча |
    | 102000 | сто две тысячи |
    | 105000 | сто пять тысяч |
    | 111000 | сто одиннадцать тысяч |
    | 121000 | сто двадцать одна тысяча |
    | 122000 | сто двадцать две тысячи |
    | 2000000 | два миллиона |
    | 5000000 | пять миллионов |
    | 21000000 | двадцать один миллион |
    | 2000000000 | два миллиарда |
    | 11000000000 | одиннадцать миллиардов |
    | 2002002 | два миллиона две тысячи два |
    | -2000 | минус две тысячи |
Scenario Outline: Ordinal numbers
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | первый |
    | 2 | второй |
    | 3 | третий |
    | 4 | четвёртый |
    | 10 | десятый |
    | 100 | сотый |
    | 1000 | тысячный |

Scenario Outline: Feminine ordinals
    Given I use the variants "gender=feminin"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | первая |

Scenario Outline: Neuter ordinals
    Given I use the variants "gender=neutrum"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | первое |

Scenario Outline: Plural ordinals
    Given I use the variants "gender=plural"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | первые |

Scenario Outline: Decimal numbers
    # NTS-34: no validated automatic decimal reading (LanguageDefault fails closed); the explicit digit-by-digit reading is pinned.
    When I convert the decimal number <number> digit by digit
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | один запятая пять |
    | 12.34 | двенадцать запятая три четыре |

Scenario Outline: Additional irregular and suffixed ordinal numbers
    Given I use the "RU" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 |  | первый |
    | 2 |  | второй |
    | 3 |  | третий |
    | 4 |  | четвёртый |
    | 5 |  | пятый |
    | 6 |  | шестой |
    | 7 |  | седьмой |
    | 8 |  | восьмой |
    | 9 |  | девятый |
    | 10 |  | десятый |
    | 11 |  | одиннадцатый |
    | 20 |  | двадцатый |
    | 21 |  | двадцать первый |
    | 40 |  | сороковой |
    | 100 |  | сотый |
    | 1000 |  | тысячный |

# NTS-08 validation: Правила русской орфографии и пунктуации (2006), §132-133: one-word ordinals in
# -сотый/-тысячный (четырёхсотый), composite ordinals separate with only the last word ordinal
# ("тысяча девятьсот девяносто четвёртый", "две тысячи первый"). From 2000 see NTS-28 below.
Scenario Outline: Russian composite ordinals below 2000
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 200 | двухсотый |
    | 1001 | тысяча первый |
    | 1994 | тысяча девятьсот девяносто четвёртый |

# NTS-28: Правила (2006) §133б: composite ordinals are written separately and only their last part is ordinal
# ("две тысячи первый", "миллион двести тысяч сто пятый", a single million without "один"); §132б: the ordinals
# whose last part is -тысячный, -миллионный are one word, their first part in the genitive
# (пятнадцатитысячный, шестисотмиллионный; двухтысячный); a round thousand after the millions keeps that form
# (the analogue of Ukrainian "мільйон трьохтисячний").
Scenario Outline: NTS-28 Russian ordinals from 2000
    Given I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 2000 |  | двухтысячный |
    | 2000 | gender=feminin,case=родительный | двухтысячной |
    | 2000 | gender=plural,case=творительный | двухтысячными |
    | 3000 |  | трёхтысячный |
    | 4000 | gender=neutrum | четырёхтысячное |
    | 5000 |  | пятитысячный |
    | 10000 |  | десятитысячный |
    | 15000 |  | пятнадцатитысячный |
    | 25000 |  | двадцатипятитысячный |
    | 40000 |  | сорокатысячный |
    | 600000 |  | шестисоттысячный |
    | 2001 |  | две тысячи первый |
    | 2001 | gender=feminin | две тысячи первая |
    | 2002 |  | две тысячи второй |
    | 2010 |  | две тысячи десятый |
    | 2021 |  | две тысячи двадцать первый |
    | 5100 |  | пять тысяч сотый |
    | 21001 |  | двадцать одна тысяча первый |
    | 999999 |  | девятьсот девяносто девять тысяч девятьсот девяносто девятый |
    | 1000000 |  | миллионный |
    | 1000000 | case=предложный | миллионном |
    | 2000000 |  | двухмиллионный |
    | 600000000 |  | шестисотмиллионный |
    | 1200105 |  | миллион двести тысяч сто пятый |
    | 1003000 |  | миллион трёхтысячный |
    | 2003000 |  | два миллиона трёхтысячный |
    | 2000001 |  | два миллиона первый |
    | -2000 |  | минус двухтысячный |

# NTS-28: a multiplier whose genitive combining form is not shown by the rules (a final "один": 21 000; a
# hundred: 100 000, 101 000) fails closed, as does the milliard and above (the generated scales do not agree,
# NTS-30).
Scenario Outline: Russian ordinals outside the validated domain are not supported
    When I attempt to convert the ordinal number <number>
    Then conversion is rejected because no ordinal form is available

Examples:
    | number |
    | 21000 |
    | 100000 |
    | 101000 |
    | 1000000000 |
    | 2021000 |

Scenario: Ordinal conversion is supported
    Given I use the "RU" number converter
    Then the converter supports ordinal conversion

Scenario: Fraction connector wording
    Given I use the "RU" number converter
    When I convert the fraction 3/2 through both public fraction APIs
    Then both fraction results are "три на два"

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "RU" number converter
    Then the converter supports clock-time conversion

# Quarter and half refer to the following hour as a masculine genitive ordinal ("четверть
# второго", "половина второго"); ":45" is "без четверти" + the cardinal of the following hour,
# "час" for one. Whole hours agree the noun with the count (час / два часа / пять часов).
Scenario Outline: Idiomatic Russian clock times
    Given I use the "RU" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | час |
    | 02:00 | два часа |
    | 04:00 | четыре часа |
    | 05:00 | пять часов |
    | 12:00 | двенадцать часов |
    | 01:15 | четверть второго |
    | 01:30 | половина второго |
    | 01:45 | без четверти два |
    | 12:15 | четверть первого |
    | 12:45 | без четверти час |
    | 13:30 | половина второго |
    | 23:45 | без четверти двенадцать |

# NTS-14. The ordinal of ноль/нуль is the adjective нулевой (Wiktionary: relative adjective,
# Zaliznyak declension type 1b, нулевой/нулевая/нулевое/нулевые; Dal records нулевой and нолевой),
# declined like второй; the mechanical "нолый" is not attested. The masculine and plural accusative
# follow the configuration's inanimate convention (= nominative), as for первый and второй.
Scenario Outline: Ordinal of zero
    Given I use the variants "<variants>"
    When I convert the ordinal number 0
    Then the result is "<expected>"

Examples:
    | variants | expected |
    |  | нулевой |
    | gender=feminin | нулевая |
    | gender=neutrum | нулевое |
    | gender=plural | нулевые |
    | case=родительный | нулевого |
    | case=винительный | нулевой |
    | gender=feminin,case=винительный | нулевую |
    | gender=neutrum,case=дательный | нулевому |
    | gender=plural,case=творительный | нулевыми |
    | gender=feminin,case=предложный | нулевой |