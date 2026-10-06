@NumberToString @CA @Valencian
Feature: Valencian number conversion

Scenario: Valencian replaces the parent clock-time convention
    Given I use the "ca-ES-valencia" number converter
    Then the converter supports clock-time conversion
    And the converter supports ordinal conversion

Scenario Outline: Valencian clock times
    Given I use the "ca-ES-valencia" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | la una en punt |
    | 01:05 | la una i cinc |
    | 01:15 | la una i quart |
    | 01:30 | la una i mitja |
    | 01:35 | les dos menys vint-i-cinc |
    | 01:45 | les dos menys quart |
    | 01:55 | les dos menys cinc |
    | 02:00 | les dos en punt |
    | 02:05 | les dos i cinc |
    | 02:15 | les dos i quart |
    | 02:30 | les dos i mitja |
    | 12:15 | les dotze i quart |
    | 13:00 | la una en punt |

# The AVL-prioritized invariable "dos" (see NTS-08-linguistic-sources.md) is deliberately scoped to
# the idiomatic ClockTime wording above only. Convert(TimeOnly)/Convert(TimeSpan) go through the
# inherited CA TimeUnits/Variants pipeline unchanged, so they keep the standard Catalan gender-agreed
# "dues hores" - not "dos hores" - for ca-ES-valencia. This is intentional, not an oversight: widening
# "dos" to that shared pipeline would also have to correct every compound (21/22, ...) and every
# gender=femení-dependent ordinal ("vint-i-dosena") across BOTH APIs, a materially larger, separately
# reviewable change.
Scenario Outline: Valencian exact-time wording keeps the inherited standard Catalan gender agreement
    Given I use the "ca-ES-valencia" number converter
    When I convert the time "<value>"
    Then the result is "<expected>"

Examples:
    | value | expected |
    | 01:00:00 | una hora |
    | 02:00:00 | dues hores |

Scenario: The same hour reads "les dos" as a clock time but "dues hores" as an exact time
    Given I use the "ca-ES-valencia" number converter
    When I convert the clock time "02:00"
    Then the result is "les dos en punt"
    When I convert the time "02:00:00"
    Then the result is "dues hores"

# NTS-14: the Catalan zero-ordinal decision is inherited.
Scenario: Zero has no ordinal form
    Given I use the "CA-valencia" number converter
    When I attempt to convert the ordinal number 0
    Then conversion is rejected because no ordinal form is available