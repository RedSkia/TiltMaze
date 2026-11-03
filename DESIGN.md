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
│  Preferences: bedste tider; spøgelser  │
└────────────────────────────────────────┘
```

Server (API med highscores pr. sværhedsgrad og seed, konti og login) kommer senere.

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
Components/Pages   Start, Game, About             Components/Layout   MainLayout, NavMenu
        │  @inject
        ▼
Services           Scores (bedste tid, spøgelser), TiltSensor (accelerometer)
        │
        ▼
TiltMaze.Core      MazeGenerator, Course, Ball      (ingen MAUI, testes med xUnit)
```


## Spillet

- **Labyrint:** `new Course(sværhedsgrad, seed)`. Samme to værdier giver altid samme labyrint, hvis du ikke ændrer koden. Easy 5x7 til Expert 11x16. Generatoren laver en DFS-labyrint, åbner nogle 2x2-rum og fjerner en del blindgyder (braiding), så der er løkker og alternative veje. Er labyrinten én lang gang, laves den igen. Start er et tilfældigt hjørne, målet er et af de fjerneste steder (bredde-først afstand).
- **Seed:** vises lille i spillet. Random maze = ny tilfældig seed. Daily maze = datoen (UTC) som tal, ens for alle. Retry bruger samme seed, New maze en ny.
- **Huller:** små (radius ca. 0,12-0,17) kan kuglen rulle forbi langs siden af gangen. Store (radius 0,45) fylder hele gangen. Store huller ligger kun på den korteste vej og kun hvis målet stadig kan nås uden om dem (bredde-først tjek), så der er altid en vej uden store huller. Små huller kan ligge overalt undtagen start og mål.
- **Fald og hop:** kuglen falder, hvis dens midte er inde i hullet og farten er under 60 % af maks-farten, eller hvis den er over hullet i mere end 0,2 s. Med fart ruller den derfor hen over små huller og, med fuld fart og et lige tilløb, over store.
- **Tegning:** ét SVG med `viewBox` i celler, så banen skaleres til skærmen med fast billedformat. Set oppefra med højde: gulv med fliser, vægge med lys top, mørk side og skygge (samme `path` tegnet tre gange), huller med dybde, kugle med skygge og glans.
- **Fysik:** `Ball.Update` kører i faste skridt på 1/120 s: fart fra vip, friktion, maks-fart, så kollision mellem kuglens cirkel og hver væg (et linjestykke). Skridtet er meget mindre end en væg er tyk, så kuglen ikke kan gå gennem den. Hul og mål tjekkes efter hvert skridt.
- **Løkke:** `PeriodicTimer` (16 ms) i `Game.razor.cs` flytter kuglen og kalder `StateHasChanged`.
- **Spøgelse:** under et løb gemmes kuglens x og y hvert 1/30 sekund i en liste af tal. Ved ny rekord på labyrinten gemmes listen i en lille fil. Næste gang på samme labyrint tegnes en halvgennemsigtig kugle, der afspiller løbet i takt med uret. Formatet er en simpel `float[]`, så det senere kan sendes til en server.
- **Bedste tid:** pr. (sværhedsgrad, seed) og pr. sværhedsgrad i Preferences. Dagens bedste tid er bare tiden for dagens seed.
- **Styring:** tastatur (WASD, piletaster) og accelerometer (`TiltSensor`, tager højde for skærmens rotation).
- **Pause:** knap, `P`/`Esc`, og automatisk når vinduet skjules (`Window.Stopped`, sensoren slukkes). Spillet fortsætter først, når man trykker Resume.
