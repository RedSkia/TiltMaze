-- E-mailen må kun skifte, når den passer til brugernavnet.
-- Uden dette kunne en indlogget bruger selv kalde PUT /auth/v1/user {email} (der er ingen bekræftelse)
-- og optage et andet navn, eller (Google) få et login med brugernavn under et andet navn end profilens.

create function public.check_email_change() returns trigger
language plpgsql security definer set search_path = '' as $$
begin
    if new.raw_app_meta_data->>'provider' = 'email' then
        -- set_username opdaterer profilen før e-mailen, så det nye navn står allerede i profiles her
        if new.email is distinct from
            (select lower(username) from public.profiles where id = new.id) || '@tiltmaze.invalid' then
            raise exception 'The email must match the username';
        end if;
    elsif lower(new.email) like '%@tiltmaze.invalid' then
        -- Google og andre: navne-e-mails er kun til login med brugernavn
        raise exception 'This email is not allowed';
    end if;
    return new;
end $$;

-- Kun når e-mailen faktisk ændres (Supabase skriver også rækken ved login)
create trigger on_auth_user_email_change
    before update of email on auth.users
    for each row when (old.email is distinct from new.email)
    execute function public.check_email_change();

revoke all on function public.check_email_change() from public, anon, authenticated;
