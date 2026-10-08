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
