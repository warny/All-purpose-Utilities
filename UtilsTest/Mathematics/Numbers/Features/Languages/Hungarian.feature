@NumberToString
Feature: Hungarian number conversion

Scenario Outline: Cardinals_Basic 1
    Given I use the "HU" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | nulla |
    | 1 | egy |
    | 2 | kettő |

Scenario Outline: Cardinals_Hundreds 2
    Given I use the "HU" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 100 | száz |
    | 101 | százegy |
    | 200 | kétszáz |
    | 221 | kétszázhuszonegy |

Scenario Outline: Cardinals_Thousands 3
    Given I use the "HU" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | ezer |
    | 2000 | kétezer |
    | 10000 | tízezer |

Scenario Outline: Cardinals_LongScale 4
    Given I use the "HU" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000000 | millió |
    | 2000000 | kétmillió |
    | 1000000000 | milliárd |
    | 1000000000000 | billió |

Scenario Outline: Ordinals_Exceptions 5
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
