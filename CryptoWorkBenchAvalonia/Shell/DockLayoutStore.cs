using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using Dock.Model.Mvvm.Core;
using Dock.Serializer.SystemTextJson;

namespace CryptoWorkBenchAvalonia.Shell;

internal sealed record LayoutWindow(double X, double Y, double Width, double Height, bool Maximized = false);
internal sealed record LayoutFile(int SchemaVersion, JsonElement Dock, string[] ClosedTools, LayoutWindow MainWindow);
internal sealed record LoadedLayout(IRootDock Root, string[] ClosedTools, LayoutWindow MainWindow);

/// <summary>A bounded presentation-only format. Never pass live application objects to the serializer.</summary>
internal sealed class DockLayoutStore
{
    internal const int MaxBytes = 256 * 1024;
    internal static readonly string[] PaneIds = [DockShellState.DocumentId, DockShellState.VariablesId,
        DockShellState.OutputId, DockShellState.InfoId, DockShellState.DiagnosticsId, DockShellState.StatusId];
    private static readonly HashSet<string> Types = new(StringComparer.Ordinal)
    {
        typeof(RootDock).FullName!, typeof(ProportionalDock).FullName!, typeof(ProportionalDockSplitter).FullName!,
        typeof(DocumentDock).FullName!, typeof(ToolDock).FullName!, typeof(Document).FullName!,
        typeof(Tool).FullName!, typeof(DockWindow).FullName!
    };
    // This is also the output projection. Dock's other defaults and runtime metadata are not part of schema 1.
    private static readonly HashSet<string> Fields = new(StringComparer.Ordinal)
    {
        "$id", "$ref", "$type", "$values", "Id", "VisibleDockables", "ActiveDockable", "Proportion",
        "Orientation", "Alignment", "Windows", "Layout", "X", "Y", "Width", "Height"
    };
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 64
    };
    internal string Path { get; }
    internal string? LastError { get; private set; }
    // Deterministic fault injection at the atomic boundary, used only by tests.
    internal Action? BeforeReplace { get; init; }
    internal DockLayoutStore(string? path = null) => Path = path ?? System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CryptoWorkBench", "dock-layout-v1.json");

    internal LoadedLayout? Load()
    {
        try
        {
            using var stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaxBytes) throw new InvalidDataException("Layout exceeds size limit.");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var loaded = Decode(reader.ReadToEnd());
            LastError = null;
            return loaded;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        catch (Exception error) when (Recoverable(error)) { Report("load", error); return null; }
    }

    internal bool Save(string json)
    {
        string? temporary = null;
        try
        {
            Decode(json);
            var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!;
            Directory.CreateDirectory(directory);
            temporary = System.IO.Path.Combine(directory, ".dock-layout-" + Guid.NewGuid().ToString("N") + ".tmp");
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                stream.Write(bytes); stream.Flush(flushToDisk: true);
            }
            Decode(File.ReadAllText(temporary));
            BeforeReplace?.Invoke();
            // Same filesystem: no delete-first fallback. Failed replacement preserves the old file.
            if (File.Exists(Path)) File.Replace(temporary, Path, null);
            else File.Move(temporary, Path);
            LastError = null;
            return true;
        }
        catch (Exception error) when (Recoverable(error)) { Report("save", error); return false; }
        finally
        {
            if (temporary != null)
                try { File.Delete(temporary); }
                catch (Exception error) when (Recoverable(error)) { Report("temporary cleanup", error); }
        }
    }

    internal void Report(string operation, Exception error)
    {
        // No file payload, source text, credentials, or arbitrary exception message in diagnostics.
        LastError = $"Layout {operation} failed ({error.GetType().Name}); default/last valid layout retained.";
        System.Diagnostics.Trace.WriteLine(LastError);
    }
    internal static bool Recoverable(Exception e) => e is IOException or InvalidDataException or UnauthorizedAccessException or
        JsonException or NotSupportedException or ArgumentException or InvalidOperationException;

    internal static string Encode(IRootDock root, string[] closed, LayoutWindow main)
    {
        Validate(root, closed);
        var clean = Project(root);
        var node = JsonNode.Parse(new DockSerializer().Serialize<IRootDock>(clean))!;
        Prune(node);
        var json = JsonSerializer.Serialize(new LayoutFile(1, JsonSerializer.SerializeToElement(node), closed, main), Options);
        Decode(json);
        return json;
    }

    internal static LoadedLayout Decode(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new InvalidDataException("Layout exceeds size limit.");
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
        CheckDuplicates(document.RootElement);
        var file = JsonSerializer.Deserialize<LayoutFile>(json, Options) ?? throw new InvalidDataException("Empty layout.");
        if (file.SchemaVersion != 1 || file.ClosedTools == null || file.MainWindow == null)
            throw new InvalidDataException("Unsupported layout schema.");
        Preflight(file.Dock);
        var root = new DockSerializer().Deserialize<IRootDock>(file.Dock.GetRawText()) ?? throw new InvalidDataException("Missing root.");
        DockContainerIdentity.NameLegacyContainers(root);
        Validate(root, file.ClosedTools);
        // Discard everything except the validated presentation fields, including all capability flags.
        return new(Project(root), file.ClosedTools, file.MainWindow);
    }

    private static void CheckDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in value.EnumerateObject())
            {
                if (!names.Add(p.Name)) throw new InvalidDataException("Duplicate JSON property.");
                CheckDuplicates(p.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var child in value.EnumerateArray()) CheckDuplicates(child);
    }
    private static void Preflight(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
            foreach (var p in value.EnumerateObject())
            {
                if (!Fields.Contains(p.Name)) throw new InvalidDataException("Unknown layout field.");
                if (p.Name == "$type" && !Types.Contains(p.Value.GetString() ?? ""))
                    throw new InvalidDataException("Unknown dock type.");
                Preflight(p.Value);
            }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var child in value.EnumerateArray()) Preflight(child);
    }
    private static void Prune(JsonNode node)
    {
        if (node is JsonObject obj)
            foreach (var key in obj.Select(p => p.Key).ToArray())
            {
                if (!Fields.Contains(key)) obj.Remove(key);
                else if (obj[key] is { } child) Prune(child);
            }
        else if (node is JsonArray array)
            foreach (var child in array) if (child != null) Prune(child);
    }

    internal static void Validate(IRootDock root, string[] closed)
    {
        var seen = new HashSet<IDockable>(ReferenceEqualityComparer.Instance);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var structuralIds = new HashSet<string>(StringComparer.Ordinal);
        void Walk(IDockable item, IDock? parent, int depth)
        {
            if (item == null || depth > 20 || seen.Count > 100 || !seen.Add(item) || !Types.Contains(item.GetType().FullName!))
                throw new InvalidDataException("Invalid or cyclic dock tree.");
            if (root.Factory != null && (!ReferenceEquals(item.Owner, parent) || !ReferenceEquals(item.Factory, root.Factory)))
                throw new InvalidDataException("Invalid live dock owner or factory.");
            if (!double.IsNaN(item.Proportion) && (!double.IsFinite(item.Proportion) || item.Proportion <= 0 || item.Proportion > 1))
                throw new InvalidDataException("Invalid split size.");
            if (item is IDocument or ITool)
            {
                if (!PaneIds.Contains(item.Id) || !ids.Add(item.Id) ||
                    (item is Document) != (item.Id == DockShellState.DocumentId) ||
                    parent is not (IDocumentDock or IToolDock) || item is Document && parent is not IDocumentDock)
                    throw new InvalidDataException("Invalid pane identity or parent.");
                if (root.Factory is EditorDockFactory { LayoutOwner: { } layout } && !ReferenceEquals(item, layout.Pane(item.Id)))
                    throw new InvalidDataException("A live pane was replaced.");
                return;
            }
            if (item is IProportionalDockSplitter)
            {
                if (parent is not IProportionalDock) throw new InvalidDataException("Invalid splitter parent.");
                return;
            }
            if (item is not IDock dock || parent is IDocumentDock or IToolDock || item is IRootDock && parent != null)
                throw new InvalidDataException("Invalid container hierarchy.");
            if (string.IsNullOrWhiteSpace(dock.Id) || PaneIds.Contains(dock.Id) || dock.Id.StartsWith(DockContainerIdentity.DynamicPrefix, StringComparison.Ordinal) && !structuralIds.Add(dock.Id))
                throw new InvalidDataException("Invalid structural container identity.");
            if (dock.Id == DockShellLayout.Documents && dock is not IDocumentDock ||
                dock.Id is DockShellLayout.Tools or DockShellLayout.Diagnostics && dock is not IToolDock)
                throw new InvalidDataException("Invalid anchor role type.");
            var children = dock.VisibleDockables ?? [];
            if (dock.ActiveDockable != null && !children.Contains(dock.ActiveDockable))
                throw new InvalidDataException("Invalid selected tab.");
            if (dock is IRootDock && (children.Count != 1 || children[0] is not IDock))
                throw new InvalidDataException("Invalid root content.");
            if (dock is IProportionalDock proportional && !Enum.IsDefined(proportional.Orientation))
                throw new InvalidDataException("Invalid split direction.");
            if (dock is IToolDock tool && !Enum.IsDefined(tool.Alignment))
                throw new InvalidDataException("Invalid tool alignment.");
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] == null || dock is IProportionalDock && (children[i] is IProportionalDockSplitter) != (i % 2 == 1))
                    throw new InvalidDataException("Invalid split sequence.");
                Walk(children[i], dock, depth + 1);
            }
            if (dock is IProportionalDock && children.Count % 2 == 0)
                throw new InvalidDataException("Empty or incomplete split.");
            if (dock is IRootDock r)
            {
                if (r.HiddenDockables?.Count > 0 || r.LeftPinnedDockables?.Count > 0 || r.RightPinnedDockables?.Count > 0 ||
                    r.TopPinnedDockables?.Count > 0 || r.BottomPinnedDockables?.Count > 0 || r.PinnedDock != null)
                    throw new InvalidDataException("Pinned/hidden trees are unsupported.");
                if (!ReferenceEquals(r, root) && r.Windows?.Count > 0 || r.Windows?.Count > 6)
                    throw new InvalidDataException("Nested floating windows are unsupported.");
                foreach (var window in r.Windows ?? [])
                {
                    if (window == null || window.Layout == null || window.IsModal || window.ParentWindow != null)
                        throw new InvalidDataException("Invalid floating configuration.");
                    if (root.Factory != null && (!ReferenceEquals(window.Owner, root) || !ReferenceEquals(window.Factory, root.Factory) ||
                        !ReferenceEquals(window.Layout.Window, window) || window.Host != null && !ReferenceEquals(window.Host.Window, window)))
                        throw new InvalidDataException("Invalid live floating ownership.");
                    Walk(window.Layout, null, depth + 1);
                    if (!DockShellLayout.VisibleTree(window.Layout).Any(p => p is IDocument or ITool))
                        throw new InvalidDataException("Empty floating window.");
                }
            }
        }
        Walk(root, null, 0);
        var mainTree = DockShellLayout.VisibleTree(root, false).OfType<IDock>().ToArray();
        if (root.Id != "dock:root" || mainTree.Count(d => d.Id == "dock:main" && d is IProportionalDock) != 1 ||
            new[] { DockShellLayout.Documents, DockShellLayout.Tools, DockShellLayout.Diagnostics }
                .Any(id => mainTree.Count(d => d.Id == id) > 1))
            throw new InvalidDataException("Invalid fixed-shell anchors.");
        foreach (var id in closed)
            if (id == DockShellState.DocumentId || !PaneIds.Contains(id) || !ids.Add(id))
                throw new InvalidDataException("Invalid closed tool inventory.");
        if (!ids.SetEquals(PaneIds)) throw new InvalidDataException("Missing pane identity.");
    }

    // This explicit copy is the privacy boundary: Context/Title/Owner/Factory never enter serialization.
    internal static IRootDock Project(IRootDock root)
    {
        var protectedDocks = DockContainerRoles.ProtectedMainDocks(root);
        IDockable Copy(IDockable source)
        {
            IDockable copy = source switch
            {
                IRootDock => new RootDock { FloatingWindowHostMode = DockFloatingWindowHostMode.Native },
                IProportionalDock p => new ProportionalDock { Orientation = p.Orientation },
                IDocumentDock => new DocumentDock { CanCreateDocument = false },
                IToolDock t => new ToolDock { Alignment = t.Alignment },
                ITool => new Tool { CanPin = false },
                IDocument => new Document { CanClose = false, CanPin = false },
                IProportionalDockSplitter => new ProportionalDockSplitter(),
                _ => throw new InvalidDataException("Unsupported dock type.")
            };
            copy.Id = source.Id; copy.Proportion = source.Proportion;
            if (copy is IDock target && source is IDock dock)
            {
                target.IsCollapsable = !protectedDocks.Contains(dock);
                target.VisibleDockables = new ObservableCollection<IDockable>((dock.VisibleDockables ?? []).Select(Copy));
                var index = dock.ActiveDockable == null ? -1 : dock.VisibleDockables!.IndexOf(dock.ActiveDockable);
                target.ActiveDockable = index < 0 ? null : target.VisibleDockables[index];
            }
            if (copy is IRootDock r && source is IRootDock original)
                r.Windows = new ObservableCollection<IDockWindow>((original.Windows ?? []).Select(w =>
                {
                    var window = new DockWindow { X = w.X, Y = w.Y, Width = w.Width, Height = w.Height,
                        Layout = (IRootDock)Copy(w.Layout!) };
                    window.Layout.Window = window;
                    return window;
                }));
            return copy;
        }
        return (IRootDock)Copy(root);
    }
}
