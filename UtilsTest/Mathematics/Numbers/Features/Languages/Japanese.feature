@NumberToString
Feature: Japanese number conversion

Scenario Outline: DecimalTest 1
    Given I use the "JA" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | 一 点 五 |
    | 12.34 | 十二 点 三 四 |

Scenario Outline: Cardinals_Thousands 2
    Given I use the "JA" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | 千 |
    | 2000 | 二 千 |

Scenario Outline: Cardinals_Hundred_NoLeadingOne 3
    Given I use the "JA" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 100 | 百 |
