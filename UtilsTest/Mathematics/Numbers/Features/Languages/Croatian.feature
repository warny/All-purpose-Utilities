@NumberToString @HR
Feature: Croatian number conversion

Scenario Outline: Basic cardinal numbers
    Given I use the "HR" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | nula |
    | 1 | jedan |
    | 2 | dva |
    | 9 | devet |
    | 10 | deset |
    | 11 | jedanaest |
    | 12 | dvanaest |
    | 19 | devetnaest |
    | 20 | dvadeset |
    | 21 | dvadeset jedan |
    | 100 | sto |
    | 101 | sto jedan |
    | 200 | dvjesto |

Scenario Outline: Thousands
    Given I use the "HR" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | tisuća |
    | 2000 | dvije tisuće |
    | 3000 | tri tisuće |
    | 4000 | četiri tisuće |
    | 5000 | pet tisuća |
    | 10000 | deset tisuća |
    | 11000 | jedanaest tisuća |
    | 12000 | dvanaest tisuća |
    | 21000 | dvadeset jedna tisuća |
    | 22000 | dvadeset dvije tisuće |
    | 23000 | dvadeset tri tisuće |
    | 25000 | dvadeset pet tisuća |
    | 102000 | sto dvije tisuće |
    | 1001 | tisuću jedan |
    | 1311 | tisuću tristo jedanaest |
    | 3733 | tri tisuće sedamsto trideset tri |

# Hrvatski pravopis (IHJJ), "Višerječnice" (rule 31): "tisuću tristo jedanaest", "tri tisuće
# sedamsto trideset tri", "tisuću jedan", "milijun petsto trideset tisuća", "dvije milijarde trideset
# tri milijuna četiristo dvadeset šest tisuća dvadeset tri". The feminine "tisuća" and "milijarda"
# take "jedna"/"dvije" and the paucal forms ("tisuće", "milijarde") after 2-4 except 12-14, the
# genitive plural ("tisuća", "milijardi") otherwise; the masculine "milijun"/"bilijun" take
# "milijuna" after every multiplier not ending in "jedan". An exact single unit has no "jedan".
Scenario Outline: Long-scale cardinal numbers
    Given I use the "HR" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000000 | milijun |
    | 2000000 | dva milijuna |
    | 3000000 | tri milijuna |
    | 4000000 | četiri milijuna |
    | 5000000 | pet milijuna |
    | 11000000 | jedanaest milijuna |
    | 21000000 | dvadeset jedan milijun |
    | 22000000 | dvadeset dva milijuna |
    | 1530000 | milijun petsto trideset tisuća |
    | 1001000 | milijun tisuća |
    | 1001001 | milijun tisuću jedan |
    | 1001311 | milijun tisuću tristo jedanaest |
    | 2001001 | dva milijuna tisuću jedan |
    | 1000000000 | milijarda |
    | 2000000000 | dvije milijarde |
    | 3000000000 | tri milijarde |
    | 4000000000 | četiri milijarde |
    | 5000000000 | pet milijardi |
    | 11000000000 | jedanaest milijardi |
    | 21000000000 | dvadeset jedna milijarda |
    | 22000000000 | dvadeset dvije milijarde |
    | 2033426023 | dvije milijarde trideset tri milijuna četiristo dvadeset šest tisuća dvadeset tri |
    | 1000000000000 | bilijun |
    | 2000000000000 | dva bilijuna |

# Hrvatski pravopis (IHJJ), rule 31: only the last component of a compound ordinal is ordinal
# ("tisuću prvi", "tisuću tristo jedanaesti", "tri tisuće sedamsto trideset treći"). The ordinal of
# 1000 is "tisućiti" (Hrvatski jezični portal). Ordinals of other round thousands, millions and
# milliards ("2000.") have no verified form and fail closed (see NumberToStringOrdinalPluginTests).
Scenario Outline: Irregular ordinal numbers
    Given I use the "HR" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | prvi |
    | 2 | drugi |
    | 3 | treći |
    | 4 | četvrti |
    | 5 | peti |
    | 6 | šesti |
    | 7 | sedmi |
    | 8 | osmi |
    | 9 | deveti |
    | 10 | deseti |
    | 11 | jedanaesti |
    | 20 | dvadeseti |
    | 21 | dvadeset prvi |
    | 31 | trideset prvi |
    | 44 | četrdeset četvrti |
    | 100 | stoti |
    | 101 | sto prvi |
    | 600 | šestoti |
    | 1000 | tisućiti |
    | 1001 | tisuću prvi |
    | 1002 | tisuću drugi |
    | 1010 | tisuću deseti |
    | 1021 | tisuću dvadeset prvi |
    | 1311 | tisuću tristo jedanaesti |
    | 2001 | dvije tisuće prvi |
    | 3733 | tri tisuće sedamsto trideset treći |
    | 1000000 | milijunti |
    | 2000001 | dva milijuna prvi |
    | 1001001 | milijun tisuću prvi |
    | 1001311 | milijun tisuću tristo jedanaesti |
    | 2001001 | dva milijuna tisuću prvi |
    | 5000001 | pet milijuna prvi |

Scenario: Ordinal conversion is supported
    Given I use the "HR" number converter
    Then the converter supports ordinal conversion

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "HR" number converter
    Then the converter supports clock-time conversion

# "Jedan je sat / dva su sata / pet je sati": the hour noun agrees with the count (paucal
# "sata" for 2-4, genitive plural "sati" from 5), selected through displayHourRange.
Scenario Outline: Idiomatic Croatian clock times
    Given I use the "HR" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | jedan sat |
    | 01:15 | jedan i petnaest |
    | 01:30 | pola dva |
    | 01:45 | petnaest do dva |
    | 02:00 | dva sata |
    | 04:00 | četiri sata |
    | 05:00 | pet sati |
    | 12:00 | dvanaest sati |
    | 13:30 | pola dva |
    | 00:00 | dvanaest sati |
    | 11:45 | petnaest do dvanaest |
    | 12:15 | dvanaest i petnaest |
    | 12:45 | petnaest do jedan |
    | 23:45 | petnaest do dvanaest |

Scenario: Temporal conversion is unsupported
    Given I use the "HR" number converter
    Then the converter does not support time conversion

Scenario: Unsupported time conversion fails closed
    Given I use the "HR" number converter
    When I attempt to convert the duration "01:00:00"
    Then conversion is rejected because time conversion is not supported

Scenario: The regional alias uses Croatian wording
    Then the "HR" and "HR-HR" converters produce the same cardinal wording for 2
