@NumberToString @SW
Feature: Swahili number conversion

Background:
    Given I use the "SW" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | sifuri |
    | 1 | moja |
    | 2 | mbili |
    | 3 | tatu |
    | 9 | tisa |
    | 10 | kumi |
    | 11 | kumi na moja |
    | 12 | kumi na mbili |
    | 19 | kumi na tisa |
    | 20 | ishirini |
    | 21 | ishirini na moja |
    | 100 | mia moja |
    | 101 | mia moja na moja |
    | 200 | mia mbili |
    | 1000 | elfu |
    | 2000 | mbili elfu |
    | 10000 | kumi elfu |
    | 1000000 | moja milioni |
    | 1000000000 | moja bilioni |
    | 1000000000000 | moja trilioni |
    | -1 | hasi moja |
