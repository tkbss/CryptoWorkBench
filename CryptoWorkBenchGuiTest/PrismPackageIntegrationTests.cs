using Avalonia.Controls;
using Avalonia.Threading;
using NUnit.Framework;
using Prism.Dialogs;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Navigation;
using Prism.Navigation.Regions;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public sealed class PrismPackageIntegrationTests
{
    [Test]
    public Task RealDryIocRegionNavigationActivatesBothViewsAndJournalReturns() =>
        GuiTestSession.Session.Dispatch(() =>
        {
            using var nativeContainer = new DryIoc.Container(DryIocContainerExtension.DefaultRules);
            var container = new DryIocContainerExtension(nativeContainer);
            container.RegisterForNavigation<FirstView>("first");
            container.RegisterForNavigation<SecondView>("second");
            container.Register<IRegionNavigationJournalEntry, RegionNavigationJournalEntry>();
            var journal = new RegionNavigationJournal();
            var navigation = new RegionNavigationService(container, new RegionNavigationContentLoader(container), journal);
            var region = new SingleActiveRegion { NavigationService = navigation };
            navigation.Region = region;
            var manager = new RegionManager();
            manager.Regions.Add("test", region);
            NavigationResult? result = null;
            manager.RequestNavigate("test", "first", r => result = r);
            Assert.That(result?.Success, Is.True, result?.Exception?.ToString());
            Assert.That(region.ActiveViews.Single(), Is.TypeOf<FirstView>());
            manager.RequestNavigate("test", "second", r => result = r);
            Assert.That(result?.Success, Is.True, result?.Exception?.ToString());
            Assert.That(region.ActiveViews.Single(), Is.TypeOf<SecondView>());
            Assert.That(journal.CanGoBack, Is.True);
            journal.GoBack();
            Assert.That(region.ActiveViews.Single(), Is.TypeOf<FirstView>());
        }, CancellationToken.None);

    [Test]
    public Task RealDialogServiceHonorsCloseVetoAndDeliversOneResult() =>
        GuiTestSession.Session.Dispatch(() =>
        {
            using var nativeContainer = new DryIoc.Container(DryIocContainerExtension.DefaultRules);
            var container = new DryIocContainerExtension(nativeContainer);
            var model = new DialogModel();
            var view = new UserControl { DataContext = model };
            var window = new DialogWindow();
            container.RegisterInstance<object>(view, "probe");
            container.RegisterInstance<IDialogWindow>(window);
            IDialogService service = new DialogService(container);
            var callbacks = 0;
            IDialogResult? result = null;
            try
            {
                service.ShowDialog("probe",
                    new DialogParameters { { KnownDialogParameters.ShowNonModal, true }, { "seed", "local-package" } },
                    new DialogCallback().OnClose(r => { result = r; callbacks++; }));
                Dispatcher.UIThread.RunJobs();
                Assert.That(model.Opened, Is.EqualTo("local-package"));
                Assert.That(window.IsVisible, Is.True);
                model.RequestClose.Invoke(ButtonResult.OK);
                Dispatcher.UIThread.RunJobs();
                Assert.That(window.IsVisible, Is.True);
                Assert.That(callbacks, Is.Zero);
                model.AllowClose = true;
                model.RequestClose.Invoke(ButtonResult.OK);
                Dispatcher.UIThread.RunJobs();
                Assert.That(window.IsVisible, Is.False);
                Assert.That(model.Closed, Is.EqualTo(1));
                Assert.That(callbacks, Is.EqualTo(1));
                Assert.That(result?.Result, Is.EqualTo(ButtonResult.OK));
                Assert.That(window.Content, Is.Null);
                Assert.That(window.DataContext, Is.Null);
            }
            finally { model.AllowClose = true; window.Close(); }
        }, CancellationToken.None);

    public sealed class FirstView : UserControl { }
    public sealed class SecondView : UserControl { }
    public sealed class DialogModel : IDialogAware
    {
        public bool AllowClose { get; set; }
        public string? Opened { get; private set; }
        public int Closed { get; private set; }
        public DialogCloseListener RequestClose { get; private set; }
        public bool CanCloseDialog() => AllowClose;
        public void OnDialogOpened(IDialogParameters parameters) => Opened = parameters.GetValue<string>("seed");
        public void OnDialogClosed() => Closed++;
    }
}
