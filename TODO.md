# TODO

- [ ] Aflever `docs/TiltMaze-kravspec.pdf`
- [ ] Prøv på rigtig telefon (OnePlus 6): retning og følsomhed af vip, landskab, hop over huller, joystick, kamera og minikort
- [ ] Mål billedhastighed på telefon med 2.5D-banen, kamera og minikort (kameraet ændrer `viewBox` hvert billede; plan B: `<canvas>`)
- [ ] Indtast eller del en seed (nu kun via adressen `/play/{sværhedsgrad}/{seed}`)
- [ ] Broer med touch/mus (Must i kravspec)
- [ ] Hent egen bedste tur fra serveren ved ny telefon/ominstallation (nu kun lokalt spøgelse)
- [ ] Prøv online-flowet på rigtig telefon (cert, `adb reverse`, kø uden net, andres spøgelser i banen)
- [ ] Rekorder i køen hører til enheden, ikke kontoen: skifter man konto, sendes de til den nye (overvej at rydde køen ved log ud)
- [ ] Computer-spilleren i `TiltMaze.Api.Tests/Player.cs` klarer kun Easy uden fald; tests af tur på Normal/Hard/Expert og med fald i løbet
- [ ] Kalibrering af vip
- [ ] Vibration ved væg, stjerner på banen
- [ ] Brugertest med mindst én person
