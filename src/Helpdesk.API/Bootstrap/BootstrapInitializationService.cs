using Helpdesk.Infrastructure;
using Helpdesk.Infrastructure.Auth.Rbac;
using Helpdesk.Infrastructure.Identity;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage;
using System.Reflection;

namespace Helpdesk.API.Bootstrap;

public sealed class BootstrapInitializationService(
    IBootstrapStateStore stateStore,
    BootstrapOptions options,
    IDataProtectionProvider dataProtection)
{
    public async Task<BootstrapInitializationResult> InitializeAsync(
        BootstrapDescriptor descriptor,
        FirstAdministratorRequest request,
        CancellationToken cancellationToken)
    {
        request = ApplyDeploymentManagedDefaults(request);
        if (descriptor.State is not BootstrapState.Configuring ||
            !IsSupportedConfiguredProvider(descriptor) ||
            descriptor.OperationId is null)
        {
            return BootstrapInitializationResult.InvalidState;
        }

        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.DisplayName) ||
            string.IsNullOrWhiteSpace(request.OrganizationName) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            !IsPermittedApplicationUrl(request.ApplicationUrl) ||
            !ArePermittedBrandingUrls(request) ||
            !IsPermittedSupportEmail(request.SupportEmail) ||
            !IsPermittedTimeZone(request.TimeZoneId))
        {
            return BootstrapInitializationResult.InvalidRequest;
        }

        var settings = new Dictionary<string, string?>
        {
            ["Database:Provider"] = descriptor.Provider,
            ["Authentication:Mode"] = "Local",
            ["DataProtection:KeyRingPath"] = Path.Combine(options.StateDirectory, "keys"),
            ["StorageOptions:RootPath"] = Path.Combine(options.DataDirectory, "storage")
        };
        if (string.Equals(descriptor.Provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            settings["Database:Sqlite:Path"] = descriptor.SqlitePath;
        }
        else
        {
            try
            {
                settings["ConnectionStrings:HelpdeskDb"] = dataProtection
                    .CreateProtector("RatelDesk.Bootstrap.PostgreSqlConnection.v1")
                    .Unprotect(descriptor.ProtectedPostgreSqlConnection!);
            }
            catch (Exception)
            {
                return BootstrapInitializationResult.InvalidState;
            }
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHelpdeskInfrastructure(configuration);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
        var identityDb = scope.ServiceProvider.GetRequiredService<RatelDeskIdentityDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        await identityDb.Database.MigrateAsync(cancellationToken);
        await RoleDefinitionSeeder.EnsureBuiltInsAsync(db, cancellationToken);

        var initialization = await db.InstanceInitializations
            .SingleOrDefaultAsync(initialization => initialization.Id == InstanceInitialization.SingletonId, cancellationToken);
        if (initialization is not null)
        {
            if (initialization.InstanceId == descriptor.InstanceId && initialization.OperationId == descriptor.OperationId)
            {
                var readyDescriptor = await stateStore.UpdateAsync(current => current with
                {
                    State = BootstrapState.Ready,
                    CompletedAtUtc = initialization.CompletedAtUtc
                }, cancellationToken);
                return new BootstrapInitializationResult(true, null, readyDescriptor);
            }

            return BootstrapInitializationResult.AlreadyInitialized;
        }

        if (await identityDb.Users.AnyAsync(cancellationToken) ||
            await db.Organizations.AnyAsync(cancellationToken))
        {
            return BootstrapInitializationResult.AlreadyInitialized;
        }

        // Identity and application data use the same selected physical
        // database. Share one connection and transaction so a failed domain
        // write never leaves an orphaned first local account behind.
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        identityDb.Database.SetDbConnection(db.Database.GetDbConnection(), contextOwnsConnection: false);
        await identityDb.Database.UseTransactionAsync(transaction.GetDbTransaction(), cancellationToken);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var administrator = new ApplicationUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            DisplayName = request.DisplayName.Trim(),
            IsInstanceAdministrator = true,
            EmailConfirmed = true
        };
        var createUser = await userManager.CreateAsync(administrator, request.Password);
        if (!createUser.Succeeded)
        {
            return BootstrapInitializationResult.PasswordRejected;
        }

        var completedAtUtc = DateTimeOffset.UtcNow;
        try
        {
            var organization = new Organization { Name = request.OrganizationName.Trim() };
            db.Organizations.Add(organization);
            db.Users.Add(new User
            {
                Id = administrator.Id,
                Name = administrator.DisplayName,
                Email = administrator.Email!,
                OrganizationId = organization.Id,
                Role = "HelpdeskAdmin"
            });
            db.InstanceInitializations.Add(new InstanceInitialization
            {
                Id = InstanceInitialization.SingletonId,
                InstanceId = descriptor.InstanceId,
                OperationId = descriptor.OperationId.Value,
                SetupVersion = GetSetupVersion(),
                TimeZoneId = request.TimeZoneId?.Trim() ?? "UTC",
                CompletedAtUtc = completedAtUtc
            });
            if (HasBrandingInput(request))
            {
                db.InstanceBrandings.Add(new InstanceBranding
                {
                    Id = 1,
                    ApplicationName = request.ApplicationName?.Trim(),
                    ApplicationUrl = request.ApplicationUrl?.Trim(),
                    OrganizationName = organization.Name,
                    SupportUrl = request.SupportUrl?.Trim(),
                    SupportEmail = request.SupportEmail?.Trim(),
                    LogoUrl = request.LogoUrl?.Trim(),
                    CompactLogoUrl = request.CompactLogoUrl?.Trim(),
                    EmailFromDisplayName = request.EmailFromDisplayName?.Trim(),
                    Tagline = request.Tagline?.Trim()
                });
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            return new BootstrapInitializationResult(false, "Initialization could not be completed.", null);
        }

        await transaction.CommitAsync(cancellationToken);

        var ready = await stateStore.UpdateAsync(current => current with
        {
            State = BootstrapState.Ready,
            CompletedAtUtc = completedAtUtc
        }, cancellationToken);
        return new BootstrapInitializationResult(true, null, ready);
    }

    private FirstAdministratorRequest ApplyDeploymentManagedDefaults(FirstAdministratorRequest request) => request with
    {
        OrganizationName = SelectDeploymentValue(options.Interactive.OrganizationName, request.OrganizationName),
        ApplicationName = SelectDeploymentValue(options.Interactive.ApplicationName, request.ApplicationName),
        ApplicationUrl = SelectDeploymentValue(options.Interactive.ApplicationUrl, request.ApplicationUrl),
        TimeZoneId = SelectDeploymentValue(options.Interactive.TimeZoneId, request.TimeZoneId)
    };

    private static string? SelectDeploymentValue(string? configured, string? submitted) =>
        string.IsNullOrWhiteSpace(configured) ? submitted : configured.Trim();

    private static bool IsSupportedConfiguredProvider(BootstrapDescriptor descriptor) =>
        (string.Equals(descriptor.Provider, "Sqlite", StringComparison.OrdinalIgnoreCase) &&
         !string.IsNullOrWhiteSpace(descriptor.SqlitePath)) ||
        (string.Equals(descriptor.Provider, "PostgreSql", StringComparison.OrdinalIgnoreCase) &&
         !string.IsNullOrWhiteSpace(descriptor.ProtectedPostgreSqlConnection));

    private static bool IsPermittedApplicationUrl(string? applicationUrl)
    {
        if (string.IsNullOrWhiteSpace(applicationUrl))
        {
            return true;
        }

        return Uri.TryCreate(applicationUrl, UriKind.Absolute, out var applicationUri) &&
               (applicationUri.Scheme == Uri.UriSchemeHttps ||
                (applicationUri.Scheme == Uri.UriSchemeHttp && applicationUri.IsLoopback));
    }

    private static bool ArePermittedBrandingUrls(FirstAdministratorRequest request) =>
        IsPermittedOptionalHttpUrl(request.SupportUrl) &&
        IsPermittedOptionalHttpUrl(request.LogoUrl) &&
        IsPermittedOptionalHttpUrl(request.CompactLogoUrl);

    private static bool IsPermittedOptionalHttpUrl(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
         (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp));

    private static bool IsPermittedSupportEmail(string? supportEmail)
    {
        if (string.IsNullOrWhiteSpace(supportEmail))
        {
            return true;
        }

        try
        {
            var parsed = new System.Net.Mail.MailAddress(supportEmail);
            return string.Equals(parsed.Address, supportEmail.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool HasBrandingInput(FirstAdministratorRequest request) =>
        !string.IsNullOrWhiteSpace(request.ApplicationName) ||
        !string.IsNullOrWhiteSpace(request.ApplicationUrl) ||
        !string.IsNullOrWhiteSpace(request.SupportUrl) ||
        !string.IsNullOrWhiteSpace(request.SupportEmail) ||
        !string.IsNullOrWhiteSpace(request.LogoUrl) ||
        !string.IsNullOrWhiteSpace(request.CompactLogoUrl) ||
        !string.IsNullOrWhiteSpace(request.EmailFromDisplayName) ||
        !string.IsNullOrWhiteSpace(request.Tagline);

    private static bool IsPermittedTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return true;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static string GetSetupVersion() =>
        typeof(BootstrapInitializationService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";
}

public sealed record FirstAdministratorRequest(
    string Email,
    string DisplayName,
    string Password,
    string OrganizationName,
    string? ApplicationName,
    string? ApplicationUrl,
    string? TimeZoneId = null,
    string? SupportUrl = null,
    string? SupportEmail = null,
    string? LogoUrl = null,
    string? CompactLogoUrl = null,
    string? EmailFromDisplayName = null,
    string? Tagline = null);

public sealed record BootstrapInitializationResult(bool Succeeded, string? Error, BootstrapDescriptor? Descriptor)
{
    public static BootstrapInitializationResult InvalidState { get; } = new(false, "Setup is not ready for initialization.", null);
    public static BootstrapInitializationResult InvalidRequest { get; } = new(false, "Required setup values are missing.", null);
    public static BootstrapInitializationResult InvalidUnattendedConfiguration { get; } = new(false, "The unattended setup configuration is incomplete or invalid.", null);
    public static BootstrapInitializationResult AlreadyInitialized { get; } = new(false, "The selected database already contains initialization data.", null);
    public static BootstrapInitializationResult PasswordRejected { get; } = new(false, "The password does not meet the configured requirements.", null);
}
