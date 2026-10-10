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

# NTS-28: RAE/ASALE, DPD "ordinales" §1 (table), §2b, §2d, §3: from the third decade the complex ordinals
# juxtapose the simple ones from the highest order down, each varying in gender (trigésimo primero, trigésima
# primera, centésimo trigésimo cuarto, milésimo ducentésimo cuadragésimo octavo); the round hundreds are
# simple (ducentésimo … noningentésimo); the thousands and millions prefix the multiplying cardinal to the
# simple ordinal (dosmilésimo, diezmilésimo, cienmilésimo, quinientosmilésimo, millonésimo,
# tresmilésimo tricentésimo cuadragésimo quinto).
Scenario Outline: NTS-28 Spanish complex ordinals
    Given I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 31 |  | trigésimo primero |
    | 31 | gender=femenino | trigésima primera |
    | 42 |  | cuadragésimo segundo |
    | 99 |  | nonagésimo noveno |
    | 101 |  | centésimo primero |
    | 101 | gender=femenino | centésima primera |
    | 120 |  | centésimo vigésimo |
    | 121 |  | centésimo vigesimoprimero |
    | 134 |  | centésimo trigésimo cuarto |
    | 134 | gender=femenino | centésima trigésima cuarta |
    | 200 |  | ducentésimo |
    | 200 | gender=femenino | ducentésima |
    | 201 |  | ducentésimo primero |
    | 300 |  | tricentésimo |
    | 400 |  | cuadringentésimo |
    | 500 |  | quingentésimo |
    | 600 |  | sexcentésimo |
    | 700 |  | septingentésimo |
    | 800 |  | octingentésimo |
    | 900 |  | noningentésimo |
    | 999 |  | noningentésimo nonagésimo noveno |
    | 1001 |  | milésimo primero |
    | 1248 |  | milésimo ducentésimo cuadragésimo octavo |
    | 2000 |  | dosmilésimo |
    | 2000 | gender=femenino | dosmilésima |
    | 2001 |  | dosmilésimo primero |
    | 3000 |  | tresmilésimo |
    | 3345 |  | tresmilésimo tricentésimo cuadragésimo quinto |
    | 10000 |  | diezmilésimo |
    | 100000 |  | cienmilésimo |
    | 500000 |  | quinientosmilésimo |
    | 1000000 |  | millonésimo |
    | 2000000 |  | dosmillonésimo |
    | 1000001 |  | millonésimo primero |
    | -31 |  | menos trigésimo primero |

# NTS-28: the DPD prefixes "the multiplying cardinal" without saying how a cardinal that varies (apocope of
# uno, feminine -cientas) or is written in several words (ciento cincuenta) is soldered: those round
# thousands and millions fail closed, as do zero (no ordinal) and the milliard and above (milmillonésimo is
# attested, but the cardinal "mil millones" + millions is not composed, NTS-30).
Scenario Outline: Spanish ordinals outside the validated domain are not supported
    Given I use the variants "<variants>"
    When I attempt to convert the ordinal number <number>
    Then conversion is rejected because no ordinal form is available

Examples:
    | number | variants |
    | 21000 |  |
    | 101000 |  |
    | 150000 |  |
    | 200000 | gender=femenino |
    | 21000000 |  |
    | 1000000000 |  |
    | 2021000 |  |

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

# NTS-27: RAE/ASALE, Diccionario panhispánico de dudas, "cardinales" §1 (table) and §4, "uno" §2.2-2.3,
# "mil" §1: uno and its compounds are apocopated before mil and before the nouns millón/millones
# (veintiún mil, treinta y un mil, ciento un mil, un millón), mil alone for 1000 (not un mil),
# 1 000 000 000 is mil millones, the hundreds are written apart from the following number (ciento uno).
Examples:
    | number | expected             |
    | 1000   | mil                  |
    | 21000  | veintiún mil         |
    | 31000  | treinta y un mil     |

Scenario Outline: NTS-27 Spanish cardinals follow the DPD table
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number     | expected                                                          |
    | 21         | veintiuno                                                         |
    | 101        | ciento uno                                                        |
    | 121        | ciento veintiuno                                                  |
    | 131        | ciento treinta y uno                                              |
    | 999        | novecientos noventa y nueve                                       |
    | 1001       | mil uno                                                           |
    | 1100       | mil cien                                                          |
    | 1101       | mil ciento uno                                                    |
    | 2021       | dos mil veintiuno                                                 |
    | 2101       | dos mil ciento uno                                                |
    | 21000      | veintiún mil                                                      |
    | 41000      | cuarenta y un mil                                                 |
    | 100000     | cien mil                                                          |
    | 101000     | ciento un mil                                                     |
    | 121000     | ciento veintiún mil                                               |
    | 131000     | ciento treinta y un mil                                           |
    | 201000     | doscientos un mil                                                 |
    | 300021     | trescientos mil veintiuno                                         |
    | 999999     | novecientos noventa y nueve mil novecientos noventa y nueve       |
    | 1000000    | un millón                                                         |
    | 1000001    | un millón uno                                                     |
    | 1000100    | un millón cien                                                    |
    | 1001000    | un millón mil                                                     |
    | 2000000    | dos millones                                                      |
    | 21000000   | veintiún millones                                                 |
    | 31000000   | treinta y un millones                                             |
    | 101000000  | ciento un millones                                                |
    | 100000000  | cien millones                                                     |
    | 1000000000 | mil millones                                                      |
    | 2000000000 | dos mil millones                                                  |
    | 21000000000 | veintiún mil millones                                            |
    | -21000     | menos veintiún mil                                                |
    | -1000000   | menos un millón                                                   |

# DPD "cardinales" §3 and "uno" §2.3: before mil the feminine concord of uno is optional (veintiún mil
# or veintiuna mil toneladas); the hundreds always agree (doscientas mil). The apocopated form is kept.
Scenario Outline: NTS-27 Spanish feminine cardinals before mil
    Given I use the variants "gender=femenino"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number  | expected              |
    | 1001    | mil una               |
    | 21000   | veintiún mil          |
    | 101     | ciento una            |
    | 201000  | doscientas un mil     |
    | 200000  | doscientas mil        |
    | 1000001 | un millón una         |

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
