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

Scenario: Ordinal conversion is supported
    Then the converter supports ordinal conversion

# Swedish compound ordinals are written as one word (tjugoförsta); the ordinals of hundra,
# tusen and miljon are hundrade, tusende and miljonte.
Scenario Outline: Swedish ordinal numbers
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | första |
    | 2 | andra |
    | 3 | tredje |
    | 4 | fjärde |
    | 5 | femte |
    | 6 | sjätte |
    | 7 | sjunde |
    | 8 | åttonde |
    | 9 | nionde |
    | 10 | tionde |
    | 11 | elfte |
    | 12 | tolfte |
    | 13 | trettonde |
    | 20 | tjugonde |
    | 21 | tjugoförsta |
    | 22 | tjugoandra |
    | 30 | trettionde |
    | 100 | hundrade |
    | 101 | hundraförsta |
    | 121 | hundratjugoförsta |
    | 200 | tvåhundrade |
    | 1000 | tusende |
    | 1001 | tusenförsta |
    | 2000 | tvåtusende |
    | 1000000 | miljonte |

Scenario: Idiomatic clock-time conversion is supported
    Then the converter supports clock-time conversion

Scenario Outline: Idiomatic Swedish clock times
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | ett |
    | 02:00 | två |
    | 01:05 | fem över ett |
    | 01:15 | kvart över ett |
    | 01:20 | tjugo över ett |
    | 01:25 | fem i halv två |
    | 01:30 | halv två |
    | 01:35 | fem över halv två |
    | 01:40 | tjugo i två |
    | 01:45 | kvart i två |
    | 01:55 | fem i två |
    | 12:30 | halv ett |
    | 13:30 | halv två |

Scenario: The regional alias uses Swedish wording
    Then the "SV" and "SV-SE" converters produce the same cardinal wording for 2
