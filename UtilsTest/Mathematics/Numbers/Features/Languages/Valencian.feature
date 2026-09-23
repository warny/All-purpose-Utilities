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
    | 01:45 | les dues menys quart |
    | 01:55 | les dues menys cinc |
