// Modtager en tid fra appen, tjekker spøgelset (anti-snyd) og gemmer tiden via databasefunktionen submit_run.
// POST { difficulty, seed, timeMs, ghost } med brugerens login (Authorization: Bearer <JWT>).
import { createClient } from "jsr:@supabase/supabase-js@2";
import { check, MAX_SAMPLES } from "../_shared/check.ts";

// Spøgelset er base64 (4 tegn pr. 3 bytes). Lidt luft til de andre felter.
const MAX_BODY = Math.ceil(MAX_SAMPLES * 8 / 3) * 4 + 1000;

// Kun service-nøglen må kalde submit_run, derfor henter vi brugeren af JWT'en og sender id'et med
const supabase = createClient(Deno.env.get("SUPABASE_URL")!, Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!);

function json(body: object, status: number): Response {
    return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

Deno.serve(async (request) => {
    if (request.method != "POST") return json({ title: "Method not allowed" }, 405);

    const length = Number(request.headers.get("content-length") ?? 0);
    if (length > MAX_BODY) return json({ title: "Run could not be verified" }, 400);

    const jwt = (request.headers.get("Authorization") ?? "").replace("Bearer ", "");
    const { data, error } = await supabase.auth.getUser(jwt);
    if (error || !data.user) return json({ title: "Unauthorized" }, 401);

    // Content-Length kan mangle eller lyve, så vi tjekker også den rigtige tekst
    const text = await request.text();
    if (text.length > MAX_BODY) return json({ title: "Run could not be verified" }, 400);

    let body;
    try {
        body = JSON.parse(text);
    } catch {
        return json({ title: "Run could not be verified" }, 400);
    }

    const problem = check(body?.difficulty, body?.seed, body?.timeMs, body?.ghost);
    if (problem != null) {
        console.log("Run rejected: " + problem);
        return json({ title: "Run could not be verified" }, 400);
    }

    const result = await supabase.rpc("submit_run", {
        p_user: data.user.id,
        p_difficulty: body.difficulty,
        p_seed: body.seed,
        p_time_ms: body.timeMs,
        p_ghost: body.ghost,
    });
    if (result.error) {
        console.log("submit_run failed: " + result.error.message);
        return json({ title: "Could not save the run" }, 500);
    }

    const run = result.data[0];   // rpc med tabel giver en liste
    return json({ id: run.id, timeMs: run.time_ms, isBest: run.is_best, rank: run.rank }, 200);
});
