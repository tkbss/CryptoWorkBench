using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CryptoScript.SecretSharing;
using CryptoScript.Variables;
using CryptoWorkBenchAvalonia.Controls;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.ViewModels;
using CryptoWorkBenchAvalonia.Views;

namespace InspectorNativeProbe;

internal static class Program
{
    [STAThread] internal static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows()) return 77;
        if (args.Contains("--preview"))
            return AppBuilder.Configure<Probe>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
        var directory=Directory.CreateTempSubdirectory("cwb-native-inspector-");
        Environment.SetEnvironmentVariable("TMP",directory.FullName);
        Environment.SetEnvironmentVariable("TEMP",directory.FullName);
        long peakDisk=0;
        using var timer=new Timer(_=>
        {
            long total=0;
            foreach(var f in directory.EnumerateFiles("cwb-inspector-*.payload"))
                try { total+=f.Length; } catch(FileNotFoundException) { }
            lock(directory) peakDisk=Math.Max(peakDisk,total);
        },null,0,10);
        int code=AppBuilder.Configure<Probe>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
        timer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        int ownFiles=directory.EnumerateFiles("cwb-inspector-*.payload").Count();
        Console.WriteLine($"sampledPeakDiskBytes={peakDisk} ownedFilesAfterDrain={ownFiles}");
        if (!directory.EnumerateFileSystemInfos().Any()) directory.Delete();
        return ownFiles==0 ? code : 1;
    }
}

