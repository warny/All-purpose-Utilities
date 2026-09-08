@NumberToString
Feature: Croatian number conversion

Scenario Outline: Cardinals_Basic 1
    Given I use the "HR" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | nula |
    | 1 | jedan |
    | 2 | dva |
    | 9 | devet |
    | 10 | deset |
    | 11 | jedanaest |
    | 12 | dvanaest |
    | 19 | devetnaest |
    | 20 | dvadeset |
    | 21 | dvadeset jedan |
    | 100 | sto |
    | 101 | sto jedan |
    | 200 | dvjesto |

Scenario Outline: Cardinals_Thousands 2
    Given I use the "HR" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | tisuća |
    | 2000 | dva tisuća |
    | 10000 | deset tisuća |

Scenario Outline: Cardinals_LongScale 3
    Given I use the "HR" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000000 | jedan milijun |
    | 1000000000 | jedan milijarda |
    | 1000000000000 | jedan bilijun |

Scenario Outline: Ordinals_Exceptions 4
    Given I use the "HR" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | prvi |
    | 2 | drugi |
    | 3 | treći |
