# Security advisory — open self-registration grants admin on existing installs

**Affects:** self-hosted Orkyo Community installations created **before 0.12.0**
whose Keycloak sign-in page is reachable from the internet **and** which have
SMTP configured.
**Does not affect:** Orkyo Cloud (the hosted SaaS), or installs that are only
reachable on a private network.
**Fixed in:** 0.12.0 — for **new** installs only. Existing installs must be
checked by hand; see below.

## What the problem is

Community holds one workspace per installation. By design, *every user of an
install is an admin of that install's organization* — a database trigger grants
admin membership whenever a user record is created. That is reasonable on a private network.

Realms shipped before 0.12.0 also enabled **Keycloak self-registration** with no
password policy. Together, those two facts mean anyone who could reach the
sign-in page could create an account and, on first request, be granted **admin**
of your organisation's data.

Email verification is on, so an attacker needs a working mailbox and your
install needs SMTP configured for the sign-up to complete. An install with no
SMTP configured was not exploitable this way.

## Check whether you are affected

In the Keycloak admin console: **Realm settings → Login → User registration**.

If it is **On**, and your Keycloak is reachable from the internet, assume you
were exposed and continue below.

## Remediate

**1. Close registration.** Realm settings → Login → turn **User registration**
off. Do this first — it stops any further accounts being created.

**2. Add a password policy.** Realm settings → Authentication → Policies →
Password policy. The shipped 0.12.0 default is:

```
length(12) and notUsername and notEmail and specialChars(1) and upperCase(1) and digits(1) and passwordHistory(3)
```

**3. Audit who has access.** Every account in the database is an admin of your
organisation, so list them and remove any you do not recognise:

```sql
SELECT u.email, u.display_name, u.created_at, m.role
FROM public.users u
JOIN public.tenant_memberships m ON m.user_id = u.id
ORDER BY u.created_at DESC;
```

Remove an unrecognised account in the Keycloak admin console (Users → select →
Delete), then delete the matching row from `public.users` — the membership row
is removed with it by cascade.

**4. If you find accounts you did not invite,** treat the data in the install as
having been readable by them and respond according to your own obligations.

## Onboarding after this change

Invitations are the supported path: **Settings → Users → Invite**. An invited
user receives the role you choose, which is why invitations were never affected
by this issue.

If you deliberately want an open sign-up page — for example on a private network
— you can re-enable registration in the Keycloak console. Please pair it with a
password policy.
