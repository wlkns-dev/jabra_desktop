using Jabra.NET.Sdk.Properties;
using Jabra.NET.Sdk.Core;
using Jabra.NET.Sdk.Core.Types;
using JabraDesktop.Core;
using JabraDesktop.Jabra;
using System.Diagnostics;

namespace JabraDesktop.Tests;

public class DevicePropertyTests
{
    static DeviceInfo Device(DeviceRole role, int? vendorId, int? productId, string name = "Device") =>
        new("session", name, false, Role: role, VendorId: vendorId, ProductId: productId);

    [Fact]
    public void FindsFirmwareForLink380()
    {
        var capability = DevicePropertyCapabilities.Find(Device(DeviceRole.Dongle, 0x0B0E, 0x24C7));

        Assert.NotNull(capability);
        Assert.Equal("firmwareVersion", capability.PropertyName);
    }

    [Fact]
    public void FindsBatteryForEvolve75Se()
    {
        var capability = DevicePropertyCapabilities.Find(Device(DeviceRole.Headset, 0x0B0E, 0x2502));

        Assert.NotNull(capability);
        Assert.Equal("batteryLevel", capability.PropertyName);
    }

    [Fact]
    public void RejectsLink370AndUnknownIdentity()
    {
        Assert.Null(DevicePropertyCapabilities.Find(Device(DeviceRole.Dongle, 0x0B0E, 0x1234, "Link 370")));
        Assert.Null(DevicePropertyCapabilities.Find(Device(DeviceRole.Headset, null, null, "Evolve 75 SE")));
    }

    [Fact]
    public void RejectsKnownIdsWithWrongRole()
    {
        Assert.Null(DevicePropertyCapabilities.Find(Device(DeviceRole.Headset, 0x0B0E, 0x24C7)));
        Assert.Null(DevicePropertyCapabilities.Find(Device(DeviceRole.Dongle, 0x0B0E, 0x2502)));
    }

    [Fact]
    public void FirmwareAcceptsOnlyANonEmptyStringValue()
    {
        Assert.Equal("2.1.0", PropertyValueMapper.Firmware(PropertyValue.FromString(" 2.1.0 ")));
        Assert.Null(PropertyValueMapper.Firmware(PropertyValue.FromString("  ")));
        Assert.Null(PropertyValueMapper.Firmware(PropertyValue.FromInt32(75)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 100)]
    [InlineData(-1, null)]
    [InlineData(101, null)]
    public void BatteryAcceptsOnlyIntegersInRange(int input, int? expected)
    {
        Assert.Equal(expected, PropertyValueMapper.BatteryPercent(PropertyValue.FromInt32(input)));
    }

    [Fact]
    public void BatteryRejectsStringAndNumberValues()
    {
        Assert.Null(PropertyValueMapper.BatteryPercent(PropertyValue.FromString("75")));
        Assert.Null(PropertyValueMapper.BatteryPercent(PropertyValue.FromDouble(75.0)));
    }

    [Fact]
    public async Task OptionalPropertyInitializationFailureIsIsolated()
    {
        var reader = new FakePropertiesReader { InitializationException = new IOException("optional module unavailable") };

        var initialized = await DevicePropertiesInitialization.TryInitializeAsync(reader, null!, CancellationToken.None);

        Assert.False(initialized);
        Assert.False(reader.GetCalled);
    }

    [Fact]
    public async Task OptionalPropertyInitializationSuccessEnablesReads()
    {
        var reader = new FakePropertiesReader();

        var initialized = await DevicePropertiesInitialization.TryInitializeAsync(reader, null!, CancellationToken.None);

        Assert.True(initialized);
        Assert.True(reader.InitializeCalled);
    }

    [Fact]
    public async Task OptionalPropertyInitializationTimeoutIsIsolated()
    {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Task.Run(async () => { await Task.Delay(300); pending.TrySetResult(); });
        var reader = new FakePropertiesReader { InitializationTask = pending.Task };
        var timer = Stopwatch.StartNew();

        var initialized = await DevicePropertiesInitialization.TryInitializeAsync(reader, null!, CancellationToken.None, TimeSpan.FromMilliseconds(30));

        Assert.False(initialized);
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(1));
        Assert.False(reader.GetCalled);
    }

