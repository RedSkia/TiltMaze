# TiltMaze (AI-noter)

Labyrintspil, .NET MAUI Blazor Hybrid (Android + Windows), senere + ASP.NET Core API. Kode HELT simpelt, skrevet som et menneske (skoleprojekt, Marcus skal kunne forklare hver linje). Dansk i docs og kommentarer, engelsk i UI. Plan: `docs/plan.md`. Design og lag: `DESIGN.md`.

## Tree
- `TiltMaze.Core/` ren C# (net10.0), ingen MAUI: `Maze.cs` (Wall, Cell, MazeGenerator: DFS + rum + braiding + Distances), `Difficulty.cs` (Easy/Normal/Hard/Expert og deres indstillinger), `Course.cs` (labyrint fra sværhedsgrad + seed: start i hjørne, mål langt væk, små og store huller, vægge som linjestykker, `Check` for hul/mål), `Ball.cs` (fysik, fast tidsskridt 1/120 s, kollision cirkel mod linje), `Format.cs` (tidstekst)
- `TiltMaze.App/` MAUI Blazor Hybrid, Android (min API 26) + Windows (`WindowsPackageType None`), id `com.redskia.tiltmaze`
  - `Components/Layout` MainLayout + NavMenu (bundmenu på telefon, sidemenu fra 768 px)
  - `Components/Pages` Start (`/`, kort pr. sværhedsgrad), Game (`/play/{DifficultyName}/{Seed:int?}`), About, NotFound. Hver side: `.razor` + `.razor.cs` + `.razor.css`
  - `Services/` `Scores` (bedste tid pr. sværhedsgrad+seed, spøgelser som filer), `TiltSensor` (accelerometer til vip)
  - `wwwroot/app.css` design-tokens og fælles klasser (samme som Pulsify), `wwwroot/images/` logo
  - `Platforms/` Android- og Windows-indgang
- `TiltMaze.Tests/` xUnit: alle sværhedsgrader x mange seeds (mål nås uden store huller, start/mål aldrig huller, samme seed = samme labyrint, små huller har en åbning), fald/hop over huller, kuglen går ikke gennem vægge

## Regler
- Al matematik (labyrint, fysik) i `TiltMaze.Core` og testet. Siderne tegner og styrer kun.
- Ingen baner/levels: hver labyrint er `(Difficulty, seed)`. Dagens labyrint har datoen (UTC, `yyyyMMdd`) som seed. Tilfældig seed: `Course.NewSeed()`.
- Enheder: 1 celle = 1 enhed, y går nedad. Start i et hjørne, mål blandt de fjerneste celler (bredde-først afstand).
- Huller: små (kuglen kan rulle forbi langs siden) og store (fylder gangen; kun på korteste vej og kun hvis målet nås uden alle store huller). Kuglen falder, når dens midte er inde i hullet og farten er under `Course.JumpSpeed`, eller den er over hullet længere end `MaxTimeOverHole`. Hul/mål tjekkes efter hvert fysik-skridt (callback til `Ball.Update`).
- Spøgelse: kuglens x,y som `float[]` hvert 1/30 s (`Scores.GhostRate`), gemmes som `ghost-{sværhedsgrad}-{seed}.bin` i `FileSystem.AppDataDirectory` ved ny rekord. Formatet er bevidst enkelt, så det senere kan uploades og afspilles/valideres på en server.
- Spil-løkke: `PeriodicTimer` 16 ms i `Game.razor.cs`, `InvokeAsync(StateHasChanged)`. Alle vægge er ét SVG-`path` (i `defs`), tegnet tre gange med `use` (skygge, side, top).
- Tal i SVG formateres med `CultureInfo.InvariantCulture` (`N()` i Game).
- Alle tekster brugeren ser er på engelsk.
- Fælles UI og tokens kommer fra `wwwroot/app.css`; sidens egen stil ligger i `.razor.css`.
- Android-tilladelser: kun `VIBRATE` (`INTERNET` kommer med API'et).

## Kommandoer
- Build: `dotnet build TiltMaze.App -c Release -f net10.0-windows10.0.19041.0` / `-f net10.0-android`
- Kør: `dotnet run --project TiltMaze.App -f net10.0-windows10.0.19041.0`, telefon: `dotnet build TiltMaze.App -t:Run -f net10.0-android`
- Test: `dotnet test`

## Git
- `feature/*` → `develop` → `main`, lineær historik (`--ff-only`), ingen AI-omtale i commits, korte beskeder i bydeform.
- Genbrug fra `D:\Desktop\Pulsify` senere: login (Identity bearer), Docker-API, SQLite-kø.
