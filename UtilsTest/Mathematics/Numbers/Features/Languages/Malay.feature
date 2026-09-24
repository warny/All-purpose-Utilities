@NumberToString @MS
Feature: Malay number conversion

Background:
    Given I use the "MS" number converter

Scenario: Malay ordinal conversion is supported
    Then the converter supports ordinal conversion

Scenario Outline: Malay cardinals preserve the lapan stem
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 8 | lapan |
    | 18 | lapan belas |
    | 28 | dua puluh lapan |
    | 80 | lapan puluh |
    | 800 | lapan ratus |
    | 1000000000 | satu bilion |
    | 1000000000000 | satu trilion |
    | 1000000000000000 | satu kuadrilion |
    | 1000000000000000000 | satu kuintilion |

Scenario Outline: Productive Malay ordinals
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | pertama |
    | 2 | kedua |
    | 3 | ketiga |
    | 8 | kelapan |
    | 10 | kesepuluh |
    | 21 | kedua puluh satu |
    | 100 | keseratus |
    | 1000 | keseribu |
    | 1000000000 | kesatu bilion |
    | 1000000000000 | kesatu trilion |
    | 1000000000000000 | kesatu kuadrilion |
    | 1000000000000000000 | kesatu kuintilion |

Scenario: Malay ClockTime uses its own pukul convention
    Then the converter supports clock-time conversion

Scenario Outline: Malay clock times
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | pukul satu |
    | 01:15 | pukul satu suku |
    | 01:30 | pukul satu setengah |
    | 01:45 | pukul dua kurang suku |
    | 01:55 | pukul dua kurang lima minit |
