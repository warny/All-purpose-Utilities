@NumberToString @ES
Feature: Spanish number conversion

Background:
    Given I use the "ES" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | uno |
    | 21 | veintiuno |
    | 31 | treinta y uno |
    | 45 | cuarenta y cinco |
    | 99 | noventa y nueve |

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
    | 22 | veintidos |
    | 29 | veintinueve |

Scenario Outline: Compound ordinal numbers
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 31 | treinta y primero |
    | 45 | cuarenta y quinto |
    | 99 | noventa y noveno |

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
