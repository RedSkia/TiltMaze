// Ende-til-ende-test mod en lokal Supabase (langsom, kræver Docker, kun før release):
//   npx supabase start
//   node supabase/tests/e2e.mjs
// Kalder Auth, RPC'er og edge function'en submit-run præcis som appen (Auth.cs og Api.cs).
// Adresse og nøgle er standardværdierne fra `npx supabase status` (kan ændres med SUPABASE_URL og SUPABASE_ANON_KEY,
// fx den hostede adresse og publishable key).
import runs from "../functions/_shared/fixtures/runs.json" with { type: "json" };

const URL = process.env.SUPABASE_URL ?? "http://127.0.0.1:54321";
const ANON = process.env.SUPABASE_ANON_KEY ??
    "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZS1kZW1vIiwicm9sZSI6ImFub24iLCJleHAiOjE5ODM4MTI5OTZ9.CRXP1A7WOeoJeXxjNni43kdQwgnWNReilDMblYTn_I0";
const PASSWORD = "e2e-password-123";
const HOSTED = !URL.includes("127.0.0.1");

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

let failed = 0;
function ok(condition, name, detail) {
    console.log((condition ? "ok   " : "FAIL ") + name + (condition || detail === undefined ? "" : "  " + JSON.stringify(detail)));
    if (!condition) failed++;
}

// Headers som appen: apikey altid, Bearer kun med brugerens token (publishable key er ingen JWT)
function headers(token) {
    const result = { apikey: ANON, "Content-Type": "application/json" };
    if (token) result.Authorization = "Bearer " + token;
    return result;
}

// POST med JSON. Hostet har Auth en grænse pr. IP: pause før hvert /auth/-kald, og ét 429 prøves igen efter 30 s
async function post(path, body, token = null) {
    if (HOSTED && path.startsWith("/auth/")) await sleep(3000);
    const send = () => fetch(URL + path, {
        method: "POST",
        headers: headers(token),
        body: typeof body == "string" ? body : JSON.stringify(body),
    });
    let response = await send();
    if (response.status == 429) {
        await sleep(30000);
        response = await send();
    }
    const text = await response.text();
    let json = null;
    try { json = JSON.parse(text); } catch { /* ikke JSON */ }
    return { status: response.status, json, text };
}

async function get(path, token = null) {
    const response = await fetch(URL + path, { headers: headers(token) });
    const text = await response.text();
    let json = null;
    try { json = JSON.parse(text); } catch { /* ikke JSON */ }
    return { status: response.status, json, text };
}

const email = (name) => name.toLowerCase() + "@tiltmaze.invalid";
const signup = (name, mail = email(name)) => post("/auth/v1/signup", { email: mail, password: PASSWORD, data: { username: name } });
const login = (name, password = PASSWORD) => post("/auth/v1/token?grant_type=password", { email: email(name), password });
const rpc = (name, body, token) => post("/rest/v1/rpc/" + name, body, token);
const submit = (run, token) => post("/functions/v1/submit-run", { difficulty: run.difficulty, seed: run.seed, timeMs: run.timeMs, ghost: run.ghost }, token);

// TokenDto i Auth.cs: access_token, expires_in (heltal), refresh_token
function isTokens(json) {
    return typeof json?.access_token == "string" && Number.isInteger(json?.expires_in) && typeof json?.refresh_token == "string";
}

// Alle oprettede brugere, så de kan slettes til sidst
const tokens = {};
const id = Math.random().toString(36).slice(2, 8);
const nameA = "E2e" + id + "A";
const nameB = "e2e" + id + "b";
const fast = runs.find((r) => r.name == "Easy 3 speed 2");          // 8017 ms
const slow = runs.find((r) => r.name == "Easy 3 irregular frames"); // 8033 ms, samme labyrint

