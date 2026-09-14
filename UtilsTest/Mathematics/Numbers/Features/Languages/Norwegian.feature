@NumberToString @NO
Feature: Norwegian number conversion

Background:
    Given I use the "NO" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | null |
    | 1 | en |
    | 2 | to |
    | 9 | ni |
    | 10 | ti |
    | 11 | elleve |
    | 19 | nitten |
    | 20 | tjue |
    | 21 | tjue en |
    | 100 | ett hundre |
    | 1000 | tusen |
    | 2000 | to tusen |
    | 1000000 | en million |
    | 2000000 | to millioner |
    | 1000000000 | en milliard |
    | 3000000000 | tre milliarder |
    | 1000000000000 | en billion |
    | 1000000000000000 | en billiard |

Scenario: Ordinal conversion is unsupported
    Then the converter does not support ordinal conversion

Scenario: Supported aliases use Norwegian wording
    Then the "NO" and "NB" converters produce the same cardinal wording for 2
    And the "NO" and "NB-NO" converters produce the same cardinal wording for 2
