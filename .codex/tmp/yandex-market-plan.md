# Yandex Market implementation plan

## Scope and evidence

Implement a usable Yandex Market FBS integration at the existing WB integration level: account connection, warehouses, cards, order discovery/statuses/history, parcels and labels, returns, accruals, organization linking, and existing UI flows. The repository explicitly scopes YM to FBS (Express uses the same contract). FBY/DBS, stock push, writing assembly/statuses/markings to YM, buyouts and supplies are outside the existing YM specification. Do not advertise unsupported capabilities.

Read `docs/README.md` and the recipes for every affected layer before editing. Relevant specifications: `marketplaces-specification.md` (YM client, account, synchronization and YM warehouses/cards sections), `marketplaces-fulfillment-specification.md` (YM account, order unit, status normalization, parcels, labels), `marketplaces-orders-specification.md`, `marketplaces-returns-specification.md` (YM section), `marketplaces-accruals-specification.md` (YM section), `organizations-specification.md`, and the backend/frontend recipes referenced from README.

WB history inspected: `151ee7b` client foundation, `89cfd0f` live verification, `6a268b9` actual support (provider/client/auth/options/DI/project/Dockerfile/frontend/labels), `c1a7d1e` sticker identifier, `050eb7d` accrual/return linkage, `85e9936` reconstruction, `7f36ed2` return attempts/linker. Current WB has FBO reconstructed from reports; do not copy that algorithm to YM. Main lesson: integration correctness includes stable source identities and links, not just listing orders.

The YM generated library already exists under `src/Marketplaces/YandexMarket`. Server integration is absent. Never edit generated C# by hand. Current whitelist contains campaigns, business orders, offer mappings, warehouses/stocks, label data/PDF, returns, auth; finance report generation/status are missing.

Official sources checked during research (2026-11-23):
- https://yandex.ru/dev/market/partner-api/doc/ru/reference/orders/getBusinessOrders
- https://yandex.ru/dev/market/partner-api/doc/ru/reference/orders/getOrders
- https://yandex.ru/dev/market/partner-api/doc/ru/reference/reports/generateUnitedNettingReport
- https://yandex.ru/dev/market/partner-api/doc/ru/reference/reports/getReportInfo
- https://yandex.ru/dev/market/partner-api/doc/en/concepts/error-codes

Local specs include live verified behavior that is more precise than generic official documentation. Use the official raw schema under `src/Marketplaces/specs/yandex-market/openapi` for field types. Do not invent response fields or map undocumented amounts/dates to financial facts.

## 1. Account identity, validation and connection UI

- Account = one selected YM campaign, not a whole business. Add nullable `ExternalBusinessId`, `ExternalCampaignId` to `Domain/MarketplaceAccount.cs`, account DTOs/changelog mapping and requests as needed. `ExternalClientId` remains Ozon-only and null for YM. Extend `MarketplaceCredentials` and EVERY constructor site (controllers, sync/label services and tests) with business/campaign context using optional appended parameters or a central account credential factory.
- Add a campaign-discovery backend action, protected by existing integrations permissions, taking an entered API key without persisting or returning it. GET `/v2/campaigns`, exhaust pages; return only `placementType=FBS` and `apiAvailability=AVAILABLE`, with campaign/business identity and useful name. Server validates selected campaign belongs to the supplied key and expected business when creating/rotating credentials; never trust arbitrary posted business IDs.
- Enforce one WMS account per YM campaign with a database unique filtered index (and user-facing duplicate validation); do not rely only on `AnyAsync`. Campaign identity is immutable on edit: change API key and other settings, not campaign of an account with history. A key rotation must retain access to the stored campaign.
- Add field-path error codes/messages following error/validation recipes; enum values append-only. Keep secrets out of URLs, logs and audit snapshots.
- `MarketplaceAccountCreatePage.tsx`: selecting YM shows API key -> load campaigns -> explicit campaign selection -> submit. Changing key invalidates stale selections. Use existing form/query patterns. `EditAccountDialog.tsx` rotates key without changing campaign. `AccountOverviewTab.tsx` shows business/campaign identifiers and sensible key labels. Extend `components/marketplace/marketplaceUtils.ts` and UI error localization. Preserve existing Ozon/WB behavior.
- Use a service for shared campaign validation/discovery logic; controllers remain thin. Add EF migration/snapshot. Update account search/DTOs and changelog field labels if needed.

## 2. Adapter, transport and code generation

