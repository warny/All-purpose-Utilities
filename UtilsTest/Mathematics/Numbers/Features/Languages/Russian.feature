@NumberToString @RU
Feature: Russian number conversion

Background:
    Given I use the "RU" number converter

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
    | 2000000 | два миллион |
    | 1000000000 | один миллиард |
    | 1000000000000 | один триллион |
    | 1000000000000000 | один квадриллион |
    | 1000000000000000000 | один квинтиллион |

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
    When I convert the decimal number <number>
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