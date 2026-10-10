@NumberToString @KO
Feature: Korean number conversion

Scenario Outline: Basic cardinal numbers
    Given I use the "KO" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | 영 |
    | 1 | 일 |
    | 2 | 이 |
    | 3 | 삼 |
    | 9 | 구 |
    | 10 | 십 |
    | 11 | 십일 |
    | 12 | 십이 |
    | 19 | 십구 |
    | 20 | 이십 |
    | 21 | 이십일 |
    | 100 | 백 |
    | 101 | 백일 |
    | 200 | 이백 |

Scenario Outline: Thousands
    Given I use the "KO" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | 천 |
    | 2000 | 이천 |
    | 10000 | 일만 |

# NTS-27: Sino-Korean numbers count by 만, 억, 조 and are spaced by units of 만 only (한글 맞춤법 제44항:
# "십이억 삼천사백오십육만 칠천팔백구십팔"); 일 is not written before 십, 백, 천 inside a unit of 만, but the
# multiplier one of 만, 억, 조 is written (일억 삼천만). Decimals and fractions keep their spacing.
Scenario Outline: NTS-27 Korean cardinals count by 만
    Given I use the "KO" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | 일 |
    | 10 | 십 |
    | 11 | 십일 |
    | 100 | 백 |
    | 110 | 백십 |
    | 1000 | 천 |
    | 1001 | 천일 |
    | 2005 | 이천오 |
    | 10000 | 일만 |
    | 10001 | 일만 일 |
    | 100000 | 십만 |
    | 1000000 | 백만 |
    | 10000000 | 천만 |
    | 100000000 | 일억 |
    | 130000000 | 일억 삼천만 |
    | 1234567898 | 십이억 삼천사백오십육만 칠천팔백구십팔 |
    | 1000000000000 | 일조 |
    | 1000000000001 | 일조 일 |
    | -10001 | 마이너스 일만 일 |

Scenario Outline: Prefixed ordinal numbers
    Given I use the "KO" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | 제일 |
    | 2 | 제이 |
    | 10 | 제십 |
    | 11 | 제십일 |

Scenario Outline: Negative cardinal numbers
    Given I use the "KO" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | -1 | 마이너스 일 |

Scenario: The converter is available
    Given I use the "KO" number converter

# NTS-08 validation: 한글 맞춤법 제43항 ("제삼 항" / "제삼항"): 제 + Sino-Korean numeral. NTS-27 rebuilt the
# cardinals by units of 만 (제44항), but the ordinals above 1000 are not validated yet: they still fail
# closed (NTS-28).
Scenario Outline: Korean ordinals in the validated domain
    Given I use the "KO" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 100 | 제백 |
    | 101 | 제백일 |
    | 1000 | 제천 |

Scenario Outline: Korean ordinals outside the validated domain are not supported
    Given I use the "KO" number converter
    When I attempt to convert the ordinal number <number>
    Then conversion is rejected because no ordinal form is available

Examples:
    | number |
    | 1001 |
    | 2000 |
    | 10000 |

Scenario: Ordinal conversion is supported
    Given I use the "KO" number converter
    Then the converter supports ordinal conversion

Scenario: Fraction connector wording
    Given I use the "KO" number converter
    When I convert the fraction 3/2 through both public fraction APIs
    Then both fraction results are "삼 나누기 이"

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "KO" number converter
    Then the converter supports clock-time conversion

# Hours use native Korean attributive numerals (한, 두, 세, 네 ... 열두) while minutes use the
# Sino-Korean cardinals. Spacing follows Hangeul Matchumbeop art. 43 (unit nouns are separated:
# "한 시 십오 분"); the first draft "한 시 십오분" attached the unit and was corrected.
Scenario Outline: Idiomatic Korean clock times
    Given I use the "KO" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | 한 시 |
    | 02:00 | 두 시 |
    | 03:00 | 세 시 |
    | 04:00 | 네 시 |
    | 11:00 | 열한 시 |
    | 12:00 | 열두 시 |
    | 01:05 | 한 시 오 분 |
    | 01:15 | 한 시 십오 분 |
    | 01:30 | 한 시 반 |
    | 01:45 | 한 시 사십오 분 |
    | 13:00 | 한 시 |
    | 05:00 | 다섯 시 |
    | 06:00 | 여섯 시 |
    | 07:00 | 일곱 시 |
    | 08:00 | 여덟 시 |
    | 09:00 | 아홉 시 |
    | 10:00 | 열 시 |
    | 00:00 | 열두 시 |
    | 00:15 | 열두 시 십오 분 |
    | 11:45 | 열한 시 사십오 분 |
    | 12:15 | 열두 시 십오 분 |
    | 12:45 | 열두 시 사십오 분 |
    | 23:45 | 열한 시 사십오 분 |

Scenario Outline: Korean clock hours do not change the Sino-Korean cardinals and ordinals
    Given I use the "KO" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | 일 |
    | 2 | 이 |
    | 12 | 십이 |
