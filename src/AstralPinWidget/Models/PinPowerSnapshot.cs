namespace AstralPinWidget.Models;

public sealed record PinPowerSnapshot(
    IReadOnlyList<PinPowerReading> Pins,
    double TotalCurrentAmps,
    double TotalPowerWatts,
    bool HardwareAlert,
    string Source,
    DateTimeOffset Timestamp);
