using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CryptoWorkBenchAvalonia.Services.Automation;

// Boot-relative milliseconds including suspend. Never substitute an uptime or UTC clock.
// Selection does not call native code: every native/ABI/conversion failure reaches ReadTime.
internal static class LeaseClock
{
    internal static Func<ulong> Create() => Create(
        OperatingSystem.IsWindows() ? OSPlatform.Windows :
        OperatingSystem.IsLinux() ? OSPlatform.Linux :
        OperatingSystem.IsMacOS() ? OSPlatform.OSX : OSPlatform.Create("Unsupported"),
        RuntimeInformation.ProcessArchitecture);

    internal static Func<ulong> Create(OSPlatform platform, Architecture architecture)
    {
        if (platform == OSPlatform.Windows && architecture == Architecture.X64) return GetTickCount64;
        if (platform == OSPlatform.Linux && architecture == Architecture.X64) return ReadLinux;
        if (platform == OSPlatform.OSX && architecture is Architecture.X64 or Architecture.Arm64) return ReadMac;
        return () => throw new PlatformNotSupportedException("No suspend-inclusive lease clock for this platform/ABI.");
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern ulong GetTickCount64();

    // Linux x64 LP64 ABI only: time_t and long are signed 64-bit, offsets 0/8, size 16.
    // Deliberately not used on 32-bit/time64 or other architectures.
    [StructLayout(LayoutKind.Sequential)]
    internal struct LinuxTimespec
    {
        internal long Seconds;
        internal long Nanoseconds;
    }

    private const int ClockBoottime = 7;
    [DllImport("libc", EntryPoint = "clock_gettime", ExactSpelling = true, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ClockGetTime(int clockId, out LinuxTimespec time);

    private static ulong ReadLinux()
    {
        if (IntPtr.Size != 8 || Marshal.SizeOf<LinuxTimespec>() != 16 ||
            Marshal.OffsetOf<LinuxTimespec>(nameof(LinuxTimespec.Nanoseconds)).ToInt64() != 8)
            throw new PlatformNotSupportedException("Unexpected Linux timespec ABI.");
        int result = ClockGetTime(ClockBoottime, out var time);
        int error = Marshal.GetLastPInvokeError();
        return LinuxMilliseconds(result, time.Seconds, time.Nanoseconds, error);
    }

    internal static ulong LinuxMilliseconds(int result, long seconds, long nanoseconds, int error)
    {
        if (result != 0) throw new Win32Exception(error, "CLOCK_BOOTTIME failed.");
        if (seconds < 0 || nanoseconds is < 0 or >= 1_000_000_000)
            throw new InvalidOperationException("Invalid CLOCK_BOOTTIME sample.");
        return checked((ulong)seconds * 1000 + (ulong)nanoseconds / 1_000_000);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MacTimebase
    {
        internal uint Numer;
        internal uint Denom;
    }

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "mach_continuous_time", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    private static extern ulong MachContinuousTime();

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "mach_timebase_info", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    private static extern int MachTimebaseInfo(out MacTimebase timebase);

    // Lazy initialization occurs inside a clock read, not service construction. Failure is cached.
    private static readonly Lazy<MacTimebase> Timebase = new(() =>
    {
        if (IntPtr.Size != 8 || Marshal.SizeOf<MacTimebase>() != 8 ||
            Marshal.OffsetOf<MacTimebase>(nameof(MacTimebase.Denom)).ToInt64() != 4)
            throw new PlatformNotSupportedException("Unexpected macOS timebase ABI.");
        int result = MachTimebaseInfo(out var timebase);
        MacMilliseconds(0, result, timebase.Numer, timebase.Denom);
        return timebase;
    });

    private static ulong ReadMac()
    {
        var timebase = Timebase.Value;
        return MacMilliseconds(MachContinuousTime(), 0, timebase.Numer, timebase.Denom);
    }

    internal static ulong MacMilliseconds(ulong ticks, int result, uint numer, uint denom)
    {
        if (result != 0) throw new InvalidOperationException("mach_timebase_info failed.");
        if (numer == 0 || denom == 0) throw new InvalidOperationException("Invalid macOS timebase.");
        // A 64-bit tick count times a 32-bit numerator fits UInt128, but the final value may not fit ulong.
        return checked((ulong)((UInt128)ticks * numer / denom / 1_000_000));
    }
}
