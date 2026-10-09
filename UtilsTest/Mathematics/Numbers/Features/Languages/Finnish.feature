@NumberToString @FI
Feature: Finnish number conversion

Scenario Outline: Basic cardinal numbers
    Given I use the "FI" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | nolla |
    | 1 | yksi |
    | 2 | kaksi |
    | 3 | kolme |
    | 9 | yhdeksän |
    | 10 | kymmenen |
    | 11 | yksitoista |
    | 12 | kaksitoista |
    | 19 | yhdeksäntoista |
    | 20 | kaksikymmentä |
    | 21 | kaksikymmentä yksi |
    | 100 | sata |
    | 200 | kaksisataa |
    | 101 | sata yksi |

Scenario Outline: Thousands
    Given I use the "FI" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | tuhat |
    | 2000 | kaksi tuhat |

Scenario Outline: Basic ordinal numbers
    Given I use the "FI" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | ensimmäinen |
    | 2 | toinen |
    | 3 | kolmas |
    | 10 | kymmenes |
    | 11 | yhdestoista |
    | 12 | kahdestoista |
    | 20 | kahdeskymmenes |
    | 100 | sadas |
    | 1000 | tuhannes |

Scenario Outline: Partitive cardinal numbers
    Given I use the "FI" number converter
    And I use the variants "case=partitiivi"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | yhtä |
    | 2 | kahta |
    | 3 | kolmea |
    | 10 | kymmentä |
    | 100 | sataa |
    | 1000 | tuhatta |

Scenario Outline: Genitive cardinal numbers
    Given I use the "FI" number converter
    And I use the variants "case=genetiivi"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | yhden |
    | 2 | kahden |
    | 100 | sadan |
    | 1000 | tuhannen |

Scenario Outline: Partitive unit cardinal numbers
    Given I use the "FI" number converter
    And I use the variants "<variants>"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 | sijamuoto=partitiivi | yhtä |
    | 2 | sijamuoto=partitiivi | kahta |
    | 3 | sijamuoto=partitiivi | kolmea |
    | 4 | sijamuoto=partitiivi | neljää |
    | 5 | sijamuoto=partitiivi | viittä |
    | 6 | sijamuoto=partitiivi | kuutta |
    | 7 | sijamuoto=partitiivi | seitsemää |
    | 8 | sijamuoto=partitiivi | kahdeksaa |
    | 9 | sijamuoto=partitiivi | yhdeksää |

Scenario Outline: Partitive scale and compound cardinal numbers
    Given I use the "FI" number converter
    And I use the variants "<variants>"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 10 | sijamuoto=partitiivi | kymmentä |
    | 100 | sijamuoto=partitiivi | sataa |
    | 1000 | sijamuoto=partitiivi | tuhatta |
    | 21 | sijamuoto=partitiivi | kaksikymmentä yhtä |
    | 22 | sijamuoto=partitiivi | kaksikymmentä kahta |
    | 201 | sijamuoto=partitiivi | kaksisataa yhtä |

Scenario Outline: Partitive cardinal numbers from eleven through nineteen
    Given I use the "FI" number converter
    And I use the variants "<variants>"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 11 | sijamuoto=partitiivi | yhtätoista |
    | 12 | sijamuoto=partitiivi | kahtatoista |
    | 17 | sijamuoto=partitiivi | seitsemäätoista |
    | 18 | sijamuoto=partitiivi | kahdeksaatoista |
    | 19 | sijamuoto=partitiivi | yhdeksäätoista |
    | 211 | sijamuoto=partitiivi | kaksisataa yhtätoista |

Scenario Outline: Genitive unit cardinal numbers
    Given I use the "FI" number converter
    And I use the variants "<variants>"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 | sijamuoto=genetiivi | yhden |
    | 2 | sijamuoto=genetiivi | kahden |
    | 3 | sijamuoto=genetiivi | kolmen |
    | 4 | sijamuoto=genetiivi | neljän |
    | 5 | sijamuoto=genetiivi | viiden |
    | 6 | sijamuoto=genetiivi | kuuden |
    | 7 | sijamuoto=genetiivi | seitsemän |
    | 8 | sijamuoto=genetiivi | kahdeksan |
    | 9 | sijamuoto=genetiivi | yhdeksän |

