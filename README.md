# TiltMaze

Labyrintspil: styr en kugle ved at vippe telefonen (Android), med joysticken på skærmen eller med piletaster/WASD (Windows). Undgå hullerne og nå målet så hurtigt som muligt. Rul hurtigt, så kan kuglen hoppe over hullerne.

Skoleprojekt i App-programmering 2026 (.NET MAUI Blazor Hybrid). Med API, konti og top-lister (Docker). Senere: broer med touch.

- Plan og kravspec: `docs/plan.md`, wireframes: `docs/wireframes.html`, PDF: `docs/TiltMaze-kravspec.pdf`
- Opbygning: `DESIGN.md`

## Kom i gang

Krav: .NET 10 SDK og MAUI-workload (`dotnet workload install maui`). Android kræver Android SDK.

```
dotnet run --project TiltMaze.App -f net10.0-windows10.0.19041.0     # Windows
dotnet build TiltMaze.App -t:Run -f net10.0-android                    # Android (telefon eller emulator)
dotnet test                                                            # tests af labyrint, fysik og API
```

## Spillet

Vælg Easy, Normal, Hard eller Expert på startsiden (valget huskes), og tryk den store **Play daily** eller **Random maze**. Sværhedsgraden bestemmer størrelse, antal løkker (alternative veje) og antal og størrelse af huller.

- **Daily maze:** dagens labyrint er ens for alle, fordi seed er datoen.
- **Random maze:** ny labyrint hver gang. Dens seed står i pausemenuen. Retry spiller samme labyrint igen, New maze laver en ny.
- Hvert løb starter med en nedtælling 3-2-1-Go. Falder du i et hul, ryster skærmen, og kuglen starter forfra.
- Bedste tid gemmes pr. labyrint (sværhedsgrad + seed) og pr. sværhedsgrad. Dit bedste løb vises som et spøgelse næste gang, og under tiden ser du, om du er foran (grøn, minus) eller bagud (rød, plus). Er du logget ind, ræser du også mod de 3 bedste andre (halvgennemsigtige kugler med kaldenavn).
- Små huller kan man rulle forbi langs væggen. Store huller fylder hele gangen, men med fuld fart og et lige tilløb kan man hoppe over dem.
- På en lille skærm følger kameraet kuglen, og et minikort i hjørnet viser hele banen.
- **Menu** midt i et løb spørger først "Leave run?".

| Platform | Styring |
|---|---|
| Android | Vip telefonen, eller træk joysticken nederst til venstre |
| Windows | Piletaster eller WASD, eller træk joysticken med musen (slås til i pausemenuen). `P` eller `Esc` pauser |

Spillet pauser, når appen skjules, og fortsætter først, når du trykker Resume.

Design: "Neon Arcade" (natblå, turkis, orange kugle, guld mål). Lavet af RedSkia.Dev.

## Online: konto, top-liste og spøgelser

Spillet virker helt uden net. Med en konto (e-mail, kodeord og kaldenavn) sendes dine rekorder til serveren, der viser en top 10 pr. labyrint (`/leaderboard`: vælg sværhedsgrad og dagens labyrint eller en seed). Serveren afspiller hver tur mod samme labyrint og fysik og afviser snyd ("Run could not be verified"). Rekorder sat uden net eller login venter i en kø og sendes automatisk senere. Kaldenavn, log ud og slet konto ligger under `/account`.

### Kør API'et lokalt

Krav: Docker og et udviklingscertifikat til HTTPS.

1. Lav certifikatet (vælg et kodeord): `dotnet dev-certs https -ep "$env:USERPROFILE\.aspnet\https\tiltmaze.pfx" -p <kodeord> --trust`
2. Kopiér `.env.example` til `.env` og skriv kodeordet: `CERT_PASSWORD=<kodeord>`. `.env` ligger ikke i git. Ligger `.pfx`-filen et andet sted, sæt `CERT_DIR=<mappen>` i `.env`.
3. Start: `docker compose up -d --build` (API på `https://localhost:7065`, data i volumen `tiltmaze-data`).
4. Stop: `docker compose down` (data bliver). Test uden Docker: `dotnet test`.

Android-telefonen når API'et via `adb reverse tcp:7065 tcp:7065`, som et Debug-build sætter op (det starter også `docker compose up`). Ændr `ApiSettings.BaseUrl`, når API'et kører på en rigtig server.

### Drift

- Databasen (`tiltmaze.db`) og nøglerne til login-tokens (`keys`) ligger i volumen `tiltmaze-data`. Backup: kopiér volumen.
- Rate limits: 20 tider i minuttet pr. bruger, 60 opslag i minuttet, 20 logins i minuttet pr. IP.
- Slet en bruger: `DELETE /api/account` (fra appen) fjerner konto, tider og spøgelser.
