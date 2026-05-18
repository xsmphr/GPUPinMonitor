using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using AstralPinWidget.Models;

namespace AstralPinWidget.Services;

public sealed class GpuTweakPowerProvider : IPinPowerProvider
{
    private const int MaxPins = 10;
    private const double NominalConnectorVoltage = 12.0;
    private readonly string _installDirectory;
    private bool _dllDirectorySet;

    public GpuTweakPowerProvider()
    {
        _installDirectory = FindInstallDirectory();
    }

    public PinPowerSnapshot Read()
    {
        EnsureReady();

        var native = new NativePowerStatus();
        var result = Native.Expan_Api_GetPowerStatus(0, ref native);
        if (result != 0)
        {
            throw new InvalidOperationException($"Expan_Api_GetPowerStatus returned {result}.");
        }

        var count = Math.Clamp(native.Count, 0, 6);
        if (count == 0)
        {
            throw new InvalidOperationException("GPU Tweak returned zero 12V-2X6 pins.");
        }

        var rawPins = native.GetPins();
        var pins = Enumerable.Range(0, count)
            .Select(i => new PinPowerReading(i + 1, NominalConnectorVoltage, rawPins[i]))
            .ToArray();

        var total = native.TotalCurrentAmps > 0 ? native.TotalCurrentAmps : pins.Sum(p => p.CurrentAmps);
        var totalPowerWatts = pins.Length > 0
            ? pins.Sum(pin => pin.VoltageVolts * pin.CurrentAmps)
            : total * NominalConnectorVoltage;
        return new PinPowerSnapshot(
            pins,
            total,
            totalPowerWatts,
            native.HardwareAlert != 0,
            "GPU Tweak III direct",
            DateTimeOffset.Now);
    }

    private void EnsureReady()
    {
        if (_dllDirectorySet)
        {
            return;
        }

        if (Environment.Is64BitProcess)
        {
            throw new InvalidOperationException("This app must run as x86 to load GPU Tweak III ExpanModule.dll.");
        }

        var dllPath = Path.Combine(_installDirectory, "ExpanModule.dll");
        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException("GPU Tweak III ExpanModule.dll was not found.", dllPath);
        }

        if (!Native.SetDllDirectory(_installDirectory))
        {
            throw new InvalidOperationException("Unable to set GPU Tweak III DLL directory.");
        }

        _dllDirectorySet = true;
    }

    private static string FindInstallDirectory()
    {
        var candidates = new List<string>();

        using var uninstallKey = Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
        if (uninstallKey is not null)
        {
            foreach (var name in uninstallKey.GetSubKeyNames())
            {
                using var appKey = uninstallKey.OpenSubKey(name);
                if (appKey is null ||
                    !string.Equals(appKey.GetValue("DisplayName") as string, "GPU Tweak III", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (appKey.GetValue("InstallLocation") is string installLocation && !string.IsNullOrWhiteSpace(installLocation))
                {
                    candidates.Add(installLocation);
                }
            }
        }

        candidates.Add(@"C:\Program Files (x86)\ASUS\GPUTweakIII");

        var found = candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .FirstOrDefault(path => File.Exists(Path.Combine(path, "ExpanModule.dll")));

        if (found is null)
        {
            throw new DirectoryNotFoundException("GPU Tweak III install folder was not found.");
        }

        return found;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePowerStatus
    {
        public float Pin1;
        public float Pin2;
        public float Pin3;
        public float Pin4;
        public float Pin5;
        public float Pin6;
        public float Pin7;
        public float Pin8;
        public float Pin9;
        public float Pin10;
        public float TotalCurrentAmps;
        public int HardwareAlert;
        public int Count;

        public readonly double[] GetPins()
        {
            return
            [
                Pin1, Pin2, Pin3, Pin4, Pin5,
                Pin6, Pin7, Pin8, Pin9, Pin10
            ];
        }
    }

    private static class Native
    {
        [DllImport("kernel32.dll", EntryPoint = "SetDllDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetDllDirectory(string lpPathName);

        [DllImport("ExpanModule.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Expan_Api_GetPowerStatus(int cardId, ref NativePowerStatus status);
    }
}
