@NumberToString
Feature: Portuguese number conversion

Scenario Outline: DecimalTest 1
    Given I use the "PT" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | um vírgula cinco |
    | 12.34 | doze vírgula três quatro |

Scenario Outline: Cardinals_Basic 2
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

Scenario Outline: Cardinals_Gender_Feminino 3
    Given I use the "PT" number converter
    And I use the variants "gender=feminino"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | uma |
    | 2 | duas |
    | 200 | duzentas |

Scenario Outline: Ordinals_MasculinoAndFeminino 4
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

Scenario Outline: Ordinals_MasculinoAndFeminino 5
    Given I use the "PT" number converter
    And I use the variants "gender=feminino"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | primeira |
    | 2 | segunda |
    | 10 | décima |
