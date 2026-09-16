@NumberToString
Feature: English number conversion

Scenario Outline: From1To999Test 1
    Given I use the "en-UK" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | -1 | minus one |
    | 0 | zero |
    | 1 | one |
    | 2 | two |
    | 11 | eleven |
    | 20 | twenty |
    | 21 | twenty-one |
    | 22 | twenty-two |
    | 60 | sixty |
    | 61 | sixty-one |
    | 62 | sixty-two |
    | 72 | seventy-two |
    | 82 | eighty-two |
    | 92 | ninety-two |
    | 111 | one hundred and eleven |
    | 121 | one hundred and twenty-one |
    | 122 | one hundred and twenty-two |
    | 160 | one hundred and sixty |
    | 161 | one hundred and sixty-one |
    | 162 | one hundred and sixty-two |
    | 200 | two hundred |
    | 201 | two hundred and one |
    | 221 | two hundred and twenty-one |
    | 222 | two hundred and twenty-two |
    | 260 | two hundred and sixty |
    | 261 | two hundred and sixty-one |
    | 262 | two hundred and sixty-two |

Scenario Outline: From1000To9999Test 2
    Given I use the "en-US" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | one thousand |
    | 1001 | one thousand, one |
    | 1002 | one thousand, two |
    | 1011 | one thousand, eleven |
    | 1020 | one thousand, twenty |
    | 1021 | one thousand, twenty-one |
    | 1022 | one thousand, twenty-two |
    | 1060 | one thousand, sixty |
    | 1061 | one thousand, sixty-one |
    | 1062 | one thousand, sixty-two |
    | 1111 | one thousand, one hundred and eleven |
    | 1121 | one thousand, one hundred and twenty-one |
    | 1122 | one thousand, one hundred and twenty-two |
    | 1160 | one thousand, one hundred and sixty |
    | 1161 | one thousand, one hundred and sixty-one |
    | 1162 | one thousand, one hundred and sixty-two |
    | 1200 | one thousand, two hundred |
    | 1201 | one thousand, two hundred and one |
    | 1221 | one thousand, two hundred and twenty-one |
    | 1222 | one thousand, two hundred and twenty-two |
    | 1260 | one thousand, two hundred and sixty |
    | 1261 | one thousand, two hundred and sixty-one |
    | 1262 | one thousand, two hundred and sixty-two |

Scenario Outline: From10000To99999Test 3
    Given I use the "en-UK" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 12000 | twelve thousand |
    | 12001 | twelve thousand, one |
    | 12002 | twelve thousand, two |
    | 12011 | twelve thousand, eleven |
    | 12020 | twelve thousand, twenty |
    | 12021 | twelve thousand, twenty-one |
    | 12022 | twelve thousand, twenty-two |
    | 12060 | twelve thousand, sixty |
    | 12061 | twelve thousand, sixty-one |
    | 12062 | twelve thousand, sixty-two |
    | 12111 | twelve thousand, one hundred and eleven |
    | 12121 | twelve thousand, one hundred and twenty-one |
    | 12122 | twelve thousand, one hundred and twenty-two |
    | 99160 | ninety-nine thousand, one hundred and sixty |
    | 99161 | ninety-nine thousand, one hundred and sixty-one |
    | 99162 | ninety-nine thousand, one hundred and sixty-two |
    | 99200 | ninety-nine thousand, two hundred |
    | 99201 | ninety-nine thousand, two hundred and one |
    | 99221 | ninety-nine thousand, two hundred and twenty-one |
    | 99222 | ninety-nine thousand, two hundred and twenty-two |
    | 99260 | ninety-nine thousand, two hundred and sixty |
    | 99261 | ninety-nine thousand, two hundred and sixty-one |
    | 99262 | ninety-nine thousand, two hundred and sixty-two |

Scenario Outline: BiggerTest 4
    Given I use the "en-UK" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | -401000 | minus four hundred and one thousand |
    | 401000 | four hundred and one thousand |
    | 999999 | nine hundred and ninety-nine thousand, nine hundred and ninety-nine |
    | 1000000 | one million |
    | 999999999 | nine hundred and ninety-nine million, nine hundred and ninety-nine thousand, nine hundred and ninety-nine |

Scenario Outline: DecimalTest 5
    Given I use the "en-UK" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | one point five tenths |
    | 12.34 | twelve point thirty-four hundredths |

Scenario Outline: BigIntTest 6
    Given I use the "EN-uk" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number                                                                          | expected                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
    | 1852673427797059126777135760139006525652319754650249024631321344126610074238975 | one quinquavingtillion, eight hundred and fifty-two quattuorvingtillion, six hundred and seventy-three tresvingtillion, four hundred and twenty-seven duovingtillion, seven hundred and ninety-seven univingtillion, fifty-nine vingtillion, one hundred and twenty-six novendecillion, seven hundred and seventy-seven octodecillion, one hundred and thirty-five septendecillion, seven hundred and sixty sedecillion, one hundred and thirty-nine quinquadecillion, six quattuordecillion, five hundred and twenty-five tredecillion, six hundred and fifty-two duodecillion, three hundred and nineteen unidecillion, seven hundred and fifty-four decillion, six hundred and fifty nonillion, two hundred and forty-nine octillion, twenty-four septillion, six hundred and thirty-one sextillion, three hundred and twenty-one quintillion, three hundred and forty-four quadrillion, one hundred and twenty-six trillion, six hundred and ten billion, seventy-four million, two hundred and thirty-eight thousand, nine hundred and seventy-five |


Scenario Outline: DateTests 6
    Given I use the "<culture>" culture info
    When I convert the date "<date>"
    Then the result is "<expected>"

Examples: 
    | culture | date       | expected                                 |
    | EN-uk   | 01/01/2026 | first January twenty twenty-six          |
    | EN-uk   | 12/29/2026 | twenty-ninth December twenty twenty-six  |
    | EN-uk   | 12/31/2026 | thirty-first December twenty twenty-six  |
    | EN-us   | 01/01/2026 | January first, twenty twenty-six         |
    | EN-us   | 12/29/2026 | December twenty-ninth, twenty twenty-six |
    | EN-us   | 12/31/2026 | December thirty-first, twenty twenty-six |

Scenario Outline: TimeTest 7
    Given I use the "EN-uk" culture info
    When I convert the time "<time>"
    Then the result is "<expected>"
   
Examples: 
    | time     | expected                          |
    | 01:01:01 | one hour one minute one second    |
    | 01:01:03 | one hour one minute three seconds |
    | 13:10:00 | thirteen hours ten minutes        |
    | 21:00:00 | twenty-one hours                  |
    | 12:00:00 | twelve hours                      |