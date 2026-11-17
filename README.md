# TiltMaze

Labyrintspil: styr en kugle ved at vippe telefonen (Android), ved at trække hvor som helst på banen (touch eller mus). Undgå hullerne og nå målet så hurtigt som muligt. Rul hurtigt, så kan kuglen hoppe over hullerne.

Skoleprojekt i App-programmering 2026 (.NET MAUI Blazor Hybrid). Med konti og top-lister i Supabase. Senere: broer med touch.

- Plan og kravspec: `docs/plan.md`, wireframes: `docs/wireframes.html`, PDF: `docs/TiltMaze-kravspec.pdf`
- Opbygning: `DESIGN.md`

## Kom i gang

Krav: .NET 10 SDK og MAUI-workload (`dotnet workload install maui`). Android kræver Android SDK.

Nemmest (dobbeltklik eller skriv i en terminal i projektmappen):

```
build              # tests + appen til Windows og Android (Release)
deploy             # appen på telefonen (USB, USB-fejlfinding slået til)
deploy windows     # Windows-appen
```

Det samme med kommandoerne direkte:

```
dotnet run --project TiltMaze.App -f net10.0-windows10.0.19041.0     # Windows
dotnet build TiltMaze.App -t:Run -f net10.0-android                    # Android (telefon eller emulator)
dotnet test                                                            # tests af labyrint, fysik og anti-snyd
```

Telefon første gang: Indstillinger → Om telefonen → tryk 7 gange på Build-nummer, slå USB-fejlfinding til i Udviklerindstillinger, vælg "Filoverførsel" ved USB og tryk "Tillad" på telefonen.

Vis telefonen på PC (fx til fremvisning): `winget install Genymobile.scrcpy`, sæt telefonen i med USB (USB-fejlfinding slået til) og skriv `scrcpy`.

## Præsentation

`docs/TiltMaze.pptx` (10 slides, dansk, med talenoter). Til fremvisning på telefonen: se slide 9 (scrcpy).

## Spillet

Vælg Easy, Normal, Hard eller Expert på startsiden (valget huskes), og tryk den store **Play daily** eller **Random maze**. Sværhedsgraden bestemmer størrelse, antal løkker (alternative veje) og antal og størrelse af huller.

- **Daily maze:** dagens labyrint er ens for alle, fordi seed er datoen.
- **Random maze:** ny labyrint hver gang. Dens seed står i pausemenuen. Retry spiller samme labyrint igen, og New maze (efter målet) laver en ny.
- Hvert løb starter med en nedtælling 3-2-1-Go. Falder du i et hul, ryster skærmen, og kuglen starter forfra.
- Bedste tid gemmes pr. labyrint (sværhedsgrad + seed) og pr. sværhedsgrad. Dit bedste løb vises som et spøgelse næste gang, og under tiden ser du, om du er foran (grøn, minus) eller bagud (rød, plus). Er du logget ind, ræser du også mod de 3 bedste andre (halvgennemsigtige kugler med brugernavn).
- Små huller kan man rulle forbi langs væggen. Store huller fylder hele gangen, men med (næsten) fuld fart og et lige tilløb kan man hoppe over dem. Tæt på et hul trækkes kuglen svagt ind mod det. Hard og Expert har flere huller, også på vejen. Der er altid en vej uden om de store huller.
- På en lille skærm følger kameraet kuglen.
- **Stjerner (1-3)** efter hver labyrint ud fra tiden mod korteste vej, **dagsstreak** (dage i træk, +5 mønter) og 12 **achievements** med mønter (gitter under Account).
- **Fog:** på Hard ser man kun et stykke omkring kuglen, på Expert endnu mindre. Kuglen vibrerer, når den rammer en væg.
- **Seed:** skriv en seed på startsiden for at spille en bestemt labyrint, og tryk "Copy seed" efter målet for at dele den.
- **Pausemenuen** (`Esc`, `P` eller menu-knappen) har Resume, Retry og Leave. Leave afslutter løbet uden flere spørgsmål.
- **Lyd:** korte arkadelyde (mønt, fald, nedtælling, mål, ny rekord, pause, dunk mod væg, klik på knapper, køb og valg i butikken), lavet med Web Audio uden lydfiler. Slås til/fra i pausemenuen eller på Account.
- **Mønter:** et par stykker pr. labyrint (flest på Expert), ofte uden for den korteste vej. Hver mønt giver 1 mønt første gang, at klare en labyrint giver 5-40 første gang, og en plads på top 10 / top 3 giver 25 / 50 én gang. Mønterne bruges i **Shop** til udseende: kuglefarver, spor bag kuglen, mønstre på kuglen, effekter ved mål og bane-temaer (kun pynt, ændrer ikke spillet).

| Platform | Styring |
|---|---|
| Android | Vip telefonen ELLER tryk og træk på banen (joystick). Vælg i pausemenuen eller på Account, aldrig begge på én gang |
| Windows | Træk med musen på banen (joystick). Tastaturet styrer ikke kuglen. `P` eller `Esc` pauser |

Spillet pauser, når appen skjules, og fortsætter først, når du trykker Resume.

Design: "Neon Arcade" (natblå, turkis, orange kugle, guld mål). Lavet af RedSkia.Dev.

## Online: konto, top-liste og spøgelser

