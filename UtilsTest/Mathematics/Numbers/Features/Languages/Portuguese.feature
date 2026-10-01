@NumberToString @PT
Feature: Portuguese number conversion

Scenario Outline: Decimal numbers
    Given I use the "PT" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected                 |
    |    1.5 | um vírgula cinco         |
    |  12.34 | doze vírgula três quatro |

Scenario Outline: Basic cardinal numbers
    Given I use the "PT" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected   |
    |      1 | um         |
    |      2 | dois       |
    |     11 | onze       |
    |     20 | vinte      |
    |     21 | vinte e um |
    |    100 | cem        |
    |    101 | cento e um |
    |    200 | duzentos   |
    |   1000 | mil        |

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
    | number | expected  |
    |      1 | primeiro  |
    |      2 | segundo   |
    |     10 | décimo    |
    |     20 | vigésimo  |
    |    100 | centésimo |
    |   1000 | milésimo  |

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

Scenario Outline: Duration wording
    Given I use the "PT" number converter
    When I convert the duration "<value>"
    Then the result is "<expected>"

Examples:
    | value | expected |
    | 01:00:00 | uma hora |
    | 02:00:00 | duas horas |
    | 21:00:00 | vinte e uma horas |
    | 22:00:00 | vinte e duas horas |
    | 00:02:00 | dois minutos |
    | 02:02:00 | duas horas dois minutos |

Scenario Outline: Time-of-day wording
    Given I use the "PT" number converter
    When I convert the time "<value>"
    Then the result is "<expected>"

Examples:
    | value | expected |
    | 01:00:00 | uma hora |
    | 21:00:00 | vinte e uma horas |
    | 02:02:00 | duas horas dois minutos |

Scenario Outline: Explicit masculine ordinal numbers
    Given I use the "PT" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 |  | primeiro |
    | 9 |  | nono |
    | 10 |  | décimo |
    | 11 |  | décimo primeiro |
    | 19 |  | décimo nono |
    | 20 |  | vigésimo |
    | 30 |  | trigésimo |
    | 100 |  | centésimo |
    | 1000 |  | milésimo |

Scenario Outline: Additional feminine ordinal numbers
    Given I use the "PT" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 | gender=feminino | primeira |
    | 9 | gender=feminino | nona |
    | 10 | gender=feminino | décima |
    | 11 | gender=feminino | décima primeira |
    | 19 | gender=feminino | décima nona |
    | 20 | gender=feminino | vigésima |
    | 100 | gender=feminino | centésima |
    | 1000 | gender=feminino | milésima |

Scenario Outline: Feminine ordinal numbers compound
    Given I use the "PT" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 21 | gender=feminino | vinte e primeira |
    | 22 | gender=feminino | vinte e segunda |
    | 23 | gender=feminino | vinte e terceira |

Scenario: Time conversion is supported
    Given I use the "PT" number converter
    Then the converter supports time conversion

Scenario: Ordinal conversion is supported
    Given I use the "PT" number converter
    Then the converter supports ordinal conversion

Scenario: Fraction connector wording
    Given I use the "PT" number converter
    When I convert the fraction 3/2 through both public fraction APIs
    Then both fraction results are "três sobre dois"

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "PT" number converter
    Then the converter supports clock-time conversion

# The PT configuration deliberately reads the clock numerically: the feminine "hora" time unit
# followed by the minutes, "e meia" for the half hour (01:45 -> "uma hora e quarenta e cinco").
# Portuguese also has idiomatic constructions after the half hour, which this configuration does
# not produce: "um quarto para as dez", "dez para as três", "três menos dez", "três menos um
# quarto" (Ciberdúvidas, "Minutos para a hora"). PT-PT and PT-BR are not distinguished. The
# 11 -> 12 -> 1 and 23 -> 12 transitions keep the 12-hour reading and the feminine agreement.
Scenario Outline: Idiomatic Portuguese clock times
    Given I use the "PT" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | uma hora |
    | 01:05 | uma hora e cinco |
    | 01:15 | uma hora e quinze |
    | 01:30 | uma hora e meia |
    | 01:45 | uma hora e quarenta e cinco |
    | 02:00 | duas horas |
    | 02:15 | duas horas e quinze |
    | 12:00 | doze horas |
    | 13:30 | uma hora e meia |
    | 00:00 | doze horas |
    | 00:15 | doze horas e quinze |
    | 11:45 | onze horas e quarenta e cinco |
    | 12:15 | doze horas e quinze |
    | 12:45 | doze horas e quarenta e cinco |
    | 23:45 | onze horas e quarenta e cinco |
