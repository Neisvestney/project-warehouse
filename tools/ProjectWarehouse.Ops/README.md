# pwops

Terminal tool for the production and development stacks: building and pushing images, rolling
them out, pulling backups and telemetry archives. Run without arguments for a menu, with a
command for a scriptable one-shot.

```
cd tools/ProjectWarehouse.Ops
dotnet run -- validate
dotnet run -- status prod
```

## Configuration

`pwops` reads `ops.json` from the working directory, then from the directory holding the
executable; `--config <path>` overrides both. The repository ships `ops.example.json` only —
real hosts and credentials live in a separate private repository, and the committed side stays
a pointer:

```json
{
  "includeConfig": "/some-path/project-warehouse-devops/ops.json"
}
```

`includeConfig` chains as deep as needed and detects cycles. Includes load first, and entries
defined by the including file replace same-named entries **whole** — an override of
`targets.prod` repeats every field it needs, not only the one it changes.

Each config in the chain is also checked for an `ops.local.json` beside it, applied right after
it. That is where a value belonging to one machine rather than to the team goes.

### overrides

The one section that patches instead of replacing:

```json
{
  "overrides": {
    "targets": {
      "prod": {
        "ssh": {
          "keyPath": "~/.ssh/id_ed25519",
          "passphrase": null
        }
      }
    }
  }
}
```

The fields written here are applied onto the matching target and everything left out keeps the
value it already had. That is what makes two lines enough: writing the same target under `targets`
would replace it whole and drop its host, volumes and postgres section along with it.

It covers `ssh` (host, port, user, keyPath, passphrase) and `repoDir`, takes the same path
variables as everywhere else — `{currentConfigDir}` being the directory of the file the override
is written in — and naming a target that does not exist fails the load rather than being ignored.

Unknown fields fail the load. A typo like `dagner` instead of `danger` would otherwise leave a
production target unmarked and unguarded, and no amount of validation downstream would notice.

### Model

Three dictionaries keyed by name, plus local paths.

| Section | Holds |
| --- | --- |
| `registries` | where images live: url, project, API flavour, credential source |
| `services` | what gets built: dockerfile, image name, compose service, tag variable |
| `targets` | where things run: local or ssh, compose file, `.env`, which registry it pulls from |
| `local` | this machine's output directories |

A target names its registry through `pullsFrom`, so "where we push" and "where the server pulls
from" stay independent — one release can serve several targets pulling from different registries.

Every service carries its own `tagVariable`, so versions move independently. Two services of the
same target sharing a variable is a validation error: they would overwrite each other in `.env`.

`danger: true` is the only risk marker. It colors the target red everywhere and gates destructive
actions behind a typed confirmation.

### Path variables

Path values expand two tokens:

| Token | Means |
| --- | --- |
| `{currentConfigDir}` | directory of the file the value is written in |
| `{projectDir}` | the code repository, from `--project` or the nearest `.git` ancestor of the working directory |

Expansion happens per file while loading, so a value keeps pointing at its own repository no
matter which config included it. An unknown token fails the load rather than reaching a command.

`local.backupsDir` and `local.telemetryArchiveDir` are additionally rooted and normalized for
this machine. Nothing else is: `repoDir` and `composeFile` may well be POSIX paths on the far
side of an SSH link.

### Image versions

Release tags are plain `major.minor.patch`. Anything else in the repository — `latest`, a commit
hash — is not a version and takes no part in ordering or auto-increment.

The tag reaches the image through the `versionBuildArg` build arg, which the Dockerfile spreads
into `VITE_APP_VERSION` for the client and `/p:Version` for the server, so both halves of the
telemetry report the same `service.version` as the image tag.

## Commands

| Command | Does |
| --- | --- |
| _(none)_ | interactive menu over the commands below |
| `validate` | loads the config chain and reports every problem at once |
| `status [target]` | git state, `.env` versions, registry's newest tag, container health |
| `release` | builds the selected services and pushes them under the next version |
| `deploy [target]` | points a target's `.env` at chosen versions and brings the stack up |
| `backup [target]` | downloads the database and volumes into `local.backupsDir` |
| `restore [target]` | writes a local backup back onto a target |
| `telemetry [target]` | pulls the OTLP archive into `local.telemetryArchiveDir` for local replay |

Global options: `--config <path>`, `--project <path>`. The menu passes whichever of them it was
given down to the command it dispatches, and dispatches by running the same parser again — a menu
entry and a typed command are the same code path, argument parsing included.

### release

Reads the published tags, offers the next version per service, then builds and pushes. Versions
move independently — each service is asked separately, and a service with nothing published yet
starts at `0.0.1`.

```
pwops release                                  # prompts for services and increments
pwops release --service server --bump minor
pwops release --service server --version 1.4.0 --yes
```

Building always happens on this machine; the target only ever pulls. Without a terminal every
prompt would fail on the same read error, so the command names the options it needs instead.

The build is captured and shown as a rolling tail; the push is not. docker only draws per-layer
upload progress when it is talking to a terminal, so the push runs on the inherited console and
draws itself.

### deploy

```
pwops deploy prod
pwops deploy prod --set server=0.0.2 --yes
```

