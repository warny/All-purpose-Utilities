@NumberToString @EN
Feature: English number conversion

Scenario Outline: Cardinal numbers below one thousand
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

Scenario Outline: Thousands
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

Scenario Outline: Tens of thousands
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

Scenario Outline: Large cardinal numbers
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

Scenario Outline: Decimal numbers
    Given I use the "en-UK" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | one point five tenths |
    | 12.34 | twelve point thirty-four hundredths |

Scenario Outline: Irregular and regular ordinal numbers
    Given I use the "EN" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | first |
    | 2 | second |
    | 3 | third |
    | 4 | fourth |
    | 5 | fifth |
    | 6 | sixth |
    | 7 | seventh |
    | 8 | eighth |
    | 9 | ninth |
    | 10 | tenth |
    | 11 | eleventh |
    | 12 | twelfth |
    | 13 | thirteenth |
    | 100 | one hundredth |
    | 1000 | one thousandth |

Scenario Outline: Compound and negative ordinal numbers
    Given I use the "EN" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 21 | twenty-first |
    | 22 | twenty-second |
    | 23 | twenty-third |
    | 24 | twenty-fourth |
    | 30 | thirtieth |
    | 31 | thirty-first |
    | 101 | one hundred and first |
    | 1001 | one thousand, first |
    | -1 | minus first |
    | -21 | minus twenty-first |
