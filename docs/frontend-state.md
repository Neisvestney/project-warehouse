# Frontend State

How state gets into and out of components: URL search params, form ↔ API error wiring, and the MobX bridge.
Architecture overview: [frontend.md](frontend.md).

## URL State Hooks

Page state — search text, filters, pagination, open drawers, active tab — lives in the URL, so every screen is
bookmarkable, shareable and back-button friendly. Transient UI state (spinners, hover, a dialog that must not
survive a reload) stays in `useState`.

### `SearchParamsProvider`

Wraps every authenticated page (mounted in `MainLayout`). Batches all `setParam` calls made in the same
synchronous tick into a **single** `setSearchParams` navigation via `queueMicrotask`. Without this, two hooks
updating in one render would each read the pre-update URL and the second would overwrite the first.

Pushed updates are also kept in an *unconfirmed* map and re-applied on every following navigation until the
router's params actually contain them. React Router commits navigations inside a transition, so a batch fired
before the previous one committed would otherwise build on a `prev` that still misses the earlier update.

The map is cleared once the URL matches the search string the last push was building — captured inside the
updater, where the result is exactly known. From that point the URL wins again, so an external back/forward
navigation can never be overwritten by a stale re-application.

### `useDebounce<T>(value, delay?)`

Generic debounce hook. Returns the debounced copy of `value`; updates only after `delay` ms of inactivity
(default 300 ms).

### `useBackClosable(open, onClose)`

