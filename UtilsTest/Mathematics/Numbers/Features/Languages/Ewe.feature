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

# These cardinals are the configured ones, not sourced forms: NTS-20 tracks their rebuild.
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

# NTS-16. The sourced Ewe ordinal is the cardinal + -lia, "first" being the suppletive gbãtɔ (Ewe Basic
# Course, Indiana University 1968: "The ordinal numerals, with the exception of 'first', are formed by
# adding /-lia/ to each of the numbers"; Omniglot and Wiktionary: evelia, etɔ̃lia, enelia, atɔ̃lia,
# ewolia). The former "etsõ" prefix is unattested. The suffix cannot be applied yet: the configured
# cardinals diverge from the same sources in almost every family (deka/ɖeka, eto/etɔ̃, atɔ/atɔ̃,
# adre/adrɛ, asea/asieke, "ewo kple deka"/wuiɖeka, "blavo eve"/blaeve, "kple"/vɔ, kpeɖe/"alafa ɖeka",
# "deka akpe"/"akpe ɖeka"), so "-lia" would form unattested words (etolia, asealia). Ordinal
# conversion stays deliberately unsupported until the cardinal system is rebuilt (NTS-20).
Scenario: Ordinal conversion is unsupported pending the cardinal audit
    Given I use the "EE" number converter
    Then the converter does not support ordinal conversion

Scenario Outline: Ordinal requests are rejected
    Given I use the "EE" number converter
    When I attempt to convert the ordinal number <number>
    Then conversion is rejected because no ordinal form is available

Examples:
    | number |
    | 0 |
    | 1 |
    | 2 |
    | 21 |

Scenario: Fraction connector wording
    Given I use the "EE" number converter
    When I convert the fraction 3/2 through both public fraction APIs
    Then both fraction results are "eto kple eve"

# No reliable Ewe source for minutes was found (only "ga eto" / "ga eto kple afa"); a clock that
# only knows :00 and :30 would round aggressively, so clock-time stays deliberately unsupported.
Scenario: Idiomatic clock-time conversion is unsupported
    Given I use the "EE" number converter
    Then the converter does not support clock-time conversion
