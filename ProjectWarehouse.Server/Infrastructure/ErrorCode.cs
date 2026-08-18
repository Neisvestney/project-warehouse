namespace ProjectWarehouse.Server.Infrastructure;

// Persisted as ints in jsonb error payloads (MarketplaceSyncRun.Error, MarketplaceAccount.LastSyncError,
// SkippedOrders): values are pinned, so members may be reordered freely but never renumbered.
public enum ErrorCode
{
    // Auth
    InvalidCredentials = 0,
    TokenOutdated = 1,
    TokenInvalid = 2,
    RefreshTokenInvalid = 3,
    RefreshTokenExpired = 4,
    RefreshTokenRevoked = 5,

    // Access
    PermissionDenied = 6,
    RoleProtected = 7,

    // Entities
    UserNotFound = 8,
    RoleNotFound = 9,
    PermissionNotFound = 10,
    UserAlreadyExists = 11,
    RoleAlreadyExists = 12,
    PermissionAlreadyAssigned = 13,
    WarehouseNotFound = 14,
    StoragePlaceNotFound = 15,
    CatalogItemNotFound = 16,
    CatalogItemCharacteristicNotFound = 17,
    StoragePlaceNodeNotFound = 18,
    StoragePlaceNodeHasChildren = 19,
    StoragePlaceNodeHasItems = 20,
    StoragePlaceNodeParentHasItems = 21,
    StoragePlaceNodeCyclicParent = 22,
    StoragePlaceNodeItemsGroupNotFound = 23,
    CatalogItemCharacteristicDuplicate = 24,
    WarehouseHasItems = 25,
    StoragePlaceHasItems = 26,
    CatalogItemIsInUse = 27,
    CatalogItemIsImmutable = 28,
    CatalogItemArticleDuplicate = 29,
    CatalogItemBarcodeDuplicate = 30,
    CatalogItemCharacteristicBarcodeDuplicate = 31,
    CatalogItemGroupInvalid = 32,
    CatalogItemManagedByGroup = 33,
    CatalogItemVariationInvalid = 34,
    CatalogItemComponentInvalid = 35,
    CatalogItemComponentNotFound = 36,
    CatalogItemCircularDependency = 37,

    // Inventory operations
    UnitInventoryItemNotFound = 38,
    InventoryItemMovedToAnotherNodeAfterPlacementCreated = 39,

    // Transfers
    TransferSameNode = 40,

    // Writeoffs
    WriteoffNotFound = 41,
    WriteoffNotDraft = 42,
    WriteoffHasNoItems = 43,
    WriteoffItemNotFound = 44,
    WriteoffInsufficientInventory = 45,
    WriteoffNotAssignedToWarehouse = 46,

    // Receipts
    ReceiptNotFound = 47,
    ReceiptInvalidStatusTransition = 48,
    ReceiptHasPlacements = 49,
    ReceiptItemNotFound = 50,
    ReceiptItemPlacementNotFound = 51,
    ReceiptNotAssignedToWarehouse = 52,
    ReceiptItemsUnderplaced = 53,
    ReceiptItemsOverplaced = 54,
    InsufficientInventory = 55,
    InventoryItemNodeMismatch = 56,
    UnitInventoryItemNumberDuplicate = 57,

    // Orders
    OrderNotFound = 58,
    OrderNotDraft = 59,
    OrderNotConfirmed = 60,
    OrderNotAssembly = 61,
    OrderInvalidStatusTransition = 62,
    OrderHasFulfillments = 63,
    OrderNotAssignedToWarehouse = 64,
    OrderBoxNotFound = 65,
    OrderBoxComponentNotFound = 66,
    AssemblyTaskNotFound = 67,
    AssemblyTaskNotDeletable = 68,
    AssemblyTaskBoxNotFound = 69,
    AssemblyTaskBoxComponentNotFound = 70,
    AssemblyFulfillmentNotFound = 71,
    AssemblyFulfillmentInvalidType = 72,
    AssemblyTaskAlreadyDone = 73,
    AssemblyTaskMoveTargetInvalid = 74,
    AssemblyTaskQuantityExceedsAvailable = 75,
    AssemblyComponentAlreadyFulfilled = 76,
    CatalogItemNotVariationMember = 77,

    // Marketplaces
    MarketplaceAccountNotFound = 78,
    MarketplaceAccountInactive = 111,
    MarketplaceAccountHasOrders = 110,
    MarketplaceCredentialsInvalid = 79,
    MarketplaceCredentialsUnreadable = 80,
    MarketplaceClientIdRequired = 81,
    MarketplaceApiError = 82,
    MarketplaceOrdersNotSupported = 109,
    MarketplaceSyncAlreadyRunning = 83,
    MarketplaceSyncInterrupted = 84,
    MarketplaceWarehouseNotFound = 87,
    MarketplaceCardNotFound = 88,
    MarketplaceCardMappingTypeNotAllowed = 85,
    MarketplaceCardMappingArchivedItem = 86,
    MarketplaceOrderNotFromMarketplace = 113,
    MarketplaceOrderNotAwaitingDeliver = 126,
    MarketplaceOrderCardNotMapped = 114,
    MarketplaceOrderWarehouseNotMapped = 115,
    MarketplaceLabelNotReady = 112,

    // Routing
    RouteNotFound = 89,

    // Validation
    Required = 90,
    TooShort = 91,
    TooLong = 92,
    InvalidFormat = 93,
    OutOfRange = 94,
    InvalidJson = 95,
    PasswordTooShort = 96,
    PasswordAtLeastOneDigit = 97,
    PasswordAtLeastOneUppercase = 98,
    PasswordAtLeastOneLowercase = 99,
    PasswordInvalid = 100,
    ValidationError = 101,

    // DataFiles
    DataFileNotFound = 102,
    DataFileEmpty = 103,
    DataFileTooLarge = 104,
    DataFileTypeNotAllowed = 105,
    DataFileNotAnImage = 106,
    DataFileWidthNotAllowed = 107,
    DataFileStorageError = 108,

    // Stocktakes
    StocktakeNotFound = 116,
    StocktakeInvalidStatusTransition = 117,
    StocktakeNotAssignedToWarehouse = 118,
    StocktakeHasNoNodes = 119,
    StocktakeNodeNotFound = 120,
    StocktakeNodeAlreadyInProgress = 121,
    StocktakeUnitCountedTwice = 122,
    StocktakeUnitItemInAnotherWarehouse = 123,
    StocktakeUnitItemDetached = 124,
    StocktakeConcurrentModification = 125,
}
