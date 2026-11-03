# TODO

- [ ] Aflever `docs/TiltMaze-kravspec.pdf`
- [ ] Prøv på rigtig telefon (OnePlus 6): retning og følsomhed af vip, landskab, hop over huller og trækket mod hullerne (er Hard/Expert fair?), flydende joystick, kamera og minikort
- [ ] Prøv fingeraftryk på telefonen og Windows Hello på PC (start låst, annuller = gæst, Unlock på Account)
- [ ] Slet den gamle API-database med e-mail-konti før næste test: `docker compose down -v` (konti er nu brugernavn + kodeord)
- [ ] Mål billedhastighed på telefon med 2.5D-banen, kamera og minikort (kameraet ændrer `viewBox` hvert billede; plan B: `<canvas>`)
- [ ] Indtast eller del en seed (nu kun via adressen `/play/{sværhedsgrad}/{seed}`)
- [ ] Broer med touch/mus (Must i kravspec)
- [ ] Hent egen bedste tur fra serveren ved ny telefon/ominstallation (nu kun lokalt spøgelse)
- [ ] Prøv online-flowet på rigtig telefon (cert, `adb reverse`, kø uden net, andres spøgelser i banen)
- [ ] Rekorder i køen hører til enheden, ikke kontoen: skifter man konto, sendes de til den nye (overvej at rydde køen ved log ud)
- [ ] Kalibrering af vip
- [ ] Vibration ved væg, stjerner på banen
- [ ] Brugertest med mindst én person
- Fravalgt: passkeys. WebAuthn virker ikke i Android WebView/BlazorWebView; det kræver Androids Credential Manager i native kode, Digital Asset Links på et rigtigt domæne og et FIDO2-bibliotek på serveren. Det kan ikke gøres simpelt og robust her, så fingeraftryk låser i stedet det gemte login op.
