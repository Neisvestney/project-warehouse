using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using DotNetEnv;
using Npgsql;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.ChangeLog;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Catalog;
using ProjectWarehouse.Server.Models.Roles;
using ProjectWarehouse.Server.Models.Users;
using ProjectWarehouse.Server.Models.InboundOrders;
using ProjectWarehouse.Server.Models.Warehouses;
using ProjectWarehouse.Server.Services;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Env.NoClobber().TraversePath().Load();
    Log.Debug("Loaded environment variables from .env file");
}
catch (FileNotFoundException)
{
    Log.Debug(".env file not found — relying on environment variables");
}
catch (Exception ex)
{
    Log.Warning(ex, "Failed to parse .env file — relying on environment variables");
}

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, config) => config
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"));

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    });

    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            var securitySchemes = new Dictionary<string, IOpenApiSecurityScheme>
            {
                ["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    In = ParameterLocation.Header,
                    BearerFormat = "Json Web Token"
                }
            };
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes = securitySchemes;

            return Task.CompletedTask;
        });

        options.AddSchemaTransformer((schema, context, _) =>
        {
            if (context.JsonTypeInfo.Kind != JsonTypeInfoKind.Object)
                return Task.CompletedTask;

            var nullabilityCtx = new NullabilityInfoContext();

            foreach (var prop in context.JsonTypeInfo.Properties)
            {
                if (prop.AttributeProvider is not PropertyInfo pi) continue;

                var info = nullabilityCtx.Create(pi);
                if (info.ReadState == NullabilityState.NotNull)
                {
                    schema.Required ??= new HashSet<string>();
                    schema.Required.Add(prop.Name);
                }
            }

            return Task.CompletedTask;
        });

        options.AddSchemaTransformer((schema, context, _) =>
        {
            var type = context.JsonTypeInfo.Type;
            if (!type.IsEnum) return Task.CompletedTask;

            schema.Enum = Enum.GetNames(type)
                .Select(n => (JsonNode)JsonValue.Create(JsonNamingPolicy.CamelCase.ConvertName(n))!)
                .ToList();
            schema.Type = JsonSchemaType.String;
            schema.Format = null;

            return Task.CompletedTask;
        });

        options.AddDocumentTransformer((document, _, _) =>
        {
            var permissionValues = Permissions.All
                .Select(p => (JsonNode)JsonValue.Create(p)!);

            document.Components ??= new OpenApiComponents();
            document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
            var schemas = document.Components.Schemas!;
            schemas["PermissionName"] = new OpenApiSchema
                { Type = JsonSchemaType.String, Enum = permissionValues.ToList() };

            return Task.CompletedTask;
        });

        options.AddOperationTransformer((operation, context, _) =>
        {
            if (context.Description.ActionDescriptor is ControllerActionDescriptor descriptor)
                operation.OperationId = descriptor.ControllerName + descriptor.ActionName;
            return Task.CompletedTask;
        });
    });

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        var connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
        var pgPassword = builder.Configuration["POSTGRES_PASSWORD"];
        if (!string.IsNullOrEmpty(pgPassword))
        {
            var csb = new NpgsqlConnectionStringBuilder(connStr);
            if (string.IsNullOrEmpty(csb.Password))
                csb.Password = pgPassword;
            connStr = csb.ConnectionString;
        }

        var dataSource = new NpgsqlDataSourceBuilder(connStr)
            .EnableDynamicJson()
            .Build();

        options.UseNpgsql(dataSource).UseProjectables();
    });

    builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.User.RequireUniqueEmail = false;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

    var jwtSettings = builder.Configuration.GetSection("Jwt");
    builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidateAudience = true,
                ValidAudience = jwtSettings["Audience"],
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!)),
                ClockSkew = TimeSpan.Zero,
                NameClaimType = "name",
            };

            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async ctx =>
                {
                    var subClaim = ctx.Principal!.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

                    if (!Guid.TryParse(subClaim, out var userId))
                    {
                        ctx.Fail("Invalid sub claim.");
                        return;
                    }

                    if (!int.TryParse(ctx.Principal.FindFirst("security_version")?.Value, out var claimedVersion))
                    {
                        ctx.Fail("Missing security_version claim.");
                        return;
                    }

                    var store = ctx.HttpContext.RequestServices.GetRequiredService<SecurityVersionStore>();
                    var currentVersion = await store.GetVersionAsync(userId);

                    if (claimedVersion != currentVersion)
                        ctx.Fail("TOKEN_OUTDATED");
                }
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        foreach (var permission in Permissions.All)
            options.AddPolicy(permission,
                p => p.Requirements.Add(new PermissionRequirement(permission)));
    });

    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        options.InvalidModelStateResponseFactory = ctx =>
        {
            var errors = ctx.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .SelectMany(kvp => kvp.Value!.Errors.Select(err =>
                {
                    var (code, args) = ModelStateErrorMapper.Resolve(err);
                    return (
                        Field: ModelStateErrorMapper.NormalizeField(kvp.Key),
                        Code: code,
                        Message: string.IsNullOrEmpty(err.ErrorMessage)
                            ? err.Exception?.Message ?? "Validation error."
                            : err.ErrorMessage,
                        Args: args
                    );
                }));

            var details = AppProblems.UnprocessableEntities(errors);
            return new ObjectResult(details) { StatusCode = details.Status };
        };
    });

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("CapacitorPolicy", policy =>
        {
            policy.WithOrigins(
                    "capacitor://localhost",  // Capacitor Android
                    "https://localhost",       // Capacitor iOS / dev
                    "http://localhost"
                )
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    builder.Services.AddHttpContextAccessor();

    builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
    builder.Services.AddScoped<ITokenService, TokenService>();
    builder.Services.AddScoped<IPermissionService, PermissionService>();
    builder.Services.AddSingleton<SecurityVersionStore>();
    builder.Services.AddAutoMapper(typeof(Program).Assembly);

    builder.Services.AddScoped<IChangeLogService, AppChangeLogService>();
    builder.Services.AddScoped<IChangeLogService<UserDetailDto>, UserDetailDtoChangelogService>();
    builder.Services.AddScoped<IChangeLogService<CatalogItemDto>, CatalogItemDtoChangelogService>();
    builder.Services.AddScoped<IChangeLogService<WarehouseDto>, WarehouseDtoChangelogService>();
    builder.Services.AddScoped<IChangeLogService<StoragePlaceNodeDetailsDto>, StoragePlaceNodeDetailsDtoChangelogService>();
    builder.Services.AddScoped<IChangeLogService<RolesListDto>, RolesListDtoChangelogService>();
    builder.Services.AddScoped<IChangeLogService<InboundOrderDto>, InboundOrderDtoChangelogService>();


    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }

    await DbSeeder.SeedAsync(app.Services);

    app.UseDefaultFiles();
    app.UseStaticFiles();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options => options
            .AddPreferredSecuritySchemes(JwtBearerDefaults.AuthenticationScheme)
            .EnablePersistentAuthentication());
    }

    app.UseHttpsRedirection();
    app.UseCors("CapacitorPolicy");
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapFallbackToFile("/index.html");

    app.Run();
}
catch (HostAbortedException)
{
    Log.Information("Host stopped with HostAbortedException");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
