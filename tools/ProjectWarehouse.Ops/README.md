# pwops

Terminal tool for the production and development stacks: building and pushing images, rolling
them out, pulling backups and telemetry archives. Run without arguments for a menu, with a
command for a scriptable one-shot.

```
cd tools/ProjectWarehouse.Ops
dotnet run -- validate
dotnet run -- status prod
```

## Global install

Publishes `pwops` once into a fixed folder, so it runs from any directory without `cd`,
`dotnet run`, or a local `ops.json`.

```powershell
dotnet publish tools\ProjectWarehouse.Ops\ProjectWarehouse.Ops.csproj -c Release -o "$env:USERPROFILE\.local\pwops"
```

In PowerShell, add a function to `$PROFILE` instead of a `PATH` shim — a `.cmd`/`.bat` shim runs
under `cmd.exe`'s batch processor, which intercepts Ctrl+C with its own "Terminate batch job
(Y/N)?" prompt no matter what the batch file runs; a PowerShell function calling the exe directly
has no such prompt, Ctrl+C just kills the process:

```powershell
function pwops {
    & "$env:USERPROFILE\.local\pwops\pwops.exe" @args --project "<repo-path>" --config "<repo-path>\tools\ProjectWarehouse.Ops\ops.json"
}
```

Replace `<repo-path>` with the absolute path to this clone. `@args` must come **before**
`--project`/`--config`: Spectre.Console.Cli reads per-command options after the command name, so
putting the flags first makes it try to parse `validate` (or whichever command) as an unknown
command.

This only covers PowerShell. If `pwops` also needs to run from `cmd.exe`, put a `pwops.cmd` shim
on `PATH` with the same argument order — but expect the Ctrl+C prompt there, since it comes from
`cmd.exe` itself and no in-file change avoids it:

```bat
@echo off
"%USERPROFILE%\.local\pwops\pwops.exe" %* --project "<repo-path>" --config "<repo-path>\tools\ProjectWarehouse.Ops\ops.json"
```

Hardcoding `--project`/`--config` is what makes the install machine-independent from the caller's
point of view: `pwops` always resolves the same repo and config no matter which directory it is
run from, instead of relying on `RepoRoot`'s `.git`-ancestor search or an `ops.json` sitting next
to the working directory.

Passing `--project` or `--config` explicitly when calling `pwops` adds a duplicate of that option;
Spectre.Console.Cli keeps the last value, so the explicit one wins, but the shim's copy is still
there.

To update after pulling new code, rerun the `dotnet publish` command above — the shim needs no
changes.

## Configuration

`pwops` reads `ops.json` from the working directory, then from the directory holding the
executable; `--config <path>` overrides both. The committed `ops.json` holds what the code
repository already describes — the `services` block and the `local` target for the dev compose
stack — and points at a private config for the rest; `ops.local.example.json` is the annotated
form of everything that can go there. Hosts, keys and credentials live in that private repository,
reached through a pointer:

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

`passphrase` may be left out even for an encrypted key: the tool asks for it, up to three tries,
and never writes it anywhere. A run without a terminal cannot be asked, so there it is an error
naming the key rather than a prompt nobody can answer.

The prompt is wiped from the screen once it is answered, retries included, and so are the registry
credentials asked for the same way — nothing about a secret, not even which key or host it was
asked for, is left in the scrollback.

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

### Volumes

A target's `volumes` maps a logical name — the name a backup part carries — onto where that data
actually sits:

```json
"volumes": {
  "keys": "dataprotection_keys",
  "datafiles": { "path": "{projectDir}/ProjectWarehouse.Server/.localdata/files" }
}
```

A string is a compose volume name. `{ "path": ... }` is a host directory the compose file binds in,
which is how a stack that keeps its data in bind mounts is described — the dev `docker-compose.yml`
is one. The path has to be absolute: docker reads a relative `-v` source as a volume name, and
would quietly fill an empty volume instead of the directory.

On an `ssh` target the path is a path on the remote host, so it has to be POSIX-absolute — a
`{projectDir}` there would expand to a directory on this machine, and the load fails saying so.

What lines up between two targets is the root, not the name. An archive holds the volume's contents
from its root down, so a part that is `/data/files` on one target has to map to the directory
standing for `/data/files` on the other, whatever each of them calls it.

### Path variables