try {
    // --- Konto ---
    let r = await signup(nameA);
    ok(r.status == 200 && isTokens(r.json), "signup giver tokens", r);
    tokens[nameA] = r.json?.access_token;

    r = await signup(nameA.toLowerCase());
    ok(r.status >= 400 && /taken|already/i.test(r.json?.msg ?? r.json?.message ?? ""), "dobbelt navn afvises (som Auth.ReadError forstår)", r);

    r = await signup("e2e" + id + "x", "someone@tiltmaze.invalid");
    ok(r.status >= 400, "e-mail der ikke passer til navnet afvises", r);
    r = await signup("e2e" + id + "!");
    ok(r.status >= 400, "ugyldigt navn afvises", r);
    r = await rpc("username_available", { p_username: "e2e" + id + "x" });
    ok(r.json === true, "afvist signup efterlader ingen profil", r);

    r = await login(nameA.toLowerCase());
    ok(r.status == 200 && isTokens(r.json), "login (navn uden forskel på store/små)", r);
    const refresh = r.json?.refresh_token;
    r = await login(nameA, "wrong-password");
    ok(r.status == 400, "forkert kodeord giver 400 (appen: Wrong username or password)", r);
    r = await post("/auth/v1/token?grant_type=refresh_token", { refresh_token: refresh });
    ok(r.status == 200 && isTokens(r.json), "refresh giver nye tokens", r);
    tokens[nameA] = r.json?.access_token;

    // username_available som anon (svaret er et rent true/false, Auth.IsAvailable læser bool?)
    r = await rpc("username_available", { p_username: nameA.toUpperCase() });
    ok(r.status == 200 && r.json === false, "username_available: taget navn = false", r);
    r = await rpc("username_available", { p_username: "free" + id });
    ok(r.status == 200 && r.json === true, "username_available: ledigt navn = true", r);
    r = await rpc("username_available", { p_username: "a!" });
    ok(r.status == 200 && r.json === false, "username_available: ugyldigt navn = false", r);

    // --- Tider (submit-run) ---
    r = await submit(fast, null);
    ok(r.status == 401, "submit-run uden login afvises", r);
    r = await submit(fast, "not-a-jwt");
    ok(r.status == 401, "submit-run med falsk token afvises", r);

    r = await submit(fast, tokens[nameA]);
    ok(r.status == 200 && Number.isInteger(r.json?.id) && r.json?.timeMs == fast.timeMs && r.json?.isBest === true && r.json?.rank == 1,
        "gyldig tur gemmes: {id, timeMs, isBest, rank}", r);
    const runA = r.json?.id;

    r = await submit({ ...fast, timeMs: fast.timeMs - 500 }, tokens[nameA]);
    ok(r.status == 400 && r.json?.title == "Run could not be verified", "ændret tid giver 400", r);
    const ghost = Buffer.from(fast.ghost, "base64");
    ghost.writeFloatLE(ghost.readFloatLE(400) + 3, 400);
    r = await submit({ ...fast, ghost: ghost.toString("base64") }, tokens[nameA]);
    ok(r.status == 400, "ændret spøgelse giver 400", r);
    r = await submit({ ...fast, difficulty: 9 }, tokens[nameA]);
    ok(r.status == 400, "ukendt sværhedsgrad giver 400", r);
    r = await post("/functions/v1/submit-run", "{not json", tokens[nameA]);
    ok(r.status == 400, "ugyldig JSON giver 400", r);

    r = await submit(slow, tokens[nameA]);
    ok(r.status == 200 && r.json?.id == runA && r.json?.timeMs == fast.timeMs && r.json?.isBest === false && r.json?.rank == 1,
        "dårligere tid overskriver ikke", r);

    r = await signup(nameB);
    tokens[nameB] = r.json?.access_token;
    r = await submit(slow, tokens[nameB]);
    ok(r.status == 200 && r.json?.isBest === true && r.json?.rank == 2, "anden bruger får plads 2", r);
    const runB = r.json?.id;

    // --- Top-liste (LeaderboardRow: rank, run_id, username, time_ms, is_me) ---
    const board = { p_difficulty: fast.difficulty, p_seed: fast.seed, p_top: 10 };
    r = await rpc("leaderboard", board);
    ok(r.status == 200 && r.json?.length == 2 && r.json.every((row) => row.is_me === false), "leaderboard som anon: is_me false", r);
    const first = r.json?.[0] ?? {};
    ok(first.rank == 1 && first.run_id == runA && first.username == nameA && first.time_ms == fast.timeMs, "leaderboard-felter passer til LeaderboardRow", first);
    r = await rpc("leaderboard", board, tokens[nameA]);
    ok(r.json?.[0]?.is_me === true && r.json?.[1]?.is_me === false, "leaderboard som bruger: is_me true på egen", r);

    // --- Spøgelser (GhostDto: username, time_ms, ghost) ---
    const maze = { p_difficulty: fast.difficulty, p_seed: fast.seed };
    r = await rpc("top_ghosts", maze, tokens[nameA]);
    ok(r.status == 200 && r.json?.length == 1 && r.json[0].username == nameB && r.json[0].time_ms == slow.timeMs && r.json[0].ghost == slow.ghost,
        "top_ghosts for A giver kun B's spøgelse", r);
    r = await rpc("top_ghosts", maze, tokens[nameB]);
    ok(r.json?.length == 1 && r.json[0].username == nameA && r.json[0].ghost == fast.ghost, "top_ghosts for B giver kun A's spøgelse", r);
    r = await rpc("top_ghosts", maze);
    ok(r.status >= 400, "top_ghosts kræver login", r);

    r = await rpc("get_ghost", { p_run_id: runA }, tokens[nameA]);
    ok(r.json?.length == 1 && r.json[0].ghost == fast.ghost, "get_ghost: eget løb", r);
    r = await rpc("get_ghost", { p_run_id: runA }, tokens[nameB]);
    ok(r.json?.length == 1, "get_ghost: andens løb i top 10", r);
    r = await rpc("get_ghost", { p_run_id: runA });
    ok(r.status >= 400, "get_ghost kræver login", r);

    // Ni brugere mere med den hurtige tid skubber B ud af top 10
    for (let i = 0; i < 9; i++) {
        const name = "e2e" + id + "c" + i;
        r = await signup(name);
        tokens[name] = r.json?.access_token;
        r = await submit(fast, tokens[name]);
        if (r.status != 200) ok(false, "tur for " + name, r);
    }
    r = await rpc("get_ghost", { p_run_id: runB }, tokens[nameA]);
    ok(r.status == 200 && r.json?.length == 0, "get_ghost: andens løb uden for top 10 giver ingenting", r);
    r = await rpc("get_ghost", { p_run_id: runB }, tokens[nameB]);
    ok(r.json?.length == 1, "get_ghost: eget løb uden for top 10", r);

    // --- Ingen direkte adgang til runs; profiles kun egen række (0002_rls_policies.sql) ---
    r = await get("/rest/v1/profiles?select=*");
    ok(r.status >= 400, "anon kan ikke læse profiles", r);
    r = await get("/rest/v1/profiles?select=*", tokens[nameA]);
    ok(r.status == 200 && r.json?.length == 1 && r.json[0].username == nameA, "bruger læser kun sin egen profil", r);
    r = await get("/rest/v1/profiles?select=username&username=eq." + nameB, tokens[nameA]);
    ok(r.status == 200 && r.json?.length == 0, "bruger kan ikke læse andres profil", r);
    for (const [label, token] of [["anon", null], ["bruger", tokens[nameA]]]) {
        r = await get("/rest/v1/runs?select=*", token);
        ok(r.status >= 400, `${label} kan ikke læse runs`, r);
        r = await post("/rest/v1/runs", { user_id: "00000000-0000-0000-0000-000000000000", difficulty: 0, seed: 3, time_ms: 1, ghost: "\\x00" }, token);
        ok(r.status >= 400, `${label} kan ikke indsætte i runs`, r);
        r = await post("/rest/v1/profiles", { id: "00000000-0000-0000-0000-000000000000", username: "hacker" }, token);
        ok(r.status >= 400, `${label} kan ikke indsætte i profiles`, r);
        r = await rpc("submit_run", { p_user: "00000000-0000-0000-0000-000000000000", p_difficulty: 0, p_seed: 3, p_time_ms: 1, p_ghost: "" }, token);
        ok(r.status >= 400, `${label} kan ikke kalde submit_run`, r);
    }
    r = await rpc("leaderboard", board);
    ok(r.json?.[0]?.time_ms == fast.timeMs, "ingen falsk tid kom på top-listen", r);

    // --- Skift navn (Api.SetUsername) ---
    r = await rpc("set_username", { p_username: nameB.toUpperCase() }, tokens[nameA]);
    ok(r.status >= 400 && /taken/i.test(r.json?.message ?? ""), "set_username: taget navn giver 'taken' (Api.ErrorOf)", r);
    r = await rpc("set_username", { p_username: "a!" }, tokens[nameA]);
    ok(r.status >= 400, "set_username: ugyldigt navn afvises", r);
    r = await rpc("set_username", { p_username: "e2e" + id + "x" });
    ok(r.status >= 400, "set_username kræver login", r);
    const newA = "E2e" + id + "New";
    r = await rpc("set_username", { p_username: newA }, tokens[nameA]);
    ok(r.status >= 200 && r.status < 300, "set_username lykkes", r);
    r = await login(newA);
    ok(r.status == 200 && isTokens(r.json), "login med nyt navn", r);
    delete tokens[nameA];
    tokens[newA] = r.json?.access_token;
    r = await login(nameA);
    ok(r.status == 400, "login med gammelt navn virker ikke", r);
    r = await rpc("leaderboard", board);
    ok(r.json?.some((row) => row.username == newA) && !r.json?.some((row) => row.username == nameA), "top-listen viser det nye navn", r);
    r = await signup(nameA);
    ok(r.status == 200, "det gamle navn kan tages af en ny bruger", r);
    tokens[nameA] = r.json?.access_token;

    // --- Slet konto (Api.DeleteAccount) ---
    r = await rpc("delete_me", {});
    ok(r.status >= 400, "delete_me kræver login", r);
    r = await rpc("delete_me", {}, tokens[nameB]);
    ok(r.status >= 200 && r.status < 300, "delete_me lykkes", r);
    delete tokens[nameB];
    r = await login(nameB);
    ok(r.status == 400, "login virker ikke efter sletning", r);
    r = await rpc("leaderboard", { ...board, p_top: 100 });
    ok(r.status == 200 && !r.json?.some((row) => row.run_id == runB), "tider er væk efter sletning", r);
    r = await rpc("username_available", { p_username: nameB });
    ok(r.json === true, "navnet er ledigt igen", r);
} catch (error) {
    ok(false, "uventet fejl: " + error);
} finally {
    // Rydder op, så testen kan køres igen
    for (const token of Object.values(tokens)) {
        if (token) await rpc("delete_me", {}, token);
    }
}

console.log(failed == 0 ? "\nAlle tjek bestået" : `\n${failed} tjek fejlede`);
process.exit(failed == 0 ? 0 : 1);
