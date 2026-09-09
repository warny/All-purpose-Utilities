@NumberToString @DE
Feature: German number conversion

Scenario Outline: Cardinal numbers below one thousand
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

Scenario Outline: Thousands
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

Scenario Outline: Tens of thousands
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

Scenario Outline: Large cardinal numbers
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

Scenario Outline: Decimal numbers
    Given I use the "de-DE" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | eins komma fünf |
    | 12.34 | zwölf komma drei vier |

Scenario Outline: Duration wording
    Given I use the "DE" number converter
    When I convert the duration "<value>"
    Then the result is "<expected>"

Examples:
    | value | expected |
    | 01:00:00 | eine Stunde |
    | 02:30:05 | zwei Stunden dreißig Minuten fünf Sekunden |

Scenario Outline: Date wording
    Given I use the "DE" number converter
    When I convert the date "<value>"
    Then the result is "<expected>"

Examples:
    | value | expected |
    | 2026-07-01 | ersten. Juli zwei tausend sechsundzwanzig |
    | 2026-07-02 | zwei. Juli zwei tausend sechsundzwanzig |

Scenario: Date and time wording
    Given I use the "DE" number converter
    When I convert the date and time "2026-07-02T14:30:05"
    Then the result is "zwei. Juli zwei tausend sechsundzwanzig vierzehn Stunden dreißig Minuten fünf Sekunden"

Scenario Outline: Default cardinal one
    Given I use the "DE" number converter
    And I use the variants "<variants>"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 |  | eins |
    | 1 | genus=maskulin | eins |
    | 1 | kasus=nominativ | eins |

Scenario Outline: Feminine cardinal one
    Given I use the "DE" number converter
    And I use the variants "<variants>"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 | genus=feminin | eine |
    | 1 | kasus=nominativ,genus=feminin | eine |
    | 1 | kasus=akkusativ,genus=feminin | eine |

Scenario Outline: Masculine accusative cardinal one
    Given I use the "DE" number converter
    And I use the variants "<variants>"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 | kasus=akkusativ,genus=maskulin | einen |

Scenario Outline: Dative cardinal one
    Given I use the "DE" number converter
    And I use the variants "<variants>"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 | kasus=dativ,genus=maskulin | einem |
    | 1 | kasus=dativ,genus=neutrum | einem |
    | 1 | kasus=dativ,genus=feminin | einer |

Scenario Outline: Genitive cardinal one
    Given I use the "DE" number converter
    And I use the variants "<variants>"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 | kasus=genitiv,genus=maskulin | eines |
    | 1 | kasus=genitiv,genus=neutrum | eines |
    | 1 | kasus=genitiv,genus=feminin | einer |

Scenario Outline: Compound cardinal numbers remain uninflected
    Given I use the "DE" number converter
    And I use the variants "<variants>"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 21 | genus=feminin | einundzwanzig |
    | 21 | kasus=akkusativ,genus=maskulin | einundzwanzig |

Scenario Outline: Irregular ordinal numbers
    Given I use the "DE" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 |  | erste |
    | 3 |  | dritte |
    | 7 |  | siebte |
    | 8 |  | achte |

Scenario Outline: Regular and suffixed ordinal numbers
    Given I use the "DE" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 2 |  | zweite |
    | 4 |  | vierte |
    | 5 |  | fünfte |
    | 6 |  | sechste |
    | 9 |  | neunte |
    | 10 |  | zehnte |
    | 11 |  | elfte |
    | 12 |  | zwölfte |
    | 13 |  | dreizehnte |
    | 19 |  | neunzehnte |
    | 20 |  | zwanzigste |
    | 21 |  | einundzwanzigste |
    | 30 |  | dreißigste |

Scenario Outline: Compound ordinal numbers
    Given I use the "DE" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1000 |  | tausendste |
    | 1001 |  | tausend erste |
    | 1003 |  | tausend dritte |

Scenario Outline: Irregular ordinal one by declension case and gender
    Given I use the "DE" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 | deklination=schwach,genus=maskulin,kasus=nominativ | erste |
    | 1 | deklination=schwach,genus=maskulin,kasus=akkusativ | ersten |
    | 1 | deklination=schwach,genus=maskulin,kasus=dativ | ersten |
    | 1 | deklination=schwach,genus=maskulin,kasus=genitiv | ersten |
    | 1 | deklination=schwach,genus=feminin,kasus=nominativ | erste |
    | 1 | deklination=schwach,genus=feminin,kasus=akkusativ | erste |
    | 1 | deklination=schwach,genus=feminin,kasus=dativ | ersten |
    | 1 | deklination=schwach,genus=feminin,kasus=genitiv | ersten |
    | 1 | deklination=schwach,genus=neutrum,kasus=nominativ | erste |
    | 1 | deklination=schwach,genus=neutrum,kasus=akkusativ | erste |
    | 1 | deklination=stark,genus=maskulin,kasus=nominativ | erster |
    | 1 | deklination=stark,genus=maskulin,kasus=akkusativ | ersten |
    | 1 | deklination=stark,genus=maskulin,kasus=dativ | erstem |
    | 1 | deklination=stark,genus=maskulin,kasus=genitiv | ersten |
    | 1 | deklination=stark,genus=feminin,kasus=nominativ | erste |
    | 1 | deklination=stark,genus=feminin,kasus=akkusativ | erste |
    | 1 | deklination=stark,genus=feminin,kasus=dativ | erster |
    | 1 | deklination=stark,genus=feminin,kasus=genitiv | erster |
    | 1 | deklination=stark,genus=neutrum,kasus=nominativ | erstes |
    | 1 | deklination=stark,genus=neutrum,kasus=akkusativ | erstes |
    | 1 | deklination=stark,genus=neutrum,kasus=dativ | erstem |
    | 1 | deklination=stark,genus=neutrum,kasus=genitiv | ersten |

