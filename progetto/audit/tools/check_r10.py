"""Structural/source checks only. Does not compile or execute .NET, PowerShell or WPF."""
from pathlib import Path
import argparse,copy,json,re,zipfile,hashlib
import xml.etree.ElementTree as ET
from button_layout_rules import violations as button_layout_violations
from ux_writing_rules import approved_revision
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--input-zip',type=Path,required=True);p.add_argument('--r9-zip',type=Path,required=True)
p.add_argument('--candidate',type=Path,default=Path('.'));p.add_argument('--output',type=Path)
a=p.parse_args();b=a.candidate.resolve();results=[]
def check(name,ok,detail):
 results.append(dict(test=name,status='PASS' if ok else 'FAIL',detail=detail));print(results[-1]['status'],name)
def archive(path):
 with zipfile.ZipFile(path) as z:return {n.split('/',1)[1]:z.read(n) for n in z.namelist() if not n.endswith('/') and not set(Path(n).parts)&{'bin','obj','publish','.vs','__pycache__'}}
user=archive(a.input_zip);r9=archive(a.r9_zip)
protected=[r for r in user if r.startswith(('src/ProcessLens.Windows/','src/ProcessLens.Core/','src/ProcessLens.Semantics/',
 'src/ProcessLens.Guardian.','src/ProcessLens.MemoryAgent/','src/ProcessLens.Profiler/')) or r in ['src/ProcessLens.App/ProfilerClient.cs','src/ProcessLens.App/AppDiagnostics.cs','src/ProcessLens.App/MemoryAnalysisAudit.cs','src/ProcessLens.App/GuardianDashboardReader.cs','src/ProcessLens.App/GuardianLineageReader.cs']]
native='src/ProcessLens.Windows/WindowsProcessCollector.cs'
expected=user[native].decode('utf-8-sig').replace('private readonly Dictionary<string, FileMetadata> _fileMetadataCache = new(StringComparer.OrdinalIgnoreCase);', '// Bound retained paths across long sessions; a miss re-reads the same metadata.\n    private readonly BoundedMetadataCache<FileMetadata> _fileMetadataCache = new(512);')
check('protected components retained with exact scoped metadata-cache change',
 all((b/r).read_bytes()==user[r] or approved_revision(b,r) for r in protected if r!=native) and
 (b/native).read_text(encoding='utf-8-sig')==expected,
 f'{len(protected)-1} protected files exact or explicitly reviewed UX edits; one exact cache declaration replacement, independent churn tests supplied; CPU acquisition added separately')
prior=['src/ProcessLens.App/BoundedCache.cs','src/ProcessLens.App/MemoryObservationBuffer.cs','src/ProcessLens.App/ProcessIconResolver.cs','src/ProcessLens.App/ProcessDisplayFormatter.cs','src/ProcessLens.App/ProcessNetworkPresentation.cs']
check('R8 R9 helper implementations retained',all((b/r).read_bytes()==r9[r] or approved_revision(b,r) for r in prior),'Prior delivered memory and numeric formatting helpers retained')
xmlpaths=list(b.rglob('*.xaml'))+list(b.rglob('*.csproj'))+list((b/'src').rglob('*.manifest'))
for x in xmlpaths:ET.parse(x)
check('XML well formed',True,str(len(xmlpaths))+' XML project/markup files; not a WPF compiler result')
x='{http://schemas.microsoft.com/winfx/2006/xaml}';local=lambda e:e.tag.split('}')[-1]
all_fields=[];all_grids=[]
for filename in ['MainWindow.xaml','MemoryDashboardView.xaml','GuardianDashboardView.xaml']:
 root=ET.parse(b/'src/ProcessLens.App'/filename);elements=list(root.iter());named={e.get(x+'Name'):e for e in elements if e.get(x+'Name')}
 check('unique names '+filename,len(named)==sum(bool(e.get(x+'Name')) for e in elements),'No duplicate generated names')
 fields=[e for e in elements if local(e)=='SelectableText'];all_fields+=fields
 check('selectable output '+filename,len(fields)>0 and all(e.get('LineHeight') is None and e.get('TextTrimming') is None for e in fields),str(len(fields))+' output controls with supported TextBox attributes')
 check('one-way output bindings '+filename,all('Mode=OneWay' in e.get('Text','') for e in fields if e.get('Text','').startswith('{Binding')),'New selectable bindings cannot write into source rows')
 styles={e.get(x+'Key'):e for e in elements if local(e)=='Style'}
 check('compatible output styles '+filename,all(not e.get('Style') or styles[e.get('Style').removeprefix('{StaticResource ').removesuffix('}')].get('TargetType')=='local:SelectableText' for e in fields),'No TextBlock-targeted style applied to TextBox subtype')
 grids=[e for e in elements if local(e)=='DataGrid'];all_grids+=grids
 check('table copy enabled '+filename,all(e.get('{clr-namespace:ProcessLens.App}GridCopySupport.Enabled')=='True' for e in grids),str(len(grids))+' tables; original selection modes retained')
 if filename=='MainWindow.xaml':main=root;main_names=named
