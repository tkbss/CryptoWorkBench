using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using CryptoScript.Model;
using CryptoScript.Operations;
using CryptoScript.Serialization;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class SecureSavePerformanceTests
{
    [Test, Explicit("Manual hardware measurement; no timing assertions or routine CI execution.")]
    public async Task MeasureLocalHardware()
    {
        var dictionary = VariableDictionary.Instance(); var previous = dictionary.GetVariables().ToArray();
        string directory = Path.Combine(Path.GetTempPath(),"S05B2-benchmark-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var rows = new List<string> { "mode,kdf,payloadBytes,fileBytes,elapsedMs,allocatedBytes,sampledPeakWorkingSetBytes" };
        try
        {
            foreach (bool pbkdf in new[] { false,true })
            foreach (bool gmac in new[] { false,true })
            foreach (int targetSize in new[] { 1024,1024*1024,SecureSaveOptions.PayloadLimit })
            {
                dictionary.Clear();
                var path = new PathVariableDeclaration { Id="path",Type=new CryptoTypePath(),Value=Path.Combine(directory,Guid.NewGuid()+".json") };
                var data = new StringVariableDeclaration { Id="data",Value="" };
                int overhead = SecureBuffer.Utf8.GetByteCount(new VariableSnapshotJsonSerializer().Serialize(new[] { data }));
                data.Value = new string('x',targetSize-overhead); dictionary.Add(path); dictionary.Add(data);
                var runner = new CryptoScriptRunner(); runner.PasswordRequested += (_,r) => Task.FromResult(SecureSaveTests.Supply(r));
                var program = ParserBuilder.StringBuild($"SecureSave(path,data,#SECUREMODE:{(gmac?"GMAC":"GCM")},#SECUREKDF:{(pbkdf?"PBKDF2":"ARGON2ID")})").program();
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                long allocated = GC.GetTotalAllocatedBytes(true), peak = 0;
                using var stopSampling = new CancellationTokenSource();
                var sample = Task.Run(async () =>
                {
                    using var process = Process.GetCurrentProcess();
                    while (!stopSampling.IsCancellationRequested)
                    {
                        process.Refresh(); peak = Math.Max(peak,process.WorkingSet64);
                        await Task.Delay(5);
                    }
                });
                var watch = Stopwatch.StartNew();
                try { await runner.ExecuteAsync(program); }
                finally { watch.Stop(); stopSampling.Cancel(); await sample; }
                rows.Add(string.Join(",",gmac?"GMAC":"GCM",pbkdf?"PBKDF2":"ARGON2ID",targetSize,
                    new FileInfo(path.Value).Length,watch.Elapsed.TotalMilliseconds.ToString("F2",CultureInfo.InvariantCulture),
                    GC.GetTotalAllocatedBytes(true)-allocated,peak));
            }
            rows.Add("kdfOnly,memoryKiB,iterations,lanes,elapsedMs,processPeakWorkingSetBytes");
            foreach (var options in new[] { new SecureSaveOptions(),new SecureSaveOptions(Memory:262144),
                new SecureSaveOptions(Pbkdf:true),new SecureSaveOptions(Pbkdf:true,PbkdfIterations:2000000) })
            {
                var watch = Stopwatch.StartNew();
                var key = SecureContainerCryptography.DeriveMaster(options,"benchmark"u8.ToArray(),new byte[16]);
                watch.Stop(); CryptographicOperations.ZeroMemory(key);
                using var process = Process.GetCurrentProcess();
                rows.Add(string.Join(",",options.KdfId,options.Memory,options.Pbkdf?options.PbkdfIterations:options.Iterations,
                    options.Lanes,watch.Elapsed.TotalMilliseconds.ToString("F2",CultureInfo.InvariantCulture),process.PeakWorkingSet64));
            }
            string result = Path.Combine(TestContext.CurrentContext.TestDirectory,"S05-B2-performance.csv");
            File.WriteAllLines(result,rows); TestContext.Out.WriteLine(string.Join(Environment.NewLine,rows));
        }
        finally
        {
            dictionary.Clear(); foreach (var v in previous) dictionary.Add(v);
            Directory.Delete(directory,true);
        }
    }
}
