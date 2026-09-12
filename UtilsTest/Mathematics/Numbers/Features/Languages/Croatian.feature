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
    | 2000 | dva tisuća |
    | 10000 | deset tisuća |

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

Scenario: Temporal conversion is unsupported
    Given I use the "HR" number converter
    Then the converter does not support time conversion

Scenario: The regional alias uses Croatian wording
    Then the "HR" and "HR-HR" converters produce the same cardinal wording for 2