- Add `Integrations/YandexMarket/IYandexMarketClient.cs`, `YandexMarketClient.cs`, `YandexMarketMarketplaceProvider.cs`, request credential adapter and response logging wrapper following current WB/Ozon separation.
- Provider flags: Warehouses, Cards, SellerInfo, Orders, Labels, Returns, Accruals. No Fbo, Buyouts, Supplies, ReversedSaleReturns or StockPush. Unreachable provider members explicitly unsupported.
- Use existing `MarketplaceRequestContext` + `IYandexMarketCredentialsProvider`/`YandexMarketAuthHandler`. Scope context around EACH async-enumerator MoveNext, not only iterator creation (AsyncLocal does not survive yields).
- Add `YandexMarketOptions` to `MarketplacesOptions`, validated settings for HTTP/page throttling, report polling timeout/interval, report request spacing, order lookback/overlap and label batching. Avoid implicitly using Ozon settings for YM.
- Register typed generated API client, adapter/provider and credential handler in `Program.cs`; add project reference in server csproj and Dockerfile project-copy entry if required for restore.
- Error wrapping preserves marketplace status and bounded response body; 401/403 are invalid credentials. YM uses 420 throttling. Finance report generation MUST NOT be automatically retried for 420. Retry transport/transient failures cautiously; never duplicate report generation through generic retries. Separate anonymous report-download transport from authenticated API and label transport.
- Add `POST /v2/reports/united-netting/generate`, `GET /v2/reports/info/{reportId}` to generator whitelist. Generator currently rejects non-GET/non-get operation IDs: explicitly allow this read-only report generation operation, not arbitrary POST mutations. Regenerate from local raw specs, preserve BSD license. Raw specs already exist; use fetch only if necessary and report any external access blocker.

## 3. Seller name, warehouses and cards

- Campaign detail supplies Name = `domain`, not business name. No synchronous seller INN/legal identity endpoint. Current `SyncSellerInfoAsync` unconditionally overwrites all identity fields with null: extend semantics narrowly so a YM name-only result DOES NOT erase INN later obtained from reports or its organization link. Existing Ozon/WB clearing behavior must remain intact.
- Business warehouses are filtered by account campaignId; map id/name/express to ExternalWarehouse, Fbs or Express, Active, no fabricated address/raw status. Exhaust all pages when applicable. General missing/archive and mapping rules remain.
- Offer mappings are business-wide, so every connected campaign stores all business cards independently. Page limit 100/page_token. ExternalId and OfferId = offer.offerId; Sku = mapping.marketSku; name, barcodes, first image, basicPrice/currency map directly. Archived = offer.archived OR selected campaign ARCHIVED OR selected campaign absent. Other campaign statuses do not archive.
- Stock synchronization/push is not a current WMS feature; do not introduce it just because the generated stock method exists.

## 4. Order discovery, changes, history and identities

- Use POST `/v1/businesses/{businessId}/orders` with mandatory one-element campaignIds everywhere; page limit <=50/page_token. Never use deprecated campaign order list. Explicitly exclude fake/test orders.
- Normalization follows fulfillment spec: PROCESSING/STARTED -> AwaitingPackaging; PROCESSING/READY_TO_SHIP -> AwaitingDeliver; shipment/delivery stages -> Delivering; delivered/pickup -> Delivered; CANCELLED -> Cancelled. Preserve raw status/substatus, warn and use Unknown for unknown values. Do not import PLACING/RESERVED/UNPAID/PENDING. Read exact mappings from spec/generated enum.
- New orders and changes should use updateDateFrom/updateDateTo watermark with overlap. Windows <=30 days. A paused account must catch up through multiple windows without adding a recent creation-date filter that drops old changed orders. Current `ImportFbsPostingsAsync` applies Ozon recent creation and only external statuses; adapt the discovery/query contract or YM path so changed working drafts are also refreshed, while preserving WMS draft confirmation workflow. Avoid a provider cache whose correctness depends on call order.
- Existing architecture runs active discovery, draft confirmation, then remaining imports and status catch-up. A practical implementation may retain active working-order discovery plus changed stream for missed completed orders if that is the smallest safe extension, but must cover every changed order and all open orders regardless of age. Do not silently interpret update windows as creation windows.
- History uses creationDateFrom/To windows <=30 days, all importable statuses. API creation filters are day-based; handle inclusive/exclusive boundaries and deduplicate IDs. Earliest probe: documented backwards 30-day windows with a finite bound and cancellation; first empty window can hide earlier activity after a gap, so do not present it as mathematically exact beyond verified coverage.
- PostingNumber = orderId; items have offerId/count/id. Add nullable `ExternalItemId` to ExternalPostingItem, OrderMarketplaceItem and DTOs/migration, populated on create AND draft rebuild/matching. Do not merge distinct source lines solely because they have the same offer SKU. Parcel contents use source item ID -> item index, not a guessed SKU match.
- Parse delivery.boxesLayout into parcel id/barcode/items, quantity OR partialCount, maintain layout changes while draft. Existing order service handles parcel topology but confirm fields carefully. Track warehouseExternalId, dates and item prices only from actual source fields with correct unit/currency semantics. Map cancellation/loss reason codes using YM table in orders spec; unknown codes preserve raw data. Do not invent CancelledAfterAssembly or delivery timestamp.
- Status catch-up by orderIds chunks <=50 is a useful safety path; no date filtering should exclude requested IDs. Preserve WMS assembly/status invariants and account/warehouse assignment rules.

