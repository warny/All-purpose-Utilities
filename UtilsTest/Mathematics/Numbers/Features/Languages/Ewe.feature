@NumberToString @EW
Feature: Ewe number conversion

Scenario Outline: Decimal numbers
    Given I use the "EE" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | deka kpɔ atɔ |
    | 12.34 | ewo kple eve kpɔ eto ene |

Scenario Outline: Basic cardinal numbers
    Given I use the "EE" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | deka |
    | 2 | eve |
    | 3 | eto |
    | 10 | ewo |
    | 11 | ewo kple deka |
    | 19 | ewo kple asea |
    | 20 | blavo eve |
    | 100 | kpeɖe |
    | 1000 | deka akpe |

Scenario Outline: Ordinal First Exception
    Given I use the "EE" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | etsõ gbãtõ |
