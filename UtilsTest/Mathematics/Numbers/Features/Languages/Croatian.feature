@NumberToString @HR
Feature: Croatian number conversion

Scenario Outline: Basic cardinal numbers
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

Scenario Outline: Thousands
    Given I use the "HR" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | tisuća |
    | 2000 | dvije tisuće |
    | 5000 | pet tisuća |\n    | 10000 | deset tisuća |

Scenario Outline: Long-scale cardinal numbers
    Given I use the "HR" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000000 | jedan milijun |
    | 2000000 | dva milijun |
    | 1000000000 | jedan milijarda |
    | 1000000000000 | jedan bilijun |

Scenario Outline: Irregular ordinal numbers
    Given I use the "HR" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | prvi |
    | 2 | drugi |
    | 3 | treći |
    | 4 | četvrti |
    | 5 | peti |
    | 6 | šesti |
    | 7 | sedmi |
    | 8 | osmi |
    | 9 | deveti |
    | 10 | deseti |
    | 11 | jedanaesti |
    | 20 | dvadeseti |
    | 21 | dvadeset prvi |
    | 31 | trideset prvi |
    | 44 | četrdeset četvrti |
    | 100 | stoti |
    | 101 | sto prvi |
    | 600 | šestoti |
    | 1000 | tisući |
    | 1001 | tisuća prvi |
    | 1000000 | milijunti |

Scenario: Ordinal conversion is supported
    Given I use the "HR" number converter
    Then the converter supports ordinal conversion

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "HR" number converter
    Then the converter supports clock-time conversion

# "Jedan je sat / dva su sata / pet je sati": the hour noun agrees with the count (paucal
# "sata" for 2-4, genitive plural "sati" from 5), selected through displayHourRange.
Scenario Outline: Idiomatic Croatian clock times
    Given I use the "HR" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | jedan sat |
    | 01:15 | jedan i petnaest |
    | 01:30 | pola dva |
    | 01:45 | petnaest do dva |
    | 02:00 | dva sata |
    | 04:00 | četiri sata |
    | 05:00 | pet sati |
    | 12:00 | dvanaest sati |
    | 13:30 | pola dva |

Scenario: Temporal conversion is unsupported
    Given I use the "HR" number converter
    Then the converter does not support time conversion

Scenario: Unsupported time conversion fails closed
    Given I use the "HR" number converter
    When I attempt to convert the duration "01:00:00"
    Then conversion is rejected because time conversion is not supported

Scenario: The regional alias uses Croatian wording
    Then the "HR" and "HR-HR" converters produce the same cardinal wording for 2
