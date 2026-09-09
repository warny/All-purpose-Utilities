@NumberToString @ZU
Feature: Zulu number conversion

Scenario Outline: Decimal numbers
    Given I use the "ZU" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | kunye phuzu isihlanu |
    | 12.34 | ishumi nambili phuzu kuthathu kune |

Scenario: Ordinal conversion is unsupported
    Given I use the "ZU" number converter
    Then the converter does not support ordinal conversion
