# NTS-08 linguistic source record

This record documents the configurations enabled in the 2026-09-23 NTS-08 slice. It is intentionally narrower than the open audit: languages not listed here remain unsupported unless their existing configuration says otherwise.

| Configuration | Cultures | BaseOn | Ordinals | Implementation | ClockTime | Step / cycle | Variants | Sources | Limitation |
|---|---|---|---|---|---|---|---|---|---|
| EN | EN and existing aliases | SCALE-SHORT | Existing | Declarative | Yes | 5 / 12 | none | Cambridge Dictionary, “Telling the time”; British Council LearnEnglish, “How to tell the time” | No day-part wording |
| EN-GB | EN-GB, EN-uk | EN | Inherited | Declarative | Inherited | 5 / 12 | none | Same as EN | Date format remains the only override |
| FR | FR, FR-fr, FR-ca | SCALE-LONG | Existing | Declarative | Existing | 5 / 24 | `gender=feminin` through time units | Office québécois de la langue française, Banque de dépannage linguistique, “Heure”; Académie française, “Dire, ne pas dire: Il est huit heures” | Existing project convention retained |
| FR-be | FR-be | FR | Inherited | Declarative | Inherited | 5 / 24 | inherited gender | Fédération Wallonie-Bruxelles, *Guide de rédaction*; Académie royale de langue et de littérature françaises de Belgique, dictionary resources | Belgian `quatre-vingts`, not Swiss `huitante` |
| FR-ch | FR-ch | FR | Inherited | Declarative | Inherited | 5 / 24 | inherited gender | Dictionnaire suisse romand; TERMDAT (Swiss Federal Chancellery) | Project convention selects `huitante` |
| CA | CA, ca-ES | none | Existing | Declarative | Yes | 15 / 12 | `gender=femení` | Institut d'Estudis Catalans, *Gramàtica essencial de la llengua catalana*, §31.2; Generalitat de Catalunya, Optimot, “expressió de les hores” | Traditional quarter system only |
| CA-valencia | ca-ES-valencia | CA | Inherited | Declarative | Yes, replaces parent | 5 / 12 | `gender=femení` for `1` only (`una`); invariable `dos` for `2-12`, per AVL priority; display-hour ranges | Acadèmia Valenciana de la Llengua, *Gramàtica normativa valenciana*, §32.2; AVL, *Diccionari normatiu valencià*, entries `quart` and `hora`; AVL glossary, entry `dos` (“l'Acadèmia Valenciana de la Llengua accepta tant l'ús variable com l'invariable d'este numeral [...] però dona prioritat a la forma invariable *dos*”) | No day-part wording; unlike CA, does not gender-agree `dos`/`dues` with `hores` |
| ID | ID, ID-ID | SCALE-SHORT | Yes | productive `long` ordinal plugin (`pertama`, otherwise `ke-` + cardinal) | Yes | 5 / 12 | none | Badan Pengembangan dan Pembinaan Bahasa, KBBI entries `pertama`, `lewat`, `setengah`, `kurang`; *Tata Bahasa Baku Bahasa Indonesia* | No day-part wording |
| MS | MS, MS-MY | ID | Yes | productive `long` Malay ordinal plugin over `lapan` cardinals | Yes, replaces parent | 5 / 12 | none | Dewan Bahasa dan Pustaka, PRPM entries `lapan`, `pukul`, `suku`, `setengah`, `bilion`, `trilion`, `kuadrilion`, `kuintilion`; DBP, *Tatabahasa Dewan* | Uses Malaysian dynamic `-lion` scales and the neutral `pukul` convention; no day-part wording |

## Source links

- Cambridge Dictionary: <https://dictionary.cambridge.org/grammar/british-grammar/time>
- British Council LearnEnglish: <https://learnenglish.britishcouncil.org/grammar/a1-a2-grammar/prepositions-time-at-in-on>
- Office québécois de la langue française: <https://vitrinelinguistique.oqlf.gouv.qc.ca/>
- Académie française: <https://www.academie-francaise.fr/>
- Fédération Wallonie-Bruxelles language service: <https://www.languefrancaise.cfwb.be/>
- TERMDAT: <https://www.termdat.ch/>
- Institut d'Estudis Catalans grammar: <https://geiec.iec.cat/>
- Optimot: <https://aplicacions.llengua.gencat.cat/llc/AppJava/index.html>
- Acadèmia Valenciana de la Llengua publications: <https://www.avl.gva.es/>
- AVL glossary, entry `dos`: <https://www.avl.gva.es/glossari/dos/>
- KBBI: <https://kbbi.kemdikbud.go.id/>
- Dewan Bahasa dan Pustaka PRPM: <https://prpm.dbp.gov.my/>
