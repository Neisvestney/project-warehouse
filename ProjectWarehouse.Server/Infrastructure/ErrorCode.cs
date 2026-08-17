namespace ProjectWarehouse.Server.Infrastructure;

public enum ErrorCode
{
    // Auth
    InvalidCredentials,
    TokenOutdated,
    TokenInvalid,
    RefreshTokenInvalid,
    RefreshTokenExpired,
    RefreshTokenRevoked,

    // Access
    PermissionDenied,
    RoleProtected,

    // Entities
    UserNotFound,
    RoleNotFound,
    PermissionNotFound,
    UserAlreadyExists,
    RoleAlreadyExists,
    PermissionAlreadyAssigned,
    WarehouseNotFound,
    StoragePlaceNotFound,
    CatalogItemNotFound,
    CatalogItemCharacteristicNotFound,
    StoragePlaceNodeNotFound,
    StoragePlaceNodeHasChildren,
    StoragePlaceNodeHasItems,
    StoragePlaceNodeParentHasItems,
    StoragePlaceNodeCyclicParent,
    StoragePlaceNodeItemsGroupNotFound,
    CatalogItemCharacteristicDuplicate,
    WarehouseHasItems,
    StoragePlaceHasItems,
    CatalogItemIsInUse,
    CatalogItemIsImmutable,
    CatalogItemArticleDuplicate,
    CatalogItemBarcodeDuplicate,
    CatalogItemCharacteristicBarcodeDuplicate,
    CatalogItemGroupInvalid,
    CatalogItemManagedByGroup,
    CatalogItemVariationInvalid,
    CatalogItemComponentInvalid,
    CatalogItemComponentNotFound,
    CatalogItemCircularDependency,

    // Inventory operations
    UnitInventoryItemNotFound,
    InventoryItemMovedToAnotherNodeAfterPlacementCreated,

    // Transfers
    TransferSameNode,

    // Writeoffs
    WriteoffNotFound,
    WriteoffNotDraft,
    WriteoffHasNoItems,
    WriteoffItemNotFound,
    WriteoffInsufficientInventory,
    WriteoffNotAssignedToWarehouse,

    // Receipts
    ReceiptNotFound,
    ReceiptInvalidStatusTransition,
    ReceiptHasPlacements,
    ReceiptItemNotFound,
    ReceiptItemPlacementNotFound,
    ReceiptNotAssignedToWarehouse,
    ReceiptItemsUnderplaced,
    ReceiptItemsOverplaced,
    InsufficientInventory,
    InventoryItemNodeMismatch,
    UnitInventoryItemNumberDuplicate,

    // Orders
    OrderNotFound,
    OrderNotDraft,
    OrderNotConfirmed,
    OrderNotAssembly,
    OrderInvalidStatusTransition,
    OrderHasFulfillments,
    OrderNotAssignedToWarehouse,
    OrderBoxNotFound,
    OrderBoxComponentNotFound,
    AssemblyTaskNotFound,
    AssemblyTaskNotDeletable,
    AssemblyTaskBoxNotFound,
    AssemblyTaskBoxComponentNotFound,
    AssemblyFulfillmentNotFound,
    AssemblyFulfillmentInvalidType,
    AssemblyTaskAlreadyDone,
    AssemblyTaskMoveTargetInvalid,
    AssemblyTaskQuantityExceedsAvailable,
    AssemblyComponentAlreadyFulfilled,
    CatalogItemNotVariationMember,

    // Marketplaces
    // Persisted as int inside MarketplaceSyncRun.Error / MarketplaceAccount.LastSyncError jsonb — append only.
    MarketplaceAccountNotFound,
    MarketplaceCredentialsInvalid,
    MarketplaceCredentialsUnreadable,
    MarketplaceClientIdRequired,
    MarketplaceApiError,
    MarketplaceSyncAlreadyRunning,
    MarketplaceSyncInterrupted,
    MarketplaceCardMappingTypeNotAllowed,
    MarketplaceCardMappingArchivedItem,
    MarketplaceWarehouseNotFound,
    MarketplaceCardNotFound,

    // Routing
    RouteNotFound,

    // Validation
    Required,
    TooShort,
    TooLong,
    InvalidFormat,
    OutOfRange,
    InvalidJson,
    PasswordTooShort,
    PasswordAtLeastOneDigit,
    PasswordAtLeastOneUppercase,
    PasswordAtLeastOneLowercase,
    PasswordInvalid,
    ValidationError,

    // DataFiles
    DataFileNotFound,
    DataFileEmpty,
    DataFileTooLarge,
    DataFileTypeNotAllowed,
    DataFileNotAnImage,
    DataFileWidthNotAllowed,
    DataFileStorageError,

    // FBS order sync. Deliberately not filed with the marketplace block above: these values are
    // persisted as ints in the Error / LastSyncError / SkippedOrders jsonb columns, so inserting into
    // the middle of the enum would silently reinterpret every error already stored.
    MarketplaceOrdersNotSupported,
    MarketplaceAccountHasOrders,
    MarketplaceAccountInactive,
    MarketplaceLabelNotReady,
    MarketplaceOrderNotFromMarketplace,
    MarketplaceOrderCardNotMapped,
    MarketplaceOrderWarehouseNotMapped,

    // Stocktakes
    StocktakeNotFound,
    StocktakeInvalidStatusTransition,
    StocktakeNotAssignedToWarehouse,
    StocktakeHasNoNodes,
    StocktakeNodeNotFound,
    StocktakeNodeAlreadyInProgress,
    StocktakeUnitCountedTwice,
    StocktakeUnitItemInAnotherWarehouse,
    StocktakeUnitItemDetached,
    StocktakeConcurrentModification,
}
