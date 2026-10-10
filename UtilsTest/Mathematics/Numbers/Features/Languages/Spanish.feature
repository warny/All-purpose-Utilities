@NumberToString @ES
Feature: Spanish number conversion

Background:
    Given I use the "ES" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected         |
    |      1 | uno              |
    |     21 | veintiuno        |
    |     31 | treinta y uno    |
    |     45 | cuarenta y cinco |
    |     99 | noventa y nueve  |

Scenario Outline: Feminine cardinal numbers
    Given I use the variants "gender=femenino"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | una |
    | 21 | veintiuna |
    | 200 | doscientas |
    | 300 | trescientas |
    | 400 | cuatrocientas |
    | 500 | quinientas |
    | 600 | seiscientas |
    | 700 | setecientas |
    | 800 | ochocientas |
    | 900 | novecientas |
    | 22 | veintidós |
    | 23 | veintitrés |
    | 26 | veintiséis |
    | 29 | veintinueve |

# NTS-08 validation: RAE/ASALE, Diccionario panhispánico de dudas, "ordinales": the ordinals of the
# second decade are preferably written as one word (vigesimoprimero, vigesimosegundo, vigesimoctavo
# rather than vigesimooctavo), the accent falls on the last component (vigesimoséptimo) and only the
# last component varies in gender (vigesimoprimera). The cardinals 22, 23 and 26 carry their accent
# (veintidós, veintitrés, veintiséis), so the ordinal word rules keyed on them apply.
Scenario Outline: Ordinals of the second decade
    Given I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 21 |  | vigesimoprimero |
    | 22 |  | vigesimosegundo |
    | 23 |  | vigesimotercero |
    | 24 |  | vigesimocuarto |
    | 25 |  | vigesimoquinto |
    | 26 |  | vigesimosexto |
    | 27 |  | vigesimoséptimo |
    | 28 |  | vigesimoctavo |
    | 29 |  | vigesimonoveno |
    | 21 | gender=femenino | vigesimoprimera |
    | 23 | gender=femenino | vigesimotercera |
    | 26 | gender=femenino | vigesimosexta |
    | 27 | gender=femenino | vigesimoséptima |
    | 28 | gender=femenino | vigesimoctava |
    | 13 |  | decimotercero |
    | 30 |  | trigésimo |
    | 50 |  | quincuagésimo |
    | 90 |  | nonagésimo |
    | 100 |  | centésimo |
    | 1000 |  | milésimo |

# NTS-08: from the third decade the DPD juxtaposes ordinals (trigésimo primero, centésimo primero,
# ducentésimo, dosmilésimo); the declarative pipeline only rewrites the last word of the cardinal
# ("treinta y primero", "doscientos"), so these values fail closed instead of returning a wrong form.
Scenario Outline: Spanish ordinals outside the validated domain are not supported
    When I attempt to convert the ordinal number <number>
    Then conversion is rejected because no ordinal form is available

Examples:
    | number |
    | 31 |
    | 45 |
    | 99 |
    | 101 |
    | 200 |
    | 2000 |
    | 1000000 |

Scenario Outline: Decimal numbers
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | uno coma cinco |
    | 12.34 | doce coma tres cuatro |

Scenario Outline: Duration wording
    When I convert the duration "<value>"
    Then the result is "<expected>"

Examples:
    | value | expected |
    | 01:00:00 | una hora |
    | 21:00:00 | veintiuna horas |
    | 1.07:00:00 | treinta y una horas |
    | 00:01:00 | un minuto |
    | 00:21:00 | veintiún minutos |
    | 00:31:00 | treinta y un minutos |
    | 00:00:01 | un segundo |
    | 00:00:21 | veintiún segundos |
    | 21:21:21 | veintiuna horas veintiún minutos veintiún segundos |

Scenario Outline: Time-of-day wording
    When I convert the time "<value>"
    Then the result is "<expected>"

Examples:
    | value | expected |
    | 01:00:00 | una hora |
    | 21:00:00 | veintiuna horas |
    | 21:21:21 | veintiuna horas veintiún minutos veintiún segundos |

