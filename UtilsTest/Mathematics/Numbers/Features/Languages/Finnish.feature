@NumberToString @FI
Feature: Finnish number conversion

# NTS-30E, Kielitoimiston ohjepankki "Luvut ja numerot: numeroiden ryhmittely": the part below a thousand is one
# word and so is the part counting the thousands (tuhat viisisataa, neljätoistatuhatta kuusisataa). The former
# rows "kaksikymmentä yksi", "sata yksi" and "kaksi tuhat" were wrong.
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
    | 21 | kaksikymmentäyksi |
    | 100 | sata |
    | 200 | kaksisataa |
    | 101 | satayksi |

Scenario Outline: Thousands
    Given I use the "FI" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | tuhat |
    | 2000 | kaksituhatta |

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

# NTS-30E, Kielitoimiston ohjepankki "Luvut ja numerot: peruslukujen taivuttaminen": every component of an
# inflected compound inflects, in one word (21:tä kahtakymmentäyhtä, 21:n kahdenkymmenenyhden). The former rows
# "kaksikymmentä yhtä" and "kahdenkymmenen yhden" (spaces, uninflected multiplier) were wrong.
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
    | 21 | sijamuoto=partitiivi | kahtakymmentäyhtä |
    | 22 | sijamuoto=partitiivi | kahtakymmentäkahta |
    | 201 | sijamuoto=partitiivi | kahtasataayhtä |

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
    | 211 | sijamuoto=partitiivi | kahtasataayhtätoista |

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
    | 21 | sijamuoto=genetiivi | kahdenkymmenenyhden |
    | 22 | sijamuoto=genetiivi | kahdenkymmenenkahden |
    | 200 | sijamuoto=genetiivi | kahdensadan |
    | 300 | sijamuoto=genetiivi | kolmensadan |
    | 900 | sijamuoto=genetiivi | yhdeksänsadan |
    | 221 | sijamuoto=genetiivi | kahdensadankahdenkymmenenyhden |
    | 222 | sijamuoto=genetiivi | kahdensadankahdenkymmenenkahden |

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
# NTS-37: the nominative is supported up to 1 999 999; a multiplied million (2 000 000) and the milliard and
# above have no sourced ordinal and fail closed.
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
    | 21 | case=genetiivi |
    | 2000000 |  |
    | 1000000000 |  |

# NTS-28: Kielitoimiston ohjepankki, "Luvut ja numerot: järjestysluvut": the ordinal marker recurs in every
# part of a multi-part ordinal, written as one word (viideskymmenesviides); ensimmäinen and toinen are more
# common than yhdes and kahdes as the last part (kahdeskymmenesensimmäinen, kolmaskymmenestoinen); in 11-19
# only the first part is ordinal (yhdestoista). Nominative only: the inflected compounds (genitive
# kolmannenkymmenennenkuudennen) need paradigms of every component that the consulted sources give for 1,
# 3, 5, 17, 20, 100 and 1000 only, so they still fail closed. NTS-37 ("pitkät luvut ääneen luettaessa":
# 3 760. kolmastuhannes seitsemässadaskuudeskymmenes): the groups of an ordinal are separated by a space, as
# those of the cardinal; the ordinals from 2000 and of the millions are pinned by NumberToStringNts37Tests.
Scenario Outline: NTS-28 Finnish compound ordinals in the nominative
    Given I use the "FI" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 21 | kahdeskymmenesensimmäinen |
    | 22 | kahdeskymmenestoinen |
    | 32 | kolmaskymmenestoinen |
    | 55 | viideskymmenesviides |
    | 101 | sadasensimmäinen |
    | 111 | sadasyhdestoista |
    | 121 | sadaskahdeskymmenesensimmäinen |
    | 999 | yhdeksässadasyhdeksäskymmenesyhdeksäs |
    | 1001 | tuhannes ensimmäinen |
    | 1999 | tuhannes yhdeksässadasyhdeksäskymmenesyhdeksäs |

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
