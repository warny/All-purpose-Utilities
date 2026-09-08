@NumberToString @CS
Feature: Czech number conversion

Background:
    Given I use the "CS" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | nula |
    | 1 | jedna |
    | 2 | dva |
    | 3 | tři |
    | 9 | devět |
    | 10 | deset |
    | 11 | jedenáct |
    | 12 | dvanáct |
    | 19 | devatenáct |
    | 20 | dvacet |
    | 21 | dvacet jedna |
    | 100 | sto |
    | 200 | dvě stě |
    | 300 | tři sta |
    | 500 | pět set |
    | 1000 | tisíc |
    | 2000 | dva tisíc |
    | 10000 | deset tisíc |
    | 1000000 | jedna milion |
    | 2000000 | dva milion |
    | 1000000000 | jedna miliard |
    | 1000000000000 | jedna bilion |
    | 1000000000000000 | jedna biliard |
    | 1000000000000000000 | jedna trilion |
