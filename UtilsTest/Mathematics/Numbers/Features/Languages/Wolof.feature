@NumberToString @WO
Feature: Wolof number conversion

# Orthography (NTS-19): decree 2005-992 for the spelling (one accent on a closed long vowel: téeméer,
# juróom; the connective -i attached to the completed term, Art. 13: "ñaari tomb"); words of a numeral
# separated by spaces as in every consulted grammar (Kosogorova 2023, Robert 2021, Gaye 1980 for the
# Peace Corps, Wiktionary): "juróom benn", "ñaar fukk". The decree hyphenates only lexicalized
# compounds (Art. 21: gaynde-géej), which numeral phrases are not.

Scenario Outline: Decimal numbers
    Given I use the "WO" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | benn pojint juróom |
    | 12.34 | fukk ak ñaar pojint ñett ñent |

# NTS-19. 1-99: Kosogorova §4.1 (29)-(31), Gaye 1980 pp. 66-67, Robert §3.2.7 ("juróóm benn",
# "ñaar fukk", "juróom benn fukk" 60, "ak" before the units). 100-999: "téeméer" alone for 100,
# "ak" before the lower part (Robert "tééméér ak benn", Kosogorova 111, 234), and the multiplier of
# hundreds bears the connective -i (Robert: "the number of hundreds or thousands are compound forms
# with the genitival linker"; Gaye: "This -i- is a linker ... between the number and the object
# counted", on the last element: "juróom benn-i dërëm"; decree Art. 13; Janga Wolof "ñaari
# téeméer"). Kosogorova's "ñaar tééméér" is the variant without -i, which Gaye notes is optional
# with ñaar only: accepted, not produced.
Scenario Outline: Cardinal numbers below a thousand
    Given I use the "WO" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | benn |
    | 2 | ñaar |
    | 4 | ñent |
    | 5 | juróom |
    | 6 | juróom benn |
    | 8 | juróom ñett |
    | 10 | fukk |
    | 11 | fukk ak benn |
    | 19 | fukk ak juróom ñent |
    | 20 | ñaar fukk |
    | 21 | ñaar fukk ak benn |
    | 24 | ñaar fukk ak ñent |
    | 40 | ñent fukk |
    | 60 | juróom benn fukk |
    | 99 | juróom ñent fukk ak juróom ñent |
    | 100 | téeméer |
    | 101 | téeméer ak benn |
    | 119 | téeméer ak fukk ak juróom ñent |
    | 200 | ñaari téeméer |
    | 234 | ñaari téeméer ak ñett fukk ak ñent |
    | 500 | juróomi téeméer |
    | 600 | juróom benni téeméer |
    | 999 | juróom ñenti téeméer ak juróom ñent fukk ak juróom ñent |

