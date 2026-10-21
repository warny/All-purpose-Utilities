# NTS-42 — deliberately unsupported fraction readings

NTS-42 collected the Ukrainian, Czech and Slovak fraction readings that NTS-40/41 left closed. Every reading a source
establishes has been implemented (NTS-42 F, NTS-44, NTS-45, NTS-46, NTS-47). NTS-42 was closed by NTS-47 (2026-10-21).
The readings below stay closed **by decision**: the linguistic evidence does not determine one canonical output, and
`Utils.NumberToString` does not invent one.

This file is the permanent reference for these closures. The full source records are in
`docs/NTS-08-linguistic-sources.md` (sections NTS-40/41, NTS-45, NTS-47) and in `DONE-2026-10-16(1).md`,
`DONE-2026-10-19(1).md`, `DONE-2026-10-21.md` and `DONE-2026-10-21(2).md`.

| Closure | Languages | Status |
|---|---|---|
| D — slash reading under an external case | CS, SK | deliberately unsupported — grammatical construction not established |
| E/CS — zero numerator in an oblique case | CS | deliberately unsupported — attested variation, no canonical form |

## D — Czech and Slovak slash reading under an external case

**Scope.** A fraction whose denominator has no fraction noun is read as a mathematical operation: CS *X lomeno Y*, SK
*X lomené Y*, with Y in the instrumental (*dvě lomeno dvaceti třemi*, *jedna lomené sto štyridsiatimi dvoma*, *jedna
lomené jedným biliónom*). This includes a Slovak zero over such a denominator (*nula lomené dvadsiatimi troma*).
Fractions with a fraction noun (*dvě třetiny*, *dve tretiny*) are not concerned: they read in every case (NTS-40).

**Attested.** The internal structure only, always as a phrase on its own:
- CS: SSJČ, *lomiti*: *dvě lomeno třemi* ("divided by"); E. Prandstetter, "Čtení matematických značek a výrazů", *Naše
  řeč* 22 (1938); current teaching material (*a/x — a lomeno x*, ČVUT FJFI, Mathematics I, lesson 9, 2024).
- SK: P. Ondrus, *Slovenská reč* 1968/2 (*jedna lomené dvoma*); Š. Peciar, "O používaní zlomkových výrazov", *Slovenská
  reč* 41 (1976) (*1/3 = jedna lomené troma*); *Slovenská reč* 43 (1978)/6 (*jeden lomené sto štyridsiatimi dvoma*).

**Searched for an external case.** IJP and the ÚJČ poradna database (no public answer on *lomeno*); Naše řeč;
Slovenská reč; Kultúra slova; school and university mathematics material; J. Sláma, "Operátor lomeno, koordinátor
lomeno, adordinátor: K funkcím slova lomeno", *Naše řeč* 105 (2022), 49–55, the one syntactic study of *lomeno*. It
distinguishes the name of the sign, the mathematical operator, the coordinator ('or') and the adordinator, and gives no
rule for the case of the first operand when the whole operation is governed by a case. Only its abstract could be read
from the project environment; the reviewer of NTS-47 read the full text, which is indexed on ResearchGate, and confirmed
this.

**Why no output.** When the phrase is governed by a preposition or a case (*s …*, *k …*), two analyses stay
grammatically possible and no source shows either:
- X takes the external case: CS *s dvěma lomeno třemi*, SK *s dvoma lomené troma*;
- X keeps its absolute arithmetic form: CS *s dvě lomeno třemi*, SK *s dva lomené troma*.

The uncertainty is **syntactic**, not morphological: both forms of X exist. Neither the existence of the instrumental of
*dva* nor the absolute form of the isolated reading decides how an arithmetic term behaves inside a sentence.
**Decision:** no output is invented.

## E/CS — Czech zero as a fraction numerator in an oblique case

**Scope.** `0/n` in the genitive, dative, accusative, locative or instrumental (*nula třetinám*? *nule třetin*? *nulou
třetinami*?). The nominative *nula lomeno třemi* is supported and unchanged.

**What the sources establish.**
- *nula* as a noun declines (pattern *žena*: *nuly, nule, nulu, nulou*; IJP, SSČ, SSJČ; the ÚJČ poradna calls it
  "obecné podstatné jméno").
- *nula* as a cardinal is attested: SSJČ, *nula*, sense 2 "čísl. označuje počet žádných jednotek"; CzechEncy, "základní
  číslovka" (*teplota nula stupňů*).
- **NovaMorf** (K. Osolsobě, J. Hlaváčová, V. Petkevič, J. Šimandl, M. Svášek, *NovaMorf — nová morfologie pro
  značkování korpusů češtiny*, 2018; concept, § 3.1.1, <https://sites.google.com/site/koncepcenovamorf/08-04>) treats
  *nula* as a cardinal in its quantitative uses and attests **both behaviours in the same context**: *záruka na
  nula/nulu vadných bodů*; its morphological model gives *nula* "kromě skloňovaných tvarů sg. pro všechny pády i
  nesklonný tvar (*do nula celá sedmi*)". Further examples: *mají nula procent hlasů*, *dělení nulou ~ dělení … dvěma*.
- Usage also has the indeclinable quantifier (*při nula stupních*).

**Why no output.** The academic description recognizes real variation — a declined and an indeclinable cardinal *nula*
— without a canonical preference, and no source gives a complete fraction paradigm deciding between, for example,
*nule třetin* and *nula třetinám*, nor the case of the fraction noun after a declined *nula*. The Slovak rule
(indeclinable *nula*, the noun in the case: Peciar 1958, Kultúra slova 2002) is codified for Slovak only. A single-output
API would have to pick one variant arbitrarily, which this project does not do. **Decision:** no output is selected.

## API behavior

These combinations intentionally return no language-specific reading, so `NumberToStringConverter.ConvertFraction`
throws `NotSupportedException`. This is a documented linguistic boundary, not an engine limitation or a pending
implementation. The configurations say so next to their language specifics (`NumberConvertionConfiguration.CS.xml`,
`NumberConvertionConfiguration.SK.xml`), and `NumberToStringNts47Tests` pins it
(`D_SlashExternalCase_DeliberatelyUnsupported`, `E_CzechZeroOblique_DeliberatelyUnsupported`).

Reopening either closure requires, together: a new source that decides the construction, an update of this file, and an
explicit change of those tests.
