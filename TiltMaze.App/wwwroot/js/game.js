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
    }
};