Scenario: Time conversion is supported
    Then the converter supports time conversion

Scenario Outline: Explicit masculine ordinal numbers
    Given I use the "ES" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 |  | primero |
    | 2 |  | segundo |
    | 10 |  | décimo |
    | 20 |  | vigésimo |

Scenario Outline: Feminine ordinal numbers
    Given I use the "ES" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 | gender=femenino | primera |
    | 2 | gender=femenino | segunda |
    | 10 | gender=femenino | décima |
    | 20 | gender=femenino | vigésima |

Scenario Outline: Caller-defined currency wording
    Given I use this currency definition
        | property         | value     |
        | unit singular    | euro      |
        | unit plural      | euros     |
        | subunit singular | céntimo   |
        | subunit plural   | céntimos  |
        | connector        | con       |
    When I convert the currency amount <amount>
    Then the result is "<expected>"

Examples:
    | amount | expected                         |
    | 1      | uno euro                         |
    | 2      | dos euros                        |
    | 1.50   | uno euro con cincuenta céntimos  |

Scenario: Castilian aliases preserve decimal wording
    Then the "ES" and "es-ES" converters produce the same decimal wording for 21.4
    And the "ES" and "ES-es" converters produce the same cardinal wording for 1000

Scenario Outline: Composite thousands preserve the terminal unit
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected             |
    | 1000   | mil                  |
    | 21000  | veintiuno mil        |
    | 31000  | treinta y uno mil    |

Scenario Outline: Feminine caller-defined currency wording
    Given I use the variants "gender=femenino"
    And I use this currency definition
        | property         | value     |
        | unit singular    | peseta    |
        | unit plural      | pesetas   |
        | subunit singular | céntimo   |
        | subunit plural   | céntimos  |
        | connector        | con       |
    When I convert the currency amount <amount>
    Then the result is "<expected>"

Examples:
    | amount | expected                    |
    | 1      | una peseta                  |
    | 31     | treinta y una pesetas       |

Scenario: Ordinal conversion is supported
    Given I use the "ES" number converter
    Then the converter supports ordinal conversion

Scenario: Fraction connector wording
    Given I use the "ES" number converter
    When I convert the fraction 3/2 through both public fraction APIs
    Then both fraction results are "tres sobre dos"

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "ES" number converter
    Then the converter supports clock-time conversion

# RAE, Diccionario panhispánico de dudas, "hora": "la una y media", "las dos y cuarto", "las seis
# menos cuarto". The feminine article is singular for one ("la una") and plural otherwise
# ("las dos"), selected through displayHourRange; {hour} is forced to the feminine numeral.
Scenario Outline: Idiomatic Spanish clock times
    Given I use the "ES" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | la una |
    | 01:05 | la una y cinco |
    | 01:15 | la una y cuarto |
    | 01:25 | la una y veinticinco |
    | 01:30 | la una y media |
    | 01:35 | las dos menos veinticinco |
    | 01:45 | las dos menos cuarto |
    | 01:55 | las dos menos cinco |
    | 02:00 | las dos |
    | 02:15 | las dos y cuarto |
    | 12:45 | la una menos cuarto |
    | 13:30 | la una y media |
    | 00:00 | las doce |

Scenario Outline: Spanish clock times round to the nearest five minutes
    Given I use the "ES" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:27 | la una y veinticinco |
    | 01:28 | la una y media |
    | 23:58 | las doce |

Scenario: The exact Spanish time API is unchanged by the clock wording
    Given I use the "ES" number converter
    When I convert the time "01:00:00"
    Then the result is "una hora"

# NTS-12: no ordinal of zero fits the library contract:
# the RAE ordinal series starts at "primero"; the informal "ceroésimo" is not a recognized form.
# The converter fails closed instead of returning the cardinal "cero" unchanged.
Scenario: Zero has no ordinal form
    Given I use the "ES" number converter
    When I attempt to convert the ordinal number 0
    Then conversion is rejected because no ordinal form is available