## 5. Parcel labels, not one label per order

- YM label granularity is one parcel. Existing `ExternalLabelPage` has no parcel identifier and `StorePagesAsync` groups by posting then takes First(): this WILL drop second and later YM parcels. Extend the contract with an optional parcel external ID (or equivalent keyed result) and modify label fetch/store/export stamping to handle all parcels. Preserve single-place WB and Ozon behavior.
- Fetch label data for each order, resolve `boxId`, `fulfilmentId`, `place` and `url`. Request each returned URL unchanged with Api-Key only for trusted YM API host; do not build shipmentId from delivery.shipment.id. URL uses no `/v2` prefix; shipmentId path parameter is deprecated and not the order shipment id.
- Bind page to parcel by boxId, barcode = fulfilmentId, not by text extraction or page position. Stamp WMS articles corresponding to that parcel's contents; support partial item parcels. Cache label on the actual MarketplaceParcel with existing real FK file lifecycle. Do not overwrite global order barcode repeatedly with multiple parcel barcodes.
- Printing remains restricted to normalized AwaitingDeliver, where YM layout is stable. Corrupt/not-ready parcel must not cause another parcel's label to be reused. Use existing failure reporting and decide atomic order failure versus per-parcel reporting according to existing UI semantics; never silently return an incomplete order as fully printable.
- Add an appropriate YM LabelKind/layout based on real format; do not reuse an Ozon layout blindly. Default request format and stamping coordinates must avoid covering the source barcode.

## 6. Returns

- GET campaign returns, limit <=100/page_token, new fromDate/toDate filters (update day), no deprecated snake-case dates.
- Expand each return item into ExternalReturn with stable ExternalId `{id}:{shopSku}`, Quantity=count, orderId, marketSku/shopSku. UNREDEEMED -> FullRefusal (order remains cancelled); RETURN -> CustomerReturn (delivered).
- Add RawRefundStatus to ExternalReturn/entity/DTO/migration and copy/update/equality paths. RawStatus is shipmentStatus, StatusChangedAt=updateDate, ReturnedAt=creationDate; FinalAt/compensation status unknown/null. IsCancelled if refund CANCELLED/REJECTED or shipment CANCELLED/EXPIRED.
- Reasons from decisions reasonType/subreasonType/comment using documented table. Source total return amount is not per-unit Price; derive price/currency from linked order item (also when linked after import), never divide an unrelated total silently.
- Incremental sync uses >=one-day overlap. History must request updates from history-start through TODAY (then filter actual creation period if needed), not stop update-date range at history-end; otherwise late-modified old returns vanish. Compensation pass unsupported; no fabricated compensation status. Preserve general late-linking/relinking logic.

## 7. Accrual report, idempotency, throttling and INN

