@NumberToString
Feature: Zulu number conversion

Scenario Outline: DecimalTest 1
    Given I use the "ZU" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | kunye phuzu isihlanu |
    | 12.34 | ishumi nambili phuzu kuthathu kune |
