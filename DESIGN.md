# TiltMaze – Design

Kort overblik over, hvordan appen er bygget og ser ud.

## Udseende: "Neon Arcade"

Et mørkt arkadespil: natblå baggrund med et svagt blåt lys fra toppen, turkis neon til vægge, knapper og aktiv menu, en orange kugle, et guld mål og koral-rød til fare (huller, fald). Tokens ligger i `wwwroot/app.css` (`--bg`, `--surface*`, `--accent`, `--ball`, `--gold`, `--danger`, afstande, runding, skygger). Kun systemskrifttyper, store tal med `tabular-nums`, så tiden ikke hopper.

- **Knapper:** store arkade-knapper med en mørk kant under (`box-shadow`), der synker ned, når man trykker. Hover gør dem lysere (kun med mus).
- **Feedback:** nedtælling 3-2-1-Go, der "popper"; ved fald ryster banen, kanten blinker rødt, en ring viser start, og telefonen vibrerer; ved mål falder konfetti, og tiden lyser i guld.
- **Mindre bevægelse:** ved `prefers-reduced-motion` er rystelse, konfetti, pop og puls slået fra.
- **Logo og ikon:** turkis labyrint med orange kugle og guld mål på en natblå flise (`images/logo.svg`, `Resources/AppIcon`, `Resources/Splash`).
- **Mærke:** RedSkia.Dev på Om-siden (rødt kort) og "by RedSkia.Dev" nederst på startsiden.

## Skærmene

- **Start:** øverst et stort kort "Today's maze" med dagens og bedste tid og en stor "Play daily"-knap (plus "Random maze"). Under det fire felter, ét pr. sværhedsgrad (størrelse, huller, bedste tid, dagens tid). Det valgte felt lyser og huskes. Bred skærm: kortet og felterne side om side.
- **Spil, telefon i højformat:** HUD øverst (pause, stor tid med forskel til spøgelset, menu og små "chips" med sværhedsgrad, bedste tid og fald). Banen fylder resten. Kan hele banen ikke vises med mindst 40 px pr. celle, følger kameraet kuglen. Der er intet minikort (det overlappede HUD og bane). Joysticken er flydende: den kommer frem, hvor fingeren trykker på banen, og forsvinder, når man slipper.
- **Spil, landskab og computer:** hele banen og et sidepanel med tid, tal, hjælpetekst og knapperne Pause og Retry.
- **Dialoger** (i appen, ikke systemets): Paused (Resume, Retry, Leave; med seed), Maze complete og Delete account? (på Account-siden). "Delete account?" bruger den fælles `ConfirmDialog`. Fokus på første knap, `Esc` lukker, resten af skærmen er `inert`.
- **Leaderboard:** top 10 pr. labyrint (dagens eller en seed), din egen linje er markeret. **Login:** brugernavn og kodeord (ingen e-mail); ved "Create account" vises mens man skriver, om navnet er ledigt. **Account:** skift brugernavn, lås op med fingeraftryk/Windows Hello til/fra, log ud og slet konto (med en in-app dialog). Er appen låst, er der en Unlock-knap.

## Det store billede

```
┌───────────── Telefon / PC ─────────────┐
│  TiltMaze.App (.NET MAUI)              │
│                                        │
│  Blazor-sider (UI i HTML/CSS + SVG)    │
│        │                               │
│        ▼                               │
│  Services (Scores, Api, TiltSensor)    │
│        │                               │
│        ▼                               │
│  TiltMaze.Core (labyrint + fysik)      │
│  SQLite: bedste tider, spøgelser, kø   │
└────────────────────────────────────────┘
```

Serveren (`TiltMaze.Api`, Docker) har konti, top-lister og spøgelser og tjekker hver tid mod den samme labyrint og fysik (anti-snyd). Appen sender tider, når der er net (`RunQueue`).

## Hvordan appen er native

Blazor Hybrid: MAUI starter en almindelig Android- eller Windows-app med én skærm, `MainPage.xaml`, der indeholder en `BlazorWebView`. Den viser vores `.razor`-sider. Koden kører som almindelig .NET inde i appen og kan derfor kalde accelerometer, vibration, SecureStorage og fingeraftryk/Windows Hello (`Biometrics`) direkte.

```
Android: MainApplication.cs ─┐
                             ├─► MauiProgram.cs ─► App.xaml ─► MainPage.xaml ─► BlazorWebView ─► Routes.razor ─► sider
Windows: Platforms/Windows/  ┘   (services          (vindue)    (én skærm)       (wwwroot/        (MainLayout,
         App.xaml                registreres)                                     index.html)       NavMenu)
```

## Lagene

```
Components/Pages   Start, Game, About, ...        Components/Layout   MainLayout, NavMenu
Components/Play    MazeLayer, Joystick            wwwroot/js/game.js  størrelse og pointer capture
        │  @inject
        ▼
Services           Scores + GameDatabase (bedste tid, spøgelser), Api, Auth, Biometrics, RunQueue, TiltSensor
        │
        ▼
TiltMaze.Core      MazeGenerator, Course, Ball, Camera, Ghost   (ingen MAUI, testes med xUnit)
```

## Spillet