Path values expand two tokens:

| Token | Means |
| --- | --- |
| `{currentConfigDir}` | directory of the file the value is written in |
| `{projectDir}` | the code repository, from `--project` or the nearest `.git` ancestor of the working directory |

Expansion happens per file while loading, so a value keeps pointing at its own repository no
matter which config included it. An unknown token fails the load rather than reaching a command.

`local.backupsDir`, `local.telemetryArchiveDir` and a local target's volume paths are additionally
rooted and normalized for this machine. Nothing else is: `repoDir`, `composeFile` and an ssh
target's volume paths may well be POSIX paths on the far side of an SSH link.

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
| `ship [target]` | release, then deploy the versions just built |
| `deploy [target]` | points a target's `.env` at chosen versions and brings the stack up |
| `backup [target]` | downloads the database and volumes into `local.backupsDir` |
| `restore [target]` | writes a local backup back onto a target |
| `telemetry [target]` | pulls the OTLP archive into `local.telemetryArchiveDir` for local replay |

Global options: `--config <path>`, `--project <path>`. The menu passes whichever of them it was
given down to the command it dispatches, and dispatches by running the same parser again — a menu
entry and a typed command are the same code path, argument parsing included.

A command finished from the menu prints the line that repeats it without the prompts:

```
repeat with: pwops backup prod --parts db,keys,datafiles
```

Only from the menu — a typed command already has its line, and echoing it back is noise. The
suggestion carries the values that were actually chosen, so it is a script-ready form of the run
that just happened. It is left out where a run has no single line that would repeat it: `--version`
applies to every service at once, so services that ended up on different versions cannot be
expressed in one command.

A menu run opens with a rule carrying the command's name, so a session that ran several of them
reads as separate blocks rather than one scroll.

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

Both the build and the push run on the inherited console and draw themselves: docker renders its
own step and per-layer progress only when it is talking to a terminal. Nothing is captured, so a
failure leaves docker's own output on screen and the tool adds one line naming what failed.

### ship

```
pwops ship prod
pwops ship prod --bump patch --yes
```

`release` and `deploy` in one pass, over the target's own services and the registry it pulls from.
The versions deployed are the ones just built, so nothing is chosen twice.

The target is read **before** the build, not after: a dirty working tree or a variable defined
twice in `.env` is worth finding out about now rather than four minutes into `docker build`. Both
plans — what gets built and what `.env` becomes — are shown together, and one confirmation covers
them. From there it is the same two commands, with the same rollback on the deploy half.

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

Omitting `--parts` opens a multi-select over everything the target offers, everything preselected.
Without a terminal to prompt on it means all of them, so a scripted run never waits for a keypress.

A compose volume name is matched by suffix. Compose prefixes a volume with its project name, and
the project name depends on where the compose file lives — matching `_<name>` avoids reproducing
that rule, and an ambiguous match is an error rather than a guess. A `volumes` entry written as a
path is already the mount source and is used as it stands.

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

`--from` and `--parts` are both prompted when left out — the backups under `local.backupsDir` as a
list, the manifest's parts as a multi-select with everything preselected.

Which services get stopped is worked out from the volumes, not assumed: `docker ps` names every
running container holding a volume being restored, and each one's compose service joins the stop
list. Emptying a volume under a process holding files open in it is how a restore turns into
corruption. A container outside the compose project cannot be stopped, so it is a refusal.

A bind is found by reading every container's mounts rather than through `docker ps --filter
volume=`, which matches a volume name or a mount point inside the container but never a bind's
source on the host. The comparison goes by the drive-relative tail of the path, because Docker
Desktop reports a bind source as its own VM sees it — `/run/desktop/mnt/host/f/...` for an
`F:\...` given on the command line.

Postgres stays up throughout; the restore talks to it. A volume postgres has mounted therefore
cannot be restored this way and is refused — the database comes back from its dump. A volume's
archive is read end to end inside the target before the volume is emptied, so a truncated tar
cannot destroy the only copy.

`db` and `keys` restore together or not at all. The data protection key ring decrypts the
marketplace API keys held in the database, so either one alone leaves them unreadable.

A volume is emptied before extraction, otherwise files no backup ever contained would survive.
`pg_restore` runs with `--clean --if-exists --single-transaction`, so a failure leaves the
database as it was rather than half-loaded.

`--yes` skips the confirmation, but never on a `danger` target: there the answer is the target's
name typed out, and a script cannot give it.

### telemetry

```
pwops telemetry prod
pwops telemetry prod --signals traces,logs
pwops telemetry prod --signals traces --rotated --since 2 --clean
```

The production collector only writes OTLP JSON to a volume and rotates it; searching and drawing
happen here. This pulls that volume into `local.telemetryArchiveDir`, which is where
`docker-compose.telemetry.yml` reads from — bring that stack up afterwards and the dashboard is
on `http://localhost:18890`.

