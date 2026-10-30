using System.Security.Claims;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Orders;

namespace ProjectWarehouse.Server.Services;

public interface IOrderFinanceService
{
    Task<OrderFinanceDto> GetAsync(ClaimsPrincipal user, Order order, CancellationToken ct = default);
}
