@NumberToString
Feature: Polish number conversion

Scenario Outline: DecimalTest 1
    Given I use the "PL" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | jeden przecinek pięć |
    | 12.34 | dwanaście przecinek trzy cztery |
