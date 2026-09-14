@NumberToString @BG
Feature: Bulgarian number conversion

Background:
    Given I use the "BG" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | нула |
    | 1 | едно |
    | 2 | две |
    | 3 | три |
    | 9 | девет |
    | 10 | десет |
    | 11 | единадесет |
    | 12 | дванадесет |
    | 19 | деветнадесет |
    | 20 | двадесет |
    | 21 | двадесет едно |
    | 100 | сто |
    | 101 | сто едно |
    | 200 | двеста |
    | 300 | триста |
    | 400 | четиристотин |
    | 999 | деветстотин деветдесет девет |
    | 1000 | хиляда |
    | 2000 | две хиляда |
    | 10000 | десет хиляда |
    | 1000000 | едно милион |
    | 1000000000 | едно милиард |
    | 1000000000000 | едно билион |
    | 1000000000000000 | едно билиард |

Scenario: The regional alias uses Bulgarian wording
    Then the "BG" and "BG-BG" converters produce the same cardinal wording for 1
