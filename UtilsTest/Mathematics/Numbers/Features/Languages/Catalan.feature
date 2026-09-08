@NumberToString
Feature: Catalan number conversion

Scenario Outline: CatalanCardinals 1
    Given I use the "CA" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 21 | vint-i-un |
    | 105 | cent cinc |
    | 321 | tres-cents vint-i-un |

Scenario Outline: CatalanDecimal 2
    Given I use the "ca-ES" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | un coma cinc |

Scenario Outline: Cardinals_Gender_Femeni 3
    Given I use the "CA" number converter
    And I use the variants "gender=femení"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | una |
    | 2 | dues |
    | 21 | vint-i-una |
    | 22 | vint-i-dues |
