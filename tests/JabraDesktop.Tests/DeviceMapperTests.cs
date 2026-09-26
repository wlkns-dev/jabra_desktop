using Jabra.NET.Sdk.Core.Types;
using JabraDesktop.Core;
using JabraDesktop.Jabra;

namespace JabraDesktop.Tests;

public sealed class DeviceMapperTests
{
    [Theory]
    [InlineData(DeviceType.Dongle, DeviceRole.Dongle)]
    [InlineData(DeviceType.Headset, DeviceRole.Headset)]
    [InlineData(DeviceType.NotInit, DeviceRole.Unknown)]
    [InlineData(DeviceType.NotGn, DeviceRole.Unknown)]
    [InlineData(DeviceType.Usb, DeviceRole.Other)]
    public void RoleMapsKnownAndUnknownSdkTypes(DeviceType type, DeviceRole expected)
    {
        Assert.Equal(expected, DeviceMapper.Role(type));
    }

    [Fact]
    public void OptionalUsbIdKeepsOnlyPositiveIdentifiers()
    {
        Assert.Equal(2830, DeviceMapper.OptionalUsbId(2830));
        Assert.Null(DeviceMapper.OptionalUsbId(0));
        Assert.Null(DeviceMapper.OptionalUsbId(-1));
    }

    [Fact]
    public void ExistingDeviceInfoConstructorDefaultsNewMetadataToUnknown()
    {
        var device = new DeviceInfo("id", "Jabra", true);

        Assert.Equal(DeviceRole.Unknown, device.Role);
        Assert.Null(device.VendorId);
        Assert.Null(device.ProductId);
    }
}
