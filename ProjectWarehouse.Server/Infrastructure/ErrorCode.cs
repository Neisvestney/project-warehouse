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

    // Inventory operations
    UnitInventoryItemNotFound,
    AssembledBundleItemNotFound,
    InventoryItemMovedToAnotherNodeAfterPlacementCreated,

    // Transfers
    TransferSameNode,

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
