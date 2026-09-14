@NumberToString @ID
Feature: Indonesian number conversion

Background:
    Given I use the "ID" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | nol |
    | 1 | satu |
    | 2 | dua |
    | 3 | tiga |
    | 9 | sembilan |
    | 10 | sepuluh |
    | 11 | sebelas |
    | 12 | dua belas |
    | 19 | sembilan belas |
    | 20 | dua puluh |
    | 21 | dua puluh satu |
    | 100 | seratus |
    | 101 | seratus satu |
    | 200 | dua ratus |
    | 1000 | seribu |
    | 2000 | dua ribu |
    | 10000 | sepuluh ribu |
    | 1000000 | satu juta |
    | 1000000000 | satu miliar |
    | 1000000000000 | satu triliun |
    | 1000000000000000 | satu kuadriliun |
    | -1 | negatif satu |
Scenario Outline: Malay alias wording
    Then the "ID" and "MS" converters produce the same cardinal wording for <number>

Examples:
    | number |
    | 21 |
    | 1000000 |

Scenario: The regional alias uses Indonesian wording
    Then the "ID" and "ID-ID" converters produce the same cardinal wording for 2
