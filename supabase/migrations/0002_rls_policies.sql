-- Synlig RLS-politik: en logget ind bruger må læse sin egen profil (appen henter brugernavnet efter Google-login).
-- runs har bevidst ingen politik og ingen rettigheder: kun RPC'erne (security definer) når tabellen,
-- så ingen kan læse andres spøgelser eller skrive tider uden om anti-snyd.

grant select on public.profiles to authenticated;

create policy profiles_select_own on public.profiles
    for select to authenticated
    using (id = (select auth.uid()));   -- (select ...) køres én gang pr. forespørgsel, ikke pr. række
