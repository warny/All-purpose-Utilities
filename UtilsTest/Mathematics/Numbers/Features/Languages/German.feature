@NumberToString
Feature: German number conversion

Scenario Outline: From1To999Test 1
    Given I use the "de-DE" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | -1 | minus eins |
    | 0 | null |
    | 1 | eins |
    | 2 | zwei |
    | 11 | elf |
    | 20 | zwanzig |
    | 21 | einundzwanzig |
    | 22 | zweiundzwanzig |
    | 60 | sechzig |
    | 61 | einundsechzig |
    | 62 | zweiundsechzig |
    | 111 | einhundertelf |
    | 121 | einhunderteinundzwanzig |
    | 122 | einhundertzweiundzwanzig |
    | 160 | einhundertsechzig |
    | 161 | einhunderteinundsechzig |
    | 162 | einhundertzweiundsechzig |
    | 200 | zweihundert |
    | 201 | zweihunderteins |
    | 221 | zweihunderteinundzwanzig |
    | 222 | zweihundertzweiundzwanzig |
    | 260 | zweihundertsechzig |
    | 261 | zweihunderteinundsechzig |
    | 262 | zweihundertzweiundsechzig |

Scenario Outline: From1000To9999Test 2
    Given I use the "de-CH" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | ein tausend |
    | 1001 | ein tausend eins |
    | 1002 | ein tausend zwei |
    | 1011 | ein tausend elf |
    | 1020 | ein tausend zwanzig |
    | 1021 | ein tausend einundzwanzig |
    | 1022 | ein tausend zweiundzwanzig |
    | 1060 | ein tausend sechzig |
    | 1061 | ein tausend einundsechzig |
    | 1062 | ein tausend zweiundsechzig |
    | 1111 | ein tausend einhundertelf |
    | 1121 | ein tausend einhunderteinundzwanzig |
    | 1122 | ein tausend einhundertzweiundzwanzig |
    | 1160 | ein tausend einhundertsechzig |
    | 1161 | ein tausend einhunderteinundsechzig |
    | 1162 | ein tausend einhundertzweiundsechzig |
    | 1200 | ein tausend zweihundert |
    | 1201 | ein tausend zweihunderteins |
    | 1221 | ein tausend zweihunderteinundzwanzig |
    | 1222 | ein tausend zweihundertzweiundzwanzig |
    | 1260 | ein tausend zweihundertsechzig |
    | 1261 | ein tausend zweihunderteinundsechzig |
    | 1262 | ein tausend zweihundertzweiundsechzig |

Scenario Outline: From10000To99999Test 3
    Given I use the "de" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 12000 | zwölf tausend |
    | 12001 | zwölf tausend eins |
    | 12002 | zwölf tausend zwei |
    | 12011 | zwölf tausend elf |
    | 12020 | zwölf tausend zwanzig |
    | 12021 | zwölf tausend einundzwanzig |
    | 12022 | zwölf tausend zweiundzwanzig |
    | 12060 | zwölf tausend sechzig |
    | 12061 | zwölf tausend einundsechzig |
    | 12062 | zwölf tausend zweiundsechzig |
    | 12111 | zwölf tausend einhundertelf |
    | 12121 | zwölf tausend einhunderteinundzwanzig |
    | 12122 | zwölf tausend einhundertzweiundzwanzig |
    | 99160 | neunundneunzig tausend einhundertsechzig |
    | 99161 | neunundneunzig tausend einhunderteinundsechzig |
    | 99162 | neunundneunzig tausend einhundertzweiundsechzig |
    | 99200 | neunundneunzig tausend zweihundert |
    | 99201 | neunundneunzig tausend zweihunderteins |
    | 99221 | neunundneunzig tausend zweihunderteinundzwanzig |
    | 99222 | neunundneunzig tausend zweihundertzweiundzwanzig |
    | 99260 | neunundneunzig tausend zweihundertsechzig |
    | 99261 | neunundneunzig tausend zweihunderteinundsechzig |
    | 99262 | neunundneunzig tausend zweihundertzweiundsechzig |

Scenario Outline: BiggerTest 4
    Given I use the "de-DE" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | -401000 | minus vierhundertein tausend |
    | 401000 | vierhundertein tausend |
    | 999999 | neunhundertneunundneunzig tausend neunhundertneunundneunzig |
    | 1000000 | eine Million |
    | 999999999 | neunhundertneunundneunzig Millionen neunhundertneunundneunzig tausend neunhundertneunundneunzig |

Scenario Outline: DecimalTest 5
    Given I use the "de-DE" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | eins komma fünf |
    | 12.34 | zwölf komma drei vier |