# NTS-19. Thousands: "junni" alone for 1000 (Robert, Wiktionary, Janga Wolof; Kosogorova "juuni ak
# juróom ñaar" 1007), "ak" before the lower part (Kosogorova 1007 and 2080, Robert 2080, Janga
# "junni ak benn"), the multiplier with the attached connective -i (Robert "ñaar-i junni ak juróóm
# ñett fukk" 2080; Janga "ñaari junni", "fukki junni", "téeméeri junni"; Boston University
# "fukki téeméeri junni"). Attested: 1000, 1001, 1007, 2000, 2080, 10000, 100000. The compound
# multipliers (11000, 21000, 99000 ...) apply the -i to the last element, as Gaye's "juróom benn-i
# dërëm": productive, not individually attested.
Scenario Outline: Cardinal numbers from a thousand
    Given I use the "WO" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | junni |
    | 1001 | junni ak benn |
    | 1005 | junni ak juróom |
    | 1007 | junni ak juróom ñaar |
    | 1010 | junni ak fukk |
    | 1011 | junni ak fukk ak benn |
    | 1100 | junni ak téeméer |
    | 1111 | junni ak téeméer ak fukk ak benn |
    | 1599 | junni ak juróomi téeméer ak juróom ñent fukk ak juróom ñent |
    | 2000 | ñaari junni |
    | 2001 | ñaari junni ak benn |
    | 2080 | ñaari junni ak juróom ñett fukk |
    | 2100 | ñaari junni ak téeméer |
    | 2345 | ñaari junni ak ñetti téeméer ak ñent fukk ak juróom |
    | 3000 | ñetti junni |
    | 4000 | ñenti junni |
    | 5000 | juróomi junni |
    | 6000 | juróom benni junni |
    | 10000 | fukki junni |
    | 11000 | fukk ak benni junni |
    | 20000 | ñaar fukki junni |
    | 21000 | ñaar fukk ak benni junni |
    | 99000 | juróom ñent fukk ak juróom ñenti junni |
    | 100000 | téeméeri junni |
    | 200000 | ñaari téeméeri junni |
    | 999999 | juróom ñenti téeméer ak juróom ñent fukk ak juróom ñenti junni ak juróom ñenti téeméer ak juróom ñent fukk ak juróom ñent |
    | -1000 | minus junni |
    | -1001 | minus junni ak benn |

# NTS-21. The scales above junni are the French loans milyoŋ (10^6) and milyaar (10^9). One is the
# bare "benn"; a multiplier above one takes the connective -i on its last element as for téeméer and
# junni (NTS-19). Attested: Wolof Bible (eBible wolmbs, Mt 25) "benn milyoŋ", "ñaari milyoŋ", "juróomi
# milyoŋ", "fukki milyoŋ"; Wolof Ajami Reader (Boston University) "juróom fukki milyoŋ", "juróomi
# milyaar"; BU 200 Word Project "benn milyoŋ"; Guérin and Wolof university teaching material (milyoŋ,
# milyaar) as checked in the PR #628 review. The other multipliers apply the rule productively.
# The pair is the long scale; bilyoŋ, bilyaar, trilyoŋ ... are the library's productive Conway
# extension in Wolof spelling (qu → kw, x → ks, v → w, c → s/k), not attested usage.
Scenario Outline: Cardinal numbers from a million
    Given I use the "WO" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000000 | benn milyoŋ |
    | 1000000000 | benn milyaar |
    | 1000000000000 | benn bilyoŋ |
    | 2000000 | ñaari milyoŋ |
    | 2000000000 | ñaari milyaar |
    | 5000000 | juróomi milyoŋ |
    | 50000000 | juróom fukki milyoŋ |
    | 5000000000 | juróomi milyaar |
    | 1000001 | benn milyoŋ ak benn |
    | 1001000 | benn milyoŋ ak junni |
    | 1001001 | benn milyoŋ ak junni ak benn |
    | 10000000 | fukki milyoŋ |
    | 21000000 | ñaar fukk ak benni milyoŋ |
    | 200000000 | ñaari téeméeri milyoŋ |
    | 1000000000000000 | benn bilyaar |
    | 1000000000000000000 | benn trilyoŋ |
    | 1000000000000000000000000 | benn kwadrilyoŋ |
    | 1000000000000000000000000000000000000 | benn sekstilyoŋ |
    | -1000000 | minus benn milyoŋ |

# NTS-19. Zero is "tus" (Boston University, The 200 Word Project, "Tus (Zero)"; Wiktionary's Wolof
# number list; Janga Wolof "tus / dara"). "dara" is the indefinite "anything / nothing" and "sero"
# a French loan that no consulted source lists.
Scenario: Zero cardinal
    Given I use the "WO" number converter
    When I convert the cardinal number 0
    Then the result is "tus"

