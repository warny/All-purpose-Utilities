@NumberToString @ZU
Feature: Zulu number conversion

Scenario Outline: Decimal numbers
    Given I use the "ZU" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | kunye phuzu isihlanu |
    | 12.34 | ishumi nambili phuzu kuthathu kune |

Scenario: Ordinal conversion is unsupported
    Given I use the "ZU" number converter
    Then the converter does not support ordinal conversion

Scenario: Fraction connector wording
    Given I use the "ZU" number converter
    When I convert the fraction 3/2 through both public fraction APIs
    Then both fraction results are "kuthathu ngaphezu kubili"

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "ZU" number converter
    Then the converter supports clock-time conversion

# Unisa "Learn online Zulu", Theme 4: "Yihora lesihlanu" (five o'clock), "Imizuzu eyishumi
# lishayile elesihlanu" (five ten), "Ligamenxe elesihlanu" (five-thirty), "... ngaphambi
# kwelesihlanu" (before five). The hour is a class-5 ordinal agreeing with "ihora", written
# literally per hour: these clock forms do not make general ordinal conversion available.
# The first draft ("ihora lokuqala nqo", "... ngaphambi kwehora lesibili") was aligned on this
# source. Quarter-hour 12-hour clock without day-part wording.
Scenario Outline: Idiomatic Zulu clock times
    Given I use the "ZU" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | ihora lokuqala |
    | 02:00 | ihora lesibili |
    | 01:15 | imizuzu eyishumi nanhlanu lishayile elokuqala |
    | 01:30 | ligamenxe elokuqala |
    | 01:45 | imizuzu eyishumi nanhlanu ngaphambi kwelesibili |
    | 05:00 | ihora lesihlanu |
    | 05:30 | ligamenxe elesihlanu |
    | 08:00 | ihora lesishiyagalombili |
    | 08:30 | ligamenxe elesishiyagalombili |
    | 12:00 | ihora leshumi nambili |
    | 13:00 | ihora lokuqala |
    | 03:00 | ihora lesithathu |
    | 04:00 | ihora lesine |
    | 06:00 | ihora lesithupha |
    | 07:00 | ihora lesikhombisa |
    | 09:00 | ihora lesishiyagalolunye |
    | 10:00 | ihora leshumi |
    | 11:00 | ihora leshumi nanye |
    | 00:00 | ihora leshumi nambili |
    | 11:45 | imizuzu eyishumi nanhlanu ngaphambi kweleshumi nambili |
    | 12:15 | imizuzu eyishumi nanhlanu lishayile eleshumi nambili |
    | 12:45 | imizuzu eyishumi nanhlanu ngaphambi kwelokuqala |
    | 23:45 | imizuzu eyishumi nanhlanu ngaphambi kweleshumi nambili |

Scenario: Zulu clock forms do not change the cardinal
    Given I use the "ZU" number converter
    When I convert the cardinal number 1
    Then the result is "kunye"
