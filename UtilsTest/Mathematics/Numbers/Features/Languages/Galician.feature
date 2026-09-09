@NumberToString @GL
Feature: Galician number conversion

Scenario Outline: Galician cardinal numbers
    Given I use the "GL" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 21 | vinte e un |
    | 105 | cento cinco |
    | 100 | cen |

Scenario Outline: Galician decimal numbers
    Given I use the "gl-ES" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | un coma cinco |

Scenario Outline: Feminine cardinal numbers
    Given I use the "GL" number converter
    And I use the variants "gender=feminino"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | unha |
    | 2 | dúas |
    | 200 | douscentas |
    | 21 | vinte e unha |
    | 22 | vinte e dúas |
    | 201 | douscentas unha |
    | 300 | trescentos |