The collector runs one file exporter per signal, so a signal is a file-name prefix in the volume
and choosing signals is choosing file names. `--signals` takes any of `traces`, `metrics`, `logs`
and is prompted as a multi-select with everything preselected; without a terminal it takes all
three rather than waiting for a keypress.

`--rotated` adds the rotation backups to the file each signal is currently being written to. The
default is the active files alone, and the difference is the bulk of the fetch: a rotation is
capped at `max_megabytes` and there are up to `max_backups` of them per signal, while the active
file is whatever has accumulated since the last rotation.

`--since <days>` narrows by file age, which only ever reaches the rotation backups — the collector
writes continuously, so an active file is always recent. On its own it therefore changes nothing,
and the command says so rather than quietly fetching the same bytes.

Given neither option, a terminal is asked for the rotations and then, only if they are wanted, for
the window as a list of ranges. The order is the explanation: the window narrows the rotations and
nothing else, so it is not a question worth asking until they are in. Either option on the command
line answers both, and a run with nothing to ask on takes the active files.

The selection is gzipped into a file on the target, then read back and expanded here. The archive
is JSON lines and compresses by an order of magnitude, which is most of what makes a full fetch
practical over an SSH link; what lands on disk is the same bytes the collector wrote, because the
replay receiver reads only uncompressed JSON.

Compressing to a file rather than straight into the stream is what gives the transfer a size to
draw a progress bar against — a compressed stream only reveals its length by ending. Gzip runs
once either way, so the size costs a file on the target rather than a second pass over the data:
a tenth of what the selection holds, removed as soon as the fetch is done. A target that could not
be tidied up is reported as a line rather than a failure — the archive is already on disk by then.

`--clean` drops the chosen signals from the local archive first, so the replay shows this fetch
alone. It removes each chosen signal in full, rotations included even when the fetch skipped them:
the replay stack reads every file in the directory, so one left behind would still turn up on the
dashboard. Signals that were not chosen are untouched, as is anything in the directory that is not
telemetry.

What arrives is staged in a scratch file and only then extracted: a transfer that dies half way
leaves a scratch file behind rather than a half-populated archive the replay stack would happily
read.

## Layout

```
Configuration/   config model, include chain, path tokens, validation
Infrastructure/  ICommandHost (local process or SSH), compose, git, .env
Registry/        docker credentials, Harbor and Distribution APIs, version math
Services/        scenario logic
Commands/        argument parsing and rendering
Ui/              menu, target picker, shared prompts, progress rendering
```

Every answer is printed back as a `label  value` line — the target and its kind, the registry, the
selected backup, the parts, the services, the version per service, the menu entry. A prompt leaves
nothing on screen once it is answered, and a value that came from an option is echoed the same way,
so a run reads alike whether it was driven by the menu or by a command line.

Long-running scenarios report through `IStepReporter`: every step opens its own progress row, and
one that moves bytes carries a bar and a transferred/total pair in `ByteSize` units. The total for
a volume comes from measuring it on the target first — the same age filter the transfer uses — and
counts file contents only, so the tar's per-file headers arrive with the bar already full. Steps
whose size cannot be known up front (`pg_dump`, whose output is compressed as it is produced) run
indeterminate and settle on their real size when they finish. A finished step swaps its spinner
for a green tick, and a step that did not get there keeps its bar where it stopped — the tick is
only ever printed for work that completed. A single read waited on — published tags, a target's
state — is one such row too, so it keeps its line and turns into a tick where it stands.

Scenarios are written against `ICommandHost`, so the same code runs against a local compose
stack and against production over SSH. Commands are passed as argument arrays rather than shell
lines: locally the process starts directly, and for SSH the line is quoted here — nothing depends
on which shell is on the far side.