Steps, in order: `git pull --ff-only`, rewrite the tag and registry variables in `.env`,
`compose pull`, `compose up -d`, then wait for the containers to report healthy. Every replica
has to settle, and a container whose image declares no healthcheck counts as settled once it is
running.

The dirty check looks at tracked changes only: untracked files cannot block a fast-forward, and
the target's working directory collects them — `.env.bak` among others. A working tree whose
state cannot be read counts as unsafe, not as clean.

The variables are snapshotted before the write and restored **whole** afterwards if any step
fails, cancellation and a dropped connection included. A variable that was absent is restored by
being removed again, so the first deploy into an `.env` that has neither variable can still roll
back. The rollback re-pulls before bringing the stack up, because the version it restores may no
longer be on the host, and it reports its own failures rather than claiming success.

`.env` is written beside the target and renamed over it, carrying the original file mode across:
the same file holds the database password, a half-written one is worse than an old one, and a
fresh file would otherwise come back world-readable. The previous contents are kept as `.env.bak`,
restricted to its owner.

A variable defined twice fails the preflight. Compose reads the last definition and an editor sees
the first, so rewriting either one is a deploy that reports success while the old image keeps
running.

`--env-file` is passed explicitly, so the file the tool rewrites is the file compose reads rather
than the one that happens to sit next to the compose file.

On failure the last 50 lines of the services' logs are printed.

### backup

```
pwops backup prod
pwops backup prod --parts db,keys
```

Each part is streamed straight into a local file, so nothing is staged on the target's disk: the
database through `pg_dump -F c`, a volume through a throwaway `busybox` container running
`tar -cf -`. Output lands in `local.backupsDir/<target>-<timestamp>/` next to a `manifest.json`
naming the parts, their sizes and the versions that were deployed.

The telemetry volume is not part of a backup, even when the target declares it: `pwops telemetry`
fetches it with an age filter and unpacks it for the replay stack, and a rotated archive of that
size in every backup would cost more than the data is worth restoring.

Volume names are matched by suffix. Compose prefixes a volume with its project name, and the
project name depends on where the compose file lives — matching `_<name>` avoids reproducing that
rule, and an ambiguous match is an error rather than a guess.

### restore

```
pwops restore prod
pwops restore prod --from ./backups/prod-2026-09-03T14-40 --parts db,keys
```

Everything checkable without touching the target is checked first: the parts exist in the
manifest, the files are there, and each one still has the size the manifest recorded. Past the
stop a refusal would cost an outage rather than an error message.

Then it takes a backup of the current state — `--no-safety-backup` opts out — uploads every
archive to a temporary directory on the target, and only then stops the application services and
restores. Uploads finish before anything is destroyed, so a transfer that dies mid-way costs
nothing but time. The stack is brought back up on the way out whichever way the restore ended,
and anything left behind — a stack that would not start, staging that would not delete — is
reported as a warning rather than swallowed.

Which services get stopped is worked out from the volumes, not assumed: `docker ps` names every
running container holding a volume being restored, and each one's compose service joins the stop
list. Emptying a volume under a process holding files open in it is how a restore turns into
corruption. A container outside the compose project cannot be stopped, so it is a refusal.

Postgres stays up throughout; the restore talks to it. A volume postgres has mounted therefore
cannot be restored this way and is refused — the database comes back from its dump. A volume's
archive is read end to end inside the target before the volume is emptied, so a truncated tar
cannot destroy the only copy.

`--yes` skips the confirmation, but never on a `danger` target: there the answer is the target's
name typed out, and a script cannot give it.

### telemetry

```
pwops telemetry prod
pwops telemetry prod --since 2 --clean
```

The production collector only writes OTLP JSON to a volume and rotates it; searching and drawing
happen here. This pulls that volume into `local.telemetryArchiveDir`, which is where
`docker-compose.telemetry.yml` reads from — bring that stack up afterwards and the dashboard is
on `http://localhost:18890`.

`--since <days>` narrows the fetch by file age, `--clean` empties the local archive first so the
replay shows this fetch alone. The tar is staged in a scratch file and only then extracted: a
transfer that dies half way leaves a scratch file behind rather than a half-populated archive the
replay stack would happily read.

`db` and `keys` restore together or not at all. The data protection key ring decrypts the
marketplace API keys held in the database, so either one alone leaves them unreadable.

A volume is emptied before extraction, otherwise files no backup ever contained would survive.
`pg_restore` runs with `--clean --if-exists --single-transaction`, so a failure leaves the
database as it was rather than half-loaded. On a `danger` target the confirmation is the target's
name typed out.

## Layout

```
Configuration/   config model, include chain, path tokens, validation
Infrastructure/  ICommandHost (local process or SSH), compose, git, .env
Registry/        docker credentials, Harbor and Distribution APIs, version math
Services/        scenario logic
Commands/        argument parsing and rendering
Ui/              menu, target picker, shared prompts
```

Scenarios are written against `ICommandHost`, so the same code runs against a local compose
stack and against production over SSH. Commands are passed as argument arrays rather than shell
lines: locally the process starts directly, and for SSH the line is quoted here — nothing depends
on which shell is on the far side.
