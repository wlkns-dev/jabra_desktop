using JabraDesktop.Core;

namespace JabraDesktop.App;

public sealed class ConsentStartupCoordinator(TermsConsentGate gate)
{
    public async Task<IDeviceBackend?> CreateBackendIfAcceptedAsync(
        Func<CancellationToken, Task<bool>> requestAcceptance,
        Func<IDeviceBackend> createBackend,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestAcceptance);
        ArgumentNullException.ThrowIfNull(createBackend);

        if (!await gate.EnsureAcceptedAsync(requestAcceptance, cancellationToken).ConfigureAwait(false)) return null;
        return createBackend();
    }
}
