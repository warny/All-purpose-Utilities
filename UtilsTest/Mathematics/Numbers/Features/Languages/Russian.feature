@NumberToString @RU
Feature: Russian number conversion

Background:
    Given I use the "RU" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | один |
    | 2 | два |
    | 11 | одиннадцать |
    | 20 | двадцать |
    | 100 | сто |
    | 1000 | тысяча |
    | 1000000 | один миллион |
    | 2000000 | два миллион |
    | 1000000000 | один миллиард |
    | 1000000000000 | один биллион |
    | 1000000000000000 | один биллиард |
    | 1000000000000000000 | один триллион |

Scenario Outline: Ordinal numbers
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | первый |
    | 2 | второй |
    | 3 | третий |
    | 4 | четвёртый |
    | 10 | десятый |
    | 100 | сотый |
    | 1000 | тысячный |

Scenario Outline: Feminine ordinals
    Given I use the variants "gender=feminin"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | первая |

Scenario Outline: Neuter ordinals
    Given I use the variants "gender=neutrum"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | первое |

Scenario Outline: Plural ordinals
    Given I use the variants "gender=plural"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | первые |

Scenario Outline: Decimal numbers
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | один запятая пять |
    | 12.34 | двенадцать запятая три четыре |

Scenario Outline: Additional irregular and suffixed ordinal numbers
    Given I use the "RU" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 |  | первый |
    | 2 |  | второй |
    | 3 |  | третий |
    | 4 |  | четвёртый |
    | 5 |  | пятый |
    | 6 |  | шестой |
    | 7 |  | седьмой |
    | 8 |  | восьмой |
    | 9 |  | девятый |
    | 10 |  | десятый |
    | 11 |  | одиннадцатый |
    | 20 |  | двадцатый |
    | 21 |  | двадцать первый |
    | 40 |  | сороковой |
    | 100 |  | сотый |
    | 1000 |  | тысячный |
