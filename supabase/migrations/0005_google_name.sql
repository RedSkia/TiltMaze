-- Google-brugere får navnet fra Gmail-adressen (det før @) i stedet for p_ + hex.
-- Kun tilladte tegn, højst 16. For kort eller optaget navn får et _ og 4 tegn af id'et bagpå.
-- create or replace beholder rettighederne fra 0001.

create or replace function public.handle_new_user() returns trigger
language plpgsql security definer set search_path = '' as $$
declare
    v_name text;
begin
    if new.raw_app_meta_data->>'provider' = 'email' then
        -- E-mailen skal høre til brugernavnet, ellers kan navne optages uden login
        if new.email is distinct from lower(new.raw_user_meta_data->>'username') || '@tiltmaze.invalid' then
            raise exception 'Invalid username';
        end if;
        insert into public.profiles (id, username)
        values (new.id, new.raw_user_meta_data->>'username');
    elsif new.raw_app_meta_data->>'provider' = 'google' then
        v_name := left(regexp_replace(split_part(coalesce(new.email, ''), '@', 1), '[^A-Za-z0-9_]', '', 'g'), 16);
        if length(v_name) < 3 or exists (select 1 from public.profiles where lower(username) = lower(v_name)) then
            v_name := left(v_name, 11) || '_' || left(replace(new.id::text, '-', ''), 4);
        end if;
        insert into public.profiles (id, username) values (new.id, v_name);
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
