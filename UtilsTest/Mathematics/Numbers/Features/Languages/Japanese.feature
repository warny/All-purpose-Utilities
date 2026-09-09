@NumberToString @JA
Feature: Japanese number conversion

Scenario Outline: Decimal numbers
    Given I use the "JA" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | 一 点 五 |
    | 12.34 | 十二 点 三 四 |

Scenario Outline: Thousands
    Given I use the "JA" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | 千 |
    | 2000 | 二 千 |

Scenario Outline: Hundreds without a leading one
    Given I use the "JA" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 100 | 百 |

Scenario Outline: Prefixed ordinal numbers
    Given I use the "JA" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 |  | 第一 |
    | 3 |  | 第三 |