Spillet virker helt uden net. Med en konto (brugernavn og kodeord, ingen e-mail, eller Google; brugernavnet står på top-listen og skal være ledigt) sendes dine rekorder til serveren, der viser en top 10 pr. labyrint (`/leaderboard`: vælg sværhedsgrad og dagens labyrint eller en seed). Serveren afspiller hver tur mod samme labyrint og fysik og afviser snyd ("Run could not be verified"). Rekorder sat uden net eller login venter i en kø og sendes automatisk senere. Skift brugernavn, lås op med fingeraftryk/Windows Hello, log ud og slet konto ligger under `/account`. Efter første login spørger appen én gang, om den skal låses op med fingeraftryk næste gang.

### Supabase (backend)

Ingen egen server: appen taler direkte med Supabase (konti, tider, top-lister). Kun anti-snyd kører som Edge Function (`supabase/functions/submit-run`, TypeScript).

1. Opret et projekt på supabase.com. Under Authentication: slå e-mailbekræftelse fra og sæt mindste kodeord til 8 (står også i `supabase/config.toml`).
2. Kør migrationerne i `supabase/migrations/` i rækkefølge (0001-0004) i SQL Editor, eller uden DB-kodeord med `npx supabase db query --remote=<ref> -f <fil> --yes` og bagefter `npx supabase migration repair 0001 0002 0003 0004 --status applied --remote=<ref> --yes`.
3. Udgiv funktionen: `npx supabase functions deploy submit-run`.
4. Skriv projektets URL og den offentlige publishable key (`sb_publishable_...`, Settings → API Keys) i `TiltMaze.App/Services/ApiSettings.cs`. Den er offentlig og sikker, fordi tabellerne har RLS og kun RPC'er giver adgang. Appen sender den kun i headeren `apikey`; `Authorization: Bearer` kun med brugerens eget token. Brug aldrig service-/secret-nøglen eller et adgangstoken (PAT) i appen; PAT bruges kun af CLI'en til deploy.
5. Test anti-snyd: `node --test supabase/functions/tests/check.test.ts`.
6. Ende-til-ende-test (langsom, kun før release): lokalt (kræver Docker) `npx supabase start`, så `node supabase/tests/e2e.mjs` (konto, login, tider, top-liste, spøgelser, RLS, navneskift, sletning), til sidst `npx supabase stop`. Mod det hostede projekt: sæt `SUPABASE_URL` og `SUPABASE_ANON_KEY` (publishable key) og kør samme fil (tager et par minutter pga. Auths grænser). Ligger projektet på Skrivebordet, blokerer Windows Defender CLI'en i at skrive i `supabase/`: kopiér mappen udenfor og brug `--workdir <kopi>`.

### Google-login (Android og Windows)

Man kan også trykke "Continue with Google" på Login. På Android sker login i en Custom Tab (Chrome), aldrig i appens eget vindue, og Supabase sender en engangskode tilbage til `com.redskia.tiltmaze://login-callback`. På Windows åbnes den almindelige browser, og appen lytter kort på `http://127.0.0.1:53682/` efter koden (WebAuthenticator virker ikke i en Windows-app uden pakke). Begge steder bytter appen koden til tokens (PKCE). En ny Google-bruger får brugernavnet fra Gmail-adressen (det før @, kun bogstaver, tal og `_`, højst 16; er det for kort eller optaget, får det `_` + 4 tegn bagpå) og kan skifte det på Account. Google-brugere har intet kodeord.

1. Google Cloud Console → APIs & Services → OAuth consent screen: app-navn TiltMaze, scopes `email`, `profile`, `openid`.
2. Credentials → Create credentials → OAuth client ID → type **Web application**. Authorized redirect URI: `https://<ref>.supabase.co/auth/v1/callback`. Kopiér Client ID og Client Secret.
3. Supabase → Authentication → Sign In / Providers → Google: slå til, indsæt Client ID og Secret, gem.
4. Supabase → Authentication → URL Configuration: Redirect URLs skal have `com.redskia.tiltmaze://login-callback` og `http://127.0.0.1:53682/` (står i `supabase/config.toml`; lægges på med `npx supabase config push`, der kun ændrer det, filen nævner; se `npx supabase config diff` først).

### Sikkerhed

- Appen har kun den offentlige publishable key; RLS og RPC-rettigheder bestemmer, hvad man må. `runs` kan kun nås via RPC'er, og en bruger kan kun læse sin egen række i `profiles`.
- Login-e-mailen kan ikke ændres til et andet brugernavns e-mail (trigger i `0004_email_guard.sql`). Log ud ugyldiggør også login'et på serveren.
- Android: kun launcheren (`MainActivity`) og Google-callback (`WebAuthenticationCallbackActivity`) er `exported=true`; alt andet i det samlede manifest er `false` (bibliotekets `ProfileInstallReceiver` kræver `android.permission.DUMP`, dvs. kun adb). `allowBackup=false`.

### Drift

- Gratis-projekter pauses efter ca. en uge uden brug: åbn dem i dashboardet før en fremvisning.
- Slet en bruger: fra appen (`delete_me`), eller i Authentication i dashboardet. Det fjerner også tider og spøgelser.
- Ændres labyrint eller `RunChecker`-reglerne i C#, skal TypeScript i `supabase/functions/_shared` følge med (`CourseFixtureTests` og `check.test.ts` viser, om de er ens).
