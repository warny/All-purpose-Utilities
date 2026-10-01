@NumberToString @SW
Feature: Swahili number conversion

Background:
    Given I use the "SW" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | sifuri |
    | 1 | moja |
    | 2 | mbili |
    | 3 | tatu |
    | 9 | tisa |
    | 10 | kumi |
    | 11 | kumi na moja |
    | 12 | kumi na mbili |
    | 19 | kumi na tisa |
    | 20 | ishirini |
    | 21 | ishirini na moja |
    | 100 | mia moja |
    | 101 | mia moja na moja |
    | 200 | mia mbili |
    | 1000 | elfu |
    | 2000 | mbili elfu |
    | 10000 | kumi elfu |
    | 1000000 | moja milioni |
    | 1000000000 | moja bilioni |
    | 1000000000000 | moja trilioni |
    | -1 | hasi moja |

Scenario: Regional aliases use Swahili wording
    Then the "SW" and "SW-KE" converters produce the same cardinal wording for 2
    And the "SW" and "SW-TZ" converters produce the same cardinal wording for 2

# Swahili ordinals are "-a" + stem with a noun-class concord ("wa kwanza", "la pili", "ya tatu");
# no concord-free standalone form exists, so ordinal conversion stays deliberately unsupported
# rather than presenting one class as universal (see NTS-08 record, Deferred).
Scenario: Ordinal conversion is unsupported
    Then the converter does not support ordinal conversion

Scenario: Idiomatic clock-time conversion is supported
    Then the converter supports clock-time conversion

# The Swahili clock counts from 06:00 (saa moja = 07:00) through hourOffset, without day-part
# words (asubuhi, mchana, jioni, usiku). "na robo", "na nusu", "kasorobo" (a quarter less than
# the following hour: 09:45 = "saa nne kasorobo"). "na nusu" is attested by both consulted sources
# and used instead of the contracted "unusu" proposed in the first draft.
Scenario Outline: Idiomatic Swahili clock times
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 07:00 | saa moja |
    | 08:00 | saa mbili |
    | 10:15 | saa nne na robo |
    | 11:30 | saa tano na nusu |
    | 09:45 | saa nne kasorobo |
    | 19:00 | saa moja |
    | 00:00 | saa sita |
    | 12:00 | saa sita |
    | 06:00 | saa kumi na mbili |
    | 05:45 | saa kumi na mbili kasorobo |
