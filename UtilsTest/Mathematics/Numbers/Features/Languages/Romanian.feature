@NumberToString @RO
Feature: Romanian number conversion

Background:
    Given I use the "RO" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | zero |
    | 1 | unu |
    | 2 | doi |
    | 10 | zece |
    | 11 | unsprezece |
    | 12 | doisprezece |
    | 19 | nouăsprezece |
    | 20 | douăzeci |
    | 21 | douăzeci și unu |
    | 22 | douăzeci și doi |
    | 100 | o sută |
    | 101 | o sută unu |
    | 200 | două sute |
    | 999 | nouă sute nouăzeci și nouă |

Scenario Outline: Scale connector wording
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | o mie |
    | 2000 | două mii |
    | 10000 | zece mii |
    | 12000 | doisprezece mii |
    | 19000 | nouăsprezece mii |
    | 20000 | douăzeci de mii |
    | 100000 | o sută de mii |
    | 1000000 | un milion |
    | 2000000 | două milioane |
    | 20000000 | douăzeci de milioane |
    | 1000000000 | un miliard |

Scenario Outline: Feminine cardinal numbers
    Given I use the variants "gen=feminin"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | una |
    | 2 | două |
    | 21 | douăzeci și una |
    | 22 | douăzeci și două |