Makes a fullscreen overlay dismissable with Back. While `open`, the hook holds one history entry of its own
(`history.pushState` with the router's own state carried over plus an `__overlay` marker), so a Back press —
including the hardware button in the Capacitor build — pops that entry and the `popstate` listener calls
`onClose` instead of the user leaving the page. Closing the overlay any other way drops the entry again in
the effect cleanup.

The marker holds the hook instance's `useId`, not a plain flag, which is what keeps stacked overlays
independent. `popstate` is a window event and reaches every open overlay's listener, so each one compares the
marker now current against its own id: the overlay whose entry was popped closes, the ones still holding
theirs ignore the event. The cleanup uses the same comparison before calling `history.back()` — without it an
overlay would drop a neighbour's entry and a single Back press would walk back several steps. The comparison
also absorbs the duplicate push StrictMode's double-invoked effect produces in dev.

`idx` is copied unchanged, so the held entry is invisible to react-router's own index tracking. That is safe
while nothing in the app uses `useBlocker` / `usePrompt`, which are the only consumers of that delta.

Used by `MainNavDrawer` and `GlobalSearchModal`.

**Links inside such an overlay must navigate with `replace`** (`<Link replace>` / `navigate(to, {replace: true})`).
The held entry carries the current URL, so replacing it puts the destination exactly where the overlay stood
and Back from there returns to the page the user came from; a plain push would leave a duplicate entry that
swallows one Back press.

That navigation also has to be **synchronous**, which is what the declarative router in `main.tsx` gives.
Under a data router with async loaders `onClose` would run before the entry is actually replaced, and the
cleanup would call `history.back()` over a navigation still in flight.

### `useSyncedWithQueryState(key, fromQuery, toQuery)`

Binds a typed state value to a single URL query param. Returns `[value, setValue]`; `setValue` writes through
`SearchParamsProvider`.

```typescript
const [search, setSearch] = useSyncedWithQueryState(
  "search",
  (q) => (typeof q === "string" ? q : ""),
  (v) => v || null,
);
```

`toQuery` returning `null` **removes** the param. Use that for the default value so the canonical URL stays
clean — but be careful when the default is not the "empty" value: `useCatalogTypesFilter` stores an empty
selection as the literal `none`, because dropping the param would read back as "all types".

#### Tabbed detail pages

Tabs inside a detail page are **not** separate routes — they are a `<Tabs>` plus one `useSyncedWithQueryState`
entry, so the page loads its entity once and every tab's own filter/sort/page params share the same URL. Reuse
this rather than adding `:id/<tab>` subroutes.

```typescript
const TAB_KEYS = ["overview", "warehouses", "cards", "runs"] as const;
type TabKey = (typeof TAB_KEYS)[number];

const [tab, setTab] = useSyncedWithQueryState<TabKey>(
  "tab",
  (q) => (TAB_KEYS.includes(q as TabKey) ? (q as TabKey) : "overview"),
  (v) => (v === "overview" ? null : v),
);
```

Render tabs conditionally (`{tab === "cards" && <CardsTab />}`) so inactive tabs hold no queries, and validate
the value against what is actually available — a deep link can name a tab the current entity does not have.

Because the tabs share one URL they also share param names, so switching tabs must clear every tab-scoped param
(`search`, `page`, `pageSize`, `sortBy`, `sortOrder`, `archived`, and page-specific ones) — otherwise `page=2`
from one tab lands the next on an empty page. `setParam` from `useSearchParamsContext` batches all same-tick
calls into one `replace` navigation, so clearing the list and setting `tab` costs a single history entry:

```typescript
const changeTab = (next: TabKey) => {
  for (const key of TAB_SCOPED_PARAMS) setParam(key, null);
  setTab(next);
};
```

### `useDebouncedSyncedWithQueryState(key, fromQuery, toQuery, delay?)`

Combines local state, `useDebounce` and `useSyncedWithQueryState` into one hook for lag-free inputs that sync to
the URL after a debounce. Returns `[localValue, setLocalValue, urlValue]`.

- `localValue` / `setLocalValue` — bind to the input element (updates instantly, no re-navigation per keystroke)
- `urlValue` — the debounced URL-synced value; pass this to API query params
- `localValue` is synced back from the URL when it changes externally (browser back/forward, deep link)
- `T` is constrained to primitives (`string | number | boolean | null | undefined`) — a `fromQuery` returning a
  fresh object every render would make the URL-change check fire on every render and loop. Object-valued params
  belong in plain `useSyncedWithQueryState`, which has no sync-back effect

The hook remembers the last value it pushed and ignores that echo when it comes back from the URL. Since the
navigation commits in a transition, the echo can arrive after the user has typed further characters — syncing it
back would rewind the input and swallow them.

```typescript
const [inputValue, setInputValue, searchString] = useDebouncedSyncedWithQueryState(
  "search",
  (q) => (typeof q === "string" ? q : ""),
  (v) => v || null,
);
```

### `useParamsState(debouncedParams, debouncedDeps, immediateParams, delay?)`

Merges debounced and immediate params into one object. Debounced params settle after `delay` ms (default
300 ms); immediate params are always current. Use the merged result as query options.

### `usePaginatedParams(debouncedParams, debouncedDeps, immediateParams?, immediateDeps?, options?)`

Pagination wrapper managing `page` and `pageSize` from the URL. Resets `page` to 1 **atomically** when debounced
params settle or immediate params change — otherwise the new filters and the old page number would fire one
wasted request together. Syncs `page` back from the URL on browser back/forward.

```typescript
const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams(
  {},
  [],
  {searchString},  // immediate params — bypass the internal debounce, also reset page
  [searchString],
);
```

`page` is **1-based** throughout the app, including `DataTableContainer`, which converts to MUI's 0-based
value internally.

### `useDrawerSearchParamsState(name)`

Manages the open/close state of a detail drawer (or dialog) by storing the selected entity id in a URL query
param. Returns `[selectedItemId, openDrawer, closeDrawer]`.

- `selectedItemId` — `string | null`; the current value of `?{name}=`, or `null` when closed. Pass directly to
  `open={!!selectedItemId}` and to the entity fetch.
- `openDrawer(id)` — navigates **forward** (no `replace`), so pressing browser back closes the drawer. It also
  records the history index the drawer opened at.
- `closeDrawer()` — walks the history back to that recorded index, so closing via button/×/escape leaves the
  user where they were before opening the drawer, however many entries were pushed while it was open. It falls
  back to dropping the param with `replace: true` whenever that walk is not well defined: no index was recorded
  (the drawer was restored from a bookmarked URL rather than opened in this session), or the recorded index is
  no longer behind the current one and has therefore gone stale. Guards against no-ops when the param is
  already absent.

The index comes from `idx` in `history.state`, which react-router maintains — **not** from `history.length`.
`history.length` counts forward entries too, never shrinks on back, and grows with anything an overlay pushes
on top (see [`useBackClosable`](#usebackclosableopen-onclose)), so a delta measured from it walks back too far.

Use it for any drawer whose open state should survive a refresh; do not use it for transient UI state.

```tsx
const [selectedId, openDrawer, closeDrawer] = useDrawerSearchParamsState("item");

<TableRow onClick={() => openDrawer(item.id)} />
<MyDrawer open={!!selectedId} onClose={closeDrawer} item={…} />
```

**Params of drawers nested inside a parent whose own open state is *not* in the URL** must be registered in
`EPHEMERAL_PARAMS` (`utils/ephemeralSearchParams.ts`) — otherwise a refresh restores the nested drawer with its
parent closed. `FulfillmentsDrawer` (`?fulfillmentCatalogItem=`) is the reference case; see
[`stripEphemeralSearchParams()`](frontend.md#stripephemeralsearchparams).

Param names in use: `?item=` (`CatalogPage`, page-local), `?storagePlace=` (`WarehouseViewPage`),
`?catalogItem=` / `?unitCatalogItem=` (`ItemsBasePage` and every page showing catalog item links),
`?fulfillmentCatalogItem=` (ephemeral). `"catalogItem"` is the shared, cold-load-safe name and must never be
reused for a nested drawer.

## Form Hooks

### `useRhfApiErrors<T extends FieldValues>(form)`

Bridges API error responses to a react-hook-form instance. Returns `{setApiError}` — call it from a mutation's
`onError`.

| Error shape | What happens |
|---|---|
| `AppProblemDetails` with field errors | Each field key (except `"root"`) → `form.setError(field, {type: "server", message})` |
| `AppProblemDetails` with root errors | `"root"` errors → `form.setError("root", …)` |
| `AppProblemDetails` with no matching errors | Falls back to `error.title ?? "Неизвестная ошибка"` on `"root"` |
| Any other error shape | Shows a modal alert via `useModal().showAlert` |

Field error messages are resolved through `resolveErrorMessage`, which prefers the detailed
`errorCodeArgMessages` template when `args` fills **every** `{placeholder}` (e.g. `insufficientInventory` gets
the item name, quantities and the cell path), otherwise falls back to `errorCodeMessages` and interpolates
whatever `args` are present.

```tsx
const form = useForm<LoginFormValues>();
const {setApiError} = useRhfApiErrors(form);

const mutation = useMutation({
  ...postApiAuthLoginMutationOptions(),
  onSuccess: handleSuccess,
  onError: setApiError,
});
```

Field-level errors surface automatically through `FormTextField`. **Root errors must be rendered manually:**

```tsx
{form.formState.errors.root && (
  <Alert severity="error">{form.formState.errors.root.message}</Alert>
)}
```

## `ObservableForm<TFieldValues>`

A class (`components/ObservableForm.ts`) creating a bidirectional bridge between a **react-hook-form** instance
and **MobX**. It holds `_data` — a MobX observable snapshot of the form values — and keeps it in sync with the
RHF form in both directions via a `watch` subscription (RHF → MobX) and a MobX `reaction` (MobX → RHF). A
`_syncing` flag prevents feedback loops.

**When to use:** when a page uses a MobX store alongside an RHF form and you need other store computeds or
reactions to react to form field changes, or you need to push external data (e.g. an API response) back into the
form from the store.

```ts
// In the store
class MyStore {
  form = new ObservableForm<MyFormValues>();

  loadData = async () => {
    const data = await fetchSomething();
    this.form.data = data; // pushes into RHF via setValue / reset
  };
}

// In the component
const rhf = useForm<MyFormValues>();
useEffect(() => store.form.init(rhf), []);

// In an observer component or MobX reaction — reactive read:
const value = store.form.data?.someField;
```

**Key behaviour:**
- `init(deps)` — connects to RHF; must be called once inside `useEffect`. Returns a cleanup function — return it
  from the effect so subscriptions are torn down on unmount.
- `data` getter — MobX-observable; reading it inside an `observer` / `computed` / `reaction` makes that context
  re-run on any field change.
- `data` setter — replaces all form values; changed fields are applied via `setValue`, a full-object replacement
  falls back to `reset` (preserving dirty/touched/error state). Throws if called before `init`.

**MobX → RHF detail:** uses `recursive-diff` to compute the minimal set of changed paths and calls `setValue`
only for those. When the diff touches the root (the entire object was replaced), it falls back to `reset` with
`keepDirtyValues`, `keepErrors`, `keepDirty`, `keepIsSubmitted`, `keepTouched`, `keepIsValid` and
`keepSubmitCount` all `true`, to preserve as much form state as possible.
