@NumberToString @VN
Feature: Vietnamese number conversion

Background:
    Given I use the "VN" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | không |
    | 1 | một |
    | 2 | hai |
    | 9 | chín |
    | 10 | mười |
    | 11 | mười một |
    | 15 | mười lăm |
    | 19 | mười chín |
    | 20 | hai mươi |
    | 21 | hai mươi mốt |
    | 25 | hai mươi lăm |
    | 100 | một trăm |
    | 1000 | nghìn |
    | 2000 | hai nghìn |
    | 1000000 | một triệu |
    | 2000000 | hai triệu |
    | 1000000000 | một tỷ |
    | 999999999999 | chín trăm chín mươi chín tỷ chín trăm chín mươi chín triệu chín trăm chín mươi chín nghìn chín trăm chín mươi chín |

Scenario Outline: Ordinal numbers
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | thứ nhất |
    | 2 | thứ hai |
    | 10 | thứ mười |
Scenario: Vietnamese regional alias wording
    Then the "VN" and "VI-VN" converters produce the same cardinal wording for 21

Scenario Outline: Hundreds use the connecting word before a final digit
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 101 | một trăm linh một |
    | 102 | một trăm linh hai |
    | 105 | một trăm linh năm |
    | 109 | một trăm linh chín |
    | 201 | hai trăm linh một |
    | 209 | hai trăm linh chín |

Scenario Outline: The connecting word is omitted when it is not needed
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 5 | năm |
    | 110 | một trăm mười |
    | 121 | một trăm hai mươi mốt |
    | 200 | hai trăm |

Scenario: Supported aliases use Vietnamese wording
    Then the "VN" and "VI" converters produce the same cardinal wording for 2
    And the "VN" and "VI-VN" converters produce the same cardinal wording for 2

Scenario: Values above the supported maximum are rejected
    When I attempt to convert the cardinal number 1000000000000
    Then conversion is rejected because the value is out of range

# The ordinal of four is the Sino-Vietnamese "thứ tư", not "thứ bốn"; like "thứ nhất" it is an
# exception to the "thứ" + cardinal rule. Compounds keep the cardinal ("thứ mười bốn").
Scenario Outline: Vietnamese irregular ordinals
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | thứ nhất |
    | 4 | thứ tư |
    | 14 | thứ mười bốn |
    | 21 | thứ hai mươi mốt |

Scenario: Idiomatic clock-time conversion is supported
    Then the converter supports clock-time conversion

# Neutral 12-hour clock without day-part wording (no sáng/chiều/tối): "<hour> giờ", "<hour> giờ
# <minutes>", "<hour> giờ rưỡi", "<next hour> giờ kém mười lăm".
Scenario Outline: Idiomatic Vietnamese clock times
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | một giờ |
    | 01:15 | một giờ mười lăm |
    | 01:30 | một giờ rưỡi |
    | 01:45 | hai giờ kém mười lăm |
    | 02:00 | hai giờ |
    | 12:45 | một giờ kém mười lăm |
    | 13:30 | một giờ rưỡi |
