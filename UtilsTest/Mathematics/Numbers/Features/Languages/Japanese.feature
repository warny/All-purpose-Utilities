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

Scenario: Ordinal conversion is supported
    Given I use the "JA" number converter
    Then the converter supports ordinal conversion

Scenario: Fraction connector wording
    Given I use the "JA" number converter
    When I convert the fraction 3/2 through both public fraction APIs
    Then both fraction results are "三 割る 二"

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "JA" number converter
    Then the converter supports clock-time conversion

# Neutral 12-hour clock without 午前/午後: "<hour>時", "<hour>時<minutes>分", "<hour>時半".
Scenario Outline: Idiomatic Japanese clock times
    Given I use the "JA" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | 一時 |
    | 01:05 | 一時五分 |
    | 01:15 | 一時十五分 |
    | 01:30 | 一時半 |
    | 01:45 | 一時四十五分 |
    | 02:00 | 二時 |
    | 04:00 | 四時 |
    | 13:00 | 一時 |
    | 01:28 | 一時半 |
