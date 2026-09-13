using Microsoft.AspNetCore.DataProtection;

namespace Helpdesk.API.Bootstrap;

internal static class BootstrapKeyRingProof
{
    private const string Purpose = "RatelDesk.Bootstrap.KeyRingProof.v1";

    public static string Create(IDataProtectionProvider protection, Guid instanceId) =>
        protection.CreateProtector(Purpose).Protect(instanceId.ToString("D"));

    public static bool IsValid(IDataProtectionProvider protection, BootstrapDescriptor descriptor) =>
        descriptor.ProtectedKeyRingProof is not null &&
        protection.CreateProtector(Purpose).Unprotect(descriptor.ProtectedKeyRingProof) == descriptor.InstanceId.ToString("D");
}
