-- Google-login: profilen laves ud fra provider i app_metadata (kun serveren kan sætte den, ikke klienten).
-- create or replace beholder rettighederne fra 0001.

create or replace function public.handle_new_user() returns trigger
language plpgsql security definer set search_path = '' as $$
begin
    if new.raw_app_meta_data->>'provider' = 'email' then
        -- E-mailen skal høre til brugernavnet, ellers kan navne optages uden login
        if new.email is distinct from lower(new.raw_user_meta_data->>'username') || '@tiltmaze.invalid' then
            raise exception 'Invalid username';
        end if;
        insert into public.profiles (id, username)
        values (new.id, new.raw_user_meta_data->>'username');
    elsif new.raw_app_meta_data->>'provider' = 'google' then
        -- Google: et navn ud fra id'et (p_ + 12 hex-tegn). Brugeren kan skifte det på Account
        insert into public.profiles (id, username)
        values (new.id, 'p_' || left(replace(new.id::text, '-', ''), 12));
    else
        -- Andre måder at logge ind på (fx anonym) er ikke slået til og får ingen profil
        raise exception 'Unsupported sign-in';
    end if;
    return new;
exception
    when unique_violation then
        raise exception 'That username is taken';
    when check_violation or not_null_violation then
        raise exception 'Invalid username';
end $$;

-- Nyt navn. E-mail-brugere logger ind med navnet, så login-e-mailen følger med.
-- Google-brugere logger ind med Google, så kun profilen ændres
create or replace function public.set_username(p_username text) returns void
language plpgsql security definer set search_path = '' as $$
declare
    v_email text := lower(p_username) || '@tiltmaze.invalid';
begin
    if auth.uid() is null then
        raise exception 'Not signed in';
    end if;
    if p_username !~ '^[A-Za-z0-9_]{3,16}$' then
        raise exception 'Invalid username';
    end if;
    update public.profiles set username = p_username where id = auth.uid();
    if (select raw_app_meta_data->>'provider' from auth.users where id = auth.uid()) = 'email' then
        update auth.users set email = v_email where id = auth.uid();
        update auth.identities set identity_data = identity_data || jsonb_build_object('email', v_email)
        where user_id = auth.uid() and provider = 'email';
    end if;
exception
    when unique_violation then
        raise exception 'That username is taken';
end $$;
