# TiltMaze

Labyrintspil: styr en kugle ved at vippe telefonen (Android), med joysticken på skærmen eller med piletaster/WASD (Windows). Undgå hullerne og nå målet så hurtigt som muligt. Rul hurtigt, så kan kuglen hoppe over hullerne.

Skoleprojekt i App-programmering 2026 (.NET MAUI Blazor Hybrid). Senere: broer med touch, API med highscores og login (Leaderboard og Account).

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

Vælg Easy, Normal, Hard eller Expert på startsiden (valget huskes), og tryk den store **Play daily** eller **Random maze**. Sværhedsgraden bestemmer størrelse, antal løkker (alternative veje) og antal og størrelse af huller.

- **Daily maze:** dagens labyrint er ens for alle, fordi seed er datoen.
- **Random maze:** ny labyrint hver gang. Dens seed står i pausemenuen. Retry spiller samme labyrint igen, New maze laver en ny.
- Hvert løb starter med en nedtælling 3-2-1-Go. Falder du i et hul, ryster skærmen, og kuglen starter forfra.
- Bedste tid gemmes pr. labyrint (sværhedsgrad + seed) og pr. sværhedsgrad. Dit bedste løb vises som et spøgelse næste gang, og under tiden ser du, om du er foran (grøn, minus) eller bagud (rød, plus).
- Små huller kan man rulle forbi langs væggen. Store huller fylder hele gangen, men med fuld fart og et lige tilløb kan man hoppe over dem.
- På en lille skærm følger kameraet kuglen, og et minikort i hjørnet viser hele banen.
- **Menu** midt i et løb spørger først "Leave run?".

| Platform | Styring |
|---|---|
| Android | Vip telefonen, eller træk joysticken nederst til venstre |
| Windows | Piletaster eller WASD, eller træk joysticken med musen (slås til i pausemenuen). `P` eller `Esc` pauser |

Spillet pauser, når appen skjules, og fortsætter først, når du trykker Resume.

Leaderboard og Account er "Coming soon", indtil API'et er klar.

Design: "Neon Arcade" (natblå, turkis, orange kugle, guld mål). Lavet af RedSkia.Dev.
