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
    | 100 | rodzaj=feminin | setna |
    | 200 |  | dwusetny |
    | 101 |  | sto pierwszy |
    | 101 | rodzaj=feminin | sto pierwsza |
    | 221 |  | dwieście dwudziesty pierwszy |

# NTS-08 validation: Zintegrowana Platforma Edukacyjna (MEN), "Odmiana liczebnika i zaimka": ordinals
# decline like adjectives (table of "drugi"); in multiword ordinals only the last two words are
# ordinal ("tysiąc pięćset dwudziesty piąty", "w roku tysiąc pięćset dwudziestym piątym"), the last
# word when there are no tens ("tysiąc osiemset pierwszy") or no tens and units ("tysiąc
# osiemsetny"); "tysięczny", "dwutysięczny", "dwa tysiące pierwszy".
Scenario Outline: Polish ordinals from a thousand to 1999
    Given I use the "PL" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1000 |  | tysięczny |
    | 1000 | rodzaj=feminin | tysięczna |
    | 1000 | przypadek=dopełniacz | tysięcznego |
    | 1001 |  | tysiąc pierwszy |
    | 1012 |  | tysiąc dwunasty |
    | 1100 |  | tysiąc setny |
    | 1100 | rodzaj=feminin | tysiąc setna |
    | 1525 |  | tysiąc pięćset dwudziesty piąty |
    | 1525 | przypadek=miejscownik | tysiąc pięćset dwudziestym piątym |
    | 1800 |  | tysiąc osiemsetny |
    | 1801 |  | tysiąc osiemset pierwszy |
    | 1999 |  | tysiąc dziewięćset dziewięćdziesiąty dziewiąty |

# NTS-08: from 2000 the configured cardinal does not inflect the thousands ("dwa tysiąc" instead of
# "dwa tysiące") and the plugin does not build the one-word round ordinals ("dwutysięczny"); these
# values fail closed.
Scenario Outline: Polish ordinals from 2000 are not supported
    Given I use the "PL" number converter
    When I attempt to convert the ordinal number <number>
    Then conversion is rejected because no ordinal form is available

Examples:
    | number |
    | 2000 |
    | 2001 |
    | 21000 |
    | 999999 |

Scenario: Ordinal conversion is supported
    Given I use the "PL" number converter
    Then the converter supports ordinal conversion

Scenario: Fraction connector wording
    Given I use the "PL" number converter
    When I convert the fraction 3/2 through both public fraction APIs
    Then both fraction results are "trzy przez dwa"

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "PL" number converter
    Then the converter supports clock-time conversion

# Poradnia PWN: whole hours are feminine ordinals ("pierwsza", "druga"); up to :25 "<n> po" +
# locative ("pięć po pierwszej"), :30 "wpół do" + genitive ("wpół do drugiej"), from :35
# "za <n>" + nominative of the following hour ("za piętnaście druga"). The ordinal plugin and
# word rules receive the rule's forced rodzaj/przypadek.
Scenario Outline: Idiomatic Polish clock times
    Given I use the "PL" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | pierwsza |
    | 01:05 | pięć po pierwszej |
    | 01:15 | piętnaście po pierwszej |
    | 01:25 | dwadzieścia pięć po pierwszej |
    | 01:30 | wpół do drugiej |
    | 01:35 | za dwadzieścia pięć druga |
    | 01:45 | za piętnaście druga |
    | 01:55 | za pięć druga |
    | 02:00 | druga |
    | 12:00 | dwunasta |
    | 12:30 | wpół do pierwszej |
    | 13:30 | wpół do drugiej |

# NTS-12: no ordinal of zero fits the library contract:
# "zerowy" is a relational adjective ("lekcja zerowa") whose full gender and case declension is
# not configured; the Polish ordinal plugin covers 1 and above only.
# The converter fails closed instead of returning the cardinal "zero" unchanged.
Scenario: Zero has no ordinal form
    Given I use the "PL" number converter
    When I attempt to convert the ordinal number 0
    Then conversion is rejected because no ordinal form is available
