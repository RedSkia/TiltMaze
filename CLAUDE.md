# TiltMaze (AI-noter)

Labyrintspil, .NET MAUI Blazor Hybrid (Android + Windows), senere + ASP.NET Core API. Kode HELT simpelt, skrevet som et menneske (skoleprojekt, Marcus skal kunne forklare hver linje). Dansk i docs og kommentarer, engelsk i UI. Plan: `docs/plan.md`. Design og lag: `DESIGN.md`.

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
- Alle tekster brugeren ser er på engelsk.
- Design "Neon Arcade": natblå baggrund, turkis accent (`--accent`), orange kugle (`--ball`), guld mål/rekord (`--gold`), koral fare (`--danger`). Kun systemskrifttyper. Arkade-knapper med kant (`box-shadow`), der trykkes ned. Alle animationer slås fra eller dæmpes ved `prefers-reduced-motion`.
- Fælles UI og tokens kommer fra `wwwroot/app.css`; sidens egen stil ligger i `.razor.css`.
- Mærke: kun RedSkia.Dev (About-kortet med RedSkia-rød `#ee3b33`, "by RedSkia.Dev" på startsiden). Intet personnavn i appen.
- Android-tilladelser: kun `VIBRATE` (`INTERNET` kommer med API'et).

## Kommandoer
- Build: `dotnet build TiltMaze.App -c Release -f net10.0-windows10.0.19041.0` / `-f net10.0-android`
- Kør: `dotnet run --project TiltMaze.App -f net10.0-windows10.0.19041.0`, telefon: `dotnet build TiltMaze.App -t:Run -f net10.0-android`
- Test: `dotnet test`

## Git
- `feature/*` → `develop` → `main`, lineær historik (`--ff-only`), ingen AI-omtale i commits, korte beskeder i bydeform.
- Genbrug fra `D:\Desktop\Pulsify` senere: login (Identity bearer), Docker-API, SQLite-kø.
