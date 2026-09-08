@NumberToString @SV
Feature: Swedish number conversion

Background:
    Given I use the "SV" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | noll |
    | 1 | ett |
    | 2 | två |
    | 9 | nio |
    | 10 | tio |
    | 11 | elva |
    | 19 | nitton |
    | 20 | tjugo |
    | 21 | tjugoett |
    | 31 | trettio ett |
    | 100 | ett hundra |
    | 1000 | tusen |
    | 2000 | två tusen |
    | 1000000 | ett miljon |
    | 2000000 | två miljoner |
    | 1000000000 | ett miljard |
    | 3000000000 | tre miljarder |
    | 1000000000000 | ett biljon |
    | 1000000000000000 | ett biljard |
    | 1000000000000000000000000 | ett kvadriljon |
