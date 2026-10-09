@NumberToString @GL
Feature: Galician number conversion

Scenario Outline: Galician cardinal numbers
    Given I use the "GL" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected    |
    |      1 | un          |
    |      2 | dous        |
    |     21 | vinte e un  |
    |    105 | cento cinco |
    |    100 | cen         |

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

Scenario Outline: Time-of-day wording
    Given I use the "GL" number converter
    When I convert the time "<value>"
    Then the result is "<expected>"

Examples:
    | value | expected |
    | 01:00:00 | unha hora |
    | 21:00:00 | vinte e unha horas |
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

# NTS-08 validation: RAG/ILG, Normas ortográficas e morfolóxicas do idioma galego (2003), §16.2
# Ordinais: décimo terceiro ... décimo noveno, vixésimo, trixésimo, cuadraxésimo, quincuaxésimo,
# sesaxésimo, septuaxésimo, octoxésimo, nonaxésimo, centésimo, milésimo; in compounds gender and
# number are marked only on the last element ("décimo primeira").
Scenario Outline: Galician ordinals from the normative list
    Given I use the "GL" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 11 |  | undécimo |
    | 13 |  | décimo terceiro |
    | 14 |  | décimo cuarto |
    | 15 |  | décimo quinto |
    | 16 |  | décimo sexto |
    | 17 |  | décimo sétimo |
    | 18 |  | décimo oitavo |
    | 19 |  | décimo noveno |
    | 13 | gender=feminino | décimo terceira |
    | 19 | gender=feminino | décimo novena |
    | 40 |  | cuadraxésimo |
    | 50 |  | quincuaxésimo |
    | 60 |  | sesaxésimo |
    | 70 |  | septuaxésimo |
    | 80 |  | octoxésimo |
    | 90 |  | nonaxésimo |
    | 40 | gender=feminino | cuadraxésima |

# NTS-08: other compounds juxtapose ordinals (vixésimo primeiro), whereas the declarative pipeline
# only rewrites the last word of the cardinal ("vinte e primeiro", "douscentos"): they fail closed.
Scenario Outline: Galician ordinals outside the validated domain are not supported
    Given I use the "GL" number converter
    And I use the variants "<variants>"
    When I attempt to convert the ordinal number <number>
    Then conversion is rejected because no ordinal form is available

Examples:
    | number | variants |
    | 21 |  |
    | 21 | gender=feminino |
    | 23 | gender=feminino |
    | 99 |  |
    | 101 |  |
    | 200 |  |
    | 2000 |  |

Scenario: Time conversion is supported
    Given I use the "GL" number converter
    Then the converter supports time conversion

Scenario: Ordinal conversion is supported
    Given I use the "GL" number converter
    Then the converter supports ordinal conversion

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "GL" number converter
    Then the converter supports clock-time conversion

# Real Academia Galega usage: "a unha e cuarto", "a unha e media", "as dúas menos cuarto". The
# article is singular for one ("a unha") and plural otherwise ("as dúas"); {hour} is forced to
# the feminine numeral.
Scenario Outline: Idiomatic Galician clock times
    Given I use the "GL" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | a unha |
    | 01:05 | a unha e cinco |
    | 01:15 | a unha e cuarto |
    | 01:30 | a unha e media |
    | 01:35 | as dúas menos vinte e cinco |
    | 01:45 | as dúas menos cuarto |
    | 02:00 | as dúas |
    | 12:45 | a unha menos cuarto |
    | 13:30 | a unha e media |

# NTS-12: no ordinal of zero fits the library contract:
# the ordinal series starts at "primeiro"; no standard ordinal of zero is recorded.
# The converter fails closed instead of returning the cardinal "cero" unchanged.
Scenario: Zero has no ordinal form
    Given I use the "GL" number converter
    When I attempt to convert the ordinal number 0
    Then conversion is rejected because no ordinal form is available
