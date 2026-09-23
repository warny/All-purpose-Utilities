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

Scenario: Indonesian ordinal and clock-time conversion are supported
    Then the converter supports ordinal conversion
    And the converter supports clock-time conversion

Scenario Outline: Productive Indonesian ordinals
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | pertama |
    | 2 | kedua |
    | 3 | ketiga |
    | 4 | keempat |
    | 10 | kesepuluh |
    | 21 | kedua puluh satu |
    | 100 | keseratus |
    | 1000 | keseribu |

Scenario Outline: Idiomatic Indonesian clock times
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | jam satu |
    | 01:05 | jam satu lewat lima |
    | 01:15 | jam satu lewat seperempat |
    | 01:30 | jam setengah dua |
    | 01:45 | jam dua kurang seperempat |
    | 01:55 | jam dua kurang lima |
