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
    | 1000 | một nghìn |
    | 2000 | hai nghìn |
    | 1000000 | một triệu |
    | 2000000 | hai triệu |
    | 1000000000 | một tỷ |
    | 999999999999 | chín trăm chín mươi chín tỷ chín trăm chín mươi chín triệu chín trăm chín mươi chín nghìn chín trăm chín mươi chín |

# NTS-27: a class (lớp) that follows a higher non-zero class is read with all its positions: "không trăm"
# for zero hundreds, then "linh" before a lone unit (Toán 4 solution keys, vietjack.com, SGK Toán 4
# p. 15 ex. 2: 400 070 192 "bốn trăm triệu không trăm bảy … nghìn một trăm chín mươi hai"; loigiaihay.com:
# 178 320 005 "… ba trăm hai mươi nghìn không trăm linh năm"); 1000 is "một nghìn".
Scenario Outline: NTS-27 Vietnamese classes after a higher class are read in full
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 100 | một trăm |
    | 101 | một trăm linh một |
    | 105 | một trăm linh năm |
    | 110 | một trăm mười |
    | 1000 | một nghìn |
    | 1001 | một nghìn không trăm linh một |
    | 1005 | một nghìn không trăm linh năm |
    | 1010 | một nghìn không trăm mười |
    | 1015 | một nghìn không trăm mười lăm |
    | 1021 | một nghìn không trăm hai mươi mốt |
    | 1100 | một nghìn một trăm |
    | 2000 | hai nghìn |
    | 2005 | hai nghìn không trăm linh năm |
    | 10005 | mười nghìn không trăm linh năm |
    | 20005 | hai mươi nghìn không trăm linh năm |
    | 100005 | một trăm nghìn không trăm linh năm |
    | 999999 | chín trăm chín mươi chín nghìn chín trăm chín mươi chín |
    | 178320005 | một trăm bảy mươi tám triệu ba trăm hai mươi nghìn không trăm linh năm |
    | 400070192 | bốn trăm triệu không trăm bảy mươi nghìn một trăm chín mươi hai |
    | -2005 | âm hai nghìn không trăm linh năm |

# NTS-30: a whole zero class between two non-zero classes is read "không" + class name (SGK Toán 4, Chân trời
# sáng tạo, Bài 25: "307 000 262 Đọc số: Ba trăm linh bảy triệu không nghìn hai trăm sáu mươi hai", "Ba triệu
# không nghìn không trăm mười ba: 3 000 013"); a trailing zero class is not read ("5 000 000 đọc là: Năm triệu").
Scenario Outline: NTS-30 Vietnamese whole zero classes
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 50000102 | năm mươi triệu không nghìn một trăm linh hai |
    | 3000013 | ba triệu không nghìn không trăm mười ba |
    | 307000262 | ba trăm linh bảy triệu không nghìn hai trăm sáu mươi hai |
    | 1000005 | một triệu không nghìn không trăm linh năm |
    | 5000000 | năm triệu |
    | 50001000 | năm mươi triệu không trăm linh một nghìn |
    | -50000102 | âm năm mươi triệu không nghìn một trăm linh hai |
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
    | 100 | thứ một trăm |
    | 101 | thứ một trăm linh một |
    | 999 | thứ chín trăm chín mươi chín |

# NTS-08 validation: Vietnamese dictionaries (vtudien, Việt-Việt): "tư … Bốn: xếp thứ tư"; "thứ … chỗ
# trong trật tự sắp xếp: hàng thứ nhất". NTS-27 corrected the cardinals from 1000 ("một nghìn", "hai nghìn
# không trăm linh năm"), but no source settles the ordinal of these values: they still fail closed (NTS-28).
# NTS-28: vtudien (Việt-Việt), thứ "Đặt trước số từ để chỉ thứ tự": before any numeral; since NTS-27 the cardinal
# is right from 1000. NTS-30: a whole zero class between two non-zero classes is read "không nghìn" (SGK Toán 4);
# two consecutive such classes are not attested and their ordinals still fail closed.
Scenario Outline: NTS-28 Vietnamese ordinals from a thousand
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | thứ một nghìn |
    | 2005 | thứ hai nghìn không trăm linh năm |
    | 1000000 | thứ một triệu |
    | 1001001 | thứ một triệu không trăm linh một nghìn không trăm linh một |
    | 1000005 | thứ một triệu không nghìn không trăm linh năm |
    | 50000102 | thứ năm mươi triệu không nghìn một trăm linh hai |

Scenario Outline: Vietnamese ordinals with consecutive zero classes are not supported
    When I attempt to convert the ordinal number <number>
    Then conversion is rejected because no ordinal form is available

Examples:
    | number |
    | 1000000005 |
    | 1000000000000 |

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
