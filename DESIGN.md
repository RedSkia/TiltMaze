# TiltMaze – Design og opbygning

Et overblik over hele appen på ét sted: hvad den gør, hvordan den er bygget, hvordan den ser ud, og hvorfor vi har valgt, som vi har. Skrevet så man kan læse den uden at kende koden.

**Indhold:** [1. Kort sagt](#1-kort-sagt) · [2. Det store billede](#2-det-store-billede) · [3. Hvordan appen er native](#3-hvordan-appen-er-native) · [4. Lagene i koden](#4-lagene-i-koden) · [5. Spillet](#5-spillet) · [6. Belønninger og butik](#6-belønninger-og-butik) · [7. Data på telefonen](#7-data-på-telefonen) · [8. Backend: Supabase](#8-backend-supabase) · [9. Anti-snyd](#9-anti-snyd) · [10. Synkronisering](#10-synkronisering) · [11. Sikkerhed](#11-sikkerhed) · [12. Udseende](#12-udseende-neon-arcade) · [13. Skærmene](#13-skærmene) · [14. Tilgængelighed og layout](#14-tilgængelighed-og-layout) · [15. Test](#15-test) · [16. Valg og begrundelser](#16-valg-og-begrundelser)

---

## 1. Kort sagt

TiltMaze er et labyrintspil. Man styrer en kugle ved at **vippe telefonen** eller **trække en finger/mus** hvor som helst på banen, undgår **huller** og når **målet** så hurtigt som muligt. Man kan samle **mønter**, købe udseende i en **butik**, og konkurrere på en **top-liste** med et **spøgelse** af sine egne og andres bedste ture.

| | |
|---|---|
| Teknik | .NET 10, .NET MAUI Blazor Hybrid (Android og Windows) |
| Backend | Supabase (Postgres, Auth, Edge Function). Ingen egen server |
| Sprog | Kode og UI på engelsk, dokumenter og kommentarer på dansk |
| Princip | Al matematik (labyrint og fysik) ligger i `TiltMaze.Core` og er testet. Siderne tegner og styrer kun |
| Offline | Spillet virker helt uden net. Konto og top-liste kræver net |

---

## 2. Det store billede

```
┌──────────────── Telefon / PC ─────────────────┐          ┌─────────────── Supabase ───────────────┐
│  TiltMaze.App (.NET MAUI Blazor Hybrid)       │          │  Auth        konti, login, tokens      │
│                                               │  HTTPS   │  Postgres    profiles, runs (RLS)      │
│  Blazor-sider  (HTML/CSS + SVG)               │ ───────► │  RPC'er      leaderboard, ghosts, ...  │
│       │                                       │          │  Edge Fn     submit-run (anti-snyd)    │
│  Services  (Scores, Wallet, Api, Auth, ...)   │ ◄─────── │                                        │
│       │                                       │          └────────────────────────────────────────┘
│  TiltMaze.Core  (labyrint, fysik, spøgelse)   │
│                                               │          ┌── Google ──┐
│  SQLite på enheden: tider, spøgelser, kø,     │ ───────► │ OAuth login │  (valgfrit, via Supabase)
│  mønter og køb                                │          └────────────┘
└───────────────────────────────────────────────┘
```

Tre pointer:
1. **Spillet er lokalt.** Labyrint, fysik, tid, mønter og butik kører og gemmes på enheden.
2. **Serveren er kun til det, der skal deles:** konti, top-lister og andres spøgelser.
3. **Serveren stoler ikke på appen.** Den bygger selv samme labyrint, afspiller turen og tjekker, at den er mulig (se [Anti-snyd](#9-anti-snyd)).

---

## 3. Hvordan appen er native

Blazor Hybrid: MAUI starter en almindelig Android- eller Windows-app med én skærm, `MainPage.xaml`, som indeholder en `BlazorWebView`. Den viser vores `.razor`-sider (HTML, CSS og SVG). Koden bag siderne kører som almindelig .NET **inde i appen** og kan derfor bruge telefonens egne funktioner direkte: accelerometer (vip), vibration, SecureStorage og fingeraftryk/Windows Hello.

```
Android: MainApplication.cs ─┐
                             ├─► MauiProgram.cs ─► App.xaml ─► MainPage.xaml ─► BlazorWebView ─► Routes.razor ─► sider
Windows: Platforms/Windows/  ┘   (services          (vindue)    (én skærm)       (wwwroot/        (MainLayout,
         App.xaml                registreres)                                     index.html)       NavMenu)
```

Platforme: Android (min. API 26) og Windows (uden pakke, `WindowsPackageType None`). App-id `com.redskia.tiltmaze`.

---

## 4. Lagene i koden

```
Components/Pages   Start, Game, Leaderboard, Shop, Account, Login, About, NotFound
Components/Layout  MainLayout (login + oplåsning ved start), NavMenu
Components/Play    MazeLayer, TrailView, BallPattern, BallPreview, Joystick, RivalGhost
wwwroot/           app.css (design), js/game.js (størrelse, joystick, lyd)
        │  @inject
        ▼
Services           Scores, Wallet, Sound, GameDatabase, Auth, Biometrics, Api, RunQueue, ApiSettings, TiltSensor
        │
        ▼
TiltMaze.Core      Rng, Maze, Difficulty, Course, Ball, Camera, Ghost, Rewards, ShopItems, Trail, Format, Nickname
                   (ingen MAUI, testes med xUnit)
```

| Projekt | Indhold |
|---|---|
| `TiltMaze.Core` | Ren C#: labyrint, fysik, kamera, spøgelse, belønninger, butikkens katalog |
| `TiltMaze.App` | MAUI-appen: sider, services, platformskode |
| `TiltMaze.Tests` | xUnit-tests af Core |
| `supabase/` | SQL-migrationer, Edge Function (TypeScript), tests |

Hver side består af `.razor` (udseende), `.razor.cs` (kode) og `.razor.css` (egen stil).

---

## 5. Spillet

### Labyrint
- En labyrint er altid `(sværhedsgrad, seed)`. **Samme to værdier giver altid samme labyrint**, også på serveren. Der er ingen faste baner.
- Tilfældighed kommer fra `Rng` (en lille seedet generator, *mulberry32*), som findes i både C# og TypeScript og giver de samme tal. Derfor kan serveren bygge præcis samme labyrint.
- Generatoren laver en DFS-labyrint, åbner nogle 2x2-rum og fjerner en del blindgyder (*braiding*), så der er løkker og alternative veje. Er labyrinten én lang gang, laves den igen.
- Start er i et hjørne, målet er blandt de fjerneste celler (bredde-først afstand).
- Enheder: 1 celle = 1 enhed, y går nedad.

| | Easy | Normal | Hard | Expert |
|---|---|---|---|---|
| Størrelse | 5x7 | | | 11x16 |
| Mønter | 3 | 5 | 7 | 9 |
| Tåge | nej | nej | ja (4,5 celler) | ja (2,5 celler) |

### Seed og dagens labyrint
- **Daily maze:** seed er dagens dato (UTC, `yyyyMMdd`). Ens for alle. Intet endpoint.
- **Random maze:** ny tilfældig seed. **Retry** bruger samme seed. På startsiden kan man skrive en seed, og efter målet kopierer "Copy seed" den.

### Huller
- **Små** (radius ca. 0,13-0,175): kuglen kan rulle forbi langs siden.
- **Store** (radius 0,46): fylder hele gangen. Ligger kun på den korteste vej, og kun hvis målet stadig kan nås uden at gå gennem *alle* store huller, så der er altid en vej uden om.
- Halvdelen af de små ligger på vejen uden om de store. Hard og Expert har flest.
- **Træk mod hullet:** tæt på et hul trækkes kuglen svagt mod midten. Vip er stærkere, så man kan styre væk.
- **Fald:** kuglen falder, hvis dens midte er inde i hullet og farten er under 75 % af maks, eller hvis den er over hullet i mere end 0,19 s. Med fart ruller man altså hen over små huller og, med næsten fuld fart og lige tilløb, over store.

### Fysik
`Ball.Update` kører i **faste skridt på 1/120 s**: fart fra vip, friktion, maks-fart, så kollision mellem kuglens cirkel og hver væg (et linjestykke). Skridtet er meget mindre end en væg er tyk, så kuglen aldrig går gennem den. Hul, træk, mønt og mål tjekkes efter hvert skridt.

**Tiden følger fysikken, ikke uret:** hvert skridt tæller 1/120 s. Langsomme billeder giver derfor ikke en anden tid, og serveren kan regne den samme tid ud.

### Styring
- **Vip** (accelerometer, `TiltSensor`, tager højde for skærmens rotation) **eller joystick**, aldrig begge. Kontakt i pausemenuen og på Account. Uden sensor (Windows) kun joystick.
- **Joysticken er flydende:** den kommer frem, hvor første finger trykker på banen, og forsvinder ved slip (styring 0). Pointer events med capture, så både finger og mus virker.
- Tastaturet styrer ikke kuglen (for nemt). `P`/`Esc` pauser.

### Løbet
1. Nedtælling 3-2-1-Go (kuglen står stille, uret går ikke).
2. Spil. Fald = banen ryster, kanten blinker rødt, telefonen vibrerer, kuglen starter forfra.
3. Mål = konfetti (eller valgt effekt) og en dialog med tid, stjerner, mønter og præstationer.
- **Pause** (knap, `P`, `Esc`, eller når vinduet skjules) åbner én dialog: Resume, Retry, Leave. Spillet fortsætter først ved Resume.

### Spil-løkke og tegning
- `PeriodicTimer` (16 ms) i `Game.razor.cs` flytter kuglen og kameraet og kalder `StateHasChanged`. Kun kugle, spøgelser, kamera og HUD ændres pr. billede.
- Banen er **ét SVG** i celle-enheder. Den faste del (`MazeLayer`: gulv, huller, mål, vægge) tegnes kun, når labyrinten skifter. Alle vægge er ét `path` tegnet fire gange (skygge, side, top, glans) for en let 2,5D-effekt. Ingen SVG-filtre.
- **Kamera:** kan hele banen ikke vises med mindst 40 px pr. celle, viser `viewBox` kun et udsnit (56 px pr. celle), der glider blødt efter kuglen og holdes inden for banen. Intet minikort.

### Spøgelse
- Under løbet gemmes kuglens x og y hvert 1/30 sekund (liste af tal). Ved ny rekord gemmes listen lokalt og sendes til serveren (som bytes: float32).
- Næste gang på samme labyrint afspilles dit bedste løb som en stiplet turkis kugle. HUD'en viser forskellen i tid: grøn (foran) eller rød (bagud).
- Er man logget ind og online, hentes også **top 3 af andres ture** (ikke ens egen) som dæmpede kugler med brugernavn.

### Spilfølelse
- **Lyd:** korte arkadelyde (mønt, fald, nedtælling, mål, rekord, pause, dunk, klik, køb) lavet med Web Audio i `game.js`. Ingen lydfiler. Til/fra i pausemenuen og på Account.
- **Vibration:** ved fald, mønt og ved væg (10-25 ms efter styrke).
- **Tåge** på Hard/Expert: kun visning, påvirker ikke fysikken.

---

## 6. Belønninger og butik

Alle tal står i `Rewards.cs` (Core).

| Hvad | Giver |
|---|---|
| Mønt samlet | 1 mønt, én gang pr. labyrint |
| Labyrint klaret første gang | 5 / 10 / 20 / 40 (Easy → Expert) |
| Top 10 / top 3 | 25 / 50, én gang pr. labyrint (højere erstatter lavere) |
| Ny dag i træk (streak) | +5 mønter |
| Præstationer | 12 stk., én gang hver |

- **Stjerner (1-3)** pr. labyrint: 3 under 0,5 s pr. celle på korteste vej, 2 under 0,9 s, ellers 1.
- **Butik (`/shop`):** fem faner: Colours, Trails, Patterns, Finish, Themes. Første ting i hver er gratis og standard, resten koster 30-250. **Kun udseende:** påvirker aldrig fysik, tid, spøgelse eller anti-snyd.
- Mønter, køb og belønninger ligger **kun lokalt** (ingen server, ingen risiko for snyd med penge).

---

## 7. Data på telefonen

Alt gemmes lokalt i **SQLite** (`GameDatabase`) og i **Preferences/SecureStorage**.

| Hvor | Hvad |
|---|---|
| SQLite `BestRun` | Bedste tid og spøgelse pr. (sværhedsgrad, seed) |
| SQLite `PendingRun` | Rekorder, der venter på at blive sendt (kø), med brugernavnet der satte dem (tom = gæst) |
| SQLite `MazeProgress` | Samlede mønter, stjerner, udbetalte bonusser pr. labyrint |
| SQLite `WalletRow` | Mønter og ejede ting (`farve-id`, `slags:id`, `achievement:id`) |
| Preferences | Valgt farve/spor/mønster/effekt/tema, lyd, vip, sværhedsgrad, streak, biometri |
| SecureStorage | Login: `refreshToken`, `refreshExpires`, `username`, `google` |

`Scores.Save` gemmer tid og spøgelse **og** lægger rekorden i køen i én transaktion, så intet går tabt, hvis appen lukkes midt i.

---

## 8. Backend: Supabase

Ingen egen server. Appen taler direkte med Supabase over HTTPS.

### Konti (Supabase Auth)
- Brugernavn bliver til e-mailen `lower(navn)@tiltmaze.invalid` (ingen bekræftelse, ingen rigtig e-mail). Brugernavn: 3-16 tegn `[A-Za-z0-9_]`, unikt uden forskel på store/små (`Nickname.cs`, samme regel i appen og API'et).
- Login-fejl er altid "Wrong username or password", så man ikke kan gætte, hvilke navne der findes.
- **Google-login** som alternativ (se [Sikkerhed](#11-sikkerhed)). Google-brugere får navn fra Gmail (det før @), og har intet kodeord.
- **Log ud** kalder serveren (`scope=local`) og rydder SecureStorage. **Slet konto** (`delete_me`) fjerner også tider og spøgelser.

### Tabeller
- `profiles` (id, brugernavn) og `runs` (bedste tid + spøgelse pr. bruger og labyrint). Kun brugerens **bedste** tid pr. labyrint gemmes.
- Begge har **Row Level Security (RLS)**. `runs` har bevidst *ingen* politik og ingen rettigheder: den kan kun nås via RPC'er. `profiles` kan en bruger kun læse sin egen række af.

### RPC'er (funktioner i databasen)
| Funktion | Hvem | Gør |
|---|---|---|
| `leaderboard(difficulty, seed, top)` | alle | top-listen |
| `top_ghosts(difficulty, seed)` | indlogget | top 3 andres spøgelser |
| `get_ghost(run_id)` | indlogget | ét spøgelse |
| `username_available` | alle | er navnet ledigt (vises mens man skriver) |
| `set_username` | indlogget | skifter navn (og login-e-mail for e-mail-brugere) |
| `delete_me` | indlogget | sletter egen konto og data |
| `submit_run` | **kun serveren** (`service_role`) | gemmer en tid, der er godkendt |

### Migrationer
`supabase/migrations/0001`-`0005` (init, RLS, Google, e-mail-vagt, Google-navn). **Ret aldrig en lagt migration, lav en ny.** Konfiguration i `supabase/config.toml`.

---

## 9. Anti-snyd

Problemet: appen kører på brugerens telefon, så en snyder kan sende hvilken som helst tid. Løsningen: serveren stoler ikke på tiden, men **afspiller turen**.

```
Appen                           Edge Function submit-run                    Database
─────                           ────────────────────────                    ────────
sender {sværhedsgrad, seed,     1. bygger SAMME labyrint (TypeScript)
 tid, spøgelse (base64)}   ───► 2. afspiller spøgelsets positioner
                                3. tjekker: ingen vægge gennem, ikke for
                                   hurtigt, ingen huller, når målet,
                                   tiden passer           ──── ok ───►  RPC submit_run
                                svar {id, tid, isBest, rank}  ◄────────  gemmer bedste tid
                                eller 400 "Run could not be verified"
```

- Labyrint og regler findes i **både C# og TypeScript** (`Rng` er ens). `supabase/functions/_shared/` (`maze.ts`, `course.ts`, `check.ts`) er en port af `Maze`, `Course` og `RunChecker`.
- **Samme resultat to steder** sikres med tests: `CourseFixtureTests` (C#) skriver et facit (`course.json`), og `check.test.ts` (Node) tjekker TypeScript mod det, plus rigtige ture lavet af en robot (`runs.json`).
- Ændres labyrint eller regler i C#, skal TypeScript følge med.
- Mønter, butik og udseende indgår **ikke** i tjekket.

---

## 10. Synkronisering

Spillet er **offline-først**: man spiller altid lokalt, og rekorder sendes, når det er muligt.

```
Ny rekord ──► SQLite (BestRun + PendingRun)  ──►  RunQueue  ──► Edge Function ──► Supabase
              (altid, uden net)                  (når der er net og login)
```

- `RunQueue` sender ventende rekorder: **ved start, efter login, ved ny rekord og når nettet kommer tilbage.**
- Den sender kun **gæsterekorder** og den **indloggede brugers egne**. Ved udløbet login bevares rekorderne, men sendes ikke under en anden bruger.
- Når serveren svarer med `rank`, får brugeren top-listebonussen (top 10 / top 3).
- Tider hentes ikke ned igen: bedste tid og spøgelse er kun lokalt. Kun *andres* spøgelser og top-listen hentes fra serveren.
- **Kendt begrænsning:** ny telefon betyder, at ens egne spøgelser ikke følger med (står i `TODO.md`).

---

## 11. Sikkerhed

| Emne | Hvordan |
|---|---|
| Kodeord | Hashes af Supabase Auth (bcrypt). Appen ser dem aldrig igen efter login |
| Google-login | OAuth 2.0 med **PKCE**: tilfældig kode-verifier (32 bytes), challenge = base64url(SHA-256). Åbner en Custom Tab (Android) eller browseren (Windows), aldrig appens egen WebView. Appen bytter engangskoden til tokens |
| Tokens | Adgangstoken er kortlivet. Refresh-token ligger i **SecureStorage** (Android Keystore / Windows). Log ud ugyldiggør dem på serveren |
| Fingeraftryk | Valgfrit: appen starter låst, `Biometrics` låser det gemte login op. Annullerer man, spiller man som gæst. Passkeys er fravalgt (virker ikke i Android WebView) |
| Autorisation | **RLS og RPC'er på serveren.** En bruger kan kun læse sin egen profil. Ændrer man et id i et kald, nægter databasen. `submit_run` kan kun kaldes af serveren |
| Nøgler | Appen har kun den **offentlige** publishable key (sendes som `apikey`). Service-nøglen findes kun i Edge Function. `Authorization: Bearer` sendes kun med brugerens eget token |
| Navne | Trigger i databasen (`0004_email_guard`) forhindrer, at man optager et andet brugernavns login-e-mail |
| Anti-snyd | Serveren afspiller hver tur (se afsnit 9) |
| Android | `allowBackup=false`. Kun `MainActivity` og Google-callback er `exported`. Tilladelser: INTERNET, ACCESS_NETWORK_STATE, VIBRATE, USE_BIOMETRIC |
| Trafik | Al kommunikation over HTTPS |
| Hemmeligheder | Ingen `.env`, kodeord eller service-nøgle i git |

**Største trussel:** snyd med falske tider. Modforanstaltning: serveren afspiller og tjekker hver tur, og tider kan kun gemmes via funktionen, ikke direkte i tabellen.

---

## 12. Udseende: "Neon Arcade"

Et mørkt arkadespil: natblå baggrund med et svagt lys fra toppen, **turkis** neon (`--accent`) til vægge, knapper og aktiv menu, en **orange** kugle (`--ball`), et **guld** mål og rekord (`--gold`) og **koral-rød** til fare (`--danger`). Tokens (farver, afstande, runding, skygger) ligger i `wwwroot/app.css`. Kun systemskrifttyper, store tal med `tabular-nums`, så tiden ikke hopper.

- **Knapper:** arkade-knapper med mørk kant under (`box-shadow`), der synker ned ved tryk. Hover kun med mus.
- **Feedback:** nedtælling der "popper", rystelse og rødt blink ved fald, konfetti og guldtid ved mål.
- **Temaer i butikken:** Neon, Lava, Ice, Retro. Et tema er en klasse (`theme-<id>`) som sætter `--m-...`-farver til bane, så banen ikke har faste farver.
- **Mindre bevægelse:** ved `prefers-reduced-motion` er rystelse, konfetti, pop og puls slået fra.
- **Logo:** turkis labyrint med orange kugle og guld mål. **Mærke:** RedSkia.Dev på Om-siden og "by RedSkia.Dev" på startsiden. Intet personnavn i appen.

---

## 13. Skærmene

| Side | Indhold |
|---|---|
| **Start** (`/`) | "Play daily"-kort med dato, egen tid og nedtælling til næste. Fire sværhedskort med størrelse, huller, bedste tid og stjerner. Seed-felt, Random maze. Streak |
| **Game** (`/play/...`) | HUD (pause, tid, forskel til spøgelse, mønter, fald), bane, flydende joystick. Landskab/PC: sidepanel med tid, tal og knapper |
| **Leaderboard** | Podie for top 3 og liste til top 10 pr. labyrint (dagens eller en seed). Egen linje markeret |
| **Shop** | Saldo, fem faner, kort med forhåndsvisning. Buy / Equip / Equipped |
| **Account** | Profil med avatar, skift navn, lyd, vip/joystick, fingeraftryk, præstationer, log ud, slet konto (farezone) |
| **Login** | Brugernavn og kodeord (viser live om navnet er ledigt) + "Continue with Google" |
| **About** | Om, sådan spiller man, privatliv, RedSkia.Dev |

**Dialoger** er i appen (ikke systemets): Paused, Maze complete og "Delete account?" (`ConfirmDialog`). Fokus på første knap (den sikre), `Esc` lukker, resten af skærmen er `inert`.

**Navigation:** bundmenu på telefon, sidemenu fra 768 px bredde (Play, Leaderboard, Shop, Account, About).

---

## 14. Tilgængelighed og layout

- **Responsivt:** testes ved 390, 900 og 1400 px bredde uden overlap. Rækker af knapper bruger `flex-wrap` eller `minmax(0, 1fr)`. Lange navne får "...".
- **Trykmål:** alt der kan trykkes er mindst **48 px** (`--touch`), med mindst 8 px mellem naboer.
- **Afstande:** kun fra 8-skalaen `--space-1..4` (8/16/24/32 px).
- **Ingen scrollbar på telefon.** Dialoger ruller kun lodret.
- **Bevægelse:** alle animationer slås fra eller dæmpes ved `prefers-reduced-motion`.
- **Tastatur:** fokus-ring på alt, dialoger har fokus og `Esc`. Spillet styres dog kun med vip eller joystick.
- **Ikke lavet endnu:** lyst tema, og måling af kontrast og skriftstørrelse til 200 %.

---

## 15. Test

| Hvad | Hvordan |
|---|---|
| Labyrint, fysik, kamera, spøgelse, mønter, belønninger, brugernavn, tid | `dotnet test` (xUnit). Alle sværhedsgrader x mange seeds: målet nås, start/mål er aldrig huller, samme seed = samme labyrint |
| Anti-snyd i TypeScript | `node --test supabase/functions/tests/check.test.ts` (facit fra C# + rigtige ture) |
| Hele flowet mod Supabase | `node supabase/tests/e2e.mjs` (langsom, kun før release): konto, login, tider, top-liste, spøgelser, **RLS (egen profil ja, andres nej)**, navneskift, sletning |

Dev: bygning og hurtige tests. E2E og Docker kun før udgivelse.

---

## 16. Valg og begrundelser

| Valg | Hvorfor |
|---|---|
| Blazor Hybrid i stedet for ren MAUI-UI | Én kodebase i HTML/CSS/SVG til begge platforme, nem at style |
| Al fysik og labyrint i `Core` | Kan testes uden app, og samme regler kan genbruges (og porteres til TypeScript) |
| Seedet `Rng` i stedet for `System.Random` | Samme tal i C# og TypeScript, så serveren bygger samme labyrint |
| Fast fysik-skridt 1/120 s | Tiden afhænger ikke af telefonens billedhastighed, og serveren kan afspille samme tid |
| Supabase, ingen egen server | Konti, database og RLS uden at drive en server. Kun anti-snyd kræver egen kode |
| Serveren afspiller spøgelset | Eneste måde at stole på en tid fra en enhed, vi ikke kontrollerer |
| Brugernavn i stedet for e-mail | Enklere for børn/spillere, ingen e-mailbekræftelse, ingen persondata |
| Offline-først med kø | Spillet virker i flyvemaskine, rekorder indhentes bagefter |
| Mønter og butik lokalt | Kun pynt, intet at vinde ved at snyde, ingen ekstra serverkode |
| Butik ændrer aldrig fysik | Alle konkurrerer på lige vilkår |
| Fingeraftryk i stedet for passkeys | Passkeys virker ikke i Android WebView uden meget native kode |
| Ingen admin-rolle | Det er et spil: reglerne håndhæves af serveren for alle, og der er intet at moderere |
| SVG i stedet for canvas | Enkelt og skarpt. Fast del tegnes kun én gang. Plan B: `<canvas>` hvis billedhastigheden er for lav på telefonen |
