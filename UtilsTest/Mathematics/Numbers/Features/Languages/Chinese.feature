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

Scenario Outline: Prefixed ordinal numbers
    Given I use the "ZH" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | 第一 |
    | 2 | 第二 |
    | 10 | 第十 |
    | 100 | 第一百 |

Scenario: Ordinal conversion is supported
    Given I use the "ZH" number converter
    Then the converter supports ordinal conversion

Scenario: Fraction connector wording
    Given I use the "ZH" number converter
    When I convert the fraction 3/2 through both public fraction APIs
    Then both fraction results are "三 除以 二"

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "ZH" number converter
    Then the converter supports clock-time conversion

# "两点" is the clock form of two (the counting cardinal stays "二"). Quarter-hour 12-hour clock
# without day-part wording: "<hour>点", "<hour>点十五分", "<hour>点半", "<hour>点四十五分".
Scenario Outline: Idiomatic Chinese clock times
    Given I use the "ZH" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | 一点 |
    | 02:00 | 两点 |
    | 01:15 | 一点十五分 |
    | 01:30 | 一点半 |
    | 01:45 | 一点四十五分 |
    | 02:30 | 两点半 |
    | 12:00 | 十二点 |
    | 14:00 | 两点 |

Scenario: The clock form of two does not change the cardinal
    Given I use the "ZH" number converter
    When I convert the cardinal number 2
    Then the result is "二"
