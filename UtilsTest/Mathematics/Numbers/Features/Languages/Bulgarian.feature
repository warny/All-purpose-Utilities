@NumberToString @BG
Feature: Bulgarian number conversion

Background:
    Given I use the "BG" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | нула |
    | 1 | едно |
    | 2 | две |
    | 3 | три |
    | 9 | девет |
    | 10 | десет |
    | 11 | единадесет |
    | 12 | дванадесет |
    | 19 | деветнадесет |
    | 20 | двадесет |
    | 21 | двадесет едно |
    | 100 | сто |
    | 101 | сто едно |
    | 200 | двеста |
    | 300 | триста |
    | 400 | четиристотин |
    | 999 | деветстотин деветдесет девет |
    | 1000 | хиляда |
    | 2000 | две хиляда |
    | 10000 | десет хиляда |
    | 1000000 | едно милион |
    | 1000000000 | едно милиард |
    | 1000000000000 | едно билион |
    | 1000000000000000 | едно билиард |

Scenario: The regional alias uses Bulgarian wording
    Then the "BG" and "BG-BG" converters produce the same cardinal wording for 1

Scenario: Ordinal conversion is supported
    Then the converter supports ordinal conversion

# Ezik.bg / Pravopisen rechnik: only the last element of a compound ordinal is ordinal, after
# "и" ("двадесет и първи", "хиляда сто и втори"); "стотен", "хиляден" and the compound
# "двехиляден" are written as one word. Without a gender the citation (masculine) form is used.
Scenario Outline: Bulgarian ordinal numbers
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | първи |
    | 2 | втори |
    | 3 | трети |
    | 4 | четвърти |
    | 5 | пети |
    | 7 | седми |
    | 8 | осми |
    | 10 | десети |
    | 11 | единадесети |
    | 15 | петнадесети |
    | 20 | двадесети |
    | 21 | двадесет и първи |
    | 22 | двадесет и втори |
    | 100 | стотен |
    | 101 | сто и първи |
    | 120 | сто и двадесети |
    | 125 | сто двадесет и пети |
    | 200 | двестотен |
    | 1000 | хиляден |
    | 1001 | хиляда и първи |
    | 1102 | хиляда сто и втори |
    | 2000 | двехиляден |
    | 2001 | две хиляди и първи |
    | 1000000 | милионен |

Scenario Outline: Bulgarian gendered ordinals
    Given I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 | gender=masculine | първи |
    | 1 | gender=feminine | първа |
    | 1 | gender=neuter | първо |
    | 3 | род=feminine | трета |
    | 21 | gender=feminine | двадесет и първа |
    | 100 | gender=feminine | стотна |
    | 1000 | gender=neuter | хилядно |

Scenario Outline: Bulgarian gendered cardinals keep the counting form by default
    Given I use the variants "<variants>"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 |  | едно |
    | 2 |  | две |
    | 1 | gender=masculine | един |
    | 2 | gender=masculine | два |
    | 1 | gender=feminine | една |

Scenario: Idiomatic clock-time conversion is supported
    Then the converter supports clock-time conversion

# "Часът е един / два": "час" is masculine, so {hour} is forced to the masculine numeral.
# "петнайсет" is the colloquial form of "петнадесет" accepted by the orthographic dictionary.
Scenario Outline: Idiomatic Bulgarian clock times
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | един |
    | 01:15 | един и петнайсет |
    | 01:30 | един и половина |
    | 01:45 | два без петнайсет |
    | 02:00 | два |
    | 13:30 | един и половина |
    | 01:07 | един |
    | 01:08 | един и петнайсет |
    | 23:53 | дванадесет |
