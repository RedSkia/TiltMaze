# TiltMaze (AI-noter)

Labyrintspil, .NET MAUI Blazor Hybrid (Android + Windows), senere + ASP.NET Core API. Kode HELT simpelt, skrevet som et menneske (skoleprojekt, Marcus skal kunne forklare hver linje). Dansk i docs og kommentarer, engelsk i UI. Plan: `docs/plan.md`. Design og lag: `DESIGN.md`.

## Tree
- `TiltMaze.Core/` ren C# (net10.0), ingen MAUI: `Maze.cs` (Wall, Cell, MazeGenerator: DFS + rum + braiding + Distances), `Level.cs` (bane n: størrelse, seed n, huller, vægge som linjestykker), `Ball.cs` (fysik, fast tidsskridt 1/120 s, kollision cirkel mod linje)
- `TiltMaze.App/` MAUI Blazor Hybrid, Android (min API 26) + Windows (`WindowsPackageType None`), id `com.redskia.tiltmaze`
  - `Components/Layout` MainLayout + NavMenu (bundmenu på telefon, sidemenu fra 768 px)
  - `Components/Pages` Levels (`/`), Game (`/play/{Level:int}`), About, NotFound. Hver side: `.razor` + `.razor.cs` + `.razor.css`
  - `Services/` `Scores` (bedste tid, Preferences), `TiltSensor` (accelerometer til vip)
  - `wwwroot/app.css` design-tokens og fælles klasser (samme som Pulsify), `wwwroot/images/` logo
  - `Platforms/` Android- og Windows-indgang
- `TiltMaze.Tests/` xUnit: samme seed = samme labyrint, alle celler nås, mål nås uden om huller, kuglen går ikke gennem vægge

## Regler
- Al matematik (labyrint, fysik) i `TiltMaze.Core` og testet. Siderne tegner og styrer kun.
- Enheder: 1 celle = 1 enhed, y går nedad. Start øverst til venstre, mål nederst til højre.
- Spil-løkke: `PeriodicTimer` 16 ms i `Game.razor.cs`, `InvokeAsync(StateHasChanged)`. Alle vægge er ét SVG-`path`, så Blazor kun sammenligner én tekst.
- Tal i SVG formateres med `CultureInfo.InvariantCulture` (`N()` i Game).
- Alle tekster brugeren ser er på engelsk.
- Fælles UI og tokens kommer fra `wwwroot/app.css`; sidens egen stil ligger i `.razor.css`.

## Kommandoer
- Build: `dotnet build TiltMaze.App -c Release -f net10.0-windows10.0.19041.0` / `-f net10.0-android`
- Kør: `dotnet run --project TiltMaze.App -f net10.0-windows10.0.19041.0`, telefon: `dotnet build TiltMaze.App -t:Run -f net10.0-android`
- Test: `dotnet test`

## Git
- `feature/*` → `develop` → `main`, lineær historik (`--ff-only`), ingen AI-omtale i commits, korte beskeder i bydeform.
- Genbrug fra `D:\Desktop\Pulsify` senere: login (Identity bearer), Docker-API, SQLite-kø.
