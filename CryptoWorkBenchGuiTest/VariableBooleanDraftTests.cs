using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.VisualTree;
using AvaloniaEdit;
using CryptoScript.Variables;
using CryptoWorkBenchAvalonia;
using CryptoWorkBenchAvalonia.Models;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.ViewModels;
using NUnit.Framework;
using static CryptoWorkBenchGuiTest.B1EditorIntegrationTests;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class VariableBooleanDraftTests
{
    [Test]
    public Task InvalidBooleanRestoresOnlyItsRowAndPreservesTheOtherEdit() => Ui(async () =>
    {
        await using var host = new Host("script");
        host.Area.PerformTextInput("!");
        ApplicationCommands.Undo.Execute(null, host.Area);
        var document = host.Registration.Snapshot();
        var dictionary = VariableDictionary.Instance();
        var previous = dictionary.GetVariables().ToArray();
        var flag = new ValVariableDeclaration(true) { Id = "flag" };
        var other = new StringVariableDeclaration { Id = "other", Value = "0x(01)" };
        var model = new VariableViewModel { Automation = host.Service };
        var gridTheme = new StyleInclude(new Uri("avares://CryptoWorkBenchAvalonia"))
            { Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml") };
        Application.Current!.Styles.Add(gridTheme);
        var view = new VariableView { DataContext = model };
        var window = new Window { Content = view, Width = 700, Height = 460 };
        try
        {
            host.Service.Mutate(() => { dictionary.Clear(); dictionary.Add(flag); dictionary.Add(other); });
            window.Show();
            window.UpdateLayout();
            model.SetupVariables();
            window.UpdateLayout();
            var grid = view.GetVisualDescendants().OfType<DataGrid>().Single(g => ReferenceEquals(g.ItemsSource, model.DataVariables));
            var details = view.GetVisualDescendants().OfType<TextBox>().Single(t => t.MinHeight == 60);
            var flagRow = model.DataVariables.Single(r => r.Identifier == flag.Id);
            var otherRow = model.DataVariables.Single(r => r.Identifier == other.Id);

            void Select(VariableModel row)
            {
                grid.SelectedItem = row;
                window.UpdateLayout();
                Assert.That(model.SelectedVariable, Is.SameAs(row));
            }
            void Modify(VariableModel row)
            {
                var button = grid.GetVisualDescendants().OfType<Button>().Single(b => ReferenceEquals(b.CommandParameter, row));
                Assert.That(button.IsEffectivelyEnabled && button.Command!.CanExecute(row), Is.True);
                button.Command!.Execute(row);
                window.UpdateLayout();
            }

            Select(otherRow);
            details.SetCurrentValue(TextBox.TextProperty, "0x(02)");
            Select(flagRow);
            details.SetCurrentValue(TextBox.TextProperty, "invalid");
            Assert.That((otherRow.Value, flagRow.Value), Is.EqualTo(("0x(02)", "invalid")));
            var local = host.Service.PresentationVersion;
            var shared = InterpreterHostQueue.StateRevision;
            Modify(flagRow);

            Assert.That(model.SelectedVariable, Is.SameAs(flagRow));
            Assert.That(grid.SelectedItem, Is.SameAs(flagRow));
            Assert.That((details.Text, flagRow.Value, flag.Value), Is.EqualTo(("true", "true", "true")));
            Assert.That(model.DataVariables.Single(r => r.Identifier == other.Id), Is.SameAs(otherRow));
            Assert.That((otherRow.Value, other.Value), Is.EqualTo(("0x(02)", "0x(01)")));
            Assert.That((host.Service.PresentationVersion, InterpreterHostQueue.StateRevision), Is.EqualTo((local, shared)));
            Assert.That(host.Registration.Snapshot(), Is.EqualTo(document));
            ApplicationCommands.Redo.Execute(null, host.Area);
            Assert.That(host.Editor.Text, Is.EqualTo("script!"));
            ApplicationCommands.Undo.Execute(null, host.Area);
            Assert.That(host.Editor.Text, Is.EqualTo("script"));

            Select(otherRow);
            Assert.That(details.Text, Is.EqualTo("0x(02)"));
            Modify(otherRow);
            Assert.That(other.Value, Is.EqualTo("0x(02)"));
            Assert.That((host.Service.PresentationVersion, InterpreterHostQueue.StateRevision), Is.EqualTo((local + 1, shared + 1)));

            var freshFlag = model.DataVariables.Single(r => r.Identifier == flag.Id);
            Select(freshFlag);
            details.SetCurrentValue(TextBox.TextProperty, "false");
            Modify(freshFlag);
            Assert.That((flag.Value, other.Value), Is.EqualTo(("false", "0x(02)")));
            Assert.That((host.Service.PresentationVersion, InterpreterHostQueue.StateRevision), Is.EqualTo((local + 2, shared + 2)));
        }
        finally
        {
            window.Close();
            Application.Current!.Styles.Remove(gridTheme);
            dictionary.Clear();
            foreach (var item in previous) dictionary.Add(item);
        }
    });
}