Scenario Outline: Regular ordinals by declension case and gender
    Given I use the "DE" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 20 | deklination=schwach,genus=maskulin,kasus=nominativ | zwanzigste |
    | 20 | deklination=schwach,genus=maskulin,kasus=akkusativ | zwanzigsten |
    | 20 | deklination=schwach,genus=maskulin,kasus=dativ | zwanzigsten |
    | 20 | deklination=schwach,genus=maskulin,kasus=genitiv | zwanzigsten |
    | 20 | deklination=schwach,genus=feminin,kasus=nominativ | zwanzigste |
    | 20 | deklination=schwach,genus=feminin,kasus=akkusativ | zwanzigste |
    | 20 | deklination=schwach,genus=feminin,kasus=dativ | zwanzigsten |
    | 20 | deklination=schwach,genus=feminin,kasus=genitiv | zwanzigsten |
    | 20 | deklination=schwach,genus=neutrum,kasus=nominativ | zwanzigste |
    | 20 | deklination=schwach,genus=neutrum,kasus=akkusativ | zwanzigste |
    | 20 | deklination=schwach,genus=neutrum,kasus=dativ | zwanzigsten |
    | 20 | deklination=schwach,genus=neutrum,kasus=genitiv | zwanzigsten |
    | 20 | deklination=stark,genus=maskulin,kasus=nominativ | zwanzigster |
    | 20 | deklination=stark,genus=maskulin,kasus=akkusativ | zwanzigsten |
    | 20 | deklination=stark,genus=maskulin,kasus=dativ | zwanzigstem |
    | 20 | deklination=stark,genus=maskulin,kasus=genitiv | zwanzigsten |
    | 20 | deklination=stark,genus=feminin,kasus=nominativ | zwanzigste |
    | 20 | deklination=stark,genus=feminin,kasus=akkusativ | zwanzigste |
    | 20 | deklination=stark,genus=feminin,kasus=dativ | zwanzigster |
    | 20 | deklination=stark,genus=feminin,kasus=genitiv | zwanzigster |
    | 20 | deklination=stark,genus=neutrum,kasus=nominativ | zwanzigstes |
    | 20 | deklination=stark,genus=neutrum,kasus=akkusativ | zwanzigstes |
    | 20 | deklination=stark,genus=neutrum,kasus=dativ | zwanzigstem |
    | 20 | deklination=stark,genus=neutrum,kasus=genitiv | zwanzigsten |
    | 21 | deklination=stark,genus=maskulin,kasus=nominativ | einundzwanzigster |
    | 21 | deklination=stark,genus=maskulin,kasus=akkusativ | einundzwanzigsten |

Scenario Outline: Caller-defined currency wording
    Given I use the "DE" number converter
    And I use this currency definition
        | property         | value |
        | unit singular    | Euro  |
        | unit plural      | Euro  |
        | subunit singular | Cent  |
        | subunit plural   | Cent  |
        | connector        | und   |
    When I convert the currency amount <amount>
    Then the result is "<expected>"

Examples:
    | amount | expected                       |
    | 1      | eins Euro                      |
    | 2      | zwei Euro                      |
    | 1.50   | eins Euro und fünfzig Cent     |

@HugeNumber
Scenario: A very large cardinal number follows the shipped scale vocabulary
    Given I use the "de-DE" number converter
    When I convert the cardinal number 1852673427797059126777135760139006525652319754650249024631321344126610074238975
    Then the result is "eine Tredezillion achthundertzweiundfünfzig Duodezilliarden sechshundertdreiundsiebzig Duodezillionen vierhundertsiebenundzwanzig Unidezilliarden siebenhundertsiebenundneunzig Unidezillionen neunundfünfzig Dezilliarden einhundertsechsundzwanzig Dezillionen siebenhundertsiebenundsiebzig Nonilliarden einhundertfünfunddreißig Nonillionen siebenhundertsechzig Octilliarden einhundertneununddreißig Octillionen sechs Septilliarden fünfhundertfünfundzwanzig Septillionen sechshundertzweiundfünfzig Sextilliarden dreihundertneunzehn Sextillionen siebenhundertvierundfünfzig Quintilliarden sechshundertfünfzig Quintillionen zweihundertneunundvierzig Quadrilliarden vierundzwanzig Quadrillionen sechshunderteinunddreißig Trilliarden dreihunderteinundzwanzig Trillionen dreihundertvierundvierzig Billiarden einhundertsechsundzwanzig Billionen sechshundertzehn Milliarden vierundsiebzig Millionen zweihundertachtunddreißig tausend neunhundertfünfundsiebzig"

Scenario: Ordinal conversion is supported
    Given I use the "DE" number converter
    Then the converter supports ordinal conversion

Scenario: Temporal conversion is supported
    Given I use the "DE" number converter
    Then the converter supports time conversion
    And the converter supports date conversion

Scenario: Feminine decimal wording
    Given I use the "DE" number converter
    And I use the variants "gender=feminin"
    When I convert the decimal number 1.5
    Then the result is "eine komma fünf"

Scenario Outline: Year wording
    Given I use the "DE" number converter
    When I convert the year <year>
    Then the result is "<expected>"

Examples:
    | year | expected                    |
    | 1984 | neunzehn vierundachtzig     |
    | 1900 | neunzehn hundert            |
    | 1100 | elf hundert                 |
    | 1999 | neunzehn neunundneunzig     |
