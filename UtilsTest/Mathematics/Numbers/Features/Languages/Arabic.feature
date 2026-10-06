@NumberToString @AR
Feature: Arabic number conversion

Scenario Outline: Decimal numbers
    Given I use the "AR" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | واحد فاصل خمسة |
    | 12.34 | اثنا عشر فاصل ثلاثة أربعة |

Scenario Outline: Basic cardinal numbers
    Given I use the "AR" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | صفر |
    | 1 | واحد |
    | 2 | اثنان |
    | 3 | ثلاثة |
    | 10 | عشرة |
    | 20 | عشرون |
    | 100 | مائة |
    | 1000 | ألف |

# NTS-10: the conjunction "و" joins the thousands to the lower group and, being a proclitic, is
# written attached to the following word ("ألف وواحد"), as it already is after the hundreds.
# Sources: Unicode CLDR RBNF ar ("ألف[ و>>]") and the University of Montana Arabic numbers sheet.
# Only the connector is in scope here; the dual/plural morphology of the thousands themselves
# (ألفان, ثلاثة آلاف) is pinned by the NTS-14 scenario below.
Scenario Outline: Thousands joined to the lower group with the attached conjunction
    Given I use the "AR" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1001 | ألف وواحد |
    | 1002 | ألف واثنان |
    | 1005 | ألف وخمسة |
    | 1010 | ألف وعشرة |
    | 1011 | ألف وأحد عشر |
    | 1021 | ألف وواحد وعشرون |
    | 1100 | ألف ومائة |
    | 1121 | ألف ومائة وواحد وعشرون |

# The feminine agreement still reaches a final unit written with the attached "و" (after the
# thousands and, likewise, after the hundreds).
Scenario Outline: Feminine units after an attached conjunction
    Given I use the "AR" number converter
    And I use the variants "gender=muʾannath"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 101 | مائة وواحدة |
    | 103 | مائة وثلاث |
    | 1001 | ألف وواحدة |
    | 1003 | ألف وثلاث |
    | 1010 | ألف وعشر |
    | 1011 | ألف وإحدى عشرة |
    | 1021 | ألف وواحدة وعشرون |

# NTS-14. The noun ألف takes the form governed by its multiplier: one and two are expressed by the
# noun alone (ألف, the dual ألفان, the multiplier word omitted); 3-10 take the plural آلاف; 11-99 the
# singular accusative ألفًا; whole hundreds the singular genitive ألف, the dual hundred standing in
# construct (مائتا ألف). For a compound multiplier the noun follows the last number written
# (Kalimah Center: "the counted noun follows the rules of the last number written",
# "ثمانية وعشرون ألفًا"; al-Dirassa; Virtual Arabic Language Academy, decision 29: "مئة وألف").
# Like the rest of the configuration, the standalone nominative is used (ألفان, not the oblique
# ألفين). The multiplier counts the masculine noun ألف, so it keeps its own form whatever the
# gender requested for the number.
Scenario Outline: Thousands take the form governed by their multiplier
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | ألف |
    | 2000 | ألفان |
    | 3000 | ثلاثة آلاف |
    | 9000 | تسعة آلاف |
    | 10000 | عشرة آلاف |
    | 11000 | أحد عشر ألفًا |
    | 12000 | اثنا عشر ألفًا |
    | 20000 | عشرون ألفًا |
    | 21000 | واحد وعشرون ألفًا |
    | 25000 | خمسة وعشرون ألفًا |
    | 100000 | مائة ألف |
    | 101000 | مائة وألف |
    | 102000 | مائة وألفان |
    | 103000 | مائة وثلاثة آلاف |
    | 110000 | مائة وعشرة آلاف |
    | 111000 | مائة وأحد عشر ألفًا |
    | 121000 | مائة وواحد وعشرون ألفًا |
    | 200000 | مائتا ألف |
    | 201000 | مائتان وألف |
    | 202000 | مائتان وألفان |
    | 345000 | ثلاثمائة وخمسة وأربعون ألفًا |
    | 999000 | تسعمائة وتسعة وتسعون ألفًا |
    | 2001 | ألفان وواحد |
    | 3001 | ثلاثة آلاف وواحد |
    | 11001 | أحد عشر ألفًا وواحد |
    | 21001 | واحد وعشرون ألفًا وواحد |
    | 123456 | مائة وثلاثة وعشرون ألفًا وأربعمائة وستة وخمسون |
    | 345678 | ثلاثمائة وخمسة وأربعون ألفًا وستمائة وثمانية وسبعون |

Scenario Outline: Feminine numbers keep the masculine agreement of the thousands multiplier
    Given I use the variants "gender=muʾannath"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 2002 | ألفان واثنتان |
    | 3000 | ثلاثة آلاف |
    | 3001 | ثلاثة آلاف وواحدة |
    | 11011 | أحد عشر ألفًا وإحدى عشرة |
    | 21000 | واحد وعشرون ألفًا |
    | 23003 | ثلاثة وعشرون ألفًا وثلاث |
    | 23023 | ثلاثة وعشرون ألفًا وثلاث وعشرون |
    | 101000 | مائة وألف |

Scenario Outline: Feminine cardinal numbers
    Given I use the "AR" number converter
    And I use the variants "gender=muʾannath"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | واحدة |
    | 2 | اثنتان |
    | 3 | ثلاث |
    | 4 | أربع |
    | 5 | خمس |
    | 6 | ست |
    | 7 | سبع |
    | 8 | ثمان |
    | 9 | تسع |
    | 10 | عشر |

