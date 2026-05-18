using System.Globalization;
using System.Runtime.InteropServices;

const string GpuTweakPath = @"C:\Program Files (x86)\ASUS\GPUTweakIII";

Native.SetDllDirectory(GpuTweakPath);
Environment.CurrentDirectory = GpuTweakPath;

Console.WriteLine($"Process: {(Environment.Is64BitProcess ? "x64" : "x86")}");
Console.WriteLine($"GPU Tweak path: {GpuTweakPath}");

ProbeCheckFunctions();
ProbePowerStatus();
ProbeBtfPowerStatus();
ProbeThermalMap();

static void ProbeCheckFunctions()
{
    Console.WriteLine("== support checks ==");
    Span<int> ids = stackalloc[] { 0, 1, 0x2B85, 0x8A5A };

    foreach (var id in ids)
    {
        var mtpd = new byte[16];
        var mtpdCode = Native.Expan_Api_Check_MTPD_Support(id, ref mtpd[0]);
        Console.WriteLine($"MTPD id={id}: code={mtpdCode}, bytes={Hex(mtpd)}");

        var function = new int[8];
        var functionBytes = MemoryMarshal.AsBytes(function.AsSpan());
        var functionCode = Native.Expan_Api_Check_Function_Support(id, ref MemoryMarshal.GetReference<byte>(functionBytes));
        Console.WriteLine($"Function id={id}: code={functionCode}, ints={string.Join(", ", function)}");
    }
}

static void ProbePowerStatus()
{
    Console.WriteLine("== Expan_Api_GetPowerStatus ==");
    Span<int> ids = stackalloc[] { 0, 1, 0x2B85, 0x8A5A };

    foreach (var id in ids)
    {
        var data = new byte[128];
        var code = Native.Expan_Api_GetPowerStatus(id, ref data[0]);
        DumpBuffer($"PowerStatus id={id} code={code}", data);
    }
}

static void ProbeBtfPowerStatus()
{
    Console.WriteLine("== Expan_Api_Get_BTFSW_PowerStatus ==");
    foreach (var selector in new[] { 1, 100, 450, 600, 1000 })
    {
        var data = new byte[128];
        var code = Native.Expan_Api_Get_BTFSW_PowerStatus(selector, ref data[0]);
        DumpBuffer($"BTFSW selector={selector} code={code}", data);
    }
}

static void ProbeThermalMap()
{
    Console.WriteLine("== Expan_Api_GetThermalMap ==");
    Span<int> ids = stackalloc[] { 0, 1, 0x2B85, 0x8A5A };

    foreach (var id in ids)
    {
        var data = new byte[256];
        var code = Native.Expan_Api_GetThermalMap(id, ref data[0]);
        DumpBuffer($"ThermalMap id={id} code={code}", data);
    }
}

static void DumpBuffer(string title, Span<byte> data)
{
    Console.WriteLine(title);
    Console.WriteLine($"  hex: {Hex(data[..Math.Min(data.Length, 96)])}");
    Console.WriteLine($"  i32: {Ints(data, 16)}");
    Console.WriteLine($"  f32: {Floats(data, 16)}");
}

static string Hex(ReadOnlySpan<byte> bytes)
{
    return string.Join(" ", bytes.ToArray().Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
}

static string Ints(ReadOnlySpan<byte> bytes, int count)
{
    var values = new List<int>();
    for (var i = 0; i < count && i * 4 + 4 <= bytes.Length; i++)
    {
        values.Add(MemoryMarshal.Read<int>(bytes[(i * 4)..]));
    }

    return string.Join(", ", values);
}

static string Floats(ReadOnlySpan<byte> bytes, int count)
{
    var values = new List<string>();
    for (var i = 0; i < count && i * 4 + 4 <= bytes.Length; i++)
    {
        var value = MemoryMarshal.Read<float>(bytes[(i * 4)..]);
        values.Add(value.ToString("0.###", CultureInfo.InvariantCulture));
    }

    return string.Join(", ", values);
}

internal static partial class Native
{
    [DllImport("kernel32.dll", EntryPoint = "SetDllDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetDllDirectory(string lpPathName);

    [DllImport("ExpanModule.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Expan_Api_Check_MTPD_Support(int cardId, ref byte output);

    [DllImport("ExpanModule.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Expan_Api_Check_Function_Support(int cardId, ref byte output);

    [DllImport("ExpanModule.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Expan_Api_GetPowerStatus(int cardId, ref byte output);

    [DllImport("ExpanModule.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Expan_Api_Get_BTFSW_PowerStatus(int selector, ref byte output);

    [DllImport("ExpanModule.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Expan_Api_GetThermalMap(int cardId, ref byte output);
}
