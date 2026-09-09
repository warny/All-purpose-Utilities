@NumberToString @PL
Feature: Polish number conversion

Scenario Outline: Decimal numbers
    Given I use the "PL" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | jeden przecinek pięć |
    | 12.34 | dwanaście przecinek trzy cztery |

Scenario Outline: Additional ordinal numbers
    Given I use the "PL" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 |  | pierwszy |
    | 2 |  | drugi |
    | 3 |  | trzeci |
    | 5 |  | piąty |
    | 10 |  | dziesiąty |
    | 11 |  | jedenasty |
    | 20 |  | dwudziesty |
    | 21 |  | dwudziesty pierwszy |
    | 100 |  | setny |
    | 1000 |  | tysięczny |

Scenario Outline: Ordinal numbers from eleven through nineteen by grammatical form
    Given I use the "PL" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 11 |  | jedenasty |
    | 11 | rodzaj=feminin | jedenasta |
    | 11 | rodzaj=feminin,przypadek=biernik | jedenastą |
    | 11 | rodzaj=plural_mos | jedenaści |
    | 11 | rodzaj=plural_mos,przypadek=dopełniacz | jedenastych |

Scenario Outline: Tens ordinal numbers by grammatical form
    Given I use the "PL" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 20 |  | dwudziesty |
    | 20 | rodzaj=feminin | dwudziesta |
    | 30 |  | trzydziesty |
    | 80 |  | osiemdziesiąty |
    | 90 | rodzaj=plural_mos | dziewięćdziesiąci |

Scenario Outline: Compound ordinal numbers by grammatical form
    Given I use the "PL" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 21 |  | dwudziesty pierwszy |
    | 21 | rodzaj=feminin | dwudziesta pierwsza |
    | 21 | przypadek=dopełniacz | dwudziestego pierwszego |
    | 32 |  | trzydziesty drugi |
    | 55 |  | pięćdziesiąty piąty |
    | 99 |  | dziewięćdziesiąty dziewiąty |

Scenario Outline: Hundreds ordinal numbers by grammatical form
    Given I use the "PL" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 100 |  | setny |
    | 100 | rodzaj=feminin | setna |
    | 200 |  | dwusetny |
    | 101 |  | sto pierwszy |
    | 101 | rodzaj=feminin | sto pierwsza |
    | 221 |  | dwieście dwudziesty pierwszy |

Scenario: Ordinal conversion is supported
    Given I use the "PL" number converter
    Then the converter supports ordinal conversion
