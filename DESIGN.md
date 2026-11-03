# TiltMaze – Design

Kort overblik over, hvordan appen er bygget. Samme opbygning og udseende som Pulsify.

## Det store billede

```
┌───────────── Telefon / PC ─────────────┐
│  TiltMaze.App (.NET MAUI)              │
│                                        │
│  Blazor-sider (UI i HTML/CSS + SVG)    │
│        │                               │
│        ▼                               │
│  Services (Scores, TiltSensor)         │
│        │                               │
│        ▼                               │
│  TiltMaze.Core (labyrint + fysik)      │
│  Preferences: bedste tider             │
└────────────────────────────────────────┘
```

Server (API med baner, highscores og login) kommer senere.

## Hvordan appen er native

Blazor Hybrid: MAUI starter en almindelig Android- eller Windows-app med én skærm, `MainPage.xaml`, der indeholder en `BlazorWebView`. Den viser vores `.razor`-sider. Koden kører som almindelig .NET inde i appen og kan derfor kalde accelerometer, vibration og Preferences direkte.

```
Android: MainApplication.cs ─┐
                             ├─► MauiProgram.cs ─► App.xaml ─► MainPage.xaml ─► BlazorWebView ─► Routes.razor ─► sider
Windows: Platforms/Windows/  ┘   (services          (vindue)    (én skærm)       (wwwroot/        (MainLayout,
         App.xaml                registreres)                                     index.html)       NavMenu)
```

## Lagene

```
Components/Pages   Levels, Game, About            Components/Layout   MainLayout, NavMenu
        │  @inject
        ▼
Services           Scores (bedste tid), TiltSensor (accelerometer)
        │
        ▼
TiltMaze.Core      MazeGenerator, Level, Ball       (ingen MAUI, testes med xUnit)
```

## Spillet

- **Bane:** `new Level(n)` laver en labyrint med seed n. Størrelse vokser fra 5x7 (bane 1) til 10x14 (bane 12). Start øverst til venstre, mål nederst til højre. Huller placeres tilfældigt (også seed), men aldrig så målet bliver spærret.
- **Tegning:** ét SVG med `viewBox` i celler, så banen skaleres til skærmen med fast billedformat. Vægge er ét `path`.
- **Fysik:** `Ball.Update` kører i faste skridt på 1/120 s: fart fra vip, friktion, maks-fart, så kollision mellem kuglens cirkel og hver væg (et linjestykke). Skridtet er meget mindre end en væg er tyk, så kuglen ikke kan gå gennem den.
- **Løkke:** `PeriodicTimer` (16 ms) i `Game.razor.cs` flytter kuglen og kalder `StateHasChanged`.
- **Styring:** tastatur (WASD, piletaster) og accelerometer (`TiltSensor`, tager højde for skærmens rotation).
- **Pause:** knap, `P`/`Esc`, og automatisk når vinduet skjules (`Window.Stopped`).
