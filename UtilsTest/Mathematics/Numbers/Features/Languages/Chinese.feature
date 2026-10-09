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

# NTS-08 validation: 國語辭典 (汉典), 第: "用於整數數詞之前。表事物的順序或等級" (第一, 第二). Above 100 the configured
# cardinal omits 零 and 一十 ("一百一" for 101, "一百十" for 110) and splits the thousands ("一 千"): only the
# round hundreds stay supported, the other ordinals fail closed.
Scenario Outline: Chinese ordinals in the validated domain
    Given I use the "ZH" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 20 | 第二十 |
    | 21 | 第二十一 |
    | 200 | 第二百 |
    | 900 | 第九百 |

Scenario Outline: Chinese ordinals outside the validated domain are not supported
    Given I use the "ZH" number converter
    When I attempt to convert the ordinal number <number>
    Then conversion is rejected because no ordinal form is available

Examples:
    | number |
    | 101 |
    | 110 |
    | 111 |
    | 1000 |
    | 10000 |

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
    | 00:00 | 十二点 |
    | 00:15 | 十二点十五分 |
    | 02:15 | 两点十五分 |
    | 11:45 | 十一点四十五分 |
    | 12:15 | 十二点十五分 |
    | 12:30 | 十二点半 |
    | 12:45 | 十二点四十五分 |
    | 23:00 | 十一点 |
    | 23:45 | 十一点四十五分 |

Scenario: The clock form of two does not change the cardinal
    Given I use the "ZH" number converter
    When I convert the cardinal number 2
    Then the result is "二"
