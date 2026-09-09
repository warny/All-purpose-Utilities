@NumberToString @PT
Feature: Portuguese number conversion

Scenario Outline: Decimal numbers
    Given I use the "PT" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | um vírgula cinco |
    | 12.34 | doze vírgula três quatro |

Scenario Outline: Basic cardinal numbers
    Given I use the "PT" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | um |
    | 2 | dois |
    | 11 | onze |
    | 20 | vinte |
    | 100 | cem |
    | 101 | cento e um |
    | 200 | duzentos |
    | 1000 | mil |

Scenario Outline: Feminine cardinal numbers
    Given I use the "PT" number converter
    And I use the variants "gender=feminino"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | uma |
    | 2 | duas |
    | 21 | vinte e uma |
    | 22 | vinte e duas |
    | 200 | duzentas |
    | 201 | duzentas e uma |
    | 202 | duzentas e duas |
    | 300 | trezentas |
    | 400 | quatrocentas |
    | 500 | quinhentas |

Scenario Outline: Masculine ordinal numbers
    Given I use the "PT" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | primeiro |
    | 2 | segundo |
    | 10 | décimo |
    | 20 | vigésimo |
    | 100 | centésimo |
    | 1000 | milésimo |

Scenario Outline: Feminine ordinal numbers
    Given I use the "PT" number converter
    And I use the variants "gender=feminino"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | primeira |
    | 2 | segunda |
    | 10 | décima |
