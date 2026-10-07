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

# NTS-16. Wolof ordinals add -eel to the LAST element of the cardinal (Kosogorova 2023, §4.2:
# "juróom ñetteel", "fukk(a) ak ñaareel", "ñaar fukk(a) ak ñenteel"; Robert, LLACAN/CNRS 2021:
# "juróóm ñaar-eel"; Ngom 2003: ñaareel, ñetteel, fukkeel). Both academic sources mark the closed
# long vowel (tééméér, juróóm) yet write -eel unaccented: the vowel is open, spelled "ee" by decree
# 2005-992 (Janga Wolof's "-éél" is neither that vowel nor the decree's one-accent rule).
# "First" is the suppletive relative form of jëkk, cited in class B (Ngom 2003: "xale b-u njëkk";
# Janga Wolof "bu njëk"). The hyphens (juróom-ñett, ñaar-fukk) are the configured cardinal spelling
# where the sources write a space; that cardinal question is tracked by NTS-19.
Scenario Outline: Sourced ordinal numbers
    Given I use the "WO" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | bu njëkk |
    | 2 | ñaareel |
    | 3 | ñetteel |
    | 8 | juróom-ñetteel |
    | 10 | fukkeel |
    | 12 | fukk ak ñaareel |
    | 24 | ñaar-fukk ak ñenteel |

# NTS-16. Productive application of the stated rule ("added to the last element of a cardinal
# numeral"; "the only exception ... is the numeral 'one'"): these exact forms are not exemplified by
# the consulted sources, Omniglot excepted for téeméereel (temeereel). The cardinals below 1000 match
# Kosogorova's (234 = ñaar tééméér ak ñett fukk ak ñent) and Robert's (101 = tééméér ak benn) ones.
Scenario Outline: Productive ordinal numbers below a thousand
    Given I use the "WO" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 11 | fukk ak benneel |
    | 21 | ñaar-fukk ak benneel |
    | 100 | téeméereel |
    | 101 | téeméer ak benneel |
    | 234 | ñaar téeméer ak ñett-fukk ak ñenteel |
    | 999 | juróom-ñent téeméer ak juróom-ñent-fukk ak juróom-ñenteel |
    | -2 | minus ñaareel |

# NTS-16. From a thousand the configured cardinal is known to diverge from the sources (no "ak" after
# junni: "benn junni benn" where Kosogorova gives "juuni ak juróom ñaar" and Robert "ñaar-i junni ak
# ..."; "benn junni" for 1000 where both give junni alone), and the vowel-final junni + -eel is only
# exemplified by Omniglot (junneel). No ordinal is built on that base until NTS-19 settles it.
Scenario Outline: Ordinals from a thousand are not supported
    Given I use the "WO" number converter
    When I attempt to convert the ordinal number <number>
    Then conversion is rejected because no ordinal form is available

Examples:
    | number |
    | 1000 |
    | 1001 |
    | 2000 |
    | 1110 |
    | 999999 |
    | -1000 |

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

# NTS-14. No consulted source attests an ordinal of zero (zero is given as tus or dara; the configured
# cardinal "sero" is itself tracked by NTS-19), so the mechanical "seroeel" is not produced: zero
# fails closed (unchanged by NTS-16).
Scenario: Zero has no ordinal form
    Given I use the "WO" number converter
    When I attempt to convert the ordinal number 0
    Then conversion is rejected because no ordinal form is available