check('process service list templates unchanged',all(ET.tostring(main_names[n])==ET.tostring(next(e for e in ET.fromstring(r9['src/ProcessLens.App/MainWindow.xaml']).iter() if e.get(x+'Name')==n)) for n in ['ProcessList','ServiceList']),'Virtualized list selection and action targets untouched')
bad_buttons = button_layout_violations(main.getroot())
check('strict R9 button styles and no fixed height', not bad_buttons, str(bad_buttons) if bad_buttons else 'Every main-window button checked, including direct RAM action')
for filename in ['GuardianDashboardView.xaml','MemoryDashboardView.xaml']:
 before=ET.fromstring(user['src/ProcessLens.App/'+filename]);after=ET.parse(b/'src/ProcessLens.App'/filename)
 oldgrids=[e for e in before.iter() if local(e)=='DataGrid'];newgrids=[copy.deepcopy(e) for e in after.iter() if local(e)=='DataGrid']
 for e in newgrids:e.attrib.pop('{clr-namespace:ProcessLens.App}GridCopySupport.Enabled',None)
 check('grid binding and selection preserved '+filename,len(oldgrids)==len(newgrids) and all(ET.tostring(i)==ET.tostring(j) for i,j in zip(oldgrids,newgrids)),'Only copy attached property added to tables')
ui=(b/'src/ProcessLens.App/MainWindow.xaml.cs').read_text(encoding='utf-8-sig')
ctl=(b/'src/ProcessLens.App/SelectableText.cs').read_text();grid=(b/'src/ProcessLens.App/GridCopySupport.cs').read_text()
report_view=(b/'src/ProcessLens.App/ReportView.cs').read_text()
check('dynamic analyses and facts selectable',ui.count('new SelectableText')==2 and report_view.count('new SelectableText')==2 and 'ReportView.Body' in ui and 'new TextBlock' not in ui and 'LineHeight =' not in ui,'Facts and structured report blocks use read-only selectable text; headers use wrapped native TextBlocks')
check('read-only copy commands',all(t in ctl for t in ['IsReadOnlyProperty.OverrideMetadata','IsUndoEnabled = false','ApplicationCommands.Copy','CopyAllCommand','_copy(SelectedText)','_copy(Text)']),'Selection, copy all, no undo history; runtime tests supplied')
check('scroll forwarded for growing output','OnPreviewMouseWheel' in ctl and 'parent.RaiseEvent' in ctl,'Outer detail scroll does not get trapped by growing output')
check('full answer snapshot copied',all(t in ui for t in ['_copyAnswerBody = result.Answer;','CopyTextFormat.Report','if (_queryBusy || _copyAnswerBody.Length == 0) return;','ProcessQueryAuditPanel.Visibility == Visibility.Visible ? ProcessQueryAuditText.Text']),'Copies original whole response, not just first visible RAM summary')
check('copy snapshot reset and disabled',ui.count('_copyAnswerBody = string.Empty;')>=5 and ui.count('CopyQueryReportButton.IsEnabled = false;')>=4,'New query, profiler and cleared panels cannot copy stale previous report')
check('user direct memory navigation preserved',all(t in ui for t in ['BuildRelatedMemoryEvidence(row, systemMemory)','InspectPidMemoryFromDashboardAsync','AnalyzeRamDirectButton_Click','MemoryDashboard.InspectProcessRequested +=']),'Current user feature integrated with lazy dashboard creation')
ctor=ui[ui.index('    public MainWindow()'):ui.index('    private async Task RefreshAsync()')]
factory=ui[ui.index('if (memory && MemoryDashboard is null)'):]
check('lazy event subscription merge fixed','MemoryDashboard.InspectProcessRequested' not in ctor and factory.index('new MemoryDashboardView()')<factory.index('MemoryDashboard.InspectProcessRequested'),'New user event is attached after lazy instance construction, avoiding null reference at startup')
check('R8 R9 UI integration preserved',all(t in ui for t in ['_deepCache = new(8)','new(16, StringComparer.OrdinalIgnoreCase)','state.Samples.Add(now, bytes)','ProcessDisplayFormatter.CpuDuration(p.CpuCumulativeSeconds)','ProcessNetworkPresentation.DescribeEndpointOwners(p)']),'Caches, allocation buffer, formatted CPU time and multiline network retained')
check('grid snapshot values and no retarget',all(t in grid for t in ['column.OnCopyingCellClipboardContent(item)','DisplayIndex','Visibility.Visible','CaptureTable(grid, true)','ContextMenuOpening','ReferenceEqualityComparer.Instance']) and '.SelectedItem =' not in grid,'Uses native clipboard bindings, visible columns, opening-time strings, unchanged selection')
check('clipboard contention handled','catch (ExternalException)' in (b/'src/ProcessLens.App/ClipboardText.cs').read_text(),'Clipboard busy produces retry message, without fatal dispatcher error')
for filename in ['publish-release.ps1','run-release.ps1','start-processlens-all.ps1']:
 s=(b/filename).read_text(encoding='utf-8-sig');check('no forced GUI termination '+filename,"Get-Process -Name 'ProcessLens' -ErrorAction SilentlyContinue | Stop-Process" not in s,'Automatic force-termination lines removed; no script executed here')