# NTS-16, spelling revised by NTS-19. Wolof ordinals add the suffix to the LAST element of the
# cardinal (Kosogorova 2023 §4.2: "juróom ñetteel", "fukk(a) ak ñaareel", "ñaar fukk(a) ak
# ñenteel"; Robert 2021: "juróóm ñaar-eel"; Ngom 2003). Its spelling is the decree's: Art. 13 prints
# "fukkéelu garab gi" ("le dixième arbre") and Art. 20 keeps a suffix's spelling invariable despite
# vowel harmony, hence -éel (the academic transcriptions -eel are phonetic). "First" is the
# suppletive relative form of jëkk, cited in class B (Ngom 2003: "xale b-u njëkk"; Janga "bu njëk").
Scenario Outline: Sourced ordinal numbers
    Given I use the "WO" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | bu njëkk |
    | 2 | ñaaréel |
    | 3 | ñettéel |
    | 7 | juróom ñaaréel |
    | 8 | juróom ñettéel |
    | 10 | fukkéel |
    | 12 | fukk ak ñaaréel |
    | 24 | ñaar fukk ak ñentéel |

# Productive application of the stated rule ("added to the last element of a cardinal numeral"; the
# only exception is 'one'): not individually exemplified by the consulted sources.
Scenario Outline: Productive ordinal numbers
    Given I use the "WO" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 11 | fukk ak bennéel |
    | 21 | ñaar fukk ak bennéel |
    | 100 | téeméeréel |
    | 101 | téeméer ak bennéel |
    | 234 | ñaari téeméer ak ñett fukk ak ñentéel |
    | 999 | juróom ñenti téeméer ak juróom ñent fukk ak juróom ñentéel |
    | 1001 | junni ak bennéel |
    | 1010 | junni ak fukkéel |
    | 1011 | junni ak fukk ak bennéel |
    | 1100 | junni ak téeméeréel |
    | 1111 | junni ak téeméer ak fukk ak bennéel |
    | 2001 | ñaari junni ak bennéel |
    | 999999 | juróom ñenti téeméer ak juróom ñent fukk ak juróom ñenti junni ak juróom ñenti téeméer ak juróom ñent fukk ak juróom ñentéel |
    | -2 | minus ñaaréel |
    | -1001 | minus junni ak bennéel |

# NTS-19. An ordinal whose last element is junni (the round thousands) would join the suffix to a
# vowel-final noun, and the only examples disagree: Omniglot "junneel" (vowel dropped), Janga Wolof
# "junniéél" (kept). No academic or normative source settles it, so these values stay rejected, as
# does zero (NTS-14: no attested ordinal of zero).
Scenario Outline: Ordinals of zero and of the round thousands are not supported
    Given I use the "WO" number converter
    When I attempt to convert the ordinal number <number>
    Then conversion is rejected because no ordinal form is available

Examples:
    | number |
    | 0 |
    | 1000 |
    | 2000 |
    | 10000 |
    | 21000 |
    | 100000 |
    | 999000 |
    | -1000 |

# NTS-21. The cardinals now reach the millions, but no consulted source exemplifies an ordinal above
# the thousands: the ordinal domain stays the one audited by NTS-16/NTS-19.
Scenario Outline: Ordinals from a million are not supported
    Given I use the "WO" number converter
    When I attempt to convert the ordinal number <number>
    Then conversion is rejected because no ordinal form is available

Examples:
    | number |
    | 1000000 |
    | 1000001 |
    | 2000000 |
    | 1000000000 |
    | -1000001 |

Scenario: Ordinal conversion is supported
    Given I use the "WO" number converter
    Then the converter supports ordinal conversion

Scenario: Fraction connector wording
    Given I use the "WO" number converter
    When I convert the fraction 3/2 through both public fraction APIs
    Then both fraction results are "ñett ci ñaar"

# Several modern Wolof clock conventions coexist (native "waxtu" and French-derived readings) and no
# single sourced system covering 01:00-01:45 was established: clock-time stays deliberately unsupported.
Scenario: Idiomatic clock-time conversion is unsupported
    Given I use the "WO" number converter
    Then the converter does not support clock-time conversion