- **Labyrint:** `new Course(sværhedsgrad, seed)`. Samme to værdier giver altid samme labyrint, hvis du ikke ændrer koden. Easy 5x7 til Expert 11x16. Generatoren laver en DFS-labyrint, åbner nogle 2x2-rum og fjerner en del blindgyder (braiding), så der er løkker og alternative veje. Er labyrinten én lang gang, laves den igen. Start er et tilfældigt hjørne, målet er et af de fjerneste steder (bredde-først afstand).
- **Seed:** vises i sidepanelet og i pausemenuen. Random maze = ny tilfældig seed. Daily maze = datoen (UTC) som tal, ens for alle. Retry bruger samme seed, New maze en ny.
- **Huller:** små (radius ca. 0,13-0,175) kan kuglen rulle forbi langs siden af gangen. Store (radius 0,46) fylder hele gangen. Store huller ligger kun på den korteste vej og kun hvis målet stadig kan nås uden om dem (bredde-først tjek), så der er altid en vej uden store huller. Halvdelen af de små ligger på den vej, resten overalt undtagen start og mål. Hard og Expert har flest (8+5 og 12+8). Tæt på et hul (radius + 0,12) trækkes kuglen svagt ind mod midten; vippet er stærkere, så man kan styre væk.
- **Fald og hop:** kuglen falder, hvis dens midte er inde i hullet og farten er under 75 % af maks-farten, eller hvis den er over hullet i mere end 0,19 s. Med fart ruller den derfor hen over små huller og, med næsten fuld fart (over 93 %) og et lige tilløb, over store.
- **Tegning:** ét SVG med `viewBox` i celler. Set oppefra med højde (2,5D), lys fra øverst til venstre: gulv med turkise fliser og kant, vægge med glans, lys top, mørk side og skygge (samme `path` tegnet fire gange), huller med indre væg, forskudt mørk brønd og rød kant, mål som guld ring med pulserende glød, kugle med glød, skygge og glans, dit eget spøgelse med stiplet kant og andres spøgelser som dæmpede kugler med brugernavn. Den faste del (`MazeLayer`) tegnes kun, når labyrinten skifter.
- **Kamera:** passer hele banen ikke med mindst 40 px pr. celle, viser `viewBox` kun et udsnit (56 px pr. celle), som glider blødt efter kuglen (`Camera.Follow`, samme brøkdel af afstanden pr. tid) og holdes inden for banen.
- **Fysik:** `Ball.Update` kører i faste skridt på 1/120 s: fart fra vip, friktion, maks-fart, så kollision mellem kuglens cirkel og hver væg (et linjestykke). Skridtet er meget mindre end en væg er tyk, så kuglen ikke kan gå gennem den. Hul og mål tjekkes efter hvert skridt.
- **Løkke:** `PeriodicTimer` (16 ms) i `Game.razor.cs` flytter kuglen og kameraet og kalder `StateHasChanged`. Før start tælles 3-2-1-Go ned (kuglen står stille, uret går ikke).
- **Styring:** accelerometer (`TiltSensor`, tager højde for skærmens rotation) og et flydende joystick (`Joystick`: et felt over hele banen med pointer events og pointer capture, så både finger og mus virker; ringen kommer frem, hvor første finger trykker, knappen følger fingeren inden for ringen, og ved slip forsvinder den, og styringen er 0). Man bruger enten vip eller joystick (kontakt i pausemenuen og på Account), aldrig begge. Tastaturet styrer ikke kuglen (for nemt), kun `P`/`Esc` pauser.
- **Spøgelse:** under et løb gemmes kuglens x og y hvert 1/30 sekund i en liste af tal. Optagelsen følger fysikken (hvert fysik-skridt tæller tiden op, og kuglens plads gemmes, hver gang tiden passerer et nyt 1/30 sekund), så langsomme billeder ikke giver dobbelte målinger. Ved ny rekord på labyrinten gemmes listen i SQLite og sendes til serveren. Næste gang på samme labyrint tegnes en halvgennemsigtig kugle, der afspiller løbet i takt med uret. Er man logget ind og online, hentes også de 3 bedste andre ture fra serveren (uden ens egen) og vises som kugler i hver sin dæmpede farve med brugernavnet som lille tekst. Formatet er en simpel `float[]` (som bytes/base64 på nettet), så serveren kan afspille og tjekke det.
- **Bedste tid:** pr. (sværhedsgrad, seed) og pr. sværhedsgrad i SQLite (`GameDatabase`). Dagens bedste tid er bare tiden for dagens seed.
- **Pause:** knap, `P`/`Esc`, og automatisk når vinduet skjules (`Window.Stopped`, sensoren slukkes). Spillet fortsætter først, når man trykker Resume.
- **Forlad løbet:** pausemenuen har Leave, som går direkte til menuen (ingen ekstra spørgsmål).
- **Mønter og butik:** mønter ligger midt i nogle celler (3-9, ikke på start, mål eller huller) og påvirker ikke fysikken. De giver penge i `Wallet` (SQLite), sammen med belønning for at klare en labyrint og bonus for top 10 / top 3. I Shop køber man udseende (farver, spor, mønstre, effekter ved mål og bane-temaer); det ændrer ikke fysik, tid eller spøgelse.