publish=(b/'publish-release.ps1').read_text(encoding='utf-8-sig')
check('publish guard precedes output replacement',publish.index('$runningGui.Count')<publish.index('Remove-Item $out'),'Existing GUI must be closed by user; no automatic termination')
for filename in ['MainWindow','MemoryDashboardView','GuardianDashboardView']:
 doc=ET.parse(b/'src/ProcessLens.App'/f'{filename}.xaml');code=(b/'src/ProcessLens.App'/f'{filename}.xaml.cs').read_text(encoding='utf-8-sig')
 events={'Click','SelectionChanged','Expanded','Collapsed','MouseEnter','MouseDoubleClick','TextChanged','KeyDown','SizeChanged'}
 check('XAML event names '+filename,all(re.search(r'\b'+re.escape(v)+r'\s*\(',code) for e in doc.iter() for k,v in e.attrib.items() if k in events),'Declaration/name presence only; C# build still required')
build=(b/'build-dashboard.ps1').read_text(encoding='utf-8-sig')
check('test suites wired',all(t in build for t in ['ProcessLens.CopyTests','ProcessLens.CopyUiTests','ProcessLens.MemoryEfficiencyTests','ProcessLens.PresentationTests','check-copy-ui-r10.ps1']),'New and prior executable regressions connected to dashboard build')
test=(b/'tests/ProcessLens.CopyUiTests/Program.cs').read_text()
check('UI tests use fake clipboard and no shown monitor','new SelectableText(value => copied = value)' in test and 'new MainWindow()' in test and not re.search(r'\.(?:Show|Run|SetActive)\(',test),'Real XAML and command routing tested on Windows; NOT EXECUTED here')
check('standalone WPF test initialization precedes layout',
    test.index('text.BeginInit();') < test.index('text.Text =') < test.index('text.EndInit();') < test.index('Size wide = LayoutOutput(text, 210);'),
    'Explicit BeginInit/EndInit for a control outside the object tree; source check only')
check('WPF layout assertion retained with native template',
    all(t in test for t in ['text.Template?.FindName("PART_ContentHost", text)',
        'double.IsFinite(text.DesiredSize.Height) && text.DesiredSize.Height > 20',
        'text.Arrange(', 'text.UpdateLayout();', 'text.IsMeasureValid && text.IsArrangeValid && text.LineCount >= 3']) and
    not re.search(r'\bTemplate\s*=', test),
    'No synthetic template or weakened height assertion; actual Windows execution still required')
check('WPF wrapping and selection regressions supplied',
    all(t in test for t in ['Size narrow = LayoutOutput(text, 70);',
        'narrow.Height > wide.Height && text.LineCount > wideLines',
        'layout and wrapping preserve original text and selection', 'copy selection remains exact after layout']),
    'Runtime assertions provided, not executed here')
layout=(b/'src/ProcessLens.App/ReportLayout.cs').read_text()
check('report parser retains unheaded paragraphs', 'ReportLayout.Sections(result.Answer)' in ui and
    '.Split(new[] { "\\n\\n" }' not in ui[ui.index('    private void RenderQueryAnswer'):ui.index('    private void ShowQueryMessage')] and
    'else lines.Add(line);' in layout,
    'Explicit heading parser replaces paragraph filtering; .NET content regressions supplied')
