# TiltMaze (AI-noter)

Labyrintspil, .NET MAUI Blazor Hybrid (Android + Windows) + ASP.NET Core API (Docker). Kode HELT simpelt, skrevet som et menneske (skoleprojekt, Marcus skal kunne forklare hver linje). Dansk i docs og kommentarer, engelsk i UI. Plan: `docs/plan.md`. Design og lag: `DESIGN.md`.

## Tree
- `TiltMaze.Core/` ren C# (net10.0), ingen MAUI: `Maze.cs` (Wall, Cell, MazeGenerator: DFS + rum + braiding + Distances), `Difficulty.cs` (Easy/Normal/Hard/Expert og deres indstillinger), `Course.cs` (labyrint fra sværhedsgrad + seed: start i hjørne, mål langt væk, små og store huller, vægge som linjestykker, `Check` for hul/mål), `Ball.cs` (fysik, fast tidsskridt 1/120 s, kollision cirkel mod linje), `Camera.cs` (udsnit der glider efter kuglen, holdes inden for banen), `Ghost.cs` (afspil optaget løb, første besøg pr. celle til mellemtider), `Format.cs` (tid og +/- forskel)
- `TiltMaze.App/` MAUI Blazor Hybrid, Android (min API 26) + Windows (`WindowsPackageType None`), id `com.redskia.tiltmaze`
  - `Components/Layout` MainLayout + NavMenu (Play, Leaderboard, Account, About; bundmenu på telefon, sidemenu fra 768 px)
  - `Components/Pages` Start (`/`, "Play daily"-kort + felter pr. sværhedsgrad), Game (`/play/{DifficultyName}/{Seed:int?}`), Leaderboard (`/leaderboard`) og Account (`/account`) er "Coming soon"-pladsholdere (bygges på anden gren), About, NotFound. Hver side: `.razor` + `.razor.cs` + `.razor.css` (pladsholderne kun `.razor`)
  - `Components/Play` dele af spilskærmen: `MazeLayer` (fast bane: gulv, huller, mål, vægge; tegnes kun ved ny labyrint, `Mini` = minikort), `Joystick` (pointer events, X/Y fra -1 til 1)
  - `Services/` `Scores` (bedste tid pr. sværhedsgrad+seed, spøgelser som filer), `TiltSensor` (accelerometer til vip)
  - `wwwroot/app.css` design-tokens "Neon Arcade" og fælles klasser, `wwwroot/js/game.js` (ResizeObserver til banens størrelse, pointer capture til joysticken), `wwwroot/images/` logo + RedSkia-logo
  - `Platforms/` Android- og Windows-indgang
- `TiltMaze.Tests/` xUnit: alle sværhedsgrader x mange seeds (mål nås uden store huller, start/mål aldrig huller, samme seed = samme labyrint, små huller har en åbning), fald/hop over huller, kuglen går ikke gennem vægge, kamera, spøgelse, tidstekster
- `TiltMaze.Core/` ren C# (net10.0), ingen MAUI: `Maze.cs` (Wall, Cell, MazeGenerator: DFS + rum + braiding + Distances), `Difficulty.cs` (Easy/Normal/Hard/Expert og deres indstillinger), `Course.cs` (labyrint fra sværhedsgrad + seed: start i hjørne, mål langt væk, små og store huller, vægge som linjestykker, `Check` for hul/mål), `Ball.cs` (fysik, fast tidsskridt 1/120 s, kollision cirkel mod linje), `Ghost.cs` (spøgelse som bytes), `Format.cs` (tidstekst)
- `TiltMaze.App/` MAUI Blazor Hybrid, Android (min API 26) + Windows (`WindowsPackageType None`), id `com.redskia.tiltmaze`
  - `Components/Layout` MainLayout + NavMenu (bundmenu på telefon, sidemenu fra 768 px)
  - `Components/Pages` Start (`/`, kort pr. sværhedsgrad), Game (`/play/{DifficultyName}/{Seed:int?}`), Leaderboard (`/leaderboard`), Account (`/account`), Login (`/login`), About, NotFound. Hver side: `.razor` + `.razor.cs` + `.razor.css`
  - `Services/` `Scores` (bedste tid/spøgelse, bruges af siderne), `GameDatabase` (SQLite: BestRun, PendingRun), `Auth` (login, tokens i SecureStorage), `Api` (HTTP), `RunQueue` (sender ventende rekorder), `ApiSettings` (adresse), `TiltSensor` (accelerometer til vip)
  - `wwwroot/app.css` design-tokens og fælles klasser (samme som Pulsify), `wwwroot/images/` logo
  - `Platforms/` Android- og Windows-indgang
