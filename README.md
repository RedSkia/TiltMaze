# TiltMaze

Labyrintspil: styr en kugle ved at vippe telefonen (Android) eller med piletaster/WASD (Windows). Undgå hullerne og nå målet så hurtigt som muligt. Bedste tid gemmes pr. bane.

Skoleprojekt i App-programmering 2026 (.NET MAUI Blazor Hybrid). Senere: broer med touch, API med baner og highscores, login.

- Plan og kravspec: `docs/plan.md`, wireframes: `docs/wireframes.html`, PDF: `docs/TiltMaze-kravspec.pdf`
- Opbygning: `DESIGN.md`

## Kom i gang

Krav: .NET 10 SDK og MAUI-workload (`dotnet workload install maui`). Android kræver Android SDK.

```
dotnet run --project TiltMaze.App -f net10.0-windows10.0.19041.0     # Windows
dotnet build TiltMaze.App -t:Run -f net10.0-android                    # Android (telefon eller emulator)
dotnet test                                                            # tests af labyrint og fysik
```

## Styring

| Platform | Styring |
|---|---|
| Android | Vip telefonen. Kuglen ruller mod den side, der peger nedad |
| Windows | Piletaster eller WASD. `P` eller `Esc` pauser |

Spillet pauser, når appen skjules. Bane 1-12 vokser fra 5x7 til 10x14 felter. Bane n bruger seed n, så en bane er altid ens.
