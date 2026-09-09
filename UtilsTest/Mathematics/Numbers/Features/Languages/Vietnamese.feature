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
