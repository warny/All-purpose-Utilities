@NumberToString
Feature: Dutch number conversion

Scenario Outline: DecimalTest 1
    Given I use the "NL" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | een komma vijf |
    | 12.34 | twaalf komma drie vier |

Scenario Outline: Cardinals_Basic 2
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

Scenario Outline: Ordinals_SuffixAndExceptions 3
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
