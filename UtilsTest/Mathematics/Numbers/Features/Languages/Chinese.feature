@NumberToString @ZH
Feature: Chinese number conversion

Scenario Outline: Decimal numbers
    Given I use the "ZH" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | 一 点 五 |
    | 12.34 | 十二 点 三 四 |

Scenario Outline: Basic cardinal numbers
    Given I use the "ZH" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | 一 |
    | 10 | 十 |
    | 20 | 二十 |
    | 100 | 一百 |
    | 1000 | 一 千 |

Scenario Outline: Ordinals Prefix Di
    Given I use the "ZH" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | 第一 |
    | 2 | 第二 |
    | 10 | 第十 |
    | 100 | 第一百 |
