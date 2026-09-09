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
    | 2 |  | tweede |
    | 3 |  | derde |
    | 4 |  | vierde |
    | 5 |  | vijfde |
    | 6 |  | zesde |
    | 7 |  | zevende |
    | 8 |  | achtste |
    | 9 |  | negende |
    | 10 |  | tiende |
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
    | 20 |  | twintigste |
    | 21 |  | eenentwintigste |
    | 100 |  | honderdste |
    | 101 |  | honderd eerste |
