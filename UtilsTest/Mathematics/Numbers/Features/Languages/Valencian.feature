@NumberToString @CA @Valencian
Feature: Valencian number conversion

Scenario: Valencian replaces the parent clock-time convention
    Given I use the "ca-ES-valencia" number converter
    Then the converter supports clock-time conversion

Scenario Outline: Valencian clock times
    Given I use the "ca-ES-valencia" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | la una en punt |
    | 01:15 | la una i quart |
    | 01:30 | la una i mitja |
    | 01:45 | les dos menys quart |
    | 01:55 | les dos menys cinc |

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
