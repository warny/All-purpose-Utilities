@NumberToString @NO
Feature: Norwegian number conversion

Background:
    Given I use the "NO" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | null |
    | 1 | en |
    | 2 | to |
    | 9 | ni |
    | 10 | ti |
    | 11 | elleve |
    | 19 | nitten |
    | 20 | tjue |
    | 21 | tjue en |
    | 100 | ett hundre |
    | 1000 | tusen |
    | 2000 | to tusen |
    | 1000000 | en million |
    | 2000000 | to millioner |
    | 1000000000 | en milliard |
    | 3000000000 | tre milliarder |
    | 1000000000000 | en billion |
    | 1000000000000000 | en billiard |

Scenario: Ordinal conversion is supported
    Then the converter supports ordinal conversion

# Riksmålsforbundet, "Kapittel 7 Tallord": compound ordinals are written as one word
# (tjueførste); the ordinals of hundre/tusen/million are hundrede/tusende/millionte. Above 100
# only the last part is ordinal ("hundre og første").
Scenario Outline: Norwegian Bokmål ordinal numbers
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | første |
    | 2 | andre |
    | 3 | tredje |
    | 4 | fjerde |
    | 5 | femte |
    | 6 | sjette |
    | 7 | sjuende |
    | 8 | åttende |
    | 9 | niende |
    | 10 | tiende |
    | 11 | ellevte |
    | 12 | tolvte |
    | 13 | trettende |
    | 20 | tjuende |
    | 21 | tjueførste |
    | 22 | tjueandre |
    | 30 | trettiende |
    | 31 | trettiførste |
    | 40 | førtiende |
    | 100 | hundrede |
    | 101 | hundre og første |
    | 200 | to hundrede |
    | 1000 | tusende |
    | 1001 | tusen og første |
    | 1000000 | millionte |

Scenario: Idiomatic clock-time conversion is supported
    Then the converter supports clock-time conversion

# "Klokka er ett": the clock hour uses the neuter numeral, forced on {hour}.
Scenario Outline: Idiomatic Norwegian clock times
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | ett |
    | 02:00 | to |
    | 01:05 | fem over ett |
    | 01:15 | kvart over ett |
    | 01:20 | ti på halv to |
    | 01:25 | fem på halv to |
    | 01:30 | halv to |
    | 01:35 | fem over halv to |
    | 01:40 | ti over halv to |
    | 01:45 | kvart på to |
    | 01:55 | fem på to |
    | 13:30 | halv to |

Scenario Outline: Norwegian gendered numeral one
    Given I use the variants "<variants>"
    When I convert the cardinal number 1
    Then the result is "<expected>"

Examples:
    | variants | expected |
    |  | en |
    | kjønn=hunkjønn | ei |
    | kjønn=intetkjønn | ett |

Scenario: Supported aliases use Norwegian wording
    Then the "NO" and "NB" converters produce the same cardinal wording for 2
    And the "NO" and "NB-NO" converters produce the same cardinal wording for 2
