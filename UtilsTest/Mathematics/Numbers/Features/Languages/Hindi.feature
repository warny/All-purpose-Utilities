@NumberToString
Feature: Hindi number conversion

Scenario Outline: DecimalTest 1
    Given I use the "HI" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | एक दशमलव पांच |
    | 12.34 | बारह दशमलव तीन चार |
