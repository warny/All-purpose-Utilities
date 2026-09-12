@NumberToString @TR
Feature: Turkish number conversion

Background:
    Given I use the "TR" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | sıfır |
    | 1 | bir |
    | 2 | iki |
    | 3 | üç |
    | 9 | dokuz |
    | 10 | on |
    | 11 | on bir |
    | 12 | on iki |
    | 19 | on dokuz |
    | 20 | yirmi |
    | 21 | yirmi bir |
    | 100 | yüz |
    | 101 | yüz bir |
    | 200 | iki yüz |
    | 1000 | bin |
    | 2000 | iki bin |
    | 10000 | on bin |
    | 1000000 | bir milyon |
    | 1000000000 | bir milyar |
    | 1000000000000 | bir trilyon |
    | 1000000000000000 | bir katrilyon |
    | 1000000000000000000 | bir kentilyon |
    | -1 | eksi bir |

Scenario: The regional alias uses Turkish wording
    Then the "TR" and "TR-TR" converters produce the same cardinal wording for 2
