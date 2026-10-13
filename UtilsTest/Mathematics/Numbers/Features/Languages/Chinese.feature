@NumberToString @ZH
Feature: Chinese number conversion

Scenario Outline: Decimal numbers
    Given I use the "ZH" number converter
    When I convert the decimal number <number>
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
    | 1000 | 一千 |

# NTS-27: Chinese counts by 万 and 亿; a skipped position inside the number is read 零 once, however many
# zeros it spans, zeros at the end of a group are not read (AllSet Learning, Chinese Grammar Wiki, "Zeros in
# numbers": 一百零一, 一千零一, 一千一百, 一万零一, 一万零一百零一); 十 keeps its 一 except at the head of the number
# (一百一十, "Chinese simply says 一百一十"; 十, 十万). 两 stays limited to the clock.
Scenario Outline: NTS-27 Chinese cardinals count by 万 with 零
    Given I use the "ZH" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 10 | 十 |
    | 11 | 十一 |
    | 20 | 二十 |
    | 101 | 一百零一 |
    | 105 | 一百零五 |
    | 110 | 一百一十 |
    | 115 | 一百一十五 |
    | 1001 | 一千零一 |
    | 1010 | 一千零一十 |
    | 1100 | 一千一百 |
    | 2005 | 二千零五 |
    | 10000 | 一万 |
    | 10001 | 一万零一 |
    | 10010 | 一万零一十 |
    | 10101 | 一万零一百零一 |
    | 100000 | 十万 |
    | 101000 | 十万一千 |
    | 1000000 | 一百万 |
    | 100000000 | 一亿 |
    | 100000001 | 一亿零一 |
    | 100100000 | 一亿零一十万 |
    | 999999999999 | 九千九百九十九亿九千九百九十九万九千九百九十九 |
    | -10001 | 负 一万零一 |

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

# NTS-08 validation: 國語辭典 (汉典), 第: "用於整數數詞之前。表事物的順序或等級" (第一, 第二). NTS-27 added 零 and 一十 to
# the cardinals and counts by 万, but the ordinals above 100 are not validated yet: only the round hundreds stay
# supported, the other ordinals still fail closed (NTS-28).
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

# NTS-28: 國語辭典 (汉典) 第 "用於整數數詞之前": before any integer numeral; since NTS-27 the cardinal has 零 and 一十
# and counts by 万, so the ordinal covers the whole cardinal domain (below 万亿).
Scenario Outline: NTS-28 Chinese ordinals of the whole cardinal domain
    Given I use the "ZH" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 101 | 第一百零一 |
    | 110 | 第一百一十 |
    | 111 | 第一百一十一 |
    | 1000 | 第一千 |
    | 10000 | 第一万 |
    | 10001 | 第一万零一 |
    | 999999999999 | 第九千九百九十九亿九千九百九十九万九千九百九十九 |

Scenario: Chinese ordinals beyond the cardinal domain are not supported
    Given I use the "ZH" number converter
    When I attempt to convert the ordinal number 1000000000000
    Then conversion is rejected because no ordinal form is available

Scenario: Ordinal conversion is supported
    Given I use the "ZH" number converter
    Then the converter supports ordinal conversion

Scenario: Fraction connector wording
    Given I use the "ZH" number converter
    When I convert the fraction 3/2 through both public fraction APIs
    Then both fraction results are "三除以二"

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
