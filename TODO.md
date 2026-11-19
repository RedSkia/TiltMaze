# TODO
- [x] DESIGN.md omskrevet (letlæseligt, alt relevant)
- [x] docs/TiltMaze.pptx omskrevet (20 slides: seed, fysik, spøgelse og løb forklaret trin for trin, talenoter)
- [x] Dokumentation (docs/TiltMaze-dokumentation.pdf) og studieguide (docs/TiltMaze-teknik-forklaret.pdf) lavet; TiltMaze-kravspec.pdf er beholdt men forældet (eget API/Docker)
- [ ] Aflever dagen før: Git-link (inviter læreren), README, dokumentation-PDF, slides.
- [ ] Mangler mod kravene: lyst tema, kontrast 4,5:1 og 200 % tekst, gitleaks/CI, skærmlæser-test, brugertest med én person mere (far har testet), rotér service_role


- [ ] Prøv på rigtig telefon (OnePlus 6): retning og følsomhed af vip, landskab, hop over huller og trækket mod hullerne (er Hard/Expert fair?), flydende joystick og kamera
- [ ] Prøv fingeraftryk på telefonen og Windows Hello på PC (start låst, annuller = gæst, Unlock på Account)
- [ ] Prøv Google-login på telefonen (virker på Windows): ny bruger får Gmail-navnet, skift navn på Account, log ud, log ind igen, slet konto, annuller Google-vinduet (pæn besked)
- [ ] Rotér `service_role`-nøglen (Project Settings → API Keys); den stod i en log
- [ ] Mål billedhastighed på telefon med 2.5D-banen og kamera (kameraet ændrer `viewBox` hvert billede; plan B: `<canvas>`)
- [ ] Prøv fog (Hard/Expert), stjernegrænser (`Rewards.Stars`) og achievements på telefon; justér tallene, hvis det er for let eller svært
- [ ] Broer med touch/mus (Must i kravspec)
- [ ] Hent egen bedste tur fra serveren ved ny telefon/ominstallation (nu kun lokalt spøgelse)
- [ ] Prøv online-flowet på rigtig telefon (kø uden net, andres spøgelser i banen)
- [ ] Rekorder i køen hører til enheden, ikke kontoen: skifter man konto, sendes de til den nye (overvej at rydde køen ved log ud)
- [ ] Kalibrering af vip
- [ ] Vibration ved væg, stjerner på banen
- [ ] Brugertest med mindst én person
- Fravalgt: passkeys. WebAuthn virker ikke i Android WebView/BlazorWebView; det kræver Androids Credential Manager i native kode, Digital Asset Links på et rigtigt domæne og et FIDO2-bibliotek på serveren. Det kan ikke gøres simpelt og robust her, så fingeraftryk låser i stedet det gemte login op.
