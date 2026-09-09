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
    | 2000 | 이 천 |
    | 10000 | 십 천 |

Scenario Outline: Ordinals Prefix
    Given I use the "KO" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | 제일 |
    | 2 | 제이 |
    | 10 | 제십 |
    | 11 | 제십일 |

Scenario Outline: Negative
    Given I use the "KO" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | -1 | 마이너스 일 |
