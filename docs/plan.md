# TiltMaze – Kravspecifikation (Lite)

**Fag:** App-programmering – Cross-platform App Development 2026 · **Navn:** Marcus

## 1. App-idé og målgruppe

**TiltMaze** er et labyrintspil. Man styrer en kugle gennem en labyrint ved at vippe telefonen (accelerometer). På
computeren styrer man med piletasterne eller WASD. Før og under en bane kan man trække broer på plads med fingeren eller
musen, så kuglen kan komme over huller. Målet er at nå mål så hurtigt som muligt.

- **Behov:** Et kort, sjovt spil til pauser, der bruger telefonens sensorer og touch på en naturlig måde.
- **Målgruppe:** Børn, unge og voksne, der kan lide hurtige færdighedsspil.
- **Vigtigste funktioner:** 1) Styr kuglen ved at vippe telefonen eller med tasterne. 2) Baner hentes fra serveren og
  virker offline. 3) Highscores gemmes lokalt og online.

## 2. Teknologivalg

| Område | Valg |
|---|---|
| Framework | .NET MAUI med Blazor Hybrid |
| Sprog | C#, Razor/HTML/CSS (banen tegnes som SVG) |
| Platforme | Android (primær) og Windows. iOS fravalgt: kræver Mac |
| Native funktionalitet | Accelerometer, touch-træk, vibration ved sammenstød, SecureStorage. Windows: tastatur og mus |
| Lokal datalagring | SQLite (`sqlite-net-pcl`): baner, highscores og kø af resultater |
| API/backend | Eget ASP.NET Core Web API: baner, highscores og login |
| Cloud-lagring | Egen server (API + database i Docker) |

**Begrundelse:** Jeg kan allerede C#, ASP.NET og Blazor, så tiden går til spillet i stedet for et nyt sprog. App og API
deler sprog og datamodeller. MAUI giver adgang til accelerometer og vibration, og Blazor gør menuer og responsivt layout
nemt. Begrænsning: spillets løkke (ca. 60 billeder i sekundet) skal holdes enkel, så Blazor kan nå at tegne den.

## 3. Funktionelle krav – MoSCoW

| Prioritet | Funktion |
|---|---|
| Must | Kuglen styres med accelerometeret (Android) og piletaster/WASD (Windows) |
| Must | Fysik: kuglen ruller, rammer vægge og falder i huller (banen starter forfra). Mål giver tid på banen |
| Must | Broer trækkes på plads med touch/mus, så kuglen kan krydse huller (begrænset antal pr. bane) |
| Must | Baneliste hentes fra API'et og gemmes i SQLite, så banerne kan spilles offline |
| Must | Bedste tid gemmes lokalt. Resultater sendes til serveren, når der er net (kø) |
| Must | Konto og login (e-mail + kodeord) for at komme på online-highscore |
| Must | Spillet pauser, når appen skjules, og fortsætter, når den vises igen |
| Should | Online top 10 pr. bane |
| Should | Kalibrering: den måde, man holder telefonen på, bliver "flad" |
| Should | Vibration, når kuglen rammer en væg eller falder i et hul |
| Should | Stjerner at samle på banen (1-3 stjerner pr. bane) |
| Could | Baneeditor: lav egne baner og del dem |
| Could | Fingeraftryk som hurtigt login |
| Won't | Multiplayer i realtid, 3D-grafik, køb i appen, iOS |

## 4. Ikke-funktionelle krav

- **Responsivt layout:** Banen har et fast forhold og skaleres til skærmen. Telefon i portræt: tid og pause øverst, banen
  under. Landskab, tablet og PC: banen i midten og et sidepanel med tid, stjerner og menu. Menu og baneliste er én kolonne
  på telefon og et gitter på større skærme. Testes på telefon, tablet-emulator og Windows-vindue i flere størrelser.
- **Sikkerhed:**
  1. *Snyd med highscores:* serveren tjekker, at banen findes, og at tiden er realistisk (fx ikke under en minimumstid).
     Resultater kræver login og er begrænset med rate limit.
  2. *Konti:* kodeord gemmes som hash (ASP.NET Core Identity). Tokens ligger i `SecureStorage`. Man kan kun se og
     slette sine egne data.
  3. *Kommunikation og hemmeligheder:* altid HTTPS, ingen nøgler i koden, og fejlbeskeder viser ingen tekniske detaljer.
  4. *Tilladelser:* kun `INTERNET` og `VIBRATE`. Accelerometeret kræver ingen tilladelse.
- **Offline og datalagring:** Baner og bedste tider ligger i SQLite. Uden net kan man spille alle hentede baner, og nye
  resultater venter i en kø, til der er forbindelse. Serveren gemmer baner, konti og online-highscores.
- **Brugervenlighed:** En første bane, der lærer spillet. Store knapper, tydelig pause, kalibrering og klare fejlbeskeder.
  En brugertest med mindst én person i uge 3.

## 5. Risici

| Risiko | Håndtering |
|---|---|
| Spillet hakker, fordi Blazor tegner for langsomt | Få elementer på banen (SVG), fast tidsskridt i fysikken. Måles tidligt. Plan B: tegn på et `<canvas>` |
| Kuglen "flyver gennem" vægge ved høj fart | Fast tidsskridt og maks-fart på kuglen. Kollision testes med unit tests |
| Accelerometeret virker forskelligt på telefoner og i emulatoren | Kalibrering, tastatur som reserve, test på min egen telefon (OnePlus 6) |

## 6. Tidsplan

| Uge | Fokus | Konkrete opgaver | Resultat |
|---|---|---|---|
| 1 | Plan og fundament | Kravspec og wireframes. MAUI-app og API i Git. Menu, baneliste og navigation. Første bane med kugle og taster/accelerometer | Prototype, hvor kuglen kan styres |
| 2 | Spil og data | Kollision, huller, mål og broer med touch. API til baner og highscores. SQLite og offline-kø. Login. Responsivt layout | Fungerende MVP |
| 3 | Kvalitet og præsentation | Test (3 skærmstørrelser, offline, fejlinput) og brugertest. Fejlrettelser og README. Torsdag: præsentation. Fredag: fremlæggelse | Færdig app og præsentation |
