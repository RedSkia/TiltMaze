-- TiltMaze: profiler, tider og RPC'er. Appen skriver aldrig direkte i tabellerne.

create table public.profiles (
    id uuid primary key references auth.users on delete cascade,
    username text not null check (username ~ '^[A-Za-z0-9_]{3,16}$')
);
-- Unikt uden forskel på store/små bogstaver
create unique index profiles_username_key on public.profiles (lower(username));

create table public.runs (
    id bigint generated always as identity primary key,
    user_id uuid not null references auth.users on delete cascade,
    difficulty int not null check (difficulty between 0 and 3),
    seed int not null check (seed >= 0),
    time_ms int not null check (time_ms > 0),
    ghost bytea not null,
    created_at timestamptz not null default now(),
    unique (user_id, difficulty, seed)
);
create index runs_leaderboard on public.runs (difficulty, seed, time_ms);

-- RLS uden politikker + ingen direkte adgang: alt går via RPC'er
alter table public.profiles enable row level security;
alter table public.runs enable row level security;
revoke all on public.profiles, public.runs from anon, authenticated;

-- Ny bruger i auth.users giver en profil. Brugernavnet kommer fra signup
create function public.handle_new_user() returns trigger
language plpgsql security definer set search_path = '' as $$
begin
    insert into public.profiles (id, username)
    values (new.id, new.raw_user_meta_data->>'username');
    return new;
exception
    when unique_violation then
        raise exception 'That username is taken';
    when check_violation or not_null_violation then
        raise exception 'Invalid username';
end $$;

create trigger on_auth_user_created
    after insert on auth.users
    for each row execute function public.handle_new_user();

-- Gem en tur (kaldes kun af edge-funktionen efter anti-snyd). Kun bedste tid gemmes
create function public.submit_run(p_user uuid, p_difficulty int, p_seed int, p_time_ms int, p_ghost text)
returns table (id bigint, time_ms int, is_best boolean, rank int)
language plpgsql security definer set search_path = '' as $$
declare
    v_old int;
    v_id bigint;
    v_best boolean;
    v_time int;
begin
    select r.time_ms into v_old from public.runs r
    where r.user_id = p_user and r.difficulty = p_difficulty and r.seed = p_seed;

    v_best := v_old is null or p_time_ms < v_old;
    if v_best then
        insert into public.runs as r (user_id, difficulty, seed, time_ms, ghost)
        values (p_user, p_difficulty, p_seed, p_time_ms, decode(p_ghost, 'base64'))
        on conflict (user_id, difficulty, seed)
        do update set time_ms = excluded.time_ms, ghost = excluded.ghost, created_at = now();
    end if;

    select r.id, r.time_ms into v_id, v_time from public.runs r
    where r.user_id = p_user and r.difficulty = p_difficulty and r.seed = p_seed;

    return query select v_id, v_time, v_best,
        (select count(*)::int + 1 from public.runs o
         where o.difficulty = p_difficulty and o.seed = p_seed and o.time_ms < v_time);
end $$;

-- Top-liste for en labyrint
create function public.leaderboard(p_difficulty int, p_seed int, p_top int default 10)
returns table (rank int, run_id bigint, username text, time_ms int, is_me boolean)
language sql stable security definer set search_path = '' as $$
    select rank() over (order by r.time_ms)::int, r.id, p.username, r.time_ms, r.user_id = auth.uid()
    from public.runs r join public.profiles p on p.id = r.user_id
    where r.difficulty = p_difficulty and r.seed = p_seed
    order by r.time_ms, r.id
    limit greatest(1, least(coalesce(p_top, 10), 100));
$$;

-- Ét spøgelse: ens eget eller et fra top 10
create function public.get_ghost(p_run_id bigint)
returns table (username text, time_ms int, ghost text)
language sql stable security definer set search_path = '' as $$
    select p.username, r.time_ms, replace(encode(r.ghost, 'base64'), E'\n', '')
    from public.runs r join public.profiles p on p.id = r.user_id
    where r.id = p_run_id
      and (r.user_id = auth.uid()
           or (select count(*) from public.runs o
               where o.difficulty = r.difficulty and o.seed = r.seed and o.time_ms < r.time_ms) < 10);
$$;

-- De 3 bedste løb fra andre
create function public.top_ghosts(p_difficulty int, p_seed int)
returns table (username text, time_ms int, ghost text)
language sql stable security definer set search_path = '' as $$
    select p.username, r.time_ms, replace(encode(r.ghost, 'base64'), E'\n', '')
    from public.runs r join public.profiles p on p.id = r.user_id
    where r.difficulty = p_difficulty and r.seed = p_seed and r.user_id <> auth.uid()
    order by r.time_ms, r.id
    limit 3;
$$;

create function public.username_available(p_username text) returns boolean
language sql stable security definer set search_path = '' as $$
    select p_username ~ '^[A-Za-z0-9_]{3,16}$'
       and not exists (select 1 from public.profiles where lower(username) = lower(p_username));
$$;

-- Nyt navn. Login bruger e-mailen, så den skal følge med
create function public.set_username(p_username text) returns void
language plpgsql security definer set search_path = '' as $$
begin
    if auth.uid() is null then
        raise exception 'Not signed in';
    end if;
    if p_username !~ '^[A-Za-z0-9_]{3,16}$' then
        raise exception 'Invalid username';
    end if;
    update public.profiles set username = p_username where id = auth.uid();
    update auth.users set email = lower(p_username) || '@tiltmaze.invalid' where id = auth.uid();
exception
    when unique_violation then
        raise exception 'That username is taken';
end $$;

-- Sletter kontoen; profil og tider følger med (cascade)
create function public.delete_me() returns void
language plpgsql security definer set search_path = '' as $$
begin
    delete from auth.users where id = auth.uid();
end $$;

-- Rettigheder: først alt væk, så kun de nævnte roller
revoke all on function
    public.handle_new_user(), public.submit_run(uuid, int, int, int, text),
    public.leaderboard(int, int, int), public.get_ghost(bigint), public.top_ghosts(int, int),
    public.username_available(text), public.set_username(text), public.delete_me()
from public, anon, authenticated;

grant execute on function public.submit_run(uuid, int, int, int, text) to service_role;
grant execute on function public.leaderboard(int, int, int) to anon, authenticated;
grant execute on function public.username_available(text) to anon, authenticated;
grant execute on function public.get_ghost(bigint) to authenticated;
grant execute on function public.top_ghosts(int, int) to authenticated;
grant execute on function public.set_username(text) to authenticated;
grant execute on function public.delete_me() to authenticated;
