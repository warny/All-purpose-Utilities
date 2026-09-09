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

Scenario Outline: Duration wording
    Given I use the "EN" number converter
    When I convert the duration "<value>"
    Then the result is "<expected>"

Examples:
    | value | expected |
    | 01:00:00 | one hour |
    | 02:30:05 | two hours thirty minutes five seconds |
    | 00:30:10 | thirty minutes ten seconds |

Scenario: Time-of-day wording
    Given I use the "EN" number converter
    When I convert the time "14:30:00"
    Then the result is "fourteen hours thirty minutes"

Scenario Outline: American date wording
    Given I use the "EN-US" number converter
    When I convert the date "<value>"
    Then the result is "<expected>"

Examples:
    | value | expected |
    | 2026-07-01 | July first, twenty twenty-six |
    | 2026-07-02 | July second, twenty twenty-six |

Scenario Outline: British date wording
    Given I use the "<culture>" number converter
    When I convert the date "<value>"
    Then the result is "<expected>"

Examples:
    | culture | value | expected |
    | EN-GB | 2026-07-01 | first July twenty twenty-six |
    | EN-GB | 2026-07-02 | second July twenty twenty-six |
    | EN-uk | 2026-07-02 | second July twenty twenty-six |

Scenario Outline: Date and time wording
    Given I use the "<culture>" number converter
    When I convert the date and time "2026-07-02T14:30:05"
    Then the result is "<expected>"

Examples:
    | culture | expected |
    | EN-US | July second, twenty twenty-six fourteen hours thirty minutes five seconds |
    | EN-GB | second July twenty twenty-six fourteen hours thirty minutes five seconds |

@HugeNumber
Scenario Outline: Very large short-scale cardinal numbers
    Given I use the "EN" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number                                                                          | expected                  |
    | 1000000000000000000000000000000                                                 | one nonillion             |
    | 1000000000000000000000000000000000000000000000000000000000000                 | one novendecillion        |
    | 1000000000000000000000000000000000000000000000000000000000000000000000000000  | one quattuorvingtillion   |

@HugeNumber
Scenario: A very large cardinal number follows the shipped scale vocabulary
    Given I use the "en-UK" number converter
    When I convert the cardinal number 1852673427797059126777135760139006525652319754650249024631321344126610074238975
    Then the result is "one quinquavingtillion, eight hundred and fifty-two quattuorvingtillion, six hundred and seventy-three tresvingtillion, four hundred and twenty-seven duovingtillion, seven hundred and ninety-seven univingtillion, fifty-nine vingtillion, one hundred and twenty-six novendecillion, seven hundred and seventy-seven octodecillion, one hundred and thirty-five septendecillion, seven hundred and sixty sedecillion, one hundred and thirty-nine quinquadecillion, six quattuordecillion, five hundred and twenty-five tredecillion, six hundred and fifty-two duodecillion, three hundred and nineteen unidecillion, seven hundred and fifty-four decillion, six hundred and fifty nonillion, two hundred and forty-nine octillion, twenty-four septillion, six hundred and thirty-one sextillion, three hundred and twenty-one quintillion, three hundred and forty-four quadrillion, one hundred and twenty-six trillion, six hundred and ten billion, seventy-four million, two hundred and thirty-eight thousand, nine hundred and seventy-five"

Scenario: Ordinal conversion is supported
    Given I use the "EN" number converter
    Then the converter supports ordinal conversion

Scenario: Temporal conversion is supported
    Given I use the "EN" number converter
    Then the converter supports time conversion
    And the converter supports date conversion

Scenario Outline: Year wording
    Given I use the "EN" number converter
    When I convert the year <year>
    Then the result is "<expected>"

Examples:
    | year | expected                  |
    | 1984 | nineteen eighty-four      |
    | 1900 | nineteen hundred          |
    | 1905 | nineteen oh five           |
    | 1100 | eleven hundred             |
    | 2024 | twenty twenty-four         |
    | 2010 | twenty ten                 |
    | 2000 | two thousand               |
    | 2005 | two thousand, five         |
    | 1066 | one thousand, sixty-six    |

Scenario Outline: Caller-defined dollar wording
    Given I use the "EN" number converter
    And I use this currency definition
        | property         | value   |
        | unit singular    | dollar  |
        | unit plural      | dollars |
        | subunit singular | cent    |
        | subunit plural   | cents   |
        | connector        | and     |
    When I convert the currency amount <amount>
    Then the result is "<expected>"

Examples:
    | amount | expected                            |
    | 0      | zero dollars                        |
    | 1      | one dollar                          |
    | 2      | two dollars                         |
    | 1.50   | one dollar and fifty cents          |
    | 12.01  | twelve dollars and one cent         |
    | -5.50  | minus five dollars and fifty cents  |
    | 1.999  | two dollars                         |
    | 0.995  | one dollar                          |
