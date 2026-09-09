@NumberToString @GL
Feature: Galician number conversion

Scenario Outline: Galician cardinal numbers
    Given I use the "GL" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 21 | vinte e un |
    | 105 | cento cinco |
    | 100 | cen |

Scenario Outline: Galician decimal numbers
    Given I use the "gl-ES" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | un coma cinco |

Scenario Outline: Feminine cardinal numbers
    Given I use the "GL" number converter
    And I use the variants "gender=feminino"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | unha |
    | 2 | dúas |
    | 200 | douscentas |
    | 21 | vinte e unha |
    | 22 | vinte e dúas |
    | 201 | douscentas unha |
    | 300 | trescentos |

Scenario Outline: Duration wording
    Given I use the "GL" number converter
    When I convert the duration "<value>"
    Then the result is "<expected>"

Examples:
    | value | expected |
    | 01:00:00 | unha hora |
    | 02:00:00 | dúas horas |
    | 21:00:00 | vinte e unha horas |
    | 22:00:00 | vinte e dúas horas |
    | 00:02:00 | dous minutos |
    | 02:02:00 | dúas horas dous minutos |

Scenario Outline: Explicit masculine ordinal numbers
    Given I use the "GL" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 |  | primeiro |
    | 6 |  | sexto |
    | 10 |  | décimo |
    | 12 |  | duodécimo |
    | 20 |  | vixésimo |
    | 30 |  | trixésimo |
    | 100 |  | centésimo |
    | 1000 |  | milésimo |

Scenario Outline: Feminine ordinal numbers
    Given I use the "GL" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 | gender=feminino | primeira |
    | 10 | gender=feminino | décima |
    | 20 | gender=feminino | vixésima |
    | 100 | gender=feminino | centésima |
    | 1000 | gender=feminino | milésima |

Scenario Outline: Feminine ordinal numbers compound
    Given I use the "GL" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 21 | gender=feminino | vinte e primeira |
    | 22 | gender=feminino | vinte e segunda |
    | 23 | gender=feminino | vinte e terceira |
