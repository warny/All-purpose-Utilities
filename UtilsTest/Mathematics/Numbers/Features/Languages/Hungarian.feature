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

# NTS-08 validation: A magyar helyesírás szabályai (AkH12) §291 (one word up to 2000, then
# hyphenated groups, round thousands and millions written together: hatmilliomodik,
# négyezer-hatszázötödik) and the MTA Nyelvtudományi Intézet "Számok" tool (helyesiras.mta.hu,
# AkH12-292), which writes the scale of one with "egy": egymilliomodik, egymilliárdodik,
# egymillió-egyedik.
Scenario Outline: Irregular ordinal numbers
    Given I use the "HU" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | első |
    | 2 | második |
    | 3 | harmadik |
    | 4 | negyedik |
    | 5 | ötödik |
    | 6 | hatodik |
    | 7 | hetedik |
    | 8 | nyolcadik |
    | 9 | kilencedik |
    | 10 | tizedik |
    | 11 | tizenegyedik |
    | 12 | tizenkettedik |
    | 20 | huszadik |
    | 21 | huszonegyedik |
    | 22 | huszonkettedik |
    | 31 | harmincegyedik |
    | 100 | századik |
    | 101 | százegyedik |
    | 200 | kétszázadik |
    | 1000 | ezredik |
    | 1001 | ezeregyedik |
    | 2000 | kétezredik |
    | 2001 | kétezer-egyedik |
    | 2002 | kétezer-kettedik |
    | 1999 | ezerkilencszázkilencvenkilencedik |
    | 21000 | huszonegyezredik |
    | 100001 | százezer-egyedik |
    | 1000000 | egymilliomodik |
    | 1000001 | egymillió-egyedik |
    | 1001000 | egymillió-ezredik |
    | 2000000 | kétmilliomodik |
    | 21000000 | huszonegymilliomodik |
    | 1000000000 | egymilliárdodik |
    | 1000000001 | egymilliárd-egyedik |
    | 2000000000 | kétmilliárdodik |

Scenario: Ordinal conversion is supported
    Given I use the "HU" number converter
    Then the converter supports ordinal conversion

Scenario: Temporal conversion is unsupported
    Given I use the "HU" number converter
    Then the converter does not support time conversion

Scenario: The regional alias uses Hungarian wording
    Then the "HU" and "HU-HU" converters produce the same cardinal wording for 2

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "HU" number converter
    Then the converter supports clock-time conversion

# Quarters refer to the following hour ("negyed kettő", "fél kettő", "háromnegyed kettő"); the
# whole hour uses the attributive numeral before "óra" ("két óra", "tizenkét óra").
Scenario Outline: Idiomatic Hungarian clock times
    Given I use the "HU" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | egy óra |
    | 01:15 | negyed kettő |
    | 01:30 | fél kettő |
    | 01:45 | háromnegyed kettő |
    | 02:00 | két óra |
    | 12:00 | tizenkét óra |
    | 11:30 | fél tizenkettő |
    | 12:15 | negyed egy |
    | 13:30 | fél kettő |
