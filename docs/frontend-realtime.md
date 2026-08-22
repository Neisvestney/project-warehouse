# Frontend Realtime

The client half of the SSE channel: the connection, what a tab subscribes to, presence, staleness and advisory
edit locks. Protocol and server side: [realtime-specification.md](realtime-specification.md).
Architecture overview: [frontend.md](frontend.md).

Server-sent events are delivered over one stream per tab (`GET /api/realtime/stream`). Events are **hints to
refetch, not a source of truth**: a handler invalidates a query and the usual REST call brings the fresh state.
Ordering isn't guaranteed, a lost event isn't fatal, and reconnection replays nothing.

## `RealtimeProvider`

Mounted in `MainLayout`, so it covers every authenticated route. The provider itself is wiring: it owns `connectionId`, dispatches events to
subscribers, and hands the rest to three hooks beside it — `useRealtimeStream` (the connection),
`useWatchRegistry` (what this tab is subscribed to, plus presence), `useHeartbeat` (liveness).

They are split because their lifetimes differ: the connection dies and comes back, the registry outlives it
(the pages behind it are still mounted), the heartbeat is bound to one `connectionId`. The background
thresholds are the one place all three meet, and they live entirely in `useRealtimeStream` — the registry's
`pause` and the heartbeat's `beat` arrive there as **parameters**. Threading them through refs instead is what
would make this a tangle.

The registry takes the connection as a **ref**, not a value: `connectionReady` produces a new id and
re-registers every subscription in that same tick, long before React re-renders with it.

**Reconnection is hand-rolled, on purpose.** The generated SSE client (`src/api/core/serverSentEvents.gen.ts`)
retries on *errors*, but exits its loop when the server closes the stream cleanly — which is exactly what
happens every time the access token expires. Its attempt counter also never resets, so backoff would keep
growing across a long session. `useRealtimeStream` therefore passes `sseMaxRetryAttempts: 1` and runs its own
loop: 1 s doubling to 30 s with jitter, reset to 1 s on every `connectionReady`.

Requests go through `client.sse.get`, so `apiClient.ts` request interceptors run on each connect and every
reconnect carries a fresh `Authorization` header. Response and error interceptors do **not** run for SSE, so
the hook stops explicitly on the `auth:clear` / `auth:refreshTokenInvalid` window events.

`document.visibilitychange` (on `visible`) and `window.online` reconnect the stream immediately — this covers
Capacitor resume without pulling in `@capacitor/app`. Nothing is refetched there:
TanStack's own `focusManager`/`onlineManager` listen to those exact two events, `staleTime` is 0 across the
app, so every active query already refetches on its own. Re-invalidating would only duplicate that and would
override the few queries that deliberately set a `staleTime`.

### Outage detection

`useOutageTracker` times the gap between `onDisconnected` and the next `connectionReady` and exposes
`onReconnectedAfterOutage` on the context — a subscription of its own, not a `RealtimeEventType`, because
nothing sent it over the wire. A gap over 10 s fires it; the first connect of a page load never does,
having no gap to measure.

