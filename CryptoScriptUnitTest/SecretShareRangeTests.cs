using CryptoScript.SecretSharing;

namespace CryptoScriptUnitTest;

public class SecretShareRangeTests
{
    [TestCase(0, 256)]
    [TestCase(0, 0)]
    [TestCase(127, 3)]
    [TestCase(255, 1)]
    [TestCase(256, 0)]
    public void ExactRangesPreserveSourceAndDestinationOutsideRange(int offset, int count)
    {
        byte[] original = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        var share = new SecretShare(1, original);
        var destination = Enumerable.Repeat((byte)0xA5, count + 2).ToArray();
        share.CopyValueRange(offset, destination.AsSpan(1, count));
        Assert.That(destination[0], Is.EqualTo(0xA5));
        Assert.That(destination[^1], Is.EqualTo(0xA5));
        Assert.That(destination.AsSpan(1, count).ToArray(), Is.EqualTo(original.AsSpan(offset, count).ToArray()));
        destination.AsSpan().Clear();
        Assert.That(share.Value, Is.EqualTo(original));
    }

    [TestCase(-1, 0)]
    [TestCase(257, 0)]
    [TestCase(int.MaxValue, 1)]
    [TestCase(int.MinValue, 0)]
    [TestCase(256, 1)]
    [TestCase(255, 2)]
    public void InvalidRangesFailBeforeAnyDestinationWrite(int offset, int count)
    {
        var share = new SecretShare(1, new byte[256]);
        var destination = Enumerable.Repeat((byte)0xA5, count).ToArray();
        Assert.Throws<ArgumentOutOfRangeException>(() => share.CopyValueRange(offset, destination));
        Assert.That(destination, Is.All.EqualTo(0xA5));
    }

    [Test]
    public void LargeShareSetCanBeReadInBoundedRangesWithoutFullGetterClones()
    {
        const int bytesPerShare = 131072;
        var descriptor = new SecretDescriptor(SecretDescriptor.Split, 255, 255, bytesPerShare * 8);
        var shares = Enumerable.Range(1, 255)
            .Select(x => new SecretShare(x, Enumerable.Repeat((byte)x, bytesPerShare).ToArray())).ToArray();
        var set = new SecretSet(descriptor, shares);
        var buffer = new byte[4096];
        long total = 0;
        foreach (var share in set.Shares)
            for (int offset = 0; offset < bytesPerShare; offset += buffer.Length)
            {
                share.CopyValueRange(offset, buffer);
                Assert.That(buffer, Is.All.EqualTo((byte)share.X));
                total += buffer.Length;
            }
        Assert.That(total, Is.EqualTo(33423360L));
    }

    [Test]
    public void RangeReadsDoNotAllocatePerShareClone()
    {
        var share = new SecretShare(1, new byte[131072]);
        var buffer = new byte[4096];
        share.CopyValueRange(0, buffer);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) share.CopyValueRange(i, buffer);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.That(allocated, Is.EqualTo(0));
    }
}
