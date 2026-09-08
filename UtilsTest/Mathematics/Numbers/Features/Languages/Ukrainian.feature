@NumberToString @UK
Feature: Ukrainian number conversion

Background:
    Given I use the "UK" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | нуль |
    | 1 | один |
    | 2 | два |
    | 9 | дев'ять |
    | 10 | десять |
    | 11 | одинадцять |
    | 19 | дев'ятнадцять |
    | 20 | двадцять |
    | 21 | двадцять один |
    | 100 | сто |
    | 1000 | тисяч |
    | 2000 | два тисяч |
    | 1000000 | один мільйон |
    | 2000000 | два мільйонів |
    | 1000000000 | один мільярд |
    | 1000000000000 | один більйон |
    | 1000000000000000000 | один трильйон |
