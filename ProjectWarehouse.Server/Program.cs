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
using Microsoft.AspNetCore.DataProtection;
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
using ProjectWarehouse.Server.Infrastructure.Marketplaces;
using ProjectWarehouse.Server.Integrations.Abstractions;
using ProjectWarehouse.Server.Integrations.Ozon;
using ProjectWarehouse.Server.Integrations.Ozon.Generated;
using ProjectWarehouse.Server.Integrations.Sync;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Catalog;
using ProjectWarehouse.Server.Models.Integrations;
using ProjectWarehouse.Server.Models.Roles;
using ProjectWarehouse.Server.Models.Users;
using ProjectWarehouse.Server.Models.Receipts;
using ProjectWarehouse.Server.Models.Warehouses;
using ProjectWarehouse.Server.Models.Writeoffs;
using ProjectWarehouse.Server.Services;
using Microsoft.Extensions.Options;
using Quartz;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Starting web host");

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
                .Select(n => (JsonNode)JsonValue.Create(JsonNamingPolicy.CamelCase.ConvertName(n)))
                .ToList();
            schema.Type = JsonSchemaType.String;
            schema.Format = null;

            return Task.CompletedTask;
        });

        options.AddDocumentTransformer((document, _, _) =>
        {
            var permissionValues = Permissions.All
                .Select(p => (JsonNode)JsonValue.Create(p));

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

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
    {
        var csb = new NpgsqlConnectionStringBuilder(connectionString);
        var pgPassword = builder.Configuration["POSTGRES_PASSWORD"];
        if (!string.IsNullOrEmpty(pgPassword) && string.IsNullOrEmpty(csb.Password))
            csb.Password = pgPassword;
        // marketplace sync holds an advisory lock on an idle connection for minutes
        if (csb.KeepAlive == 0)
            csb.KeepAlive = 30;
        connectionString = csb.ConnectionString;
    }

    var dataSource = new NpgsqlDataSourceBuilder(connectionString)
        .EnableDynamicJson()
        .Build();

    builder.Services.AddSingleton(dataSource);
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(dataSource).UseProjectables());

    builder.Services.Configure<MarketplacesOptions>(
        builder.Configuration.GetSection(MarketplacesOptions.SectionName));
    var marketplacesOptions = builder.Configuration.GetSection(MarketplacesOptions.SectionName)
        .Get<MarketplacesOptions>() ?? new MarketplacesOptions();

    Directory.CreateDirectory(marketplacesOptions.KeyRingPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(marketplacesOptions.KeyRingPath))
        .SetApplicationName("ProjectWarehouse");

    builder.Services.AddScoped<IMarketplaceCredentialProtector, MarketplaceCredentialProtector>();

    // singleton: the state is ambient (AsyncLocal), and the auth handler lives in IHttpClientFactory's own scope
    builder.Services.AddSingleton<MarketplaceRequestContext>();
    builder.Services.AddTransient<OzonAuthHandler>();
    builder.Services.AddScoped<IOzonClient, OzonClient>();
    builder.Services.AddScoped<IMarketplaceProvider, OzonMarketplaceProvider>();
    builder.Services.AddScoped<IMarketplaceProviderRegistry, MarketplaceProviderRegistry>();

    // in-memory job store, consistent with the project's existing single-node assumption
    builder.Services.AddQuartz(q =>
    {
        var jobKey = new JobKey(MarketplaceSyncScanJob.Key);
        q.AddJob<MarketplaceSyncScanJob>(jobKey);
        q.AddTrigger(t => t
            .ForJob(jobKey)
            .WithIdentity(MarketplaceSyncScanJob.Key + "-trigger")
            .WithCronSchedule(marketplacesOptions.SyncScanCron));
    });
    builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

    builder.Services.AddSingleton<IMarketplaceSyncQueue, MarketplaceSyncQueue>();
    builder.Services.AddHostedService<MarketplaceSyncWorker>();
    builder.Services.AddScoped<IMarketplaceSyncService, MarketplaceSyncService>();

    var ozonTimeout = TimeSpan.FromSeconds(marketplacesOptions.Ozon.TimeoutSeconds);
    builder.Services.AddHttpClient<IOzonApiClient, OzonApiClient>(c =>
            c.BaseAddress = new Uri(marketplacesOptions.Ozon.BaseUrl))
        .AddHttpMessageHandler<OzonAuthHandler>()
        .AddStandardResilienceHandler(r =>
        {
            // the handler's own timeouts win over HttpClient.Timeout; its defaults (10s per attempt)
            // cut off Ozon's slower endpoints, and SamplingDuration must be >= 2x AttemptTimeout
            r.AttemptTimeout.Timeout = ozonTimeout;
            r.TotalRequestTimeout.Timeout = ozonTimeout * 3;
            r.CircuitBreaker.SamplingDuration = ozonTimeout * 6;
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

    builder.Services.AddScoped<IListUpdater, ListUpdater>();
    
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
    builder.Services.AddScoped<IChangeLogService<ReceiptDto>, ReceiptDtoChangelogService>();
    builder.Services.AddScoped<IChangeLogService<WriteoffDto>, WriteoffDtoChangelogService>();
    builder.Services.AddScoped<IChangeLogService<MarketplaceAccountDto>, MarketplaceAccountDtoChangelogService>();
    builder.Services.AddScoped<IChangeLogService<MarketplaceCardDto>, MarketplaceCardDtoChangelogService>();
    builder.Services.AddScoped<IInventoryService, InventoryService>();
    builder.Services.AddScoped<ICatalogService, CatalogService>();
    builder.Services.AddScoped<IUserQueryFilterService, UserQueryFilterService>();
    builder.Services.AddScoped<IOrderService, OrderService>();
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
    app.Map("/api/{**path}", (async ctx =>
    {
        var problem = AppProblems.NotFound(ErrorCode.RouteNotFound, "The requested API endpoint does not exist.");
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
        ctx.Response.ContentType = "application/problem+json";
        await ctx.Response.WriteAsJsonAsync(problem, ctx.RequestServices
            .GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions);
    }));
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
