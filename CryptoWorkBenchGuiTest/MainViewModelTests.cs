using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.ViewModels;
using FluentAssertions;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

public class MainViewModelTests
{
    [Test]
    public void HistoryCommands_ExecuteMatchingHistoryServiceOperations()
    {
        var history = new HistoryServiceFake();
        var sut = new MainViewModel(history);

        sut.InfoHistoryBackCommand.Execute();

        history.InfoBackCallCount.Should().Be(1);
        history.InfoForwardCallCount.Should().Be(0);
        history.LineBackCallCount.Should().Be(0);
        history.LineForwardCallCount.Should().Be(0);

        sut.InfoHistoryForwardCommand.Execute();

        history.InfoBackCallCount.Should().Be(1);
        history.InfoForwardCallCount.Should().Be(1);
        history.LineBackCallCount.Should().Be(0);
        history.LineForwardCallCount.Should().Be(0);

        sut.LineHistoryBackCommand.Execute();

        history.InfoBackCallCount.Should().Be(1);
        history.InfoForwardCallCount.Should().Be(1);
        history.LineBackCallCount.Should().Be(1);
        history.LineForwardCallCount.Should().Be(0);

        sut.LineHistoryForwardCommand.Execute();

        history.InfoBackCallCount.Should().Be(1);
        history.InfoForwardCallCount.Should().Be(1);
        history.LineBackCallCount.Should().Be(1);
        history.LineForwardCallCount.Should().Be(1);
    }

    private sealed class HistoryServiceFake : IHistoryService
    {
        public bool InfoCanBack => true;
        public bool InfoCanForward => true;
        public bool LineCanBack => true;
        public bool LineCanForward => true;

        public int InfoBackCallCount { get; private set; }
        public int InfoForwardCallCount { get; private set; }
        public int LineBackCallCount { get; private set; }
        public int LineForwardCallCount { get; private set; }

        public event EventHandler<string>? InfoHistoryChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<string>? LineHistoryChanged
        {
            add { }
            remove { }
        }

        public void InfoBack() => InfoBackCallCount++;
        public void InfoForward() => InfoForwardCallCount++;
        public void LineBack() => LineBackCallCount++;
        public void LineForward() => LineForwardCallCount++;
        public void AddInfo(string entry) { }
        public void AddLine(string entry) { }
    }
}
