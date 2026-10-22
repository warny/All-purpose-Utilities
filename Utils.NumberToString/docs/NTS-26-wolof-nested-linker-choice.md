# NTS-26 — Wolof genitive linker inside nested multipliers

NTS-26 was opened by NTS-19/NTS-21 and re-audited on 2026-10-11. It was closed on 2026-10-22, together with NTS-33,
**by documentation**: the variation is attested, the current output is a defensible library convention, and no
behaviour changed. This file is the permanent reference for that decision; read it before reopening the ticket.

## 1. Question

A Wolof multiplier of *téeméer* (hundred) or *junni* (thousand) takes the genitive linker *-i* on its last element
(*ñaari téeméer* 200, *ñaari junni* 2000). When such a hundreds phrase is itself the multiplier of a higher scale, two
linkers can appear in the chain: one inside the hundreds phrase, before *téeméer*, and one after *téeméer*, before the
higher scale noun. The question is whether the inner one belongs in the default output:

| Value | With the inner linker (current) | Without the inner linker |
|---|---|---|
| 200 000 | *ñaari téeméeri junni* | *ñaar téeméeri junni* |
| 200 000 000 | *ñaari téeméeri milyoŋ* | *ñaar téeméeri milyoŋ* |
| 900 000 000 | *juróom ñenti téeméeri milyoŋ* | *juróom ñent téeméeri milyoŋ* |

## 2. Current library form

`NumberConvertionConfiguration.WO.xml` writes the multiplier of *téeméer* with *-i* in every position (the level-3
`Digit` strings, NTS-19) and adds the outer *-i* on the multiplier of *junni* (`Replacement`, NTS-19) and of *milyoŋ*,
*milyaar* and the higher scales (`groupWithScale` trigger, NTS-21). A nested hundreds multiplier therefore carries
both linkers: *ñaari téeméeri milyoŋ*. The forms are pinned by `NumberToStringNts21Tests` (including
`Wolof_NestedLinker_Nts26_RetainedByDecision`) and `Wolof.feature`.

## 3. Evidence with the linker

- S. Guérin (CNRS / LLACAN) analyses the multiples of hundreds and thousands as genitive constructions and gives the
  nested chain with a linker at each level: *ñaar-i téeméer-i dërëm*.
- Pedagogical material attests the linker before *téeméer* and *junni* in simple multipliers: *juróom ñaari téeméer*,
  *ñenti téeméer*, *ñaari junni*.

## 4. Evidence without the linker

- F. Ngom et al., *Wolof Ajami Reader*, Boston University, 2025, p. 192: *juróom ñent téeméeri milyoŋ* and *juróom
  ñaar téeméeri milyoŋ*, without the linker before *téeméer*.
- S. Gaye, 1980, records forms without this linker and treats *-i* as optional in some contexts with *ñaar*.
- Kosogorova, 2023, records further forms without it.

## 5. Decree and general genitive rule

Decree 2005-992 on Wolof orthography establishes the attached genitive linker *-u/-i* in general. It does not regulate
the chain of linkers inside nested numeral multipliers, and therefore does not settle the question either way.

## 6. Decision

The current form with the inner *-i* (*ñaari téeméeri milyoŋ*) is **retained as the library default**. It is not a
known defect:

- it is linguistically attested (Guérin's genitive analysis and nested example);
- no source shows it to be wrong;
- the form without the inner linker is attested as well (Ajami Reader, Gaye, Kosogorova), so the evidence shows
  variation, not a single norm;
- changing the default silently would rewrite many byte-stable Wolof cardinals and ordinals, below and above a
  million.

No variant dimension is introduced: the sources do not describe what conditions the choice, so an option could not be
documented honestly.

## 7. Reopening criteria

Reopen NTS-26 only with a source or corpus study that characterizes the variation, for example:

- a dialectal distribution of the two patterns;
- a register distinction (written standard, school usage, oral usage);
- a syntactic environment that selects one of them (e.g. *ñaar* versus the other multipliers, nesting depth);
- or free variation documented well enough to justify a variant dimension whose values can be described.

Any change must then keep the current default unless the source establishes the other form as the norm, and must be
checked with a before/after dump of every Wolof converter.
