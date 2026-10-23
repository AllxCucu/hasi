# ADR 0005 – Noun genders from an open morphological dictionary

- Status: **Proposed** – waiting for the project owner's approval before any data is used
- Date: 2026-10-23

## Context

The Romanian normalizer must know the gender of the counted noun: "doi lei" / "două ore", "un an" /
"o oră", and "de" from 20 up. Today a small hand-written lexicon (~150 words) plus plural-ending
heuristics decide it. The owner asked for a morphological dictionary with a license compatible with
GPL-3.0-or-later, checked before use.

## Candidates (checked 2026-10-23)

| Source | License | Gender data | Verdict |
|---|---|---|---|
| **MULTEXT-East free lexicons 4.0, Romanian** (`wfl-ro.txt`, CLARIN.SI handle 11356/1041) | **CC BY-SA 4.0** | Yes, in the MSD tag of every form. Inspected: 120,228 noun forms, 18,705 noun lemmas. Neuter is encoded the way agreement works: masculine in the singular (`dosar Ncms-n`), feminine in the plural (`dosare Ncfp-n`), which is exactly what number agreement needs. | **Recommended** |
| Wiktionary via kaikki.org (wiktextract) | CC BY-SA 4.0 + GFDL (Wiktionary text license) | Gender tags per entry | Good **supplement** for newer words; larger download, needs filtering |
| dexonline | Definitions: GPL-2.0-or-later; source: AGPL-3.0-or-later; **inflection data: not covered** by the license page, and the site forbids scraping without written consent | Yes (full paradigms) | **Not usable** without written permission from dexonline; we could ask them |
| UniMorph `ron` | **CC BY-SA 3.0** | Yes | **Not compatible**: only CC BY-SA 4.0 is declared one-way compatible with GPLv3 |

CC BY-SA 4.0 → GPL-3.0: Creative Commons declared GPLv3 a compatible license for BY-SA 4.0 (2015), so
material adapted from CC BY-SA 4.0 may be distributed under GPLv3, with attribution.

## Proposal

1. A build-time tool (`tools/`, not shipped) reads `wfl-ro.txt` and writes a compact table
   `form → agreement gender` (m/f) for noun forms only (~120,000 lines, about 2 MB, less compressed),
   shipped as `Data/substantive.txt` with the attribution and license header.
2. While importing, cedilla letters (ş ţ, used by the older lexicon) are converted to comma-below (ș ț).
3. Attribution in "Despre HASI" and THIRD-PARTY-NOTICES: MULTEXT-East (T. Erjavec et al.), Romanian
   lexicon (D. Tufiș et al., RACAI), CC BY-SA 4.0.
4. A small **user override file** `Data/genuri.txt` (same `scris => rostit`-style format as the other
   tables, for example `minut => n`) is checked first, so the tester can fix a word without waiting for me.

## Fallback for unknown nouns (in this order)

1. User override file, then the dictionary.
2. Plural endings that are reliable: `-uri` (neuter → "două"), `-ări`, `-ții`, `-uni`, `-ăți` (feminine).
3. Singular endings: `-ă`, `-ie`, `-ea` → feminine ("o"); otherwise masculine/neuter singular ("un").
4. **"de" does not depend on gender**: from 20 up it is inserted whenever the next word is a noun-like word
   (lower-case, not a function word), even if the gender is unknown.
5. If the gender is still unknown for 1, 2, 12, 22… (the only numbers where it matters), HASI uses the
   masculine form and, in diagnostic mode, logs `LEXICON? <word>` so the tester can report it.

## Decision

Pending the owner's approval of the MULTEXT-East data (and, optionally, of asking dexonline for permission).