public sealed class Probe : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        var lifetime = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        var window = lifetime.MainWindow = new Window { Width=600, Height=650,Title="Property inspector native verification" };
        bool preview = lifetime.Args?.Contains("--preview") == true;
        window.Opened += (_,_) => Dispatcher.UIThread.Post(async () =>
        {
            int code=0;
            try { await Verify(window,preview); }
            catch (Exception e) { Console.Error.WriteLine(e); code=1; }
            lifetime.Shutdown(code);
        });
        base.OnFrameworkInitializationCompleted();
    }
    private static async Task Until(Func<bool> ready)
    {
        var elapsed=Stopwatch.StartNew();
        while (!ready()) { if (elapsed.Elapsed.TotalMinutes>2) throw new TimeoutException(); await Task.Delay(15); }
    }
    [DllImport("user32")] private static extern bool OpenClipboard(IntPtr window);
    [DllImport("user32")] private static extern bool CloseClipboard();
    [DllImport("user32")] private static extern IntPtr GetClipboardData(uint format);
    [DllImport("kernel32")] private static extern IntPtr GlobalLock(IntPtr memory);
    [DllImport("kernel32")] private static extern bool GlobalUnlock(IntPtr memory);
    [DllImport("kernel32")] private static extern nuint GlobalSize(IntPtr memory);
    private static void VerifyNativeText(string expected)
    {
        if (!OpenClipboard(IntPtr.Zero)) throw new Win32Exception();
        try
        {
            var handle=GetClipboardData(13);
            if(handle==IntPtr.Zero || GlobalSize(handle)<(nuint)(expected.Length+1)*2) throw new InvalidDataException("Short CF_UNICODETEXT.");
            var address=GlobalLock(handle);
            if(address==IntPtr.Zero) throw new Win32Exception();
            try
            {
                var buffer=new char[32768];
                for(int i=0;i<expected.Length;)
                {
                    int n=Math.Min(buffer.Length,expected.Length-i);
                    Marshal.Copy(IntPtr.Add(address,i*2),buffer,0,n);
                    if(!buffer.AsSpan(0,n).SequenceEqual(expected.AsSpan(i,n))) throw new InvalidDataException("CF_UNICODETEXT differs.");
                    i+=n;
                }
                if(Marshal.ReadInt16(address,expected.Length*2)!=0) throw new InvalidDataException("Missing terminator.");
            }
            finally { GlobalUnlock(handle); }
        }
        finally { CloseClipboard(); }
    }
    [DllImport("user32",SetLastError=true)] private static extern bool PostMessage(IntPtr hwnd,uint message,nint key,nint flags);
    private async Task Verify(Window window,bool preview)
    {
        await using var host=new WorkbenchAutomationService();
        host.AttachEditor(new WorkbenchTextEditor(),null!);
        var variables=new VariableViewModel { Automation=host };
        byte[] bytes=new byte[131072];
        var shares=new List<SecretShare>();
        for(int x=1;x<=255;x++){ Array.Fill(bytes,(byte)x); shares.Add(new(x,bytes)); }
        var secret=new SecretVariableDeclaration(new(new(SecretDescriptor.Split,255,255,1048576),shares)){Id="large"};
        host.Mutate(()=>
        {
            var dictionary=VariableDictionary.Instance();
            dictionary.Add(secret);
            if (!preview) return;
            dictionary.Add(new KeyVariableDeclaration { Id="aes",Type=new CryptoScript.Model.CryptoTypeKey(),
                KeyType=KeyType.Secret(KeyAlgorithm.Aes),ValueFormat=FormatConversions.HEX,
                KeySizeInBits=new KeySize(128),Value="0x(000102030405060708090A0B0C0D0E0F)" });
            dictionary.Add(new StringVariableDeclaration { Id="text",Value="A\r\nΩ\n" });
            dictionary.Add(new ParameterVariableDeclaration { Id="parameters" });
            dictionary.Add(new ValVariableDeclaration(true){Id="result"});
            dictionary.Add(new PathVariableDeclaration {Id="path",Value="example.sbk"});
            dictionary.Add(new BlockHeaderVariableDeclaration {Id="header",Definition=CryptoScript.CryptoAlgorithm.WRAPPERS.Tr31HeaderDefinition.Create(
                new Dictionary<string,string>{["KBVID"]="D",["KEYU"]="K0",["ALGO"]="A",["MODEU"]="B",["EXP"]="E"})});
        },variables.ApplyVariables);
        variables.SelectedVariable=variables.DataVariables.Single(v=>v.Identifier=="large");
        using var model=new VariableInspectorViewModel(variables);
        var renders = new List<double>();
        long renderStart = 0;
        model.Changed += () => renderStart=Stopwatch.GetTimestamp();
        var view=new DockVariableInspectorView(model);
        model.Changed += () => renders.Add(Stopwatch.GetElapsedTime(renderStart).TotalMilliseconds);
        window.Content=view;
        await Until(()=>model.Page?.RuntimeType==typeof(SecretVariableDeclaration).FullName);
        if (preview)
        {
            var selector=new ComboBox { ItemsSource=variables.DataVariables.Select(v=>v.Identifier).ToArray(),SelectedItem="large" };
            selector.SelectionChanged+=(_,_)=>variables.SelectedVariable=variables.DataVariables.Single(v=>v.Identifier==(string)selector.SelectedItem!);
            var container=new DockPanel();
            window.Content=null;
            DockPanel.SetDock(selector,Avalonia.Controls.Dock.Top);
            container.Children.Add(selector); container.Children.Add(view); window.Content=container;
            var closed = new TaskCompletionSource();
            window.Closed += (_,_) => closed.TrySetResult();
            await closed.Task;
            return;
        }
        Button Button(string name)=>view.GetVisualDescendants().OfType<Button>().Single(b=>AutomationProperties.GetName(b)==name);
        foreach (var theme in new[]{ThemeVariant.Light,ThemeVariant.Dark,ThemeVariant.Default})
        {
            RequestedThemeVariant=theme; window.Width=360;
            if (!Button("Inspect SecretSet").Focus()) throw new InvalidOperationException("Property focus failed.");
        }
        window.Activate();
        Button("Inspect SecretSet").Focus();
        var hwnd=window.TryGetPlatformHandle()!.Handle;
        if (!PostMessage(hwnd,0x100,9,1) || !PostMessage(hwnd,0x101,9,unchecked((int)0xC0000001)))
            throw new Win32Exception();
        await Until(()=>!Button("Inspect SecretSet").IsFocused);
        Button("Inspect SecretSet").Focus();
        if (!PostMessage(hwnd,0x100,13,1) || !PostMessage(hwnd,0x101,13,unchecked((int)0xC0000001)))
            throw new Win32Exception();
        await Until(()=>model.Page?.RuntimeType==typeof(SecretSet).FullName);
        foreach(var name in new[]{"Shares","[0]","Value"})
            await model.OpenAsync(model.Page!.Rows.Single(r=>r.Name==name));
        Button("Open full value").RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        await Until(()=>!model.Busy);
        var detail=view.GetVisualDescendants().OfType<TextBox>().Single();
        if(model.Detail?.Length!=262148 || detail.Text?.Length!=4096 || !detail.IsReadOnly)
            throw new InvalidDataException("Long share view is incomplete.");
        Button("Next text page").RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        if(detail.Text!=model.Detail.Substring(4096,4096)) throw new InvalidDataException("Text page differs.");
        await model.OpenAsync(null,ancestor:1);
        Button("Parent property").RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        await Until(()=>!model.CanBack);
        foreach (var sample in new[]{" A\r\nΩ\n","0x(aabb)", "\ud800", ""})
        {
            try
            {
                await view.CopyAsync(sample);
                if (await window.Clipboard!.TryGetTextAsync()!=sample) throw new InvalidDataException();
                Console.WriteLine($"Native scalar units={sample.Length}: exact");
            }
            catch(IOException e) { Console.WriteLine($"Native scalar units={sample.Length}: platform error {e.Message}"); }
        }
        try { await view.CopyAsync("a\0b"); throw new InvalidDataException("Embedded NUL was accepted."); }
        catch(NotSupportedException) { Console.WriteLine("Embedded NUL rejected before handoff."); }
        for(int i=0;i<16;i++)
        {
            await model.OpenAsync(model.Page!.Rows.Single(r=>r.Name=="SecretSet"));
            await model.OpenAsync(null,back:true);
        }
        var clock=Stopwatch.StartNew();
        long baseline=Process.GetCurrentProcess().WorkingSet64;
        Button("Copy full value").RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        await Until(()=>!model.Busy);
        if(model.Message?.StartsWith("Copied",StringComparison.Ordinal)!=true) throw new InvalidOperationException(model.Status);
        long handoff=clock.ElapsedMilliseconds;
        string text=await window.Clipboard!.TryGetTextAsync() ?? throw new InvalidOperationException("Clipboard empty.");
        VerifyNativeText(text);
        var peak=Process.GetCurrentProcess().PeakWorkingSet64;
        var renderTimes=new List<double>();
        for(int i=0;i<32;i++)
        {
            var started=Stopwatch.GetTimestamp();
            await model.OpenAsync(null);
            renderTimes.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
        renderTimes.Sort();
        long clipboardMs=0;
        await model.ValueAsync(null,false,async full=>
        {
            var t=Stopwatch.StartNew();
            await window.Clipboard.SetTextAsync(full);
            clipboardMs=t.ElapsedMilliseconds;
        });
        if(model.Message?.StartsWith("Copied")!=true) throw new InvalidOperationException(model.Status);
        using var parsed=JsonDocument.Parse(text);
        var actual=parsed.RootElement.GetProperty("SecretSet").GetProperty("Shares");
        if(actual.GetArrayLength()!=255) throw new InvalidDataException();
        for(int x=1;x<=255;x++)
        {
            var item=actual[x-1]; var hex=item.GetProperty("Value").GetString()!;
            if(item.GetProperty("X").GetInt32()!=x || hex.Length!=262148 || !hex.StartsWith("0x(") || hex[^1]!=')')
                throw new InvalidDataException();
            var pair=x.ToString("X2");
            for(int i=3;i<hex.Length-1;i+=2) if(hex[i]!=pair[0] || hex[i+1]!=pair[1]) throw new InvalidDataException();
        }
        renders.Sort();
        Console.WriteLine($"renderSamples={renders.Count} renderP95Ms={renders[(int)((renders.Count-1)*0.95)]:F3} renderMaxMs={renders[^1]:F3}");
        Console.WriteLine($"PASS platform={Environment.OSVersion} runtime={Environment.Version} chars={text.Length} elapsedMs={handoff} warmClipboardSetMs={clipboardMs} cachedNavigationP95Ms={renderTimes[30]:F3} cachedNavigationMaxMs={renderTimes[^1]:F3} baselineWorkingSet={baseline} peakWorkingSet={peak}");
    }
}
