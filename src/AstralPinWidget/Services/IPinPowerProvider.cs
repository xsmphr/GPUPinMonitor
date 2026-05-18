using AstralPinWidget.Models;

namespace AstralPinWidget.Services;

public interface IPinPowerProvider
{
    PinPowerSnapshot Read();
}
