using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ProcessLens.Core;
using ProcessLens.Semantics;
using L=ProcessLens.Core.SysPulseLocalization;
int count=0;
void Check(bool ok,string label){if(!ok)throw new Exception("FAIL localization: "+label);count++;Console.WriteLine("PASS localization: "+label);}
void Set(AppLanguage language){L.SetLanguage(language,persist:false);CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(language==AppLanguage.English?"en-US":"it-IT");}
Set(AppLanguage.Italian);
string inventoryPath=Path.Combine(AppContext.BaseDirectory,"message-inventory.json");
using(var inventory=System.Text.Json.JsonDocument.Parse(File.ReadAllText(inventoryPath))) {
 foreach(var entry in inventory.RootElement.EnumerateArray()) {
  string? key=entry.GetProperty("text").GetString();if(key is not null&&!L.HasTranslation(key))throw new Exception("Missing exact analysis dictionary key: "+key);
  if(key is not null && entry.GetProperty("kind").GetString()=="xaml" && L.ByKey(L.KeyFor(key))!=key)throw new Exception("Unresolvable XAML dictionary key: "+key);
 }
 Check(true,"all source fragments, knowledge entries and XAML keys have exact dictionary entries");
}
Check(L.CatalogCount>4000,"complete dictionary embedded without external translation service");
string raw=new("Funzione sconosciuta".AsSpan());Check(L.Render(raw,AppLanguage.English)==raw,"untracked data never translated");
string label=L.Literal("Funzione sconosciuta");Check(L.Render(label,AppLanguage.English)=="Unknown function","exact analysis key");
string message=L.Capture($"Processo: {L.Opaque(raw)} · PID {42}");Check(L.Render(message,AppLanguage.Italian)=="Processo: Funzione sconosciuta · PID 42","Italian bytes unchanged");Check(L.Render(message,AppLanguage.English)=="Process: Funzione sconosciuta · PID 42","process name preserved inside translated template");
var builder=new StringBuilder();L.AppendLine(builder,L.Literal("Funzione sconosciuta"));L.AppendLine(builder,message);var report=L.BuilderText(builder);
Check(L.Render(report,AppLanguage.English).Contains("Unknown function")&&L.Render(report,AppLanguage.English).Contains(raw),"composed report preserves structured arguments");
string en=L.Translate(label,AppLanguage.English);Check(L.Translate(en,AppLanguage.Italian)==label,"explicit translation reversible");
string concat=L.Concat(message,L.Literal("\nRuolo: "),label);Check(L.Render(concat,AppLanguage.English).Contains("\nRole: Unknown function"),"nested composition preserves translated clauses");
var identities=new[]{"unknown.exe","python.exe","node.exe","svchost.exe","chrome.exe","dotnet.exe"};
var questions=new[]{"Cosa sta facendo?","Quanta RAM usa?","Ha porte aperte?","Quando finirà?","È bloccato?","Chi lo ha avviato?","Perché è aperto?","Posso chiuderlo?","A che punto è?"};
var forbidden=new Regex(@"\b(?:sconosciut[oa]|rilevat[ioa]|campionat[oa]|connessioni|disponibile|memoria|processo padre|non verificat[oa]|stimato|manca|finirà|risorse|avanzamento|lettura|evidenze|finestra principale|possibile padre|parametri|funzione)\b",RegexOptions.IgnoreCase);
foreach(var name in identities) {
 var p=new ProcessSnapshot{Pid=123,ParentPid=30,Name=name,ProductName="Prodotto di prova",CompanyName="Società di prova",ExecutablePath=@"C:\Dati\Funzione sconosciuta\"+name,CommandLine=name=="python.exe"?"python.exe -m http.server 8080":name=="chrome.exe"?"chrome.exe --type=renderer":name=="node.exe"?"node.exe app.js":name,IsDeepInspected=true,WorkingSetAvailable=true,WorkingSetBytes=104857600,PrivateWorkingSetAvailable=true,PrivateWorkingSetBytes=26214400,CpuSampleAvailable=true,CpuPercent=12.5,TcpListeningCount=1,ListeningTcpPorts=new[]{8080}};
 Set(AppLanguage.Italian);var insight=new ProcessIntelligenceAnalyzer().Analyze(p);
 foreach(var question in questions) {
  var result=new ProcessQueryEngine().Ask(p,insight,question,null);string it=result.Answer;
  Set(AppLanguage.English);string english=result.Answer;
  Check(english!=it,"full answer changes language: "+name+" / "+question);
  string appText=english.Replace("Prodotto di prova","").Replace("Società di prova","");
  Check(!forbidden.IsMatch(appText),"no Italian analysis clauses: "+name+" / "+question);
  Set(AppLanguage.Italian);Check(result.Answer==it,"Italian restored exactly: "+name+" / "+question);
  Check(p.Name==name && p.PrivateWorkingSetBytes==26214400 && p.ListeningTcpPorts.Single()==8080,"identity and counters untouched");
  if(name=="unknown.exe")Console.WriteLine("EXAMPLE "+question+"\n"+english+"\nEND EXAMPLE");
 }
}
Set(AppLanguage.English);
var englishQuestions=new[]{"What is it doing?","How much memory does it use?","Does it have open ports?","When will it finish?","Is it stuck?","Who started it?","Why is it running?","Can I close it?","What is its progress?"};
var process=new ProcessSnapshot{Pid=900,ParentPid=0,Name="unknown.exe"};var info=new ProcessIntelligenceAnalyzer().Analyze(process);var engine=new ProcessQueryEngine();
for(int q=0;q<questions.Length;q++) {
 var fromItalian=engine.Ask(process,info,questions[q],null);var fromEnglish=engine.Ask(process,info,englishQuestions[q],null);
 Check(fromItalian.Title==fromEnglish.Title,"English question routes to the same analysis: "+englishQuestions[q]);
 Check(!forbidden.IsMatch(fromEnglish.Answer),"English query answer uses English clauses");
}
var named=process with {ProductName="Funzione sconosciuta"};
var namedInsight=new ProcessIntelligenceAnalyzer().Analyze(named);
var namedResult=engine.Ask(named,namedInsight,"What is it doing?",null);
Check(namedResult.Answer.Contains("Process: Funzione sconosciuta"),"file metadata matching a dictionary key is preserved as data");
using(var json=System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new {Description=label, Name=L.Raw(raw), PID=123, Bytes=26214400},L.ReportJsonOptions()))) {
 Check(json.RootElement.GetProperty("Description").GetString()=="Unknown function","exported diagnostic JSON uses English descriptions");
 Check(json.RootElement.GetProperty("Name").GetString()==raw && json.RootElement.GetProperty("PID").GetInt32()==123 && json.RootElement.GetProperty("Bytes").GetInt32()==26214400,"JSON export preserves raw identity and numeric evidence");
}
using(var dictionaryJson=System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new Dictionary<string,string>{{"Funzione sconosciuta",label}},L.ReportJsonOptions()))) {
 Check(dictionaryJson.RootElement.GetProperty("Funzione sconosciuta").GetString()=="Unknown function","JSON dictionary property names remain protocol keys");
}
var invariantTemplate=L.Capture($"CPU nell’intervallo campionato: {12.5:F1}");
Check(L.Render(invariantTemplate,AppLanguage.English)=="CPU in the sample interval: 12.5","English numeric format retained in complete diagnostic template");
using(var reviewed=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"reviewed-terms.json")))) {
 foreach(var term in reviewed.RootElement.EnumerateObject())
  Check(L.Translate(term.Name,AppLanguage.English)==term.Value.GetString(),"R35 R1 reviewed term: "+term.Name.Replace("\n"," ").Replace("\t"," "));
}
using(var catalog=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"analysis-dictionary.json")))) {
 var unrelated=new Regex(@"Commission shall|European Union|implementing acts|delegated acts|appropriation is intended|following table|for the purposes of this Regulation",RegexOptions.IgnoreCase);
 int checkedTerms=0;
 foreach(var term in catalog.RootElement.EnumerateObject()) {
  string translated=term.Value.GetString()!;
  if(L.Translate(term.Name,AppLanguage.English)!=translated)throw new Exception("Embedded dictionary differs from tested catalog: "+term.Name);
  if(term.Name!=translated && unrelated.IsMatch(translated))throw new Exception("Unrelated generated prose in dictionary: "+term.Name);
  if(Regex.IsMatch(term.Name,@"^--?[A-Za-z][A-Za-z0-9-]*(?:[ =]|$)") && translated!=term.Name)throw new Exception("Translated command-line syntax: "+term.Name);
  checkedTerms++;
 }
 Check(checkedTerms==L.CatalogCount,"R35 R1 entire embedded catalog checked for unrelated prose and protocol corruption");
}
Check(L.Translate("Programmi (*.exe;*.com)|*.exe;*.com",AppLanguage.English)=="Programs (*.exe;*.com)|*.exe;*.com","R35 R1 native file dialog filter retains Windows wildcard grammar");
Check(L.Translate("dd/MM/yyyy HH:mm:ss",AppLanguage.English)=="dd/MM/yyyy HH:mm:ss","R35 R1 date format remains a format string");
Check(L.Translate("Segoe UI",AppLanguage.English)=="Segoe UI","R35 R1 font family remains an identifier");
Set(AppLanguage.English);
var nativeSource=new NativeCaptureSource("adapter-key",L.Literal("Dispositivi: N/D"));
Check(nativeSource.DisplayName=="Devices: N/A" && nativeSource.Name=="Dispositivi: N/D" && nativeSource.Key=="adapter-key","R35 R1 source display is translated without changing source identity");
Set(AppLanguage.Italian);Console.WriteLine($"Localization regressions complete: {count} checks.");
