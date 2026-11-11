using System.ComponentModel;
using System.Numerics;
using System.Runtime.InteropServices;
using CryptoWorkBenchAvalonia.Services.Automation;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

public class LeaseClockTests
{
    [TestCase(0L, 0L, 0UL)]
    [TestCase(12L, 999_999L, 12_000UL)]
    [TestCase(12L, 999_999_999L, 12_999UL)]
    public void LinuxSampleUsesWholeMilliseconds(long seconds, long nanos, ulong expected) =>
        Assert.That(LeaseClock.LinuxMilliseconds(0, seconds, nanos, 0), Is.EqualTo(expected));

    [TestCase(-1L, 0L)]
    [TestCase(0L, -1L)]
    [TestCase(0L, 1_000_000_000L)]
    public void InvalidLinuxSampleIsRejected(long seconds, long nanos) =>
        Assert.Throws<InvalidOperationException>(() => LeaseClock.LinuxMilliseconds(0, seconds, nanos, 0));

    [TestCase(-1)]
    [TestCase(1)]
    public void LinuxNativeFailureIsNotAZeroSample(int result)
    {
        var error = Assert.Throws<Win32Exception>(() => LeaseClock.LinuxMilliseconds(result, 0, 0, 22));
        Assert.That(error!.NativeErrorCode, Is.EqualTo(22));
    }

    [Test]
    public void LinuxConversionChecksMultiplicationAndAdditionOverflow()
    {
        long seconds = (long)(ulong.MaxValue / 1000);
        Assert.That(LeaseClock.LinuxMilliseconds(0, seconds, 615_000_000, 0), Is.EqualTo(ulong.MaxValue));
        Assert.Throws<OverflowException>(() => LeaseClock.LinuxMilliseconds(0, seconds, 616_000_000, 0));
        Assert.Throws<OverflowException>(() => LeaseClock.LinuxMilliseconds(0, seconds + 1, 0, 0));
    }

    [TestCase(0UL, 1U, 1U)]
    [TestCase(999_999UL, 1U, 1U)]
    [TestCase(24_000UL, 125U, 3U)]
    [TestCase(ulong.MaxValue, 125U, 3U)]
    [TestCase(ulong.MaxValue, uint.MaxValue, uint.MaxValue)]
    public void MacConversionMatchesIntegerRationalArithmetic(ulong ticks, uint numer, uint denom)
    {
        var expected = (ulong)((BigInteger)ticks * numer / denom / 1_000_000);
        Assert.That(LeaseClock.MacMilliseconds(ticks, 0, numer, denom), Is.EqualTo(expected));
    }

    [TestCase(1, 1U, 1U)]
    [TestCase(-1, 1U, 1U)]
    [TestCase(0, 0U, 1U)]
    [TestCase(0, 1U, 0U)]
    public void InvalidMacTimebaseIsRejected(int result, uint numer, uint denom) =>
        Assert.Throws<InvalidOperationException>(() => LeaseClock.MacMilliseconds(1, result, numer, denom));

    [Test]
    public void MacFinalNarrowingCannotWrap() =>
        Assert.Throws<OverflowException>(() => LeaseClock.MacMilliseconds(ulong.MaxValue, 0, uint.MaxValue, 1));

    [Test]
    public void NativeStructuresMatchTheSupported64BitAbis()
    {
        Assert.That(Marshal.SizeOf<LeaseClock.LinuxTimespec>(), Is.EqualTo(16));
        Assert.That(Marshal.OffsetOf<LeaseClock.LinuxTimespec>("Seconds").ToInt64(), Is.Zero);
        Assert.That(Marshal.OffsetOf<LeaseClock.LinuxTimespec>("Nanoseconds").ToInt64(), Is.EqualTo(8));
        Assert.That(Marshal.SizeOf<LeaseClock.MacTimebase>(), Is.EqualTo(8));
        Assert.That(Marshal.OffsetOf<LeaseClock.MacTimebase>("Numer").ToInt64(), Is.Zero);
        Assert.That(Marshal.OffsetOf<LeaseClock.MacTimebase>("Denom").ToInt64(), Is.EqualTo(4));
    }

    [TestCase("WINDOWS", Architecture.Arm64)]
    [TestCase("WINDOWS", Architecture.X86)]
    [TestCase("LINUX", Architecture.Arm64)]
    [TestCase("LINUX", Architecture.X86)]
    [TestCase("OSX", Architecture.X86)]
    [TestCase("FREEBSD", Architecture.X64)]
    public void UnsupportedPlatformOrAbiDefersFailureUntilRead(string os, Architecture architecture)
    {
        var read = LeaseClock.Create(OSPlatform.Create(os), architecture);
        Assert.Throws<PlatformNotSupportedException>(() => read());
        Assert.Throws<PlatformNotSupportedException>(() => read());
    }
}
