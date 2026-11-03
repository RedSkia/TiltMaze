# TODO

- [ ] Aflever `docs/TiltMaze-kravspec.pdf`
- [ ] Prøv på rigtig telefon (OnePlus 6): retning og følsomhed af vip, landskab, hop over huller, joystick, kamera og minikort
- [ ] Mål billedhastighed på telefon med 2.5D-banen, kamera og minikort (kameraet ændrer `viewBox` hvert billede; plan B: `<canvas>`)
- [ ] Indtast eller del en seed (nu kun via adressen `/play/{sværhedsgrad}/{seed}`)
- [ ] Broer med touch/mus (Must i kravspec)
- [ ] SQLite i stedet for Preferences: bedste tider
- [ ] Leaderboard- og Account-siderne er pladsholdere ("Coming soon"); bygges på API-grenen
- [ ] API-projekt: highscores pr. sværhedsgrad + seed, konti og login (genbrug fra Pulsify), global top 10 og løb mod andres spøgelser (upload og tjek af spøgelset på serveren). `INTERNET`-tilladelse i Android-manifestet
- [ ] Ræs mod andres spøgelser i Game: `Api.GetTopGhosts(difficulty, seed)` er klar, mangler tegning af op til 3 kugler
- [ ] Hent egen bedste tur fra serveren ved ny telefon/ominstallation (nu kun lokalt spøgelse)
- [ ] Prøv online-flowet på rigtig telefon (cert, `adb reverse`, kø uden net)
- [ ] Rekorder i køen hører til enheden, ikke kontoen: skifter man konto, sendes de til den nye (overvej at rydde køen ved log ud)
- [ ] Computer-spilleren i `TiltMaze.Api.Tests/Player.cs` klarer kun Easy uden fald; tests af tur på Normal/Hard/Expert og med fald i løbet
- [ ] Kalibrering af vip
- [ ] Vibration ved væg, stjerner på banen
- [ ] Brugertest med mindst én person
