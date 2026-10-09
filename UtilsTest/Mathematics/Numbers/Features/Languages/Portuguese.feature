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

# NTS-08 validation: Priberam (vigésimo "depois do décimo nono", septuagésimo "depois do septuagésimo
# nono", nonagésimo "após o octogésimo nono", centésimo, milésimo): 1-20, the round tens, 100 and 1000.
# Other compounds juxtapose ordinals (vigésimo primeiro), whereas the declarative pipeline only
# rewrites the last word of the cardinal ("vinte e primeiro", "duzentos"): they fail closed.
Scenario Outline: Portuguese ordinals outside the validated domain are not supported
    Given I use the "PT" number converter
    And I use the variants "<variants>"
    When I attempt to convert the ordinal number <number>
    Then conversion is rejected because no ordinal form is available

Examples:
    | number | variants |
    | 21 |  |
    | 21 | gender=feminino |
    | 22 | gender=feminino |
    | 99 |  |
    | 101 |  |
    | 200 |  |
    | 2000 |  |

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

# NTS-08 (European Portuguese): Ciberdúvidas, "Minutos para a hora" (C. Rocha, 2025): after the half
# hour, current usage in Portugal is "[minutos] para [hora]" or "[hora] menos [minutos]" ("são três
# menos dez", "são três menos um quarto"); the configuration uses the second. Priberam, "quarto":
# "são três e um quarto". Ciberdúvidas, "Eram duas horas da tarde": the whole hour with "horas".
# The former direct reading after the half hour ("uma hora e quarenta e cinco") is not the documented
# European convention and was corrected. Brazilian Portuguese is not modelled.
Scenario Outline: Idiomatic Portuguese clock times
    Given I use the "PT" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | uma hora |
    | 01:05 | uma e cinco |
    | 01:10 | uma e dez |
    | 01:15 | uma e um quarto |
    | 01:20 | uma e vinte |
    | 01:25 | uma e vinte e cinco |
    | 01:30 | uma e meia |
    | 01:35 | duas menos vinte e cinco |
    | 01:40 | duas menos vinte |
    | 01:45 | duas menos um quarto |
    | 01:50 | duas menos dez |
    | 01:55 | duas menos cinco |
    | 02:00 | duas horas |
    | 02:15 | duas e um quarto |
    | 12:00 | doze horas |
    | 13:30 | uma e meia |
    | 00:00 | doze horas |
    | 00:15 | doze e um quarto |
    | 11:45 | doze menos um quarto |
    | 12:15 | doze e um quarto |
    | 12:45 | uma menos um quarto |
    | 23:45 | doze menos um quarto |

# NTS-12: no ordinal of zero fits the library contract:
# the ordinal series starts at "primeiro"; the informal "zerésimo" is not a standard form.
# The converter fails closed instead of returning the cardinal "zero" unchanged.
Scenario: Zero has no ordinal form
    Given I use the "PT" number converter
    When I attempt to convert the ordinal number 0
    Then conversion is rejected because no ordinal form is available