Scenario Outline: Genitive tens and hundreds
    Given I use the "FI" number converter
    And I use the variants "<variants>"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 20 | sijamuoto=genetiivi | kahdenkymmenen |
    | 30 | sijamuoto=genetiivi | kolmenkymmenen |
    | 70 | sijamuoto=genetiivi | seitsemänkymmenen |
    | 21 | sijamuoto=genetiivi | kahdenkymmenen yhden |
    | 22 | sijamuoto=genetiivi | kahdenkymmenen kahden |
    | 200 | sijamuoto=genetiivi | kahdensadan |
    | 300 | sijamuoto=genetiivi | kolmensadan |
    | 900 | sijamuoto=genetiivi | yhdeksänsadan |
    | 221 | sijamuoto=genetiivi | kahdensadan kahdenkymmenen yhden |
    | 222 | sijamuoto=genetiivi | kahdensadan kahdenkymmenen kahden |

Scenario Outline: Genitive cardinal numbers from eleven through sixteen
    Given I use the "FI" number converter
    And I use the variants "<variants>"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 11 | sijamuoto=genetiivi | yhdentoista |
    | 12 | sijamuoto=genetiivi | kahdentoista |
    | 16 | sijamuoto=genetiivi | kuudentoista |
    | 17 | sijamuoto=genetiivi | seitsemäntoista |
    | 18 | sijamuoto=genetiivi | kahdeksantoista |
    | 19 | sijamuoto=genetiivi | yhdeksäntoista |

Scenario Outline: Explicit nominative cardinal numbers
    Given I use the "FI" number converter
    And I use the variants "<variants>"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 | sijamuoto=nominatiivi | yksi |
    | 20 | sijamuoto=nominatiivi | kaksikymmentä |

Scenario Outline: Additional ordinal numbers
    Given I use the "FI" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 4 |  | neljäs |
    | 5 |  | viides |
    | 30 |  | kolmaskymmenes |
    | 200 |  | kahdessadas |
    | 17 | case=nominatiivi | seitsemästoista |

# NTS-08: the Aalto University course "Numeroiden taivutus" inflects every stem of an ordinal
# (genitive ensimmäisen, seitsemännentoista; partitive ensimmäistä, seitsemättätoista) and compounds
# fuse their ordinal components (kahdeskymmenesensimmäinen). The declarative pipeline gave the cardinal
# case forms (genitive "kolmen", partitive "kolmea") and "kaksikymmentä ensimmäinen": inflected
# ordinals and compounds fail closed; the nominative of 0-20, the round tens and hundreds and 1000
# stay supported.
Scenario Outline: Finnish ordinals outside the validated domain are not supported
    Given I use the "FI" number converter
    And I use the variants "<variants>"
    When I attempt to convert the ordinal number <number>
    Then conversion is rejected because no ordinal form is available

Examples:
    | number | variants |
    | 3 | case=genetiivi |
    | 1 | case=partitiivi |
    | 20 | sijamuoto=genetiivi |
    | 21 |  |
    | 101 |  |
    | 2000 |  |

Scenario: Ordinal conversion is supported
    Given I use the "FI" number converter
    Then the converter supports ordinal conversion

Scenario: Fraction connector wording
    Given I use the "FI" number converter
    When I convert the fraction 3/2 through both public fraction APIs
    Then both fraction results are "kolme yli kaksi"

Scenario: Finnish variant metadata
    Given I use the "FI" number converter
    Then the converter exposes exactly these variant dimensions
        | name | local name | values                               |
        | case | sijamuoto  | nominatiivi,partitiivi,genetiivi     |

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "FI" number converter
    Then the converter supports clock-time conversion

# Kielitoimisto: "varttia yli yksi", "puoli kaksi" (half refers to the following hour),
# "varttia vaille kaksi". Quarter-hour 12-hour clock without day-part wording.
Scenario Outline: Idiomatic Finnish clock times
    Given I use the "FI" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | yksi |
    | 01:15 | varttia yli yksi |
    | 01:30 | puoli kaksi |
    | 01:45 | varttia vaille kaksi |
    | 02:00 | kaksi |
    | 12:30 | puoli yksi |
    | 13:30 | puoli kaksi |
    | 01:07 | yksi |
    | 01:08 | varttia yli yksi |

# NTS-12: Finnish has a dedicated ordinal of zero, "nollas" (Wiktionary, citing Kielitoimiston
# sanakirja; kieli.net), configured as an explicit ordinal exception.
Scenario: Ordinal of zero
    Given I use the "FI" number converter
    When I convert the ordinal number 0
    Then the result is "nollas"
