using AvaloniaEdit.Document;
using CryptoWorkBenchAvalonia.Controls;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.ViewModels;
using NUnit.Framework;
namespace CryptoWorkBenchGuiTest;
[NonParallelizable]
public class HistoryCorrectionTests
{
 private static Task Ui(Func<Task> body) => GuiTestSession.Session.Dispatch(async () => { await body(); return true; }, CancellationToken.None);
 [Test] public Task DisposeFailureReleasesOnlyOriginalBinding() => Ui(async () => {
  await using var s=new WorkbenchAutomationService(); var h=new HistoryService();
  var vm=new CryptoScriptEditViewModel(new(),new(),new(),new(h),null!,h,s);
  vm.TextEditor=new WorkbenchTextEditor {Text="base"}; vm.AttachEditor();
  vm.PropertyChanged+=(_,e)=> {if(e.PropertyName=="TextEditor") throw new Exception("cleanup");};
  Assert.That(()=>vm.Dispose(),Throws.Exception);
  using var next=new CryptoScriptEditViewModel(new(),new(),new(),new(h),null!,h,s);
  vm.Dispose(); next.TextEditor=new WorkbenchTextEditor {Text="next"}; next.AttachEditor();
  h.AddLine("target"); h.AddLine("last"); h.LineBack(); Assert.That(next.TextEditor.Text,Is.EqualTo("target"));
 });
 [Test] public Task OriginalAnchorCounterexample() => Ui(async () => {
  await using var s=new WorkbenchAutomationService(); var h=new HistoryService();
  using var vm=new CryptoScriptEditViewModel(new(),new(),new(),new(h),null!,h,s);
  vm.TextEditor=new WorkbenchTextEditor {Text="abcd"}; vm.AttachEditor();
  var a=vm.TextEditor.Document.CreateAnchor(0); a.SurviveDeletion=true;
  h.AddLine("WXYZ"); h.AddLine("last"); h.LineBack(); Assert.That(a.Offset,Is.EqualTo(4));
 });
 public static IEnumerable<TestCaseData> AnchorCases() {
  foreach(var input in new[]{"p\nabcd","p\r\nabcd","p\n"})
   foreach(var movement in new[]{AnchorMovementType.Default,AnchorMovementType.BeforeInsertion,AnchorMovementType.AfterInsertion})
    foreach(bool survive in new[]{false,true})
     yield return new TestCaseData(input,movement,survive);
 }
 [TestCaseSource(nameof(AnchorCases))]
 public Task NativeReferenceMatrix(string input, AnchorMovementType movement, bool survive) => Ui(async () => {
  await using var s=new WorkbenchAutomationService(); var h=new HistoryService();
  using var vm=new CryptoScriptEditViewModel(new(),new(),new(),new(h),null!,h,s);
  var editor=new WorkbenchTextEditor {Text=input}; vm.TextEditor=editor; vm.AttachEditor();
  var reference=new AvaloniaEdit.TextEditor {Text=input};
  editor.CaretOffset=reference.CaretOffset=input.Length;
  editor.Select(0,input.Length); reference.Select(0,input.Length);
  var positions=Enumerable.Range(0,input.Length+1).ToArray();
  TextAnchor[] Anchors(TextDocument d) => positions.Select(p=> {var a=d.CreateAnchor(p);a.MovementType=movement;a.SurviveDeletion=survive;return a;}).ToArray();
  var actual=Anchors(editor.Document);var expected=Anchors(reference.Document);
  var events=new List<string>();var baseline=new List<string>();
  void Track(TextDocument d,List<string> log) {d.UpdateStarted+=(_,_)=>log.Add("start");d.Changing+=(_,e)=>log.Add($"changing:{e.Offset}:{e.RemovalLength}:{e.InsertionLength}");d.Changed+=(_,e)=>log.Add($"changed:{e.Offset}:{e.RemovalLength}:{e.InsertionLength}");d.UpdateFinished+=(_,_)=>log.Add("finish");}
  Track(editor.Document,events); Track(reference.Document,baseline);
  string target="WX\r\nYZ";
  var line=reference.Document.Lines.Last();using(reference.Document.RunUpdate()){reference.Document.Remove(line.Offset,line.Length);reference.Document.Insert(line.Offset,target);}
  h.AddLine(target);h.AddLine("last");h.LineBack();
  for(int i=0;i<actual.Length;i++){Assert.That(actual[i].IsDeleted,Is.EqualTo(expected[i].IsDeleted));if(!actual[i].IsDeleted)Assert.That(actual[i].Offset,Is.EqualTo(expected[i].Offset));}
  Assert.That(editor.Text,Is.EqualTo(reference.Text)); Assert.That(events,Is.EqualTo(baseline));
  Assert.That(editor.CaretOffset,Is.EqualTo(reference.CaretOffset));Assert.That(editor.SelectionStart,Is.EqualTo(reference.SelectionStart));Assert.That(editor.SelectionLength,Is.EqualTo(reference.SelectionLength));
  editor.Document.UndoStack.Undo();Assert.That(editor.Text,Is.EqualTo(input));Assert.That(editor.Document.UndoStack.CanUndo,Is.False);
  editor.Document.UndoStack.Redo();Assert.That(editor.Text,Is.EqualTo(reference.Text));
 });
 [Test] public Task FailedConstructorDoesNotRetainBinding() => Ui(async () => {
  await using var s=new WorkbenchAutomationService(); var h=new HistoryService();
  Assert.That(()=>new CryptoScriptEditViewModel(new(),null!,new(),new(h),null!,h,s),Throws.Exception);
  using var next=new CryptoScriptEditViewModel(new(),new(),new(),new(h),null!,h,s);
 });
 [Test] public Task MultipleCleanupFailuresAreReportedAndBindingReleased() => Ui(async () => {
  await using var s=new WorkbenchAutomationService();var h=new HistoryService();
  var vm=new CryptoScriptEditViewModel(new(),new(),new(),new(h),null!,h,s);
  vm.TextEditor=new WorkbenchTextEditor();vm.AttachEditor();
  vm.PropertyChanged+=(_,e)=>{if(e.PropertyName=="TextEditor")throw new Exception("property");};
  var lifetime=(CancellationTokenSource)typeof(CryptoScriptEditViewModel).GetField("_lifetime",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(vm)!;
  using var registration=lifetime.Token.Register(()=>throw new Exception("cancel"));
  var error=Assert.Throws<AggregateException>(()=>vm.Dispose());Assert.That(error!.Flatten().InnerExceptions.Count,Is.EqualTo(2));
  using var next=new CryptoScriptEditViewModel(new(),new(),new(),new(h),null!,h,s); vm.Dispose();
 });
 [TestCase("remove")][TestCase("insert")][TestCase("finish")][TestCase("dispose")]
 public Task TwoStepFailureAndActiveCleanup(string phase) => Ui(async () => {
  await using var s=new WorkbenchAutomationService();var h=new HistoryService();
  using var vm=new CryptoScriptEditViewModel(new(),new(),new(),new(h),null!,h,s);
  var e=new WorkbenchTextEditor{Text="abcd"};vm.TextEditor=e;vm.AttachEditor();int changes=0;bool pending=false;
  e.Document.Changed+=(_,_)=>{changes++;if(phase=="dispose" && changes==1){vm.Dispose();pending=s.EditorCoordinator.Pending>0;}if((phase=="remove" && changes==1)||(phase=="insert" && changes==2))throw new Exception();};
  if(phase=="finish")e.Document.UpdateFinished+=(_,_)=>throw new Exception();
  h.AddLine("WXYZ");h.AddLine("last");h.LineBack();
  if(phase=="dispose"){Assert.That(pending,Is.True);Assert.That(e.Text,Is.EqualTo("WXYZ"));Assert.That(s.EditorCoordinator.Pending,Is.Zero);}
  else{Assert.That(s.EditorCoordinator.LastReceipt!.Finalization,Is.EqualTo(EditorFinalization.Faulted));Assert.That(s.EditorCoordinator.LastReceipt.HistoryEffect,Is.EqualTo(EditorHistoryEffect.Indeterminate));Assert.That(e.Text,Is.EqualTo(phase=="remove"?"":"WXYZ"));}
 }); [Test] public Task NativeCallbacksCannotBorrowTwoStepPermission() => Ui(async () => {
  await using var s=new WorkbenchAutomationService();var h=new HistoryService();
  using var vm=new CryptoScriptEditViewModel(new(),new(),new(),new(h),null!,h,s);
  var e=new WorkbenchTextEditor{Text="abcd"};e.IsModified=false;vm.TextEditor=e;vm.AttachEditor();int rejected=0;
  void Attack(){try{e.ReplaceHistoryLine(e.Document,0,0,"bad");}catch(CryptoScript.Model.ExecutionBusyException){rejected++;}}
  e.Document.UpdateStarted+=(_,_)=>Attack(); e.Document.Changed+=(_,_)=>Attack();
  e.PropertyChanged+=(_,a)=>{if(a.Property==AvaloniaEdit.TextEditor.IsModifiedProperty)Attack();};
  h.AddLine("WXYZ");h.AddLine("last");h.LineBack();
  Assert.That(e.Text,Is.EqualTo("WXYZ"));Assert.That(rejected,Is.GreaterThanOrEqualTo(4));
  Assert.That(s.EditorCoordinator.LastReceipt!.Finalization,Is.EqualTo(EditorFinalization.Complete));
 });}
