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

Scenario: The regional alias uses Danish wording
    Then the "DA" and "DA-DK" converters produce the same cardinal wording for 2

Scenario: Ordinal conversion is supported
    Then the converter supports ordinal conversion

# Dansk Sprognævn / Retskrivningsordbogen: compound ordinals below 100 are written as one word
# (enogtyvende); the ordinals of hundrede and tusind are "hundrede" and "tusinde". Above 100 the
# last part alone is ordinal ("hundrede og første"). The vigesimal tens use their ordinal forms.
Scenario Outline: Danish ordinal numbers
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | første |
    | 2 | anden |
    | 3 | tredje |
    | 4 | fjerde |
    | 5 | femte |
    | 6 | sjette |
    | 7 | syvende |
    | 8 | ottende |
    | 9 | niende |
    | 10 | tiende |
    | 11 | ellevte |
    | 12 | tolvte |
    | 13 | trettende |
    | 19 | nittende |
    | 20 | tyvende |
    | 21 | enogtyvende |
    | 22 | toogtyvende |
    | 30 | tredivte |
    | 40 | fyrretyvende |
    | 50 | halvtredsindstyvende |
    | 60 | tresindstyvende |
    | 70 | halvfjerdsindstyvende |
    | 80 | firsindstyvende |
    | 99 | nioghalvfemsindstyvende |
    | 100 | hundrede |
    | 101 | hundrede og første |
    | 121 | hundrede og enogtyvende |
    | 200 | to hundrede |
    | 1000 | tusinde |
    | 1001 | tusind og første |
    | 2000 | to tusinde |
    | 1000000 | millionte |

Scenario: Idiomatic clock-time conversion is supported
    Then the converter supports clock-time conversion

# The clock hour "et" is the neuter numeral (klokken er et); it is produced by forcing the
# neuter gender on {hour}, so the plain cardinal stays "en".
Scenario Outline: Idiomatic Danish clock times
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | et |
    | 02:00 | to |
    | 01:05 | fem over et |
    | 01:15 | kvart over et |
    | 01:20 | tyve over et |
    | 01:25 | fem i halv to |
    | 01:30 | halv to |
    | 01:35 | fem over halv to |
    | 01:40 | tyve i to |
    | 01:45 | kvart i to |
    | 01:55 | fem i to |
    | 12:30 | halv et |
    | 13:30 | halv to |

Scenario Outline: Danish neuter numeral is available but is not the default cardinal
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | en |
    | 101 | hundrede og en |

Scenario Outline: Danish neuter cardinal
    Given I use the variants "køn=intetkøn"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | et |
    | 101 | hundrede og et |
