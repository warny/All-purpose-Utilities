@NumberToString @WO
Feature: Wolof number conversion

Scenario Outline: Decimal numbers
    Given I use the "WO" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | benn pojint juróom |
    | 12.34 | fukk ak ñaar pojint ñett ñent |

Scenario Outline: Basic cardinal numbers
    Given I use the "WO" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | benn |
    | 2 | ñaar |
    | 10 | fukk |
    | 11 | fukk ak benn |
    | 20 | ñaar-fukk |
    | 100 | téeméer |
    | 1000 | benn junni |

Scenario Outline: Irregular and suffixed ordinal numbers
    Given I use the "WO" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | bu njëkk |
    | 2 | ñaarël |
    | 10 | fukkël |

Scenario: Ordinal conversion is supported
    Given I use the "WO" number converter
    Then the converter supports ordinal conversion

Scenario: Fraction connector wording
    Given I use the "WO" number converter
    When I convert the fraction 3/2 through both public fraction APIs
    Then both fraction results are "ñett ci ñaar"

# Several modern Wolof clock conventions coexist (native "waxtu" and French-derived readings) and no
# single sourced system covering 01:00-01:45 was established: clock-time stays deliberately unsupported.
Scenario: Idiomatic clock-time conversion is unsupported
    Given I use the "WO" number converter
    Then the converter does not support clock-time conversion

# NTS-14. Wolof ordinals add -eel/-éél to the cardinal (Janga Wolof: ñaaréél, fukkéél), "first"
# being bu njëkk; no consulted source attests an ordinal of zero (zero is given as tus or dara), so
# the mechanical "seroël" is not produced: zero fails closed. The -ël spelling of the other ordinals
# is tracked separately (NTS-16).
Scenario: Zero has no ordinal form
    Given I use the "WO" number converter
    When I attempt to convert the ordinal number 0
    Then conversion is rejected because no ordinal form is available