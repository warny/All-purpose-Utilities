@NumberToString @RO
Feature: Romanian number conversion

Background:
    Given I use the "RO" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | zero |
    | 1 | unu |
    | 2 | doi |
    | 10 | zece |
    | 11 | unsprezece |
    | 12 | doisprezece |
    | 19 | nouăsprezece |
    | 20 | douăzeci |
    | 21 | douăzeci și unu |
    | 22 | douăzeci și doi |
    | 100 | o sută |
    | 101 | o sută unu |
    | 200 | două sute |
    | 999 | nouă sute nouăzeci și nouă |

Scenario Outline: Scale connector wording
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | o mie |
    | 2000 | două mii |
    | 10000 | zece mii |
    | 12000 | doisprezece mii |
    | 19000 | nouăsprezece mii |
    | 20000 | douăzeci de mii |
    | 100000 | o sută de mii |
    | 1000000 | un milion |
    | 2000000 | două milioane |
    | 20000000 | douăzeci de milioane |
    | 1000000000 | un miliard |

Scenario Outline: Feminine cardinal numbers
    Given I use the variants "gen=feminin"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | una |
    | 2 | două |
    | 21 | douăzeci și una |
    | 22 | douăzeci și două |

Scenario: Ordinal conversion is supported
    Then the converter supports ordinal conversion

# DOOM (dexonline): ordinals are "al" + cardinal + "-lea" (masculine) and "a" + cardinal + "-a"
# (feminine), except "primul"/"prima"; only the last word changes ("al douăzeci și unulea"),
# "al o sutălea"/"a o suta", "al două sutelea", "al o miilea"/"a o mia", "al două miilea".
Scenario Outline: Romanian masculine ordinal numbers
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | primul |
    | 2 | al doilea |
    | 3 | al treilea |
    | 4 | al patrulea |
    | 8 | al optulea |
    | 9 | al nouălea |
    | 10 | al zecelea |
    | 11 | al unsprezecelea |
    | 12 | al doisprezecelea |
    | 20 | al douăzecilea |
    | 21 | al douăzeci și unulea |
    | 22 | al douăzeci și doilea |
    | 100 | al o sutălea |
    | 101 | al o sută unulea |
    | 200 | al două sutelea |
    | 1000 | al o miilea |
    | 1001 | al o mie unulea |
    | 2000 | al două miilea |

Scenario Outline: Romanian feminine ordinal numbers
    Given I use the variants "gen=feminin"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | prima |
    | 2 | a doua |
    | 3 | a treia |
    | 4 | a patra |
    | 12 | a douăsprezecea |
    | 21 | a douăzeci și una |
    | 22 | a douăzeci și doua |
    | 100 | a o suta |
    | 200 | a două suta |
    | 1000 | a o mia |

Scenario: The feminine twelve agrees in gender
    Given I use the variants "gen=feminin"
    When I convert the cardinal number 12
    Then the result is "douăsprezece"

Scenario: Idiomatic clock-time conversion is supported
    Then the converter supports clock-time conversion

# "ora unu", "ora două" ... "ora douăsprezece": the hour takes the feminine numeral except one,
# which keeps "unu". Quarters: "și un sfert", "și jumătate", "<next> fără un sfert".
Scenario Outline: Idiomatic Romanian clock times
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | ora unu |
    | 01:15 | unu și un sfert |
    | 01:30 | unu și jumătate |
    | 01:45 | două fără un sfert |
    | 02:00 | ora două |
    | 12:00 | ora douăsprezece |
    | 11:45 | douăsprezece fără un sfert |
    | 13:30 | unu și jumătate |
