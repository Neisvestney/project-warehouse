namespace ProjectWarehouse.Server.Domain;

public enum MarketplaceRuleOperator
{
    Equals = 0,
    Contains = 1,
    StartsWith = 2,
    EndsWith = 3,
    Regex = 4,
}
