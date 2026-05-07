using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var adminRole = await roleManager.FindByNameAsync("Admin");
        if (adminRole is null)
        {
            adminRole = new ApplicationRole { Name = "Admin" };
            await roleManager.CreateAsync(adminRole);
        }

        var existingPermissions = await db.RolePermissions
            .Where(rp => rp.RoleId == adminRole.Id)
            .Select(rp => rp.Permission)
            .ToListAsync();

        var missing = Permissions.All.Except(existingPermissions).ToList();
        if (missing.Count > 0)
        {
            db.RolePermissions.AddRange(missing.Select(p =>
                new RolePermission { RoleId = adminRole.Id, Permission = p }));
            await db.SaveChangesAsync();
        }

        var adminUsername = config["Seed:AdminUsername"] ?? "admin";
        var adminPassword = config["Seed:AdminPassword"]
            ?? throw new InvalidOperationException("Seed:AdminPassword is not configured.");

        var adminUser = await userManager.FindByNameAsync(adminUsername);
        if (adminUser is null)
        {
            adminUser = new ApplicationUser { UserName = adminUsername };
            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create seed admin user: {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
            await userManager.AddToRoleAsync(adminUser, "Admin");
    }
}
