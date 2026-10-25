@NumberToString @JA
Feature: Japanese number conversion

Scenario Outline: Decimal numbers
    Given I use the "JA" number converter
    # NTS-34: no validated automatic decimal reading (LanguageDefault fails closed); the explicit digit-by-digit reading is pinned.
    When I convert the decimal number <number> digit by digit
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | 一点五 |
    | 12.34 | 十二点三四 |
    # NTS-30A: a decimal digit 0 is read 零 (leading, trailing, internal).
    | 21.05 | 二十一点零五 |
    | 21.50 | 二十一点五零 |
    | 21.505 | 二十一点五零五 |
    | 21.005 | 二十一点零零五 |
    | 0.05 | 零点零五 |

Scenario Outline: Thousands
    Given I use the "JA" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | 千 |
    | 2000 | 二千 |

# NTS-27: Japanese counts by groups of four digits named 万, 億, 兆 (ja.wikipedia "漢数字": "数を下位
# … から 4 桁ごとに区切り、各 4 桁の組に「万」、「億」などの単位を付けて、上位から読む"); inside a group
# 一 is not said before 百 and 十 and is optional before 千 ("百位または十位が 1 のとき、「一」を言わず
# … 千位は「一」を読んでも読まなくても良い"; the configuration keeps 千). No 零 and no spaces.
Scenario Outline: NTS-27 Japanese cardinals count by 万
    Given I use the "JA" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | 一 |
    | 10 | 十 |
    | 11 | 十一 |
    | 20 | 二十 |
    | 100 | 百 |
    | 101 | 百一 |
    | 110 | 百十 |
    | 1000 | 千 |
    | 1001 | 千一 |
    | 2005 | 二千五 |
    | 10000 | 一万 |
    | 10001 | 一万一 |
    | 11000 | 一万千 |
    | 100000 | 十万 |
    | 1000000 | 百万 |
    | 10000000 | 千万 |
    | 100000000 | 一億 |
    | 100010000 | 一億一万 |
    | 123456789 | 一億二千三百四十五万六千七百八十九 |
    | 1000000000000 | 一兆 |
    | 9999999999999999 | 九千九百九十九兆九千九百九十九億九千九百九十九万九千九百九十九 |
    | -2005 | マイナス 二千五 |

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

# NTS-08 validation: デジタル大辞泉, 第: "数を表す語に付いて、ものの順序を表す" (第一, 第五巻). NTS-27 rebuilt the
# cardinals above 1000 (counting by 万), but the ordinals there are not validated yet: they still fail
# closed (NTS-28).
Scenario Outline: Japanese ordinals in the validated domain
    Given I use the "JA" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 100 | 第百 |
    | 101 | 第百一 |
    | 1000 | 第千 |

# NTS-28: デジタル大辞泉 第 "数を表す語に付いて、ものの順序を表す": 第 before any numeral; since NTS-27 the cardinal
# counts by 万, so the ordinal covers the whole cardinal domain (to 9999兆).
Scenario Outline: NTS-28 Japanese ordinals of the whole cardinal domain
    Given I use the "JA" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1001 | 第千一 |
    | 2005 | 第二千五 |
    | 10000 | 第一万 |
    | 123456789 | 第一億二千三百四十五万六千七百八十九 |
    | 9999999999999999 | 第九千九百九十九兆九千九百九十九億九千九百九十九万九千九百九十九 |

Scenario: Japanese ordinals beyond the cardinal domain are not supported
    Given I use the "JA" number converter
    When I attempt to convert the ordinal number 10000000000000000
    Then conversion is rejected because no ordinal form is available

Scenario: Ordinal conversion is supported
    Given I use the "JA" number converter
    Then the converter supports ordinal conversion

Scenario: Fraction connector wording
    Given I use the "JA" number converter
    When I convert the fraction 3/2 through both public fraction APIs
    Then both fraction results are "三割る二"

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
