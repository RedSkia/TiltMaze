// Kør: node --test supabase/functions/tests
// Facit-filerne er lavet af C# (TiltMaze.Tests/CourseFixtureTests og en tur-generator med Player.cs).
import { test } from "node:test";
import assert from "node:assert/strict";
import courses from "../_shared/fixtures/course.json" with { type: "json" };
import runs from "../_shared/fixtures/runs.json" with { type: "json" };
import { Course } from "../_shared/course.ts";
import { check, MAX_SAMPLES } from "../_shared/check.ts";

test("samme labyrint som i C#", () => {
    for (const expected of courses) {
        const course = new Course(expected.difficulty, expected.seed);

        let wallSum = 0;
        for (const wall of course.segments) wallSum += wall.x1 + wall.y1 + wall.x2 + wall.y2;
        const holes = course.holes.map((h) => [h.x, h.y, h.radius]);
        const name = `difficulty ${expected.difficulty} seed ${expected.seed}`;

        assert.deepEqual([course.start.x, course.start.y], expected.start, name + " start");
        assert.deepEqual([course.goal.x, course.goal.y], expected.goal, name + " goal");
        assert.equal(course.segments.length, expected.walls, name + " walls");
        assert.equal(wallSum, expected.wallSum, name + " wall sum");
        assert.deepEqual(holes, expected.holes, name + " holes");
    }
});

test("rigtige ture er i orden", () => {
    for (const run of runs) {
        assert.equal(check(run.difficulty, run.seed, run.timeMs, run.ghost), null, run.name);
    }
});

// Hjælp: spøgelse som base64 af float32 (lille endian) og tilbage
function toBase64(numbers: number[]): string {
    return Buffer.from(new Float32Array(numbers).buffer).toString("base64");
}

function toNumbers(base64: string): number[] {
    const bytes = Buffer.from(base64, "base64");
    return Array.from(new Float32Array(bytes.buffer, bytes.byteOffset, bytes.length / 4));
}

const run = runs[0];   // Easy 3
const samples = toNumbers(run.ghost);
const course = new Course(run.difficulty, run.seed);

test("teleport afvises", () => {
    const ghost = toBase64([samples[0], samples[1], samples[samples.length - 2], samples[samples.length - 1]]);
    assert.equal(check(run.difficulty, run.seed, 33, ghost), "moved too far between two points");
});

test("gennem væg afvises", () => {
    // Lige linje fra start til mål i små skridt, men gennem væggene
    const startX = course.start.x + 0.5, startY = course.start.y + 0.5;
    const goalX = course.goal.x + 0.5, goalY = course.goal.y + 0.5;
    const steps = Math.ceil(Math.hypot(goalX - startX, goalY - startY) / 0.1);
    const ghost: number[] = [];
    for (let i = 0; i <= steps; i++) {
        ghost.push(startX + (goalX - startX) * i / steps, startY + (goalY - startY) * i / steps);
    }
    assert.equal(check(run.difficulty, run.seed, Math.floor(steps * 1000 / 30), toBase64(ghost)), "inside a wall");
});

test("for hurtig afvises", () => {
    // Hver 4. måling: samme vej, men 4 gange så hurtigt
    const ghost: number[] = [];
    for (let i = 0; i < samples.length / 2; i += 4) ghost.push(samples[i * 2], samples[i * 2 + 1]);
    ghost.push(samples[samples.length - 2], samples[samples.length - 1]);
    const timeMs = Math.floor((ghost.length / 2 - 1) * 1000 / 30);
    assert.notEqual(check(run.difficulty, run.seed, timeMs, toBase64(ghost)), null);
});

test("forkert mål afvises", () => {
    // Samme tur, men slutter i start-cellen
    const ghost = samples.slice(0, samples.length - 2).concat([samples[0], samples[1]]);
    assert.notEqual(check(run.difficulty, run.seed, run.timeMs, toBase64(ghost)), null);
});

test("forkert tid afvises", () => {
    assert.equal(check(run.difficulty, run.seed, run.timeMs * 2, run.ghost), "time does not match ghost length");
    assert.equal(check(run.difficulty, run.seed, 0, run.ghost), "bad length");
});

test("for langt spøgelse afvises", () => {
    const ghost = toBase64(new Array((MAX_SAMPLES + 1) * 2).fill(1));
    assert.equal(check(run.difficulty, run.seed, 1000, ghost), "too long");
});

test("dårlig input afvises", () => {
    assert.equal(check(run.difficulty, run.seed, run.timeMs, "this is not base64!"), "bad base64");
    assert.equal(check(run.difficulty, run.seed, run.timeMs, "AAAAAAAAAAA="), "bad length");
    assert.equal(check(4, run.seed, run.timeMs, run.ghost), "bad difficulty");
    assert.equal(check(run.difficulty, -1, run.timeMs, run.ghost), "bad seed");
    assert.equal(check(run.difficulty, run.seed, 34, toBase64([NaN, 0, 0, 0])), "bad number");
});