check('overview and numbered chapters visible',all(t in ui for t in ['ProcessQueryOverviewLabel.Visibility = Visibility.Visible',
    'ProcessQueryOverviewSourceText.Text = sections[0].Heading', 'DetailEvidenceOverviewText.Text =', '{index:00}', '{++chapter:00}']) and
    (b/'src/ProcessLens.App/MainWindow.xaml').read_text(encoding='utf-8-sig').count('Text="QUADRO GENERALE"') == 2,
    'Query summary and evidence overview precede separately numbered detail chapters')
check('chapter headers wrap and bodies are lazy',all(t in report_view for t in ['TextWrapping = TextWrapping.Wrap',
    'header.MaxWidth = Math.Max(1, item.ActualWidth - 64)', 'item.Content = content()', 'item.Content = null']),
    'Size-constrained header; controls created on expansion and released on collapse, no timer')
check('evidence refresh preserves active chapter',all(t in ui for t in ['_renderedEvidenceGroupsPid',
    'string.Equals(evidence, _renderedEvidenceText, StringComparison.Ordinal)', 'expanded.Contains(groupName)', 'section.Tag = groupName']),
    'Unchanged evidence skips rebuild; changed same-PID evidence restores open group names')
check('all-process collection and copy sources retained',
    (b/native).read_text(encoding='utf-8-sig')==expected and
    '_copyAnswerBody = result.Answer;' in ui and 'ReportLayout' not in (b/'src/ProcessLens.App/CopyTextFormat.cs').read_text(),
    'Enumerator and source report copy do not depend on visual formatting')
exit_watch=(b/'src/ProcessLens.App/ProcessExitWatch.cs').read_text()
check('exit notice bound to verified process object',all(t in exit_watch for t in [
    'OpenProcess(Synchronize | QueryLimitedInformation', 'GetProcessTimes(handle.SafeWaitHandle',
    'DateTime.FromFileTimeUtc(created).Ticks != identity.StartUtcTicks',
    'ThreadPool.RegisterWaitForSingleObject(handle', 'Timeout.Infinite, executeOnlyOnce: true']),
    'One handle for exact creation-time verification and signal wait, no enumeration-based exit inference')
check('exit watcher has read-only rights and lifecycle',not re.search(r'\b(?:TerminateProcess|WriteProcessMemory|VirtualAllocEx)\s*\(',exit_watch) and
    '_registration?.Unregister(null);' in exit_watch and '_handle.Dispose();' in exit_watch and 'Interlocked.Exchange(ref _disposed' in exit_watch,
    'Read-only observation; unregister and release handle on replacement/close, at most two active watchers')
check('exit toast passive and temporary',all(t in ui for t in ['TimeSpan.FromSeconds(8)', 'ShowProcessNotice("Processo chiuso", message)',
    '_processNoticeTimer.Stop();', '_selectedExitWatch?.Dispose();', '_requestedExitWatch?.Dispose();']) and
    'ProcessNoticeCard' in main_names and main_names['ProcessNoticeCard'].get('Visibility')=='Collapsed',
    'In-window notice with executable/PID; no focus change or modal success acknowledgement')
check('termination request does not become assumed exit','else if (identity is null || !_exitNotices.WasShown(identity))' in ui and
    'ShowProcessNotice("Esito richiesta", result.Message)' in ui and '_exitNotices.Confirm(identity)' in ui,
    'Confirmed exit comes from the signal callback; existing native result message retained separately')
check('exit notice regressions and backend self-check supplied',all(t in test for t in [
    'read-only Windows exit watch binds to this test process', 'native handle rejects a different creation timestamp',
    'ProcessNoticeCard']) and 'reused PID with a different creation timestamp' in (b/'tests/ProcessLens.CopyTests/Program.cs').read_text(),
    'Independent read-only Windows self-process binding plus pure deduplication tests; NOT EXECUTED here')
tray=(b/'src/ProcessLens.App/TrayWindowController.cs').read_text()
app=(b/'src/ProcessLens.App/App.xaml.cs').read_text()
check('X preserves taskbar and explicit Exit closes',all(t in tray for t in ['WindowLifetimePolicy.Close(', 'e.Cancel = true;',
    '_window.ShowInTaskbar = true;', 'Esci da ProcessLens', 'AllowExit(); _shutdown();', '_window.WindowState = WindowState.Minimized;']),
    'X minimizes with a persistent taskbar button regardless of tray availability; explicit Exit closes')
check('restore supports hidden and minimized window',all(t in tray for t in ['Mostra ProcessLens',
    '_window.ShowInTaskbar = true;', '_window.Show();', '_window.WindowState = restoreState;', '_window.Activate();']) and
    'TrayWindowController.TryRestoreExisting()' in app and 'SendMessageTimeoutW' in tray,
    'Tray activation and acknowledged single-instance message; older-version fallback retained')