- `TiltMaze.Tests/` xUnit: alle sværhedsgrader x mange seeds (mål nås uden store huller, start/mål aldrig huller, samme seed = samme labyrint, små huller har en åbning), fald/hop over huller, kuglen går ikke gennem vægge
- `TiltMaze.Api/` ASP.NET Core minimal API (net10.0, refererer Core): `Program.cs` (opsætning + alle endpoints), `GameDb.cs` (EF Core SQLite: Identity-brugere med `Nickname`, `Runs`, DTO'er), `RunChecker.cs` (anti-snyd), `IdentityErrors.cs` (engelske fejltekster), `Dockerfile`
- `TiltMaze.Api.Tests/` xUnit + `WebApplicationFactory` (midlertidig SQLite): endpoints, rate limit, anti-snyd (teleport, gennem væg, for hurtig, forkert mål/tid). `Player.cs` er en computer-spiller, der laver rigtige ture
- `compose.yaml` (API i Docker), `.env` (kun `CERT_PASSWORD`, aldrig i git; skabelon `.env.example`)

## API
- Adresse `https://localhost:7065` (Docker 7065→8443, cert `tiltmaze.pfx` fra `CERT_DIR`, standard `%USERPROFILE%/.aspnet/https`). Volumen `tiltmaze-data` har databasen og `keys` (DataProtection). Android når den via `adb reverse tcp:7065` (sættes ved Debug-build, sammen med `docker compose up -d --build`).
- Login (Identity, bearer): `POST /api/auth/login`, `/api/auth/refresh` (og Identitys `/register`, men uden kaldenavn kan man ikke sende tider). Konto: `POST /api/account {email,password,nickname}`, `GET /api/account`, `PUT /api/account/nickname`, `DELETE /api/account`. Kaldenavn: unikt (store/små ens), 3-16 tegn `[A-Za-z0-9_]`.
- `POST /api/runs {difficulty, seed, timeMs, ghost(base64 float32 LE)}` (login, 20/min): `RunChecker` bygger `Course(difficulty, seed)` og tjekker spøgelset (start, mål, skridt ≤ maks-fart, aldrig i væg, fald = hop til start, tid = antal målinger ±1). Fejl: 400 "Run could not be verified". Kun brugerens bedste tid pr. labyrint gemmes.
- `GET /api/leaderboard/{difficulty}/{seed}?top=10` (åben; `isMe` hvis logget ind) → `[{rank, runId, nickname, timeMs, isMe}]`. `GET /api/runs/{id}/ghost` (login; kun top 10 og egne). `GET /api/daily` → `{seed}`.
- Fejl er altid ProblemDetails med engelsk `title`. Rate limits: `runs` 20/min og `read` 60/min pr. bruger/IP, `auth` 20/min pr. IP.
- Appen: tider ligger i SQLite (`GameDatabase`); ny rekord → `PendingRun` → `RunQueue` sender ved start, efter login, ved ny rekord og når nettet kommer tilbage. `Api.GetTopGhosts(difficulty, seed)` giver top 3 spøgelser (bruges i Game senere). Tokens i `SecureStorage`: `refreshToken`, `refreshExpires`, `email`, `nickname`.

## Regler
- Al matematik (labyrint, fysik) i `TiltMaze.Core` og testet. Siderne tegner og styrer kun.
- Ingen baner/levels: hver labyrint er `(Difficulty, seed)`. Dagens labyrint har datoen (UTC, `yyyyMMdd`) som seed. Tilfældig seed: `Course.NewSeed()`.
- Enheder: 1 celle = 1 enhed, y går nedad. Start i et hjørne, mål blandt de fjerneste celler (bredde-først afstand).
- Huller: små (kuglen kan rulle forbi langs siden) og store (fylder gangen; kun på korteste vej og kun hvis målet nås uden alle store huller). Kuglen falder, når dens midte er inde i hullet og farten er under `Course.JumpSpeed`, eller den er over hullet længere end `MaxTimeOverHole`. Hul/mål tjekkes efter hvert fysik-skridt (callback til `Ball.Update`).
- Spøgelse: kuglens x,y som `float[]` hvert 1/30 s (`Scores.GhostRate`), gemmes som `ghost-{sværhedsgrad}-{seed}.bin` i `FileSystem.AppDataDirectory` ved ny rekord. Formatet er bevidst enkelt, så det senere kan uploades og afspilles/valideres på en server.
- Spil-løkke: `PeriodicTimer` 16 ms i `Game.razor.cs`, `InvokeAsync(StateHasChanged)`. Kun kugle, spøgelse, kamera (`viewBox`), HUD og minikortets prik ændres pr. billede. Den faste bane ligger i `MazeLayer`, hvis `ShouldRender` kun er true ved en ny `Course`. Alle vægge er ét SVG-`path` (i `defs`), tegnet fire gange med `use` (skygge, side, top, glans). Ingen SVG-filtre (blur) på banen.
- Kamera: følger kuglen, når hele banen ikke kan vises med mindst 40 px pr. celle (`MinCell`); så vises 56 px pr. celle (`CameraCell`) og et minikort. Banens størrelse kommer fra `tiltMaze.watchSize` (ResizeObserver) → `[JSInvokable] BoardResized`.
- Styring lægges sammen og klippes til -1..1: taster + vip + joystick. Joysticken vises som standard på telefon/tablet, slås til/fra i pausemenuen (Preferences `joystick`). Valgt sværhedsgrad på startsiden: Preferences `difficulty`.
- Løbet: nedtælling 3-2-1-Go (`Beat` 0,7 s), fald = rystelse + rødt blink + vibration, mål = konfetti + dialog. Menu midt i et løb giver in-app dialogen "Leave run?". Dialoger: fokus på første knap, `Esc` lukker, resten af skærmen er `inert`.
- Tal i SVG formateres med `CultureInfo.InvariantCulture` (`N()` i Game, MazeLayer og Joystick).
- Spøgelse: kuglens x,y som `float[]` hvert 1/30 s (`Scores.GhostRate`), gemmes i SQLite (`BestRun.Ghost`) ved ny rekord og sendes til serveren som base64 af float32 (`Ghost.ToBytes` i Core). Serveren afspiller og tjekker det.
- Spil-løkke: `PeriodicTimer` 16 ms i `Game.razor.cs`, `InvokeAsync(StateHasChanged)`. Alle vægge er ét SVG-`path` (i `defs`), tegnet tre gange med `use` (skygge, side, top).
- Tal i SVG formateres med `CultureInfo.InvariantCulture` (`N()` i Game).
- Alle tekster brugeren ser er på engelsk.
- Design "Neon Arcade": natblå baggrund, turkis accent (`--accent`), orange kugle (`--ball`), guld mål/rekord (`--gold`), koral fare (`--danger`). Kun systemskrifttyper. Arkade-knapper med kant (`box-shadow`), der trykkes ned. Alle animationer slås fra eller dæmpes ved `prefers-reduced-motion`.
- Fælles UI og tokens kommer fra `wwwroot/app.css`; sidens egen stil ligger i `.razor.css`.
- Mærke: kun RedSkia.Dev (About-kortet med RedSkia-rød `#ee3b33`, "by RedSkia.Dev" på startsiden). Intet personnavn i appen.
- Android-tilladelser: kun `VIBRATE` (`INTERNET` kommer med API'et).
- Android-tilladelser: `VIBRATE` og `INTERNET`.

## Kommandoer
- Build: `dotnet build TiltMaze.App -c Release -f net10.0-windows10.0.19041.0` / `-f net10.0-android`
- Kør: `dotnet run --project TiltMaze.App -f net10.0-windows10.0.19041.0`, telefon: `dotnet build TiltMaze.App -t:Run -f net10.0-android`
- Test: `dotnet test` (Core og API). API lokalt: `docker compose up -d --build` (kræver `.env` med `CERT_PASSWORD`)

## Git
- `feature/*` → `develop` → `main`, lineær historik (`--ff-only`), ingen AI-omtale i commits, korte beskeder i bydeform.
- Login, Docker-API og SQLite-kø er genbrugt fra Pulsify (`D:/Desktop/Pulsify`).
