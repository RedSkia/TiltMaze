# TiltMaze – Kravspecifikation (Lite)

**Fag:** App-programmering – Cross-platform App Development 2026 · **Navn:** Marcus

## 1. App-idé og målgruppe

**TiltMaze** er et labyrintspil set ovenfra (2,5D). Man styrer en kugle gennem en labyrint ved at vippe telefonen
(accelerometer) eller med en joystick på skærmen. På computeren bruger man piletaster/WASD. Hver labyrint laves
procedurelt ud fra et seed, så der altid er en ny bane, og "dagens labyrint" er den samme for alle. Der er huller i
forskellige størrelser: med fart og præcision kan kuglen rulle hen over dem. Man ræser mod sin egen og andres bedste tur,
som vises som "spøgelseskugler".

- **Behov:** Et kort, sjovt færdighedsspil til pauser, der bruger telefonens sensorer og touch på en naturlig måde.
- **Målgruppe:** Unge og voksne, der kan lide hurtige spil og at konkurrere med andre.
- **Vigtigste funktioner:** 1) Styr kuglen ved at vippe, med joystick eller taster. 2) Nye labyrinter fra seed, også
  offline. 3) Top-liste med konto, hvor man ræser mod andres spøgelser.

## 2. Teknologivalg

| Område | Valg |
|---|---|
| Framework | .NET MAUI med Blazor Hybrid |
| Sprog | C#, Razor/HTML/CSS (banen tegnes som SVG) |
| Platforme | Android (primær) og Windows. iOS fravalgt: kræver Mac |
| Native funktionalitet | Accelerometer, touch (joystick), vibration ved fald, SecureStorage, fingeraftryk/Windows Hello. Windows: tastatur og mus |
| Lokal datalagring | SQLite (`sqlite-net-pcl`): bedste tider, spøgelser og kø af resultater |
| API/backend | Eget ASP.NET Core Web API: konti, top-lister og spøgelser pr. sværhedsgrad og seed |
| Cloud-lagring | Egen server (API + database i Docker) |

**Begrundelse:** Jeg kan allerede C#, ASP.NET og Blazor, så tiden går til spillet i stedet for et nyt sprog. App og API
deler sprog og kode: serveren bruger samme labyrint- og fysikkode som appen til at tjekke resultater. MAUI giver adgang til
accelerometer og vibration, og Blazor gør menuer og responsivt layout nemt. Begrænsning: spillets løkke (ca. 60 billeder i
sekundet) skal holdes enkel, så Blazor kan nå at tegne den.

## 3. Funktionelle krav – MoSCoW

| Prioritet | Funktion |
|---|---|
| Must | Kuglen styres med accelerometeret (Android), en joystick der trækkes med fingeren, og piletaster/WASD (Windows) |
| Must | Fysik: kuglen ruller, rammer vægge og falder i huller (tilbage til start). Med nok fart ruller den hen over hullet |
| Must | Labyrinter laves procedurelt ud fra sværhedsgrad og seed (DFS + sløjfer). Dagens labyrint har datoen som seed |
| Must | Bedste tider og spøgelser gemmes lokalt i SQLite, så alt kan spilles offline |
| Must | Konto og login (brugernavn + kodeord, ingen e-mail; brugernavnet er unikt og står på top-listen). Resultater sendes til serveren, når der er net (kø) |
| Must | Top-liste pr. labyrint (liste, der opdateres) |
| Must | Pause, når appen skjules. Dialogboks "Forlad løbet?" ved Menu midt i et spil |
| Should | Ræs mod spøgelser: egen bedste tur og top 3 fra serveren vises som gennemsigtige kugler |
| Should | Anti-snyd: serveren afspiller spøgelset med samme fysik og tjekker, at tiden passer |
| Should | Kamera, der følger kuglen på små skærme, med minikort |
| Should | Vibration ved fald og kalibrering af "flad" telefon |
| Could | Indtast et seed for at dele en labyrint med en ven |
| Could | Fingeraftryk/Windows Hello låser det gemte login op (biometri) |
| Won't | Multiplayer i realtid, 3D-grafik, køb i appen, iOS |

## 4. Ikke-funktionelle krav

- **Responsivt layout:** Telefon i portræt: HUD øverst, banen under, og kameraet følger kuglen. Landskab, tablet og PC:
  hele banen i midten og et sidepanel med tid, top-liste og menu. Startsiden er én kolonne på telefon og et gitter på
  større skærme. Testes på telefon, tablet-emulator og Windows-vindue i flere størrelser.
- **Sikkerhed:**
  1. *Snyd med highscores:* serveren genberegner turen ud fra spøgelset (samme fysikkode) og afviser tider, der ikke
     passer (også størrelse, sværhedsgrad og seed). Resultater kræver login og er begrænset med rate limit.
  2. *Konti:* brugernavn + kodeord, ingen e-mail. Kodeord gemmes som hash (ASP.NET Core Identity), mindst 8 tegn, og
     5 forkerte forsøg låser kontoen i 5 minutter. Login og navne-tjek har rate limit pr. IP, og et forkert login giver
     altid samme svar. Refresh-token ligger kun i `SecureStorage`. Fingeraftryk låser kun login op på telefonen; ingen
     biometri sendes. Slettes kontoen, slettes tider og spøgelser med.
  3. *Kommunikation og hemmeligheder:* kun HTTPS (HSTS), ingen CORS, ingen nøgler i koden (token-nøgler i Docker-volumen),
     og fejlbeskeder viser ingen tekniske detaljer.
  4. *Tilladelser:* kun `INTERNET`, `VIBRATE` og `USE_BIOMETRIC`. Accelerometeret kræver ingen tilladelse.
- **Offline og datalagring:** Labyrinter laves i appen, så alt kan spilles uden net. Tider og spøgelser ligger i SQLite,
  og nye resultater venter i en kø, til der er forbindelse. Serveren gemmer konti, resultater og spøgelser.
- **Brugervenlighed:** Store knapper, tydelig pause og nedtælling før start, kalibrering og klare fejlbeskeder. En
  brugertest med mindst én person i uge 3.

## 5. Risici

| Risiko | Håndtering |
|---|---|
| Spillet hakker, fordi Blazor tegner for langsomt | Få elementer (vægge som én SVG-sti), fast tidsskridt i fysikken. Plan B: tegn på et `<canvas>` |
| Kuglen "flyver gennem" vægge ved høj fart | Fast tidsskridt og maks-fart. Kollision og "alle labyrinter kan løses" testes med unit tests |
| Accelerometeret virker forskelligt på telefoner | Kalibrering, joystick og tastatur som reserve, test på min egen telefon (OnePlus 6) |

## 6. Tidsplan

| Uge | Fokus | Konkrete opgaver | Resultat |
|---|---|---|---|
| 1 | Plan og fundament | Kravspec og wireframes. MAUI-app i Git. Labyrint-generator, fysik og styring. Startside og spilskærm | Spilbar prototype |
| 2 | Data og online | API med konti, top-liste og spøgelser. SQLite og offline-kø. Joystick, dialog, kamera og spøgelser. Responsivt layout | Fungerende MVP |
| 3 | Kvalitet og præsentation | Test (3 skærmstørrelser, offline, fejlinput) og brugertest. Fejlrettelser og README. Torsdag: præsentation. Fredag: fremlæggelse | Færdig app og præsentation |
