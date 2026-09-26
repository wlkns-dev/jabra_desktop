using JabraDesktop.App;

namespace JabraDesktop.Tests;

public sealed class TermsConsentGateTests
{
    const string CurrentTermsVersion = "jabra-sdk-terms-1";

    [Fact]
    public async Task DecliningDoesNotPersistConsent()
    {
        var store = new AppPreferencesStore(TestPath());
        var gate = new TermsConsentGate(store, CurrentTermsVersion);

        var accepted = await gate.EnsureAcceptedAsync((_) => Task.FromResult(false));

        Assert.False(accepted);
        Assert.Null(store.Load().AcceptedTermsVersion);
    }

    [Fact]
    public async Task CurrentConsentDoesNotPromptAgain()
    {
        var store = new AppPreferencesStore(TestPath());
        store.Save(new AppPreferences(AcceptedTermsVersion: CurrentTermsVersion, AcceptedTermsAt: DateTimeOffset.UnixEpoch));
        var gate = new TermsConsentGate(store, CurrentTermsVersion);
        var promptCount = 0;

        var accepted = await gate.EnsureAcceptedAsync((_) =>
        {
            promptCount++;
            return Task.FromResult(false);
        });

        Assert.True(accepted);
        Assert.Equal(0, promptCount);
    }

    [Fact]
    public async Task StaleConsentPromptsAndPersistsTheCurrentVersion()
    {
        var store = new AppPreferencesStore(TestPath());
        store.Save(new AppPreferences(AcceptedTermsVersion: "jabra-sdk-terms-0", AcceptedTermsAt: DateTimeOffset.UnixEpoch));
        var gate = new TermsConsentGate(store, CurrentTermsVersion);
        var promptCount = 0;

        var accepted = await gate.EnsureAcceptedAsync((_) =>
        {
            promptCount++;
            return Task.FromResult(true);
        });

        Assert.True(accepted);
        Assert.Equal(1, promptCount);
        Assert.Equal(CurrentTermsVersion, store.Load().AcceptedTermsVersion);
        Assert.True(store.Load().AcceptedTermsAt > DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public async Task ConsentStorageFailureIsPropagated()
    {
        var directoryBlocker = Path.Combine(Path.GetTempPath(), $"jabra-tests-{Guid.NewGuid():N}");
        File.WriteAllText(directoryBlocker, "file blocks a directory");
        var store = new AppPreferencesStore(Path.Combine(directoryBlocker, "settings.json"));
        var gate = new TermsConsentGate(store, CurrentTermsVersion);

        await Assert.ThrowsAnyAsync<Exception>(() => gate.EnsureAcceptedAsync((_) => Task.FromResult(true)));
    }

    static string TestPath() => Path.Combine(Path.GetTempPath(), $"jabra-tests-{Guid.NewGuid():N}", "settings.json");
}
