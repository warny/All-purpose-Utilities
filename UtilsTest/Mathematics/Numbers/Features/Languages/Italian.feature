@NumberToString @IT
Feature: Italian number conversion

Background:
    Given I use the "IT" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | uno |
    | 11 | undici |
    | 20 | venti |
    | 21 | venti uno |
    | 22 | venti due |
    | 29 | venti nove |
    | 100 | cento |
    | 1000 | mille |
    | 2000 | due mila |

Scenario Outline: Feminine cardinal numbers
    Given I use the variants "gender=femminile"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | una |
    | 21 | venti una |
    | 22 | venti due |

Scenario Outline: Ordinal numbers
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | primo |
    | 2 | secondo |
    | 3 | terzo |
    | 10 | decimo |
    | 11 | undicesimo |
    | 20 | ventesimo |
    | 100 | centesimo |

Scenario Outline: Feminine ordinal numbers
    Given I use the variants "gender=femminile"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | prima |
    | 2 | seconda |
    | 11 | undicesima |

Scenario Outline: Decimal numbers
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | uno virgola cinque |
    | 12.34 | dodici virgola tre quattro |

Scenario Outline: Masculine ordinal numbers
    Given I use the "IT" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1000 |  | millesimo |

Scenario Outline: Additional feminine ordinal numbers
    Given I use the "IT" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 20 | gender=femminile | ventesima |
    | 1000 | gender=femminile | millesima |

Scenario Outline: Caller-defined currency wording
    Given I use this currency definition
        | property         | value      |
        | unit singular    | euro       |
        | unit plural      | euro       |
        | subunit singular | centesimo  |
        | subunit plural   | centesimi  |
        | connector        | e          |
    When I convert the currency amount <amount>
    Then the result is "<expected>"

Examples:
    | amount | expected                            |
    | 1      | uno euro                            |
    | 2      | due euro                            |
    | 1.50   | uno euro e cinquanta centesimi      |

Scenario: Ordinal conversion is supported
    Given I use the "IT" number converter
    Then the converter supports ordinal conversion
