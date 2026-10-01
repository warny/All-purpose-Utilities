@NumberToString @NL
Feature: Dutch number conversion

Scenario Outline: Decimal numbers
    Given I use the "NL" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | een komma vijf |
    | 12.34 | twaalf komma drie vier |

Scenario Outline: Basic cardinal numbers
    Given I use the "NL" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | een |
    | 11 | elf |
    | 12 | twaalf |
    | 20 | twintig |
    | 21 | eenentwintig |
    | 100 | honderd |
    | 1000 | duizend |

Scenario Outline: Irregular and suffixed ordinal numbers
    Given I use the "NL" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | eerste |
    | 2 | tweede |
    | 3 | derde |
    | 4 | vierde |
    | 5 | vijfde |
    | 10 | tiende |
    | 20 | twintigste |
    | 100 | honderdste |

Scenario Outline: Additional ordinal numbers for units and teens
    Given I use the "NL" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 6 |  | zesde |
    | 7 |  | zevende |
    | 8 |  | achtste |
    | 9 |  | negende |
    | 11 |  | elfde |
    | 12 |  | twaalfde |
    | 13 |  | dertiende |
    | 19 |  | negentiende |

Scenario Outline: Tens and compound ordinal numbers
    Given I use the "NL" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 21 |  | eenentwintigste |
    | 101 |  | honderd eerste |

Scenario: Ordinal conversion is supported
    Given I use the "NL" number converter
    Then the converter supports ordinal conversion

Scenario Outline: Year wording
    Given I use the "NL" number converter
    When I convert the year <year>
    Then the result is "<expected>"

Examples:
    | year | expected                  |
    | 1984 | negentien vierentachtig   |
    | 1900 | negentien honderd         |
    | 1100 | elf honderd               |

Scenario: Fraction connector wording
    Given I use the "NL" number converter
    When I convert the fraction 3/2 through both public fraction APIs
    Then both fraction results are "drie op twee"

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "NL" number converter
    Then the converter supports clock-time conversion

# Taaladvies.net "Kloktijden": quarters of the hour read "over <h>", "voor half <h+1>",
# "over half <h+1>", "voor <h+1>". The whole hour keeps the distinctive accent ("één uur",
# Taaladvies "Klemtoonteken"), which is not needed once "een" cannot be read as the article.
# "half twee" (two words) is retained; Taaladvies accepts both "half twee" and "halftwee".
Scenario Outline: Idiomatic Dutch clock times
    Given I use the "NL" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | één uur |
    | 02:00 | twee uur |
    | 01:05 | vijf over een |
    | 01:10 | tien over een |
    | 01:15 | kwart over een |
    | 01:20 | tien voor half twee |
    | 01:25 | vijf voor half twee |
    | 01:30 | half twee |
    | 01:35 | vijf over half twee |
    | 01:40 | tien over half twee |
    | 01:45 | kwart voor twee |
    | 01:50 | tien voor twee |
    | 01:55 | vijf voor twee |
    | 12:00 | twaalf uur |
    | 13:30 | half twee |
    | 12:45 | kwart voor een |

Scenario Outline: Dutch clock times round to the nearest five minutes and carry past midnight
    Given I use the "NL" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:27 | vijf voor half twee |
    | 01:28 | half twee |
    | 23:58 | twaalf uur |

Scenario: Clock wording does not change Dutch cardinals
    Given I use the "NL" number converter
    When I convert the cardinal number 1
    Then the result is "een"
