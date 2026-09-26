namespace JabraDesktop.App;

public sealed class TermsConsentGate(AppPreferencesStore store, string currentTermsVersion)
{
    public async Task<bool> EnsureAcceptedAsync(
        Func<CancellationToken, Task<bool>> requestAcceptance,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestAcceptance);
        cancellationToken.ThrowIfCancellationRequested();

        var preferences = store.Load();
        if (string.Equals(preferences.AcceptedTermsVersion, currentTermsVersion, StringComparison.Ordinal))
            return true;

        if (!await requestAcceptance(cancellationToken)) return false;

        store.Save(preferences with
        {
            AcceptedTermsVersion = currentTermsVersion,
            AcceptedTermsAt = DateTimeOffset.UtcNow
        });
        return true;
    }
}
