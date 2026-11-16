// Port af Maze.cs og Rng.cs. Skal give præcis samme labyrint som C# for samme seed.
// En celles vægge er et tal: nord 1, øst 2, syd 4, vest 8 (flere kan lægges sammen med |).

export const NORTH = 1;
export const EAST = 2;
export const SOUTH = 4;
export const WEST = 8;
export const ALL = 15;

export type Cell = { x: number; y: number };

// Seedet tilfældighed (mulberry32). Kun 32 bit heltal, så resultatet er det samme som i C#.
export class Rng {
    state: number;

    constructor(seed: number) {
        this.state = seed >>> 0;
    }

    nextUInt(): number {
        this.state = (this.state + 0x6D2B79F5) >>> 0;
        let t = this.state;
        t = Math.imul(t ^ (t >>> 15), t | 1);
        t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
        return (t ^ (t >>> 14)) >>> 0;
    }

    // Et tal fra 0 til og med int.MaxValue
    next(): number {
        return this.nextUInt() >>> 1;
    }

    // Et helt tal fra 0 til n - 1
    nextInt(n: number): number {
        return this.nextUInt() % n;
    }

    // Et tal fra 0 (med) til 1 (uden)
    nextDouble(): number {
        return this.nextUInt() / 4294967296;
    }
}

// Nord, Øst, Syd, Vest. Retning d + 2 er altid modsat side.
const stepX = [0, 1, 0, -1];
const stepY = [-1, 0, 1, 0];
const walls = [NORTH, EAST, SOUTH, WEST];

// grid[x][y] er cellens vægge
export function generate(width: number, height: number, seed: number, roomCount: number, braidChance: number): number[][] {
    const grid: number[][] = [];
    const visited: boolean[][] = [];
    for (let x = 0; x < width; x++) {
        grid.push(new Array(height).fill(ALL));
        visited.push(new Array(height).fill(false));
    }

    const random = new Rng(seed);
    const path: Cell[] = [{ x: 0, y: 0 }];
    visited[0][0] = true;

    while (path.length > 0) {
        const cell = path[path.length - 1];

        const options: number[] = [];
        for (let d = 0; d < 4; d++) {
            const nx = cell.x + stepX[d];
            const ny = cell.y + stepY[d];
            if (inside(grid, nx, ny) && !visited[nx][ny]) options.push(d);
        }

        if (options.length == 0) {
            path.pop();
            continue;
        }

        const direction = options[random.nextInt(options.length)];
        open(grid, cell.x, cell.y, direction);
        const next = { x: cell.x + stepX[direction], y: cell.y + stepY[direction] };
        visited[next.x][next.y] = true;
        path.push(next);
    }

    carveRooms(grid, random, roomCount);
    braid(grid, random, braidChance);
    return grid;
}

// Antal åbne sider på en celle
export function openings(cell: number): number {
    let count = 0;
    for (const wall of walls) {
        if ((cell & wall) == 0) count++;
    }
    return count;
}

// Antal skridt fra start til hver celle (bredde-først). -1 = kan ikke nås. Celler i blocked må ikke bruges.
export function distances(grid: number[][], start: Cell, blocked?: boolean[][]): number[][] {
    const result: number[][] = [];
    for (let x = 0; x < grid.length; x++) {
        result.push(new Array(grid[0].length).fill(-1));
    }

    const queue: Cell[] = [start];
    result[start.x][start.y] = 0;

    for (let i = 0; i < queue.length; i++) {
        const cell = queue[i];
        for (let d = 0; d < 4; d++) {
            const nx = cell.x + stepX[d];
            const ny = cell.y + stepY[d];
            if ((grid[cell.x][cell.y] & walls[d]) != 0 || !inside(grid, nx, ny) || result[nx][ny] >= 0) continue;
            if (blocked && blocked[nx][ny]) continue;

            result[nx][ny] = result[cell.x][cell.y] + 1;
            queue.push({ x: nx, y: ny });
        }
    }
    return result;
}

function inside(grid: number[][], x: number, y: number): boolean {
    return x >= 0 && y >= 0 && x < grid.length && y < grid[0].length;
}

// Fjerner væggen mellem en celle og dens nabo i retning d (fra begge celler)
function open(grid: number[][], x: number, y: number, d: number) {
    grid[x][y] &= ~walls[d];
    grid[x + stepX[d]][y + stepY[d]] &= ~walls[(d + 2) % 4];
}

function carveRooms(grid: number[][], random: Rng, roomCount: number) {
    const width = grid.length;
    const height = grid[0].length;
    if (width < 2 || height < 2) return;

    for (let i = 0; i < roomCount; i++) {
        const x = random.nextInt(width - 1);
        const y = random.nextInt(height - 1);
        open(grid, x, y, 1);
        open(grid, x, y + 1, 1);
        open(grid, x, y, 2);
        open(grid, x + 1, y, 2);
    }
}

// Åbner en ekstra væg i nogle af blindgyderne. NextDouble kaldes kun for blindgyder (som i C#).
function braid(grid: number[][], random: Rng, braidChance: number) {
    for (let x = 0; x < grid.length; x++) {
        for (let y = 0; y < grid[0].length; y++) {
            if (openings(grid[x][y]) != 1 || random.nextDouble() >= braidChance) continue;

            const closed: number[] = [];
            for (let d = 0; d < 4; d++) {
                if ((grid[x][y] & walls[d]) != 0 && inside(grid, x + stepX[d], y + stepY[d])) closed.push(d);
            }
            if (closed.length > 0) open(grid, x, y, closed[random.nextInt(closed.length)]);
        }
    }
}
