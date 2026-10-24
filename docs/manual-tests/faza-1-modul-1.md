# Faza 1, modulul 1: Ceas, dată și calendar (plus corecturile din 0.11)

Pornești din terminal: `dotnet run --project src\Hasi.App -- --diagnostic`. Vocea din build este din nou „Paul,
RHVoice direct” (copia de test stă acum în `%LOCALAPPDATA%\HASI.Dev\rhvoice`). Numerele se aud în cuvinte.
[sunet] înseamnă sunetul scurt de la capătul listei.

## A. Corecturile din 0.11

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| C1-01 | (pornire) | Vocea este „Paul, RHVoice direct”, nu „Paul, prin SAPI”. În jurnal, rândul `RHVOICE` arată `HASI.Dev\rhvoice`. |
| C1-02 | Despre HASI, Enter; săgeata jos de 3 ori | „Sinteza vocală RHVoice versiunea 1.18.4, de Olga Yakovleva și colaboratorii. … Sursa exactă este indicată în fișierul de licențe, 4 din 11.” **Fără commit în voce.** Pe ecran apare și „(commit fa9dd196)”. |
| C1-03 | F2 pe același rând | același text, tot fără commit |

## B. Meniul principal

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| C1-04 | (pornire) | „Bun venit în HASI. Meniu principal. 5 opțiuni. Selectat: Ceas și calendar, 1 din 5.” |
| C1-05 | Enter; F1 | „Ceas și calendar. 4 opțiuni. Selectat: Ora, 1 din 4.”; ajutorul se termină cu „Oriunde în HASI, Control T spune ora.” |

## C. Ora și data

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| C1-06 | Enter pe Ora | „Este ora …”: aceeași oră ca ceasul din bara de activități. La o oră fixă: „Este ora cincisprezece.”, fără „fix”. |
| C1-07 | Săgeata jos, Enter pe Data | „Astăzi este sâmbătă, douăzeci și patru octombrie două mii douăzeci și șase.” (ziua de azi). Rămâi în meniu. |
| C1-08 | Mâine, 25 octombrie, după ora 4 dimineața (după trecerea la ora de iarnă): Enter pe Ora | ora nouă, de iarnă, aceeași cu ceasul Windows |

## D. Calendar

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| C1-09 | Săgeata jos, Enter pe Calendar | „Calendar. Sâmbătă, douăzeci și patru octombrie două mii douăzeci și șase, astăzi.” |
| C1-10 | Săgeata jos; săgeata sus | „Duminică, douăzeci și cinci octombrie …”; înapoi la azi, cu „astăzi” |
| C1-11 | Page Down; apoi End; apoi săgeata jos | „Miercuri, douăzeci și cinci noiembrie …”; „Luni, treizeci noiembrie … Sărbătoare legală: Sfântul Apostol Andrei, Ocrotitorul României.”; „Marți, întâi decembrie … Sărbătoare legală: Ziua Națională a României.” |
| C1-12 | Săgeata dreapta, apoi săgeata stânga | luna următoare (1 ianuarie 2027, „Anul Nou, prima zi”), apoi înapoi la 1 decembrie. Stânga și dreapta merg și pe laptop, fără Fn. |
| C1-13 | Home; Control + săgeata jos; Control + săgeata sus | prima zi a lunii; ultima; din nou prima |
| C1-14 | Page Up de 7 ori (până la 1 mai 2026); apoi End; apoi săgeata jos | „Vineri, întâi mai … Sărbătoare legală: Ziua Muncii.”; „Duminică, treizeci și unu mai … Sărbătoare legală: Rusaliile ortodoxe, prima zi.”; „Luni, întâi iunie … Sărbători legale: Ziua Copilului și Rusaliile ortodoxe, a doua zi.” |
| C1-15 | F1, apoi Escape | ajutorul cu tastele, terminat cu „… începând cu anul două mii șaptesprezece.”; apoi „Ceas și calendar. Selectat: Calendar, 3 din 4.” |

## E. Sărbători legale

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| C1-16 | Săgeata jos, Enter pe Sărbători legale | „Sărbători legale două mii douăzeci și șase. șaisprezece zile de sărbătoare. Selectat: Luni, treizeci noiembrie: Sfântul Apostol Andrei, Ocrotitorul României, treisprezece din șaisprezece.” |
| C1-17 | Săgeata sus de 2 ori | „Sâmbătă, cincisprezece august: Adormirea Maicii Domnului, …”; „Luni, întâi iunie: Ziua Copilului și Rusaliile ortodoxe, a doua zi, …” |
| C1-18 | Home, apoi săgeata jos de 6 ori | 1 ianuarie, 2 ianuarie, 6 ianuarie („Botezul Domnului, Boboteaza”), 7 ianuarie („Soborul Sfântului Ioan Botezătorul”), 24 ianuarie, „Vineri, zece aprilie: Vinerea Mare ortodoxă”, „Duminică, douăsprezece aprilie: Paștele ortodox, prima zi” |
| C1-19 | Săgeata dreapta | „Sărbători legale două mii douăzeci și șapte. șaptesprezece zile de sărbătoare. Selectat: Vineri, întâi ianuarie: Anul Nou, prima zi, unu din șaptesprezece.” |
| C1-20 | Săgeata stânga de 10 ori (până la 2017), apoi încă o dată | ultimul an: „Sărbători legale două mii șaptesprezece …”; apoi [sunet] „HASI cunoaște sărbătorile legale din anul două mii șaptesprezece până în anul două mii nouăzeci și nouă.” |
| C1-21 | F1; Control + U; Escape | ajutorul spune că Paștele, Vinerea Mare și Rusaliile sunt cele ortodoxe și ce se întâmplă cu celelalte culte; „Sunteți în: Meniu principal, Ceas și calendar, Sărbători legale două mii șaptesprezece.”; înapoi în Ceas și calendar |

## F. Control + T și ora fixă

| # | Ce apăs | Ce trebuie să aud |
|---|---|---|
| C1-22 | Control + T în meniul principal, în Setări, în Test pronunție (după ce scrii ceva), în întrebarea de la Alt + F4 | de fiecare dată doar „Este ora …”; nu se schimbă ecranul, selecția sau textul scris; Alt + F4 rămâne neconfirmat (Escape îl anulează) |
| C1-23 | Învață tastele: Control + T, Page Down, Page Up | „Control plus litera T: spune ora.”, „Page Down: trece la luna următoare în calendar.”, „Page Up: trece la luna anterioară în calendar.”; nu se execută nimic |
| C1-24 | Setări: „Ora fixă rostită”, Enter | „Ora fixă rostită. 2 opțiuni. Selectat: Nu, actual, 2 din 2.” (oprită implicit). F1 explică setarea. |
| C1-25 | Alegi „Da”; aștepți următoarea oră fixă, cu HASI deschis | „Ora fixă rostită: Da. …”; la ora fixă, o singură dată „Este ora …” (de exemplu „Este ora șaisprezece.”), după mesajul care se aude atunci, fără să se schimbe ecranul. Nu schimba ceasul Windows pentru test. |
| C1-26 | Pui la loc „Nu” | la următoarea oră fixă nu se mai aude nimic; Control + T merge în continuare |

## Lista de regresie

`docs/manual-tests/regresie.md` (fostul `regresie-faza-0.md`): pașii 1–5, 14, 28 și 29 s-au schimbat pentru meniul
principal cu 5 opțiuni, Setări cu 12 opțiuni și rândul RHVoice din Despre HASI. Pașii noi 41–45 (secțiunea J) sunt
pentru acest modul.