- Netting report with format=JSON, language explicitly Russian if required for stable source labels, businessId + campaignIds=[selected campaign]. Async report poll DONE then download zip and parse transaction_date.json; inspect raw report schema/verified docs before implementing parser. Stream or bound downloaded content, do not write arbitrary zip entries to disk.
- Parse transactionDate `dd.MM.yyyy HH:mm` as marketplace accounting time, transactionSum signed, orderId/shopSku/type/source/service/count/inn. Stable accrual group `{orderId}:{yyyy-MM-dd}` or `account:{date}`; deterministic line sorting by RawTypeId, Sku, Category, Amount. Group rewritten entirely. Ignore seller's own balance top-up. Sale includes marketplace discount bonus, returns negative Sale, bonus spending remains expense; do not count bonus twice.
- Map exact netting source/service labels to categories in accrual spec, unknown services -> Other with raw diagnostic, never guess missing fees. Currency RUB. ExternalAccrualLine currently only Sku and linker only compares card.Sku: implement YM shopSku->OfferId matching (explicit line OfferId or provider-specific resolution preserving existing stored Sku convention), including late relink and multiple-line orders. Do not let an unmatched line fall onto an arbitrary first item.
- Report periods <=3 calendar months. History cuts into windows; space generation calls >=2 minutes BUSINESS-wide, including calls by different WMS accounts/keys for same business. Current worker is serial but calls may also occur outside it; a singleton keyed coordinator is a reasonable current single-node solution, with persisted account pause/watermark for restarts. Do not hold DB row locks or transaction open while waiting/polling/downloading.
- Add nullable AccrualsPausedUntil to account. On report-generation 420: no retry, pause >=2 minutes, preserve unconsumed period/watermark, successful sync with `MarketplaceSyncRun.Warning`. On partial history import, retain saved windows, warn and finish step; next attempt can re-read idempotently. Never move AccrualsSyncedAt/FullPassAt beyond an unread window. Preserve relinking of saved groups even if later generation is throttled.
- INN from nonempty report rows updates account under entity lock and calls `IOrganizationService.LinkByInnAsync`; empty reports leave it untouched, manual organization links remain. Transport/provider needs an explicit metadata path for INN (including reports whose only rows are ignored balance operations), not a cast to concrete client or hidden AsyncLocal side effect. Names/legal identity unavailable from campaign API remain null.

## 8. Validation and handoff

- Add meaningful tests for campaign validation/isolation/duplicates and key rotation, complete pagination, order status/creation-vs-update windows including old changed orders, source line and parcel mapping, multi-parcel labels and corrupt/missing pages, return cancelled/refund statuses and history late updates, accrual parser/category/sign/stable grouping/OfferId linkage, 420 pause/no watermark advance/partial import/business quota, and INN not cleared by seller step.
- Use fake generated-client HTTP responses or adapter mocks; use existing PostgreSQL integration-test fixture where real EF/unique-index behavior matters. Do not claim DB invariants are validated with InMemory alone.
- Build backend and run relevant/full server test project as appropriate. Regenerate frontend API from backend OpenAPI using existing workflow; start/stop dev servers through Rider MCP only, CLI builds/codegen commands through exec. If restore/network/runtime blocks occur, report exact checks that could not run and continue unaffected checks.
- After frontend edits run `npm run prettier:fix`, `npm run eslint:fix`, `npm run typecheck` in src/projectwarehouse.client. Review changes from repository-wide fix scripts and avoid unrelated edits.
- Self-check implementation; do not launch your own review agent. Parent will request independent level-2 codereview using `C:/Users/neisv/.codex/skills/codereview/SKILL.md` after your final report. Fix confirmed in-scope findings when parent sends them.
- Do not create commits/deploy or call external marketplace write operations. Use existing local API dev token for safe local tests. Real-key live checks only if key is already available and authorized; never print it. Absence of credentials does not block fixture-based implementation, but disclose that real account flow was not tested.

## 9. Documentation as final implementation step

Update README marketplace summary, platform, fulfillment, orders, returns, accruals, frontend component/state docs only where behavior/invariants changed. Remove YM “not implemented” claims only for delivered functionality. Describe current behavior without change history or duplicating derivable DTO/API listings. Document supported FBS scope, campaign selection, report quota, pause/watermark semantics, parcel labels and known runtime verification limits.

## Acceptance and risks

No blocking product question identified: repository already specifies YM FBS and the one-campaign account model. Essential acceptance: a key with multiple FBS campaigns requires explicit choice; campaigns cannot leak each other's orders/returns/accruals; old changed orders are imported; all parcel labels print on the correct parcels; rerunning report/returns makes no duplicates; 420 gives warning/pause with resumable marks; seller-name sync never erases report INN; Ozon/WB tests stay green.

Risks needing honest reporting: finance source lacks true transaction ID and localized labels can change; earliest-date probe cannot prove absence before a quiet month; live label layout/zip rows need verified fixtures; shared business quota must cover more than per-account locks; full-history generation is intentionally slow. Do not address these by dropping finance, returns or multi-parcel support.
