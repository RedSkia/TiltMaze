// Små hjælpere til spillet, som Blazor ikke kan selv
window.tiltMaze = {
    // Fortæller C# banens størrelse i pixels, hver gang den ændres (vinduet skaleres eller telefonen drejes)
    watchSize: function (element, dotnet) {
        const observer = new ResizeObserver(function () {
            if (!element.isConnected) {
                observer.disconnect();   // siden er lukket
                return;
            }
            dotnet.invokeMethodAsync('BoardResized', element.clientWidth, element.clientHeight);
        });
        observer.observe(element);
    },

    // Joysticken får fingerens/musens bevægelser, også når den kommer uden for ringen
    capture: function (element, pointerId) {
        try {
            element.setPointerCapture(pointerId);
        } catch {
            // fingeren er allerede løftet
        }
    },

    // Lyde: laves med Web Audio, så der ikke er lydfiler. Alle er korte og lave.
    sound: {
        context: null,

        // Én fælles AudioContext. Den oprettes første gang, og skal genoptages efter et tryk (kravet i WebView)
        ctx: function () {
            if (!this.context)
                this.context = new AudioContext();
            if (this.context.state === 'suspended')
                this.context.resume();
            return this.context;
        },

        // Én tone: bølgeform, start- og slutfrekvens (Hz), hvornår den starter (sek), hvor længe, og hvor højt
        tone: function (type, from, to, start, length, volume) {
            const c = this.ctx();
            const osc = c.createOscillator();
            const gain = c.createGain();
            const t = c.currentTime + start;
            osc.type = type;
            osc.frequency.setValueAtTime(from, t);
            osc.frequency.linearRampToValueAtTime(to, t + length);
            gain.gain.setValueAtTime(volume, t);
            gain.gain.exponentialRampToValueAtTime(0.0001, t + length);   // lyden klinger ud
            osc.connect(gain);
            gain.connect(c.destination);
            osc.start(t);
            osc.stop(t + length);
        },

        // Mønt: to lyse firkant-toner, B5 og E6
        coin: function () { this.tone('square', 988, 988, 0, 0.07, 0.05); this.tone('square', 1319, 1319, 0.07, 0.15, 0.05); },
        // Fald: sinus der falder fra 600 Hz til 100 Hz
        fall: function () { this.tone('sine', 600, 100, 0, 0.45, 0.12); },
        // Nedtælling: kort sinus-bip på A4
        countdown: function () { this.tone('sine', 440, 440, 0, 0.12, 0.1); },
        // Go: højere og længere sinus-bip på A5
        go: function () { this.tone('sine', 880, 880, 0, 0.3, 0.1); },
        // Mål: trekant-toner op gennem C-dur, C5 E5 G5 C6
        finish: function () {
            [523, 659, 784, 1047].forEach((f, i) => this.tone('triangle', f, f, i * 0.1, 0.18, 0.12));
        },
        // Ny rekord: længere fanfare i trekant-toner, C5 E5 G5 C6 G5 C6 E6
        record: function () {
            [523, 659, 784, 1047, 784, 1047, 1319].forEach((f, i) => this.tone('triangle', f, f, i * 0.12, i == 6 ? 0.5 : 0.18, 0.12));
        },
        // Køb: to hurtige firkant-toner, G5 og C6, som en kasse
        buy: function () { this.tone('square', 784, 784, 0, 0.08, 0.05); this.tone('square', 1047, 1047, 0.08, 0.2, 0.05); }
    }
};
