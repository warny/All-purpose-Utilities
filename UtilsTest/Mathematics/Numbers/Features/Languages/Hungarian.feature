@NumberToString @HU
Feature: Hungarian number conversion

Scenario Outline: Basic cardinal numbers
    Given I use the "HU" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | nulla |
    | 1 | egy |
    | 2 | kettő |
    | 3 | három |
    | 10 | tíz |
    | 11 | tizenegy |
    | 12 | tizenkét |
    | 20 | húsz |
    | 21 | huszonegy |
    | 30 | harminc |
    | 31 | harmincegy |

Scenario Outline: Hundreds
    Given I use the "HU" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 100 | száz |
    | 101 | százegy |
    | 200 | kétszáz |
    | 300 | háromszáz |
    | 400 | négyszáz |
    | 221 | kétszázhuszonegy |

Scenario Outline: Thousands
    Given I use the "HU" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | ezer |
    | 2000 | kétezer |
    | 10000 | tízezer |

Scenario Outline: Long-scale cardinal numbers
    Given I use the "HU" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000000 | millió |
    | 2000000 | kétmillió |
    | 1000000000 | milliárd |
    | 1000000000000 | billió |

Scenario Outline: Irregular ordinal numbers
    Given I use the "HU" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | első |
    | 2 | második |
    | 3 | harmadik |
    | 10 | tizedik |
    | 100 | századik |
    | 1000 | ezredik |

Scenario: Ordinal conversion is supported
    Given I use the "HU" number converter
    Then the converter supports ordinal conversion

Scenario: Temporal conversion is unsupported
    Given I use the "HU" number converter
    Then the converter does not support time conversion

Scenario: The regional alias uses Hungarian wording
    Then the "HU" and "HU-HU" converters produce the same cardinal wording for 2
