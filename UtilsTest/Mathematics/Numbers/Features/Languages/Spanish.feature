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
    | 21 | veintiuna |
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