    [Fact]
    public async Task SynchronouslyBlockingPropertyCallIsBoundedAndRemainsSingleFlight()
    {
        var runner = new DevicePropertyReadRunner(TimeSpan.FromMilliseconds(30));
        var timer = Stopwatch.StartNew();

        var result = await runner.ReadAsync(() =>
        {
            Thread.Sleep(300);
            return Task.FromResult(PropertyValue.FromString("late"));
        }, CancellationToken.None);

        Assert.Null(result);
        Assert.True(timer.Elapsed < TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public void RemovedDeviceDoesNotAcceptLatePropertyValue()
    {
        var original = Device(DeviceRole.Headset, 0x0B0E, 0x2502);

        var updated = DevicePropertyValueUpdate.TryApply(original,
            DevicePropertyCapabilities.Find(original)!, PropertyValue.FromInt32(82), isAttached: false);

        Assert.Null(updated);
        Assert.Null(original.BatteryPercent);
    }

    sealed class FakePropertiesReader : IDevicePropertiesReader
    {
        public Exception? InitializationException { get; init; }
        public Task? InitializationTask { get; init; }
        public bool InitializeCalled { get; private set; }
        public bool GetCalled { get; private set; }
        public Task InitializeAsync(IApi api, CancellationToken token)
        {
            InitializeCalled = true;
            if (InitializationException is not null) return Task.FromException(InitializationException);
            return InitializationTask ?? Task.CompletedTask;
        }
        public Task<PropertyValue> GetAsync(IDevice device, string propertyName, CancellationToken token)
        {
            GetCalled = true;
            return Task.FromResult(PropertyValue.FromString("value"));
        }
    }

    [Fact]
    public async Task TimedOutPropertyReadReturnsUnavailableWithoutWaitingForNativeTask()
    {
        var runner=new DevicePropertyReadRunner(TimeSpan.FromMilliseconds(30));
        var pending=new TaskCompletionSource<PropertyValue>(TaskCreationOptions.RunContinuationsAsynchronously);
        var timer=Stopwatch.StartNew();

        var result=await runner.ReadAsync(()=>pending.Task,CancellationToken.None);

        Assert.Null(result);
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(1));
        pending.SetResult(PropertyValue.FromString("late-value"));
    }

    [Fact]
    public async Task TimedOutPropertyReadPreventsOverlappingNativeReadsUntilSettled()
    {
        var runner=new DevicePropertyReadRunner(TimeSpan.FromMilliseconds(30));
        var pending=new TaskCompletionSource<PropertyValue>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls=0;
        await runner.ReadAsync(()=>pending.Task,CancellationToken.None);

        var overlapping=await runner.ReadAsync(() =>
        {
            calls++;
            return Task.FromResult(PropertyValue.FromString("new-value"));
        },CancellationToken.None);

        Assert.Null(overlapping);
        Assert.Equal(0,calls);
        pending.SetResult(PropertyValue.FromString("late-value"));
        await Task.Delay(10);
        Assert.Equal("new-value",(await runner.ReadAsync(() =>
        {
            calls++;
            return Task.FromResult(PropertyValue.FromString("new-value"));
        },CancellationToken.None))?.AsString());
        Assert.Equal(1,calls);
    }

    [Fact]
    public async Task FailedPropertyReadReturnsUnavailable()
    {
        var runner=new DevicePropertyReadRunner(TimeSpan.FromSeconds(1));

        var result=await runner.ReadAsync(() => Task.FromException<PropertyValue>(new IOException("private SDK error")),CancellationToken.None);

        Assert.Null(result);
    }
}
