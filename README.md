# TiltMaze

Labyrintspil: styr en kugle ved at vippe telefonen (Android) eller med piletaster/WASD (Windows). Undgå hullerne og nå målet så hurtigt som muligt. Rul hurtigt, så kan kuglen hoppe over hullerne.

Skoleprojekt i App-programmering 2026 (.NET MAUI Blazor Hybrid). Senere: broer med touch, API med highscores, login.

- Plan og kravspec: `docs/plan.md`, wireframes: `docs/wireframes.html`, PDF: `docs/TiltMaze-kravspec.pdf`
- Opbygning: `DESIGN.md`

## Kom i gang

Krav: .NET 10 SDK og MAUI-workload (`dotnet workload install maui`). Android kræver Android SDK.

```
dotnet run --project TiltMaze.App -f net10.0-windows10.0.19041.0     # Windows
dotnet build TiltMaze.App -t:Run -f net10.0-android                    # Android (telefon eller emulator)
dotnet test                                                            # tests af labyrint og fysik
```

## Spillet

Vælg Easy, Normal, Hard eller Expert på startsiden. Sværhedsgraden bestemmer størrelse, antal løkker (alternative veje) og antal og størrelse af huller.

- **Random maze:** ny labyrint hver gang. Dens seed vises lille i spillet. Retry spiller samme labyrint igen, New maze laver en ny.
- **Daily maze:** dagens labyrint er ens for alle, fordi seed er datoen.
- Bedste tid gemmes pr. labyrint (sværhedsgrad + seed) og pr. sværhedsgrad. Dit bedste løb vises som et spøgelse næste gang du spiller samme labyrint.
- Små huller kan man rulle forbi langs væggen. Store huller fylder hele gangen, men med fuld fart og et lige tilløb kan man hoppe over dem.

| Platform | Styring |
|---|---|
| Android | Vip telefonen. Kuglen ruller mod den side, der peger nedad |
| Windows | Piletaster eller WASD. `P` eller `Esc` pauser |

Spillet pauser, når appen skjules, og fortsætter først, når du trykker Resume.
