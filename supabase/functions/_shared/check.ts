// Anti-snyd: port af RunChecker.cs, linje for linje. Tjekker at et spøgelse kan være en rigtig tur
// på labyrinten (sværhedsgrad + seed). Serveren bygger selv labyrinten, så appen ikke bestemmer reglerne.
import {
    BALL_RADIUS,
    Course,
    GHOST_RATE,
    JUMP_SPEED,
    MAX_SPEED,
    MAX_TIME_OVER_HOLE,
    WALL_HALF_WIDTH,
} from "./course.ts";

const DT = 1 / GHOST_RATE;
const MAX_STEP = MAX_SPEED * (DT + 0.03);   // længste skridt mellem to målinger (lidt luft til et langsomt billede)
const MAX_TOTAL_FACTOR = 1.02;              // hele turen må højst være maks-fart x tiden (+ 2 %)
const START_TOLERANCE = 0.1;                // kuglen starter (og starter igen efter et hul) lige ved startcellens midte
const WALL_TOLERANCE = 0.02;                // luft til at tallene er afrundet til 32 bit
const GOAL_TOLERANCE = MAX_STEP;            // sidste måling er taget op til 1/30 s før kuglen nåede målet
export const MAX_SAMPLES = 30 * 60 * 30;    // højst 30 minutter
const MAX_SAMPLES_IN_HOLE = Math.floor(MAX_TIME_OVER_HOLE * GHOST_RATE) + 4;

// Giver null, hvis turen er i orden, ellers en kort grund (kun til log og test, aldrig til brugeren).
// ghostBase64 er float32 (lille endian) x0, y0, x1, y1, ...
export function check(difficulty: number, seed: number, timeMs: number, ghostBase64: string): string | null {
    if (!Number.isInteger(difficulty) || difficulty < 0 || difficulty > 3) return "bad difficulty";
    if (!Number.isInteger(seed) || seed < 0 || seed > 2147483647) return "bad seed";
    if (!Number.isInteger(timeMs)) return "bad time";
    if (typeof ghostBase64 != "string") return "bad ghost";

    // Tjek længden før vi pakker ud, så en kæmpe tekst ikke koster noget
    if (ghostBase64.length > Math.ceil(MAX_SAMPLES * 8 / 3) * 4) return "too long";
    const ghost = readGhost(ghostBase64);
    if (ghost == null) return "bad base64";

    const course = new Course(difficulty, seed);
    return checkRun(course, timeMs, ghost);
}

function readGhost(base64: string): number[] | null {
    let text: string;
    try {
        text = atob(base64);
    } catch {
        return null;
    }
    if (text.length % 4 != 0) return null;

    const bytes = new DataView(new ArrayBuffer(text.length));
    for (let i = 0; i < text.length; i++) {
        bytes.setUint8(i, text.charCodeAt(i));
    }
    const ghost: number[] = [];
    for (let i = 0; i < text.length; i += 4) {
        ghost.push(bytes.getFloat32(i, true));
    }
    return ghost;
}

function checkRun(course: Course, timeMs: number, ghost: number[]): string | null {
    if (timeMs < 1 || ghost.length < 4 || ghost.length % 2 != 0) return "bad length";
    const count = ghost.length / 2;
    if (count > MAX_SAMPLES) return "too long";

    // Antal målinger skal passe til tiden (±1 billede)
    const expected = Math.floor(timeMs * 30 / 1000) + 1;
    if (Math.abs(count - expected) > 1) return "time does not match ghost length";

    for (const value of ghost) {
        if (!Number.isFinite(value)) return "bad number";
    }

    const startX = course.start.x + 0.5;
    const startY = course.start.y + 0.5;
    if (distance(ghost[0], ghost[1], startX, startY) > START_TOLERANCE) return "does not start at the start";

    let total = 0;          // samlet vej
    let samplesInHole = 0;  // hvor mange målinger i træk kuglen har været inde i et hul

    for (let i = 0; i < count; i++) {
        const x = ghost[i * 2];
        const y = ghost[i * 2 + 1];

        if (x < 0 || y < 0 || x > course.width || y > course.height) return "outside the maze";
        if (insideWall(course, x, y)) return "inside a wall";

        if (i > 0) {
            const px = ghost[i * 2 - 2];
            const py = ghost[i * 2 - 1];
            const step = distance(x, y, px, py);

            if (step > MAX_STEP) {
                // Eneste lovlige hop: kuglen faldt i et hul og er tilbage ved start
                if (distance(x, y, startX, startY) > START_TOLERANCE || !holeNear(course, px, py)) {
                    return "moved too far between two points";
                }
            } else {
                total += step;
            }

            // Fald: ligger kuglen i et hul uden fart, skal næste måling være tilbage ved start
            if (holeInside(course, x, y) && i < count - 1 && step * GHOST_RATE < JUMP_SPEED * 0.25) {
                const nx = ghost[i * 2 + 2];
                const ny = ghost[i * 2 + 3];
                if (distance(nx, ny, startX, startY) > START_TOLERANCE) return "stopped in a hole";
            }
        }

        if (holeInside(course, x, y)) {
            samplesInHole++;
            if (samplesInHole > MAX_SAMPLES_IN_HOLE) return "too long over a hole";
        } else {
            samplesInHole = 0;
        }
    }

    // Hele turen må ikke være hurtigere end maks-fart
    const seconds = (count - 1) * DT;
    if (total > MAX_SPEED * seconds * MAX_TOTAL_FACTOR + 0.5) return "faster than the maximum speed";

    // Slutter i målet
    const goalX = course.goal.x + 0.5;
    const goalY = course.goal.y + 0.5;
    if (distance(ghost[ghost.length - 2], ghost[ghost.length - 1], goalX, goalY) > 0.25 + GOAL_TOLERANCE) {
        return "does not end in the goal";
    }

    return null;
}

function distance(x1: number, y1: number, x2: number, y2: number): number {
    const dx = x1 - x2;
    const dy = y1 - y2;
    return Math.sqrt(dx * dx + dy * dy);
}

// Er kuglens midte så tæt på en væg, at den ville være skubbet ud af den?
function insideWall(course: Course, x: number, y: number): boolean {
    const minDistance = BALL_RADIUS + WALL_HALF_WIDTH - WALL_TOLERANCE;
    for (const wall of course.segments) {
        const dx = wall.x2 - wall.x1;
        const dy = wall.y2 - wall.y1;
        let t = ((x - wall.x1) * dx + (y - wall.y1) * dy) / (dx * dx + dy * dy);
        t = Math.min(Math.max(t, 0), 1);
        if (distance(x, y, wall.x1 + t * dx, wall.y1 + t * dy) < minDistance) return true;
    }
    return false;
}

// Er kuglens midte inde i et hul (lidt indenfor kanten, så afrunding ikke giver falsk alarm)?
function holeInside(course: Course, x: number, y: number): boolean {
    for (const hole of course.holes) {
        if (distance(x, y, hole.centerX, hole.centerY) < hole.fallRadius - 0.02) return true;
    }
    return false;
}

// Er der et hul kuglen kan være faldet i, når den var her (et skridt fra hullet)?
function holeNear(course: Course, x: number, y: number): boolean {
    for (const hole of course.holes) {
        if (distance(x, y, hole.centerX, hole.centerY) < hole.fallRadius + MAX_STEP) return true;
    }
    return false;
}
