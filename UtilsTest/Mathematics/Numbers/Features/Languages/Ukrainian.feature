@NumberToString @UK
Feature: Ukrainian number conversion

Background:
    Given I use the "UK" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | нуль |
    | 1 | один |
    | 2 | два |
    | 9 | дев'ять |
    | 10 | десять |
    | 11 | одинадцять |
    | 19 | дев'ятнадцять |
    | 20 | двадцять |
    | 21 | двадцять один |
    | 100 | сто |
    | 1000 | тисяч |
    | 2000 | два тисяч |
    | 1000000 | один мільйон |
    | 2000000 | два мільйонів |
    | 1000000000 | один мільярд |
    | 1000000000000 | один трильйон |
    | 1000000000000000000 | один квінтильйон |

Scenario: Ordinal conversion is supported
    Then the converter supports ordinal conversion

# Only the last component of a Ukrainian compound ordinal is ordinal ("двадцять перший",
# "тисяча перший"). The apostrophe is the ASCII one used by the cardinals ("п'ять").
# NTS-08 validation: Український правопис 2019, §38: compound ordinals in -сотий/-тисячний/
# -мільйонний/-мільярдний are one word (трьохсотий, двохтисячний, десятитисячний); composite ones
# are separate and write a single million or milliard without "один" ("мільйон трьохтисячний",
# "мільярд чотирьохмільйонний", "тисяча дев'ятсот вісімдесят восьмий").
Scenario Outline: Ukrainian ordinal numbers
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | перший |
    | 2 | другий |
    | 3 | третій |
    | 4 | четвертий |
    | 5 | п'ятий |
    | 10 | десятий |
    | 11 | одинадцятий |
    | 20 | двадцятий |
    | 21 | двадцять перший |
    | 40 | сороковий |
    | 100 | сотий |
    | 101 | сто перший |
    | 1000 | тисячний |
    | 1001 | тисяча перший |
    | 2000 | двохтисячний |
    | 2001 | дві тисячі перший |
    | 300 | трьохсотий |
    | 1988 | тисяча дев'ятсот вісімдесят восьмий |
    | 10000 | десятитисячний |
    | 1000000 | мільйонний |
    | 1000001 | мільйон перший |
    | 1003000 | мільйон трьохтисячний |
    | 1000000000 | мільярдний |
    | 1000000001 | мільярд перший |

# NTS-28: Український правопис 2019, §38.1.2 (slovnyk.ua): двохтисячний, десятитисячний, чотирьохмільйонний,
# п'ятдесятимільйонний, шістдесятип'ятимільйонний, семимільярдний, трьохмільярдний — the multiplier in the
# genitive, a compound multiplier with both parts in the genitive; note 1: мільйон трьохтисячний.
Scenario Outline: NTS-28 Ukrainian round thousands, millions and milliards
    Given I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 15000 |  | п'ятнадцятитисячний |
    | 20000 |  | двадцятитисячний |
    | 20000 | gender=жіночий,case=родовий | двадцятитисячної |
    | 25000 |  | двадцятип'ятитисячний |
    | 40000 |  | сорокатисячний |
    | 2000000 |  | двохмільйонний |
    | 4000000 |  | чотирьохмільйонний |
    | 50000000 |  | п'ятдесятимільйонний |
    | 65000000 |  | шістдесятип'ятимільйонний |
    | 65000000 | gender=середній | шістдесятип'ятимільйонне |
    | 2000000000 |  | двохмільярдний |
    | 2003000 |  | два мільйони трьохтисячний |
    | 1065000000 |  | мільярд шістдесятип'ятимільйонний |

# NTS-28: §38 shows no multiplier ending in "один" after the tens, nor a multiplier from 90 (дев'яноста or
# дев'яносто?), and its only hundreds example (п'ятсоттридцятитисячний) keeps the hundreds in the nominative,
# which leaves the other hundreds open: those round values fail closed.
Scenario Outline: Ukrainian ordinals outside the validated domain are not supported
    When I attempt to convert the ordinal number <number>
    Then conversion is rejected because no ordinal form is available

Examples:
    | number |
    | 21000 |
    | 90000 |
    | 100000 |
    | 530000 |
    | 21000000 |

Scenario Outline: Ukrainian ordinals agree in gender and case
    Given I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 | gender=жіночий | перша |
    | 3 | gender=жіночий | третя |
    | 2 | gender=жіночий,case=родовий | другої |
    | 3 | gender=жіночий,case=родовий | третьої |
    | 1 | gender=жіночий,case=місцевий | першій |
    | 2 | gender=жіночий,case=знахідний | другу |
    | 1 | gender=середній | перше |
    | 2 | рід=чоловічий,відмінок=родовий | другого |

Scenario Outline: Ukrainian gendered cardinals
    Given I use the variants "<variants>"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 |  | один |
    | 1 | gender=жіночий | одна |
    | 2 | gender=жіночий | дві |
    | 1 | gender=середній | одне |

Scenario: Idiomatic clock-time conversion is supported
    Then the converter supports clock-time conversion

# "година" is feminine: the hour is a feminine ordinal whose case follows the construction:
# nominative "перша", locative after "по" ("чверть по першій"), accusative after "на"
# ("пів на другу"), genitive after "до" ("чверть до другої").
Scenario Outline: Idiomatic Ukrainian clock times
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | перша |
    | 01:15 | чверть по першій |
    | 01:30 | пів на другу |
    | 01:45 | чверть до другої |
    | 02:00 | друга |
    | 03:30 | пів на четверту |
    | 12:30 | пів на першу |
    | 13:30 | пів на другу |

Scenario: The regional alias uses Ukrainian wording
    Then the "UK" and "UK-UA" converters produce the same cardinal wording for 2