The threshold separates the two reasons a stream ends. A token expiry closes it cleanly and the next
attempt succeeds within a second. A restarted server is unreachable until the container comes back, which
is far longer. Precision is not required — the only consumer is the service worker update check
([frontend.md](frontend.md#update-checks)), where a false positive costs one request.

**A backgrounded tab gives up in two steps.** 20 s hidden → `unwatch` on everything it watched: no more events,
and it drops out of everyone's presence. 2 min hidden → the stream is aborted, which is what makes the server
release this connection's locks (the heartbeat stops with the `connectionId`). "Hidden" is
`document.visibilityState`, not focus — a half-covered window is still being read.

The 20 s step sends an off-cycle heartbeat first and only unwatches when the answer lists **no locks**: a tab
that holds one is still an editor, and unsubscribing it would leave it blind to changes on the very object it
is about to save over. Coming back needs no new machinery — the watch entries survive the pause, only losing
their `confirmed` flag, so the return re-registers them down the same path a reconnect takes, and
`useEditLock` re-acquires under the new `connectionId` exactly as it does after a dropped stream.

## `useRealtimeEvent(type, handler)`

```typescript
useRealtimeEvent("marketplaceSyncProgress", (_event, payload) => {
  if (payload.accountId === id) refreshAccountData();
});
```

`handler` receives the envelope and the payload already narrowed to the variant for `type`. The generator emits
the payload's own discriminator as optional, so `payload.type` cannot narrow the union — the envelope `type` is
the reliable discriminator, and `RealtimeEventPayloadFor<T>` maps it to the payload shape. The handler is kept
in a ref, so an inline arrow doesn't cause re-subscription.

## `useEntityWatch(entityType, entityId, onWatched?)` / `useEntityWatchMany(entityType, entityIds, onWatched?)`

Subscribes the connection to an object for the component's lifetime and unsubscribes on unmount. Required even
on read-only pages — an object's events reach only the connections that asked for them. `useEntityWatchMany`
takes several ids at once (`SyncOrdersDialog` watches every picked account).

Subscriptions are ref-counted in the provider, so a page and a dialog watching the same object don't cancel
each other out. Failures are silent by design: an entity missing from the response means no view permission
and `422` means the connection already died — in both cases the polling fallback keeps the screen alive, and a
fresh connection re-sends the watch.

**One request per render, not per object.** The provider collects every `watch`/`unwatch` registered during a
render into a microtask and sends one batched call — the assembly screen registers a watch per visible order,
and a request each would blow through the six-per-origin cap on its own. An entry registered and dropped
within the same batch is skipped rather than raced against its own unwatch.

**Subscribed → refetch.** `onWatched` fires after every confirmed `watch`, including re-subscription after a
reconnect, and the page invalidates its queries there. Without it, anything that changed between the action and
the subscription being registered would be lost: a run that finished while `watch` was still in flight would
leave `Running` on screen forever.

**Polling stays as a fallback.** `isWatching` is true only when every requested subscription is confirmed, and
pages gate `refetchInterval` on it (`MarketplaceAccountPage`, `AccountSyncRunsTab`, `SyncOrdersDialog`). The
condition is "no stream **or** no confirmed subscription", not just "stream dropped" — a transport problem must
never leave the user staring at a frozen screen.

## `useEntityPresence(entityType, entityId)`

Returns everyone **else** currently looking at the object — the provider keeps the list per watched key,
seeded from the `watch` response and replaced by each `entityPresenceChanged`. The hook only reads it: the page
must already be subscribed through `useEntityWatch` or `useEditLock`, and presence disappears together with the
subscription.

`EntityViewers` renders it as an `AvatarGroup` of `UserAvatar`s with a tooltip per person; a new avatar pops in
with a short CSS keyframe (avatars are keyed by user, so existing ones do not replay it), leaving is instant.
`AppBreadcrumbs` takes it as `viewersOf={{entityType, entityId}}` and puts it right after the path, so a page
adds presence in one line. The catalog drawer and the card-mapping dialog place `EntityViewers` by hand.

Where it shows: order, receipt, writeoff, stocktake, warehouse, employee and roles pages, both the edit and the
view variants, plus the card-mapping dialog. In the catalog drawer only in edit mode. Screens that watch many
objects at once — assembly, storage nodes, the sync dialog — deliberately show nothing: a row of avatars per
table row is noise, not information.

Names come from the stream connection, which reads them off the token's `given_name`/`family_name` claims —
the same full name `EditLockBanner` and `StaleDataBanner` show, never the login.

## `useStaleData(entityType, entityId, {isDirty, dataUpdatedAt, isFetching, isLoading, onRefresh})`

Warns that the object on screen may have been saved by someone else. Subscribes with `useEntityWatch` and
listens to two triggers: `entityChanged` (the precise one — the object really was written) and
`editLockReleased` from another user (the fallback, for edits that produced no changelog entry).

```typescript
const stale = useStaleData("receipt", id, {
  isDirty: isEditing,
  dataUpdatedAt,
  isFetching,
  isLoading,
  onRefresh: refreshReceipt,
});
```

An untouched form is refreshed silently — `isDirty === false` means there is nothing to lose, and a banner
there would be noise. A modified one gets `StaleDataBanner`: auto-refreshing it would erase what the user
typed, which is the very loss the warning is about.

**The subscription refetch is silent.** `onRefresh` also runs on every confirmed subscription, which on mount
lands right behind the page's first read — two requests back to back. The second one is correctness (it closes
the window where an event could be missed), but showing a loader for it would just make the page flicker, so
the hook marks it through `useSilentRefresh` and reports `showLoadingOverlay` with that refetch excluded. The
page passes `isFetching`/`isLoading` from its own `useQuery` for this and hands `showLoadingOverlay` straight
to `LoadingOverlay`. A `entityChanged` refetch is not marked and still shows the overlay.

**The flag clears itself.** `isStale` is `dataUpdatedAt < staleAt`, not a boolean the hook has to reset:
TanStack refetches every active query on focus and `staleTime` is 0 app-wide, so a read that landed after the
event has already answered the warning. The page passes `dataUpdatedAt` from its own `useQuery` for this.

It exists separately from `useEditLock` because a page can hold an editable form without being allowed to
lock the object — `CanEdit(Order)` excludes `orders.assemble_assigned`, so an assembler gets no lock and would
otherwise get no warning either.

## `useSilentRefresh(isFetching, isLoading)`

Marks individual refetches as background ones: `markSilent()` right before the invalidation, and
`isSilentRefresh` holds until the resulting fetch settles (`isFetching` back to `false`), with a one-second
grace period in case the refetch never starts at all. Also returns the ready `showLoadingOverlay`.
`useStaleData` uses it internally; a page that watches an object with a bare `useEntityWatch` — the marketplace
account page — uses it directly for the same reason.

**The flag covers the query, not one fetch.** An `entityChanged` invalidation that lands while a marked refetch
is in flight cancels it and starts its own (`invalidateQueries` defaults to `cancelRefetch: true`), but
`isFetching` never dips in between, so that refetch stays without an overlay too. The window is the couple of
hundred milliseconds right after mount and the worst case is a hidden overlay — the data still arrives and the
staleness banner still fires — so the causes are deliberately not told apart. Doing it would take promoting the
flag by hand on every visible trigger, or subscribing to `QueryCache` fetch-start events; neither is worth it.

## `useEditLock(entityType, entityId, {isDirty, dataUpdatedAt, isFetching, isLoading, onRefresh, enabled})`

Claims the object while it is being edited, on top of `useStaleData` (whose whole result it re-exports).
Acquires on mount, releases on unmount and on `beforeunload`, and re-acquires under the new `connectionId`
after a reconnect — the server dropped the old lock when the stream broke. Holding it needs no heartbeat of its
own: a lock lives as long as the connections holding it, which `RealtimeProvider` keeps alive for all of them.

**Ownership never changes behind the page's back.** A second tab of the same user *joins* the lock rather than
taking it over, so between acquiring and releasing there is nothing for the hook to watch — only another user
holding the object can keep it from being claimed. Its own 20 s interval is just a retry while that other user
holds it; it skips its tick while the tab is hidden — nobody there is waiting to edit — and a tab returning
from the background retries at once, since release events only arrive while the stream is up. The unload
release goes out as a `keepalive` fetch, since a normal one is cancelled and `sendBeacon` cannot carry the
bearer header.

```typescript
const lock = useEditLock("writeoff", id, {
  isDirty: isEditing,
  dataUpdatedAt,
  onRefresh: refreshWriteoff,
  enabled: isEditing && canEdit,
});
```

`enabled` gates only the claim, never the subscription: pages with an explicit edit mode (receipt, writeoff,
catalog drawer) take the lock when the user starts editing, so simply reading an object does not show everyone
else a false "being edited by …". The order page has no such mode and locks on mount.

On receipts and writeoffs that means **either** editor — the info form or the items drawer. The sections lift
their open state through `onEditingChange`, and the page ORs them: the items editor is the longer sitting of
the two, and leaving it unlocked would have missed the collision it exists to prevent.

Stocktakes do the same for the info form and the scope picker, but **counting is not a locking mode**: cells
are saved one at a time and several people counting different cells of one stocktake is the normal case, not a
collision — the same reasoning that keeps the assembly screen lock-free. Counting still watches the stocktake,
so a scope change by someone else refetches straight away.

Returns `{isOwner, heldBy, isLoading}` alongside the staleness fields. `isOwner === false` with a `heldBy`
renders `EditLockBanner` — **a warning only**: fields stay enabled, saving is not blocked, and `PUT` does not
check the lock. `409 editLockHeld` is never surfaced as an error; its `args` become the banner.

## `EditLockBanner` / `StaleDataBanner`

Mutually exclusive, rendered in the same slot at the top of the page: the lock banner while someone else holds
the object, the staleness banner ("… сохранил изменения. Данные могли устареть" plus «Обновить») once it is
released.

Wired with a lock: order, receipt, writeoff, stocktake, warehouse edit and user edit pages, the roles screen,
the catalog item drawer and the card mapping dialog. Watch-only, no lock: `/operations/assembly`, stocktake
counting, and the storage place drawer — parallel work there is normal, or the edits save immediately and
there is no unsaved state to guard.

**Read-only pages take `useStaleData` too** (warehouse and user view). With no form, `isDirty` is always false,
so the hook refetches silently and no banner ever appears — but without it a page left open shows someone
else's edit only after the tab loses and regains focus, which may be never.

Two pages need a note. The **warehouse editor** passes `isDirty: true` unconditionally — the canvas holds
unsaved layout from the moment it opens — and its `onRefresh` also resets the "loaded once" flag, since
invalidating the query alone would never reach the mobx store. The **roles screen** has no per-object id at
all: roles are versioned as one object, so it subscribes under the all-zero guid the changelog uses.
