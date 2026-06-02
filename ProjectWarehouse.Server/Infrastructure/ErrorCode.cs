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
    CatalogItemTooManyCombinations,

    // Inventory operations
    UnitInventoryItemNotFound,
    AssembledBundleItemNotFound,
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
    UnitInventoryItemNumberDuplicate,

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
}