check('native tray lifecycle handles Explorer restart',all(t in tray for t in ['Shell_NotifyIconW(0, ref data)',
    'Shell_NotifyIconW(2, ref data)', 'TaskbarCreated', 'if (!EnsureTray() && !_window.IsVisible) RequestRestore();',
    'if (data.Icon == IntPtr.Zero) return false;']), 'No hiding without restore route; shared icon and tray entry cleaned on exit')
check('tray only attached to real application startup','new TrayWindowController(window' in app and
    'new TrayWindowController' not in ui and 'ShutdownMode = ShutdownMode.OnExplicitShutdown;' in app and
    'SessionEnding +=' in app, 'Unshown MainWindow test construction creates no shell icon; logout and fatal exit remain allowed')
check('hidden dashboards paused and visible dashboards resumed',
    'window.SetDashboardWindowVisibility' in app and 'WindowLifetimePolicy.PollingActive' in tray and
    'GuardianDashboard?.SetActive(active && _guardianMode);' in ui and 'MemoryDashboard?.SetActive(active && _memoryMode);' in ui,
    'Only GUI polling; Guardian service, MemoryAgent and notification policy not changed')
audit_view=(b/'src/ProcessLens.App/AuditLogView.cs').read_text()
audit_model=(b/'src/ProcessLens.App/AuditLogPresentation.cs').read_text()
check('actual audit panel wired to structured cards',all(t in ui for t in ['ProcessQueryAuditText_TextChanged',
    'AuditLogPresentation.Parse(ProcessQueryAuditText.Text)', '_auditLogView.Render(_auditDocument)', 'ProcessQueryAuditPanel_Collapsed']) and
    all(n in main_names for n in ['ProcessQueryAuditEntriesPanel','ProcessQueryAuditOverviewText','ProcessQueryAuditLocationText']),
    'The specific RAM verification panel from the screenshot now uses structured entries')
check('audit text spacing uses supported native properties',all(t in audit_view for t in [
    'FontSize = 12.5', 'new FontFamily("Segoe UI")', 'Padding = new Thickness(10)', 'Margin = new Thickness(0, 0, 0, 6)']) and
    'LineHeight' not in audit_view,
    'Larger body, distinct timestamp/stage, padded cards and gaps; no unsupported TextBox LineHeight')
check('audit originals copied rather than reconstructed','string snapshot = ProcessQueryAuditText.Text;' in ui and
    'ProcessQueryAuditPanel.Visibility == Visibility.Visible ? ProcessQueryAuditText.Text' in ui and
    (b/'src/ProcessLens.App/MemoryAnalysisAudit.cs').read_bytes()==user['src/ProcessLens.App/MemoryAnalysisAudit.cs'],
    'UI adapter only; original text/file generator and diagnostic event IDs unchanged')
check('audit append and collapse preserve lifecycle',all(t in audit_view for t in [
    '_rendered.SequenceEqual(document.Entries.Take(_rendered.Count))', 'document.Entries.Skip(_rendered.Count)',
    '_host.Children.Clear()']) and '_auditLogView?.Clear();' in ui,
    'Unchanged prefix keeps text selection; new source replaces rows; collapse releases cards')
check('audit parser retains limits and unknown content',all(t in audit_model for t in [
    'TimeSpan.TryParseExact', 'Message = previous.Message + "\\n" + line', 'new Entry("", "Registro", line, line)',
    'location = line;']) and 'audit complete copy retains original CRLF' in (b/'tests/ProcessLens.CopyTests/Program.cs').read_text(),
    'RAM/profiler entries, continuation lines, source location, cultures and original-copy regressions supplied; .NET NOT EXECUTED')
from live_resource_rules import violations as resource_violations
resource_files={p.relative_to(b).as_posix():p.read_text(encoding='utf-8-sig') for p in b.rglob('*') if p.suffix in ['.cs','.xaml','.ps1']}
resource_bad=resource_violations(resource_files)
check('R10 R7 CPU restore self-resource source invariants',not resource_bad,str(resource_bad) if resource_bad else '13 conservative source guards; executable .NET and Windows checks supplied, not executed')
from ram_monitor_rules import violations as monitor_violations
monitor_bad=monitor_violations(resource_files)
check('R10 R8 monitor and RAM service navigation source invariants',not monitor_bad,str(monitor_bad) if monitor_bad else '13 source invariants only; Windows build and runtime still required')
output=a.output or b/'audit/R10/structural-results.json';output.parent.mkdir(parents=True,exist_ok=True);output.write_text(json.dumps(results,ensure_ascii=False,indent=2)+'\n')
raise SystemExit(0 if all(r['status']=='PASS' for r in results) else 1)