Scenario Outline: Masculine ordinal numbers
    Given I use the "AR" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | أول |
    | 2 | ثانٍ |
    | 3 | ثالث |
    | 10 | عاشر |
    | 11 | حادي عشر |
    | 12 | ثاني عشر |
    | 19 | تاسع عشر |

Scenario Outline: Feminine ordinal numbers
    Given I use the "AR" number converter
    And I use the variants "gender=muʾannath"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | أولى |
    | 2 | ثانية |
    | 3 | ثالثة |
    | 10 | عاشرة |
    | 11 | حادية عشرة |
    | 19 | تاسعة عشرة |

Scenario Outline: Additional feminine ordinal numbers
    Given I use the "AR" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 4 | gender=muʾannath | رابعة |
    | 5 | gender=muʾannath | خامسة |
    | 6 | gender=muʾannath | سادسة |
    | 7 | gender=muʾannath | سابعة |
    | 8 | gender=muʾannath | ثامنة |
    | 9 | gender=muʾannath | تاسعة |

Scenario Outline: Masculine ordinal numbers from eleven through nineteen
    Given I use the "AR" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 13 |  | ثالث عشر |
    | 15 |  | خامس عشر |

Scenario Outline: Feminine ordinal numbers from eleven through nineteen
    Given I use the "AR" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 12 | gender=muʾannath | ثانية عشرة |
    | 13 | gender=muʾannath | ثالثة عشرة |
    | 15 | gender=muʾannath | خامسة عشرة |

Scenario: Ordinal conversion is supported
    Given I use the "AR" number converter
    Then the converter supports ordinal conversion

Scenario: Fraction connector wording
    Given I use the "AR" number converter
    When I convert the fraction 3/2 through both public fraction APIs
    Then both fraction results are "ثلاثة على اثنان"

# Compound cardinals: 11-19 put the unit first and keep "عشر" ("أحد عشر", "اثنا عشر"; feminine
# "إحدى عشرة", "اثنتا عشرة"); 21-99 put the unit first joined by "و" ("واحد وعشرون"); hundreds
# join their remainder with "و" ("مائة وواحد").
Scenario Outline: Arabic compound cardinal numbers
    Given I use the "AR" number converter
    And I use the variants "<variants>"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 11 |  | أحد عشر |
    | 12 |  | اثنا عشر |
    | 13 |  | ثلاثة عشر |
    | 19 |  | تسعة عشر |
    | 21 |  | واحد وعشرون |
    | 22 |  | اثنان وعشرون |
    | 35 |  | خمسة وثلاثون |
    | 101 |  | مائة وواحد |
    | 121 |  | مائة وواحد وعشرون |
    | 11 | gender=muʾannath | إحدى عشرة |
    | 12 | gender=muʾannath | اثنتا عشرة |
    | 13 | gender=muʾannath | ثلاث عشرة |
    | 21 | gender=muʾannath | واحدة وعشرون |
    | 23 | gender=muʾannath | ثلاث وعشرون |

# Contract: like 1-19, ordinals are the indefinite short nominative form without the article.
# From 21 the unit is an ordinal and the tens stay cardinal ("حادٍ وعشرون", feminine "حادية
# وعشرون"); round tens, one hundred and one thousand use the cardinal word, which is the
# ordinal form in Modern Standard Arabic ("العشرون", "المائة", "الألف" with the article).
Scenario Outline: Arabic ordinals above nineteen
    Given I use the "AR" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 20 |  | عشرون |
    | 21 |  | حادٍ وعشرون |
    | 22 |  | ثانٍ وعشرون |
    | 23 |  | ثالث وعشرون |
    | 30 |  | ثلاثون |
    | 99 |  | تاسع وتسعون |
    | 100 |  | مائة |
    | 1000 |  | ألف |
    | 20 | gender=muʾannath | عشرون |
    | 21 | gender=muʾannath | حادية وعشرون |
    | 22 | gender=muʾannath | ثانية وعشرون |
    | 23 | gender=muʾannath | ثالثة وعشرون |

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "AR" number converter
    Then the converter supports clock-time conversion

# "الساعة" is feminine: the hour is the definite feminine ordinal ("الثانية", "الحادية عشرة"),
# except one, which is "الواحدة". Quarter-hour 12-hour clock without day-part wording; the
# only vowel marks are the tanwin already used by the configuration ("ثانٍ", "ربعًا").
Scenario Outline: Idiomatic Arabic clock times
    Given I use the "AR" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | الواحدة |
    | 01:15 | الواحدة والربع |
    | 01:30 | الواحدة والنصف |
    | 01:45 | الثانية إلا ربعًا |
    | 02:00 | الثانية |
    | 03:00 | الثالثة |
    | 11:00 | الحادية عشرة |
    | 12:00 | الثانية عشرة |
    | 12:45 | الواحدة إلا ربعًا |
    | 13:30 | الواحدة والنصف |
    | 00:00 | الثانية عشرة |
    | 00:15 | الثانية عشرة والربع |
    | 11:45 | الثانية عشرة إلا ربعًا |
    | 12:15 | الثانية عشرة والربع |
    | 23:45 | الثانية عشرة إلا ربعًا |
