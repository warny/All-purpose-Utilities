@NumberToString @DA
Feature: Danish number conversion

Background:
    Given I use the "DA" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | nul |
    | 1 | en |
    | 2 | to |
    | 3 | tre |
    | 10 | ti |
    | 11 | elleve |
    | 12 | tolv |
    | 19 | nitten |
    | 20 | tyve |
    | 21 | en og tyve |
    | 30 | tredive |
    | 50 | halvtreds |
    | 99 | ni og halvfems |
    | 100 | hundrede |
    | 101 | hundrede og en |
    | 200 | to hundrede |
    | 1000 | tusind |
    | 2000 | to tusind |
    | 1000000 | en million |
    | 2000000 | to millioner |
    | 1000000000 | en milliard |
    | 2000000000 | to milliarder |
    | 1000000000000 | en billion |
    | 1000000000000000 | en billiard |
