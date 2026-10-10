---
title: "OpenSky credentials"
description: "How to put the OpenSky client id and secret into the user-secrets store, check them, get them into a built head, and rotate or remove them."
type: guide
---

# OpenSky credentials

The live aircraft source authenticates with an OAuth2 client id and client
secret. The application reads both from the `dotnet user-secrets` store on the
machine that builds it — never from a file in the repository. Without them the
application still starts; the live source is refused and the fleet stays empty.

Why it works this way is
[`aircraft-source`](../src/Transporter/Integrations/OpenSky/.spec/README.md)
B-058 and its
[decision 0004](../src/Transporter/Integrations/OpenSky/.spec/decisions/0004-the-credentials-live-in-the-user-secrets-store.md).
This page is only the steps.

## 1. Get the pair

Sign in at [opensky-network.org](https://opensky-network.org), open the account
page, and create an API client. It gives a `client_id` and a `client_secret`.
The secret is shown once; keep it in a password manager.

## 2. Put the pair into the store

Run this in a terminal of your own, from any directory. The id
`transporter-gui` is the one the head project declares, and it is what ties the
store to this application.

```sh
printf 'Client id: ';     read -r  OPENSKY_ID
printf 'Client secret: '; read -rs OPENSKY_SECRET; echo

dotnet user-secrets set "OpenSky:ClientId"     "$OPENSKY_ID"     --id transporter-gui
dotnet user-secrets set "OpenSky:ClientSecret" "$OPENSKY_SECRET" --id transporter-gui
```

Reading the values with `read` keeps them out of the shell's history, which a
value typed on the command line goes into. `set` answers
`Successfully saved OpenSky:ClientSecret to the secret store.` and does not
print the value.

The two key names are exact. They are the configuration paths the application
binds, so a misspelt key is stored without complaint and never read.

The store is one file, shared by every clone and worktree on the machine:

```text
~/.microsoft/usersecrets/transporter-gui/secrets.json
```

## 3. Check it

Which keys are set, without printing either value:

```sh
dotnet user-secrets list --id transporter-gui | cut -d= -f1
```

**`dotnet user-secrets list` on its own prints both values.** Do not run it on
a shared screen or in a recorded session.

Whether the provider accepts the pair, printing only the status code — while
the two variables from step 2 are still set:

```sh
curl -s -o /dev/null -w '%{http_code}\n' \
  -d grant_type=client_credentials \
  --data-urlencode "client_id=$OPENSKY_ID" --data-urlencode "client_secret=$OPENSKY_SECRET" \
  https://auth.opensky-network.org/auth/realms/opensky-network/protocol/openid-connect/token
```

`200` is a working pair; `401` is a wrong id or secret. Then drop the
variables:

```sh
unset OPENSKY_ID OPENSKY_SECRET
```

## 4. Build the head

**The store is read when the head is built, not when it runs.** A sandboxed
Mac Catalyst or iOS application cannot open the home directory, so the build
copies the store into the application bundle. A head built before step 2 has no
credential until it is built again.

```sh
dotnet build src/Gui -t:Run -f net10.0-maccatalyst
```

To see that the build packaged it, without opening it:

```sh
ls src/Gui/bin/Debug/net10.0-maccatalyst/*/Gui.app/Contents/Resources/secrets.json
```

**A bundle built on a machine that holds the store contains the client
secret**, in every configuration, Release included. Never send, attach, upload
or commit a built bundle. Build it on the machine it will run on.

## 5. Rotate or remove

To rotate, create a new secret on the OpenSky account page, repeat step 2, and
build again.

To remove the pair from the machine:

```sh
dotnet user-secrets clear --id transporter-gui
```

Then build again, so the bundle stops carrying the old copy. A head already
installed on a simulator or a device keeps its copy until it is reinstalled.

## If the fleet stays empty

| Check                                                         | What it means                                                                           |
| ------------------------------------------------------------- | --------------------------------------------------------------------------------------- |
| Step 3's key list shows neither key, or a misspelt one        | The store is empty or the key is wrong. Repeat step 2 with the exact names.             |
| Step 4's `ls` finds no file                                   | The head was built before the store was set, or under another id. Build again.          |
| Step 3's `curl` prints `401`                                  | The provider rejects the pair. Create a new secret and repeat step 2.                   |
| All three pass and the machine is a cloud VM, or behind a VPN | The provider blocks hyperscaler addresses (`README.md` § "Gotchas"). Run from a laptop. |

A missing credential does not stop the application or show a message today;
whether it should is `aircraft-source` § 11 row 7.

## Never

- A credential in `appsettings.json`, a test, a fixture, a log or a screenshot.
- `dotnet user-secrets list` with its values on a shared screen.
- A built bundle anywhere but the machine that built it.
