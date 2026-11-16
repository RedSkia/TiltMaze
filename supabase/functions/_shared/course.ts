// Port af Difficulty.cs og Course.cs (kun det anti-snyd bruger: start, mål, vægge og huller).
// Mønter er ikke med: de trækkes efter hullerne og ændrer hverken vægge eller huller.
import { type Cell, distances, generate, NORTH, EAST, SOUTH, WEST, Rng, openings } from "./maze.ts";

// Tal fra Ball.cs, Course.cs og Ghost.cs
export const BALL_RADIUS = 0.26;
export const WALL_HALF_WIDTH = 0.04;
export const MAX_SPEED = 5;
export const BIG_RADIUS = 0.46;
export const JUMP_SPEED = MAX_SPEED * 0.75;
export const MAX_TIME_OVER_HOLE = 0.19;
export const GHOST_RATE = 30;

export type Segment = { x1: number; y1: number; x2: number; y2: number };

export type Hole = { x: number; y: number; radius: number; centerX: number; centerY: number; fallRadius: number; isBig: boolean };

// Bredde, højde, rum, løkker, små huller, store huller, mindste og største lille hul
const settingsList = [
    { width: 5, height: 7, rooms: 1, braid: 0.7, small: 2, big: 1, smallMin: 0.13, smallMax: 0.15 },
    { width: 7, height: 10, rooms: 2, braid: 0.5, small: 4, big: 2, smallMin: 0.13, smallMax: 0.16 },
    { width: 9, height: 13, rooms: 3, braid: 0.5, small: 8, big: 5, smallMin: 0.14, smallMax: 0.17 },
    { width: 11, height: 16, rooms: 4, braid: 0.5, small: 12, big: 8, smallMin: 0.15, smallMax: 0.175 },
];

function makeHole(x: number, y: number, radius: number): Hole {
    return { x, y, radius, centerX: x + 0.5, centerY: y + 0.5, fallRadius: radius - 0.02, isBig: radius >= BIG_RADIUS };
}

// difficulty: 0 = Easy, 1 = Normal, 2 = Hard, 3 = Expert (som i C#). Samme (sværhedsgrad, seed) giver altid samme labyrint.
export class Course {
    width: number;
    height: number;
    walls: number[][] = [];
    start: Cell = { x: 0, y: 0 };
    goal: Cell = { x: 0, y: 0 };
    holes: Hole[] = [];
    segments: Segment[] = [];

    constructor(difficulty: number, seed: number) {
        const settings = settingsList[difficulty];
        this.width = settings.width;
        this.height = settings.height;

        // Rækkefølgen af kald til random er den samme som i Course.cs
        const random = new Rng(seed);
        this.buildMaze(settings, random);
        this.placeHoles(settings, random);
        this.buildSegments();
    }

    // Er den for kedelig (næsten ingen kryds), prøver vi igen med det næste tal fra samme seed
    buildMaze(settings: typeof settingsList[0], random: Rng) {
        const width = this.width;
        const height = this.height;

        for (let attempt = 0; attempt < 20; attempt++) {
            this.walls = generate(width, height, random.next(), settings.rooms, settings.braid);

            const corners = [{ x: 0, y: 0 }, { x: width - 1, y: 0 }, { x: 0, y: height - 1 }, { x: width - 1, y: height - 1 }];
            this.start = corners[random.nextInt(4)];
            const dist = distances(this.walls, this.start);
            let farthest = 0;
            for (const column of dist) {
                for (const d of column) farthest = Math.max(farthest, d);
            }

            const far: Cell[] = [];
            for (let x = 0; x < width; x++) {
                for (let y = 0; y < height; y++) {
                    if (dist[x][y] >= farthest * 0.9) far.push({ x, y });
                }
            }
            this.goal = far[random.nextInt(far.length)];

            if (this.crossingCount() >= Math.floor(width * height / 6) && dist[this.goal.x][this.goal.y] <= width * height * 0.45) return;
        }
    }

    // Antal celler med 3 eller 4 åbne sider
    crossingCount(): number {
        let count = 0;
        for (const column of this.walls) {
            for (const cell of column) {
                if (openings(cell) >= 3) count++;
            }
        }
        return count;
    }

    // Store huller først (kun på korteste vej, målet skal kunne nås), så små huller
    placeHoles(settings: typeof settingsList[0], random: Rng) {
        const width = this.width;
        const height = this.height;
        const used = emptyGrid(width, height);
        used[this.start.x][this.start.y] = true;
        used[this.goal.x][this.goal.y] = true;
        const blocked = emptyGrid(width, height);   // kun store huller

        let big = 0;
        for (let tries = 0; tries < 300 && big < settings.big; tries++) {
            const cell = { x: random.nextInt(width), y: random.nextInt(height) };
            if (used[cell.x][cell.y] || openings(this.walls[cell.x][cell.y]) < 2) continue;
            if (!this.isOnShortestPath(cell, blocked)) continue;

            blocked[cell.x][cell.y] = true;
            if (distances(this.walls, this.start, blocked)[this.goal.x][this.goal.y] < 0) {
                blocked[cell.x][cell.y] = false;   // ville spærre vejen
                continue;
            }
            used[cell.x][cell.y] = true;
            this.holes.push(makeHole(cell.x, cell.y, BIG_RADIUS));
            big++;
        }

        let small = 0;
        for (let tries = 0; tries < 300 && small < settings.small; tries++) {
            const cell = { x: random.nextInt(width), y: random.nextInt(height) };
            if (used[cell.x][cell.y]) continue;
            const onRoute = small < Math.floor(settings.small / 2) && tries < 200;
            if (onRoute && !this.isOnShortestPath(cell, blocked)) continue;

            used[cell.x][cell.y] = true;
            const radius = settings.smallMin + random.nextDouble() * (settings.smallMax - settings.smallMin);
            this.holes.push(makeHole(cell.x, cell.y, radius));
            small++;
        }
    }

    // Ligger cellen på en korteste vej fra start til mål (uden om de store huller sat indtil nu)?
    isOnShortestPath(cell: Cell, blocked: boolean[][]): boolean {
        const fromStart = distances(this.walls, this.start, blocked);
        const fromGoal = distances(this.walls, this.goal, blocked);
        const d = fromStart[cell.x][cell.y];
        return d >= 0 && d + fromGoal[cell.x][cell.y] == fromStart[this.goal.x][this.goal.y];
    }

    // Alle vægge som linjestykker, hver væg kun én gang
    buildSegments() {
        for (let x = 0; x < this.width; x++) {
            for (let y = 0; y < this.height; y++) {
                const w = this.walls[x][y];
                if (w & NORTH) this.segments.push({ x1: x, y1: y, x2: x + 1, y2: y });
                if (w & WEST) this.segments.push({ x1: x, y1: y, x2: x, y2: y + 1 });
                if (x == this.width - 1 && (w & EAST)) this.segments.push({ x1: x + 1, y1: y, x2: x + 1, y2: y + 1 });
                if (y == this.height - 1 && (w & SOUTH)) this.segments.push({ x1: x, y1: y + 1, x2: x + 1, y2: y + 1 });
            }
        }
    }
}

function emptyGrid(width: number, height: number): boolean[][] {
    const grid: boolean[][] = [];
    for (let x = 0; x < width; x++) {
        grid.push(new Array(height).fill(false));
    }
    return grid;
}
