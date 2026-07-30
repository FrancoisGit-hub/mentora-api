using Mentora.Core.Enums;

namespace Mentora.Core.DTOs.Device;

public record RegisterDeviceRequest(string Token, DevicePlatform Platform);
