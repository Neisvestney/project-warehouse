# Backlog

Work that is understood, measured and deliberately not done yet. Every entry names the reason it is
blocked and the concrete event that unblocks it — an entry without a trigger belongs in an issue, not
here. When the trigger fires, do the work and delete the entry.

## Drop the `"use no memo"` opt-outs on MobX components

**What it buys.** React Compiler memoizes all of `src` except the `observer` components in
`RolesSettingsPage` and `WarehouseEditPage`, which opt out with a `"use no memo"` directive — see
[frontend.md](frontend.md#react-compiler). Removing the directives puts the warehouse floor-plan
editor and the roles matrix, the two heaviest interactive screens in the app, under the same
memoization as everything else.

**Why it is off.** MobX relies on interior mutability: the store reference stays the same while its
fields change, so the compiler never records an observable read as a dependency of the JSX cache. The
component keeps its subscription and still re-renders, and still returns the element built on the
first render — the subtree freezes permanently. `react-hooks/incompatible-library` does not detect the
pattern, so nothing but the directive stands between the compiler and a silently dead canvas.

[mobxjs/mobx#3874](https://github.com/mobxjs/mobx/issues/3874) tracks this upstream. Open since May
2024, labelled `✋ on hold`, no assignee — the bindings are not being adapted, so the directives are
the permanent arrangement rather than a stopgap.

**Trigger.** Either mobx-react-lite ships a compiler-compatible subscription API and #3874 closes, or
`rolesStore` / `warehouseEditStore` move to `useSyncExternalStore` with immutable snapshots, which the
compiler reads correctly. Then delete the directives and confirm with a Babel probe that those files
emit `_c(n)` cache slots, and by hand that the canvas still repaints on every store mutation —
the failure mode is a frozen subtree, and no test in the repo covers it.

## Live-режим телеметрии через обратный SSH-туннель

**Что это даёт.** Прод-коллектор пишет телеметрию в файлы, и до дашборда она доезжает только после
скачивания архива.
Второй экспортёр `otlp` в тот же пайплайн, направленный в `localhost:4317` внутри обратного туннеля
(`ssh -R 4317:localhost:4317`), даёт живой поток на машину разработчика, пока туннель открыт, не
трогая файловый архив. С `sending_queue` на диске батчи, накопленные при закрытом туннеле,
досылаются при следующем подключении.

**Почему не сделано.** Ради разбора инцидента постфактум это лишний движущийся узел: очередь на
диске конкурирует за место с самим архивом, а `retry_on_failure` при постоянно закрытом туннеле
держит батчи в памяти коллектора, которому выделено 192 МБ. Пока телеметрию смотрят после события,
а не во время, файлов достаточно.

**Триггер.** Появление класса багов, воспроизводимых только на проде и только вживую — гонка,
исчезающая при повторном прогоне, или зависание, которое нечем поймать по факту. Тогда добавить
экспортёр `otlp` с `sending_queue: {storage: file_storage}` в пайплайны
`otel/collector.prod.yaml` и вынести адрес туннеля в переменную окружения.

## Drop empty `MarketplaceSyncScanJob` runs from traces

**What it buys.** `SyncScanCron: "0 * * * * ?"` wakes the job once a minute, and in the vast majority
of runs it finds no account due for a sync. That is roughly fifteen hundred traces a day, each one a
single span over a single `SELECT`. In the file archive they eat rotation budget; in the dashboard
they bury the trace list, so every incident starts with scrolling past noise.

**Why it is off.** By design the filtering belongs in the collector's `filter/noise` (see
[observability-specification.md](observability-specification.md#сэмплинг)), and there is nothing
there to filter on: `OpenTelemetry.Instrumentation.Quartz` records the job name, the trigger and the
duration — no attribute says "did nothing". Duration is not a usable proxy either, since an empty
pass and a pass with one fast account differ by milliseconds.

**Trigger.** The job setting an attribute of its own on the current span — say
`app.sync.accounts_scanned` — right after it selects candidates. The collector rule is then a single
line (`attributes["app.sync.accounts_scanned"] == 0`) and ships by restarting one container. Adding
the attribute ahead of this task buys nothing on its own.

## Wrap the remaining business operations in spans

**What it buys.** `withOperationSpan` (see
[observability-specification.md](observability-specification.md#спаны-операций)) covers one
operation, `order.self_assign`. The rest are unwrapped — confirmation, assembly hand-off and
completion, shipping, receipts, transfers, write-offs, stocktakes, manual marketplace sync, label
printing. Until a call is wrapped, the operation's trace
starts at the HTTP request, so "why did this take a minute" cannot be answered for the part that
happened in the browser.

**Why it is off.** One wrapped operation is enough to show the scheme works end to end, and the rest
touch a dozen components across four features. Doing them alongside the infrastructure would have
mixed a wide mechanical edit into the change that has to be reviewed carefully.

**Trigger.** None — this is ready to be picked up. A single-request operation is one
`useOperationMutation(...)` at the call site; one that sends several requests calls
`withOperationSpan(...)` from the handler and spreads `op` into each request. Attributes are the ones the
table already names.
