using JabraDesktop.Core;
using JabraDesktop.Jabra;
using Jabra.NET.Sdk.DevicePairing;
namespace JabraDesktop.Tests;
public class MappingTests
{
    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("  ")]
    public void MissingNamesHaveReadableFallback(string? name) => Assert.Equal("Unbenanntes Gerät", DeviceMapper.DisplayName(name));
    [Theory]
    [InlineData(BluetoothConnectionStatus.CONNECTED, LinkState.Connected)]
    [InlineData(BluetoothConnectionStatus.DISCONNECTED, LinkState.Disconnected)]
    [InlineData(BluetoothConnectionStatus.NONE, LinkState.Unknown)]
    [InlineData((BluetoothConnectionStatus)999, LinkState.Unknown)]
    public void OnlyConfirmedConnectionsAreConnected(BluetoothConnectionStatus input, LinkState expected) => Assert.Equal(expected, DeviceMapper.State(input));
}
