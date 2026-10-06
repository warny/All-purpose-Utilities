@NumberToString @EE
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

Scenario Outline: Irregular first ordinal
    Given I use the "EE" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | etsõ gbãtõ |

Scenario Outline: Prefixed ordinal numbers
    Given I use the "EE" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 2 |  | etsõ eve |
    | 3 |  | etsõ eto |
    | 9 |  | etsõ asea |

Scenario: Ordinal conversion is supported
    Given I use the "EE" number converter
    Then the converter supports ordinal conversion

Scenario: Fraction connector wording
    Given I use the "EE" number converter
    When I convert the fraction 3/2 through both public fraction APIs
    Then both fraction results are "eto kple eve"

# No reliable Ewe source for minutes was found (only "ga eto" / "ga eto kple afa"); a clock that
# only knows :00 and :30 would round aggressively, so clock-time stays deliberately unsupported.
Scenario: Idiomatic clock-time conversion is unsupported
    Given I use the "EE" number converter
    Then the converter does not support clock-time conversion

# NTS-14. No consulted source attests an ordinal of zero (zero is nadeke/nanekeo; Omniglot and
# Wiktionary list gbãtɔ, evelia, etɔ̃lia ...), so the prefix does not produce "etsõ zero": zero fails
# closed. The prefix-based formation of the other ordinals is tracked separately (NTS-16).
Scenario: Zero has no ordinal form
    Given I use the "EE" number converter
    When I attempt to convert the ordinal number 0
    Then conversion is rejected because no ordinal form is available