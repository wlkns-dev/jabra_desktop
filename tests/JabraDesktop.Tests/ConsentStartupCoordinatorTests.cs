using JabraDesktop.App;
using JabraDesktop.Core;

namespace JabraDesktop.Tests;

public sealed class ConsentStartupCoordinatorTests
{
    [Fact]
    public async Task DeclinedConsentDoesNotCreateBackend()
    {
        var store = new AppPreferencesStore(TestPath());
        var coordinator = new ConsentStartupCoordinator(new TermsConsentGate(store, ConsentTerms.CurrentVersion));
        var factoryCalls = 0;

        var backend = await coordinator.CreateBackendIfAcceptedAsync(_ => Task.FromResult(false), () =>
        {
            factoryCalls++;
            return new FakeBackend();
        });

        Assert.Null(backend);
        Assert.Equal(0, factoryCalls);
    }

    [Fact]
    public async Task CurrentConsentSkipsPromptAndCreatesBackendOnce()
    {
        var store = new AppPreferencesStore(TestPath());
        store.Save(new AppPreferences(null, ThemePreference.System, ConsentTerms.CurrentVersion, DateTimeOffset.UtcNow));
        var coordinator = new ConsentStartupCoordinator(new TermsConsentGate(store, ConsentTerms.CurrentVersion));
        var promptCalls = 0;
        var factoryCalls = 0;

        var backend = await coordinator.CreateBackendIfAcceptedAsync(_ =>
        {
            promptCalls++;
            return Task.FromResult(true);
        }, () =>
        {
            factoryCalls++;
            return new FakeBackend();
        });

        Assert.NotNull(backend);
        Assert.Equal(0, promptCalls);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task ConsentWriteFailureDoesNotCreateBackend()
    {
        var blocker = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        File.WriteAllText(blocker, "file prevents settings directory creation");
        var store = new AppPreferencesStore(Path.Combine(blocker, "settings.json"));
        var coordinator = new ConsentStartupCoordinator(new TermsConsentGate(store, ConsentTerms.CurrentVersion));
        var factoryCalls = 0;

        await Assert.ThrowsAnyAsync<IOException>(() => coordinator.CreateBackendIfAcceptedAsync(
            _ => Task.FromResult(true),
            () =>
            {
                factoryCalls++;
                return new FakeBackend();
            }));

        Assert.Equal(0, factoryCalls);
        File.Delete(blocker);
    }

    static string TestPath() => Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json");
}
