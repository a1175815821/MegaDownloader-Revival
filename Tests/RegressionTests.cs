using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using MegaDownloader;

// Runs against the built .NET Framework application, with isolated files and
// loopback HTTP fixtures. No real MEGA account or user configuration is loaded.
public class RegressionTests {
    static string root;
    static string projectRoot;
    static string bin;
    static int assertions;
    static readonly BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly BindingFlags shared = BindingFlags.Static | BindingFlags.NonPublic;
    static void Print(string name, params object[] fields) {
        Console.WriteLine(name + " | " + string.Join(" | ", fields.Select(x => Convert.ToString(x))));
    }
    static Exception Unwrap(Exception ex) { while(ex.InnerException != null) ex=ex.InnerException; return ex; }
    static void Check(bool condition, string name) {
        if(!condition) throw new Exception("FAIL: " + name);
        Interlocked.Increment(ref assertions);
    }
    public static void Main(string[] args) {
        if(args.Length==4 && args[0]=="--buffer-mutex-child") {
            bin=args[1];
            AppDomain.CurrentDomain.AssemblyResolve += (s,e) => {
                string p=Path.Combine(bin,new AssemblyName(e.Name).Name+".dll");
                if(!File.Exists(p)) p=Path.Combine(bin,new AssemblyName(e.Name).Name+".exe");
                return File.Exists(p) ? Assembly.LoadFrom(p) : null;
            };
            BufferMutexChild(args[2],args[3]);
            return;
        }
        projectRoot=args[0]; bin=args[1];
        AppDomain.CurrentDomain.AssemblyResolve += (s,e) => {
            string n=new AssemblyName(e.Name).Name;
            string p=Path.Combine(bin,n+".dll");
            if(!File.Exists(p)) p=Path.Combine(bin,n+".exe");
            return File.Exists(p) ? Assembly.LoadFrom(p) : null;
        };
        root=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"run-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.SetCurrentDirectory(root);
        try { Run(); Console.WriteLine("PASS: " + assertions + " assertions. Temporary files: " + root); }
        catch(Exception ex) { Console.WriteLine(ex); Environment.ExitCode=1; }
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static void Run() {
        typeof(Log).GetField("_LogLevel",shared).SetValue(null,Enum.ToObject(typeof(Log.LevelLogType),-1));
        foreach(string name in new[]{"cs-nodot","cs-with.dot"}) {
            string d=Path.Combine(root,name); Directory.CreateDirectory(d);
            string real=Path.Combine(d,"README"); string part=real+".part";
            File.WriteAllText(real,"old"); File.WriteAllText(part,"new");
            using(var fd=new FileDownloader(false)) {
                fd.LocalDirectory=d;
                string renamed=(string)typeof(FileDownloader).GetMethod("RenamePartToReal",hidden).Invoke(fd,new object[]{part,real});
                Check(renamed=="README (2)","extensionless collision in "+name);
                Check(File.ReadAllText(real)=="old" && File.ReadAllText(Path.Combine(d,renamed))=="new", "both extensionless files preserved");
            }
        }
        foreach(string kind in new[]{"same-size","different-size"}) {
            string d=Path.Combine(root,"cs-"+kind); Directory.CreateDirectory(d);
            File.WriteAllText(Path.Combine(d,"data.txt"),"ORIGINAL");
            string zip=Path.Combine(root,"cs-"+kind+".zip");
            MakeZip(zip,"data.txt",kind=="same-size"?"REPLACED":"NEW");
            string error=DescompresorController.ExtractArchiveSync(zip,d,"");
            Print("extract_collision",kind,"error="+error,"content="+File.ReadAllText(Path.Combine(d,"data.txt")),"preservedCopy="+File.Exists(Path.Combine(d,"data (2).txt")));
            Check(error=="", "ZIP extraction succeeded: "+kind);
            Check(File.ReadAllText(Path.Combine(d,"data (2).txt"))=="ORIGINAL", "ZIP preserves original bytes: "+kind);
            Check(File.ReadAllText(Path.Combine(d,"data.txt"))==(kind=="same-size"?"REPLACED":"NEW"),"ZIP extracts incoming bytes");
        }
        string empty=Path.Combine(root,"cs-emptydir"); Directory.CreateDirectory(empty);
        string emptyZip=Path.Combine(root,"cs-emptydir.zip"); MakeZip(emptyZip,"empty/","");
        Check(DescompresorController.ExtractArchiveSync(emptyZip,empty,"")=="" && Directory.Exists(Path.Combine(empty,"empty")),"ZIP creates empty directory");
        Directory.SetCurrentDirectory(projectRoot);
        string volume=Path.GetPathRoot(projectRoot);
        Check(PathGuard.GetSafeFilePathUnderRoot(volume,"example.txt")==Path.Combine(volume,"example.txt"),"volume root remains absolute");
        Check(PathGuard.GetSafePathUnderRoot(volume,"",true)==volume,"empty relative path preserves root separator");
        Check(PathGuard.GetSafeFilePathUnderRoot(@"\\audit-server\share\","file.txt")==@"\\audit-server\share\file.txt","UNC root preserved without network access");
        ExpectRejected(()=>PathGuard.GetSafePathUnderRoot(projectRoot,@"..\escaped",false),"relative traversal rejected");
        ExpectRejected(()=>PathGuard.EnsurePathUnderRoot(projectRoot,projectRoot+"-sibling",false),"sibling directory rejected");
        ExpectRejected(()=>PathGuard.EnsurePathUnderRoot(volume,volume,false),"root itself rejected when forbidden");
        Directory.SetCurrentDirectory(root);
        using(var interrupted=new Fichero()) {
            interrupted.FileID="TESTFILE";
            interrupted.EstadoDescarga=Estado.Descomprimiendo;
            var pack=new Paquete(); pack.AgregarFichero(interrupted);
            var packs=new List<Paquete>(); packs.Add(pack);
            typeof(Paquete).GetMethod("MarcarFicherosComoParados",shared).Invoke(null,new object[]{packs});
            Check(interrupted.EstadoDescarga==Estado.EnCola,"interrupted extraction can be scheduled again");
        }
        var plain=Enumerable.Range(0,4096).Select(i=>(byte)(i%251+1)).ToArray();
        string key=MakeKey(plain);
        string check=Path.Combine(root,"mac-control.bin"); File.WriteAllBytes(check,plain);
        bool mac=(bool)typeof(Criptografia).GetMethod("VerifyMegaMetaMac",shared).Invoke(null,new object[]{check,key});
        Check(mac,"independently generated MetaMAC accepted by application");
        byte[] encrypted=Encrypt(plain,key);
        using(var server=new LocalFileServer(encrypted)) {
            DownloadCase(server,plain,key,"fresh",0,true);
            DownloadCase(server,plain,key,"resume-existing",2048,true);
            DownloadCase(server,plain,key,"resume-missing",2048,false);
            DownloadCase(server,plain,key,"resume-final",4096,false);
            DownloadCase(server,plain,key,"resume-modern",2048,true);
            DownloadCase(server,plain,key,"missing-modern-with-foreign-legacy",2048,false);
            DownloadCase(server,plain,key,"resume-corrupt-final",4096,false);
            StreamingCases(server,plain,key);
        }
        SameNameDownloads(plain,key,encrypted);
        SameNameDownloads(plain,key,encrypted,true);
        ConcurrentFinalNames();
        PartialPersistenceAndCleanup(plain,key);
        using(var file=new Fichero()) {
            foreach(var state in new[]{Estado.Descomprimiendo,Estado.ComprobandoMD5}) {
                file.EstadoDescarga=state;
                typeof(Fichero).GetMethod("downloader_FileDownloadSucceeded",hidden).Invoke(file,new object[]{null,EventArgs.Empty});
                Check(file.EstadoDescarga==state,"delayed success preserves post-processing state "+state);
            }
        }
        ProcessResourceMonitoring();
        DownloadScheduling();
        BatchPreflight();
        PreflightSelectionCases();
        PreflightTreeCases();
        QueueViewSnapshotCases();
        PreflightWarmupCases();
        PackageImportCases();
        ScheduledQueueIntegration();
        InstanceBufferMutex();
        LanguageBuiltinWrites();
        FeatureDialogs();
        ArchiveEdges();
        PreserveCompletedName(plain,key,encrypted);
        // Cross multiple streaming buffers and end in a partial AES block.
        byte[] large=Enumerable.Range(0,65539).Select(i=>(byte)(i%251+1)).ToArray();
        string largeKey=MakeKey(large);
        using(var server=new LocalFileServer(Encrypt(large,largeKey))) StreamingCases(server,large,largeKey);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static void BufferMutexChild(string marker, string name) {
        File.WriteAllText(marker,"waiting");
        using(var gate=(System.Threading.Mutex)typeof(ApplicationInstanceManager).GetMethod("AcquireBufferMutex",shared).Invoke(null,new object[]{name})) {
            File.WriteAllText(marker,"acquired");
            gate.ReleaseMutex();
        }
    }
    static void InstanceBufferMutex() {
        string name=@"Local\MegaDownloader-Test-"+Guid.NewGuid().ToString("N");
        string marker=Path.Combine(root,"mutex-child.txt");
        var method=typeof(ApplicationInstanceManager).GetMethod("AcquireBufferMutex",shared);
        using(var gate=(System.Threading.Mutex)method.Invoke(null,new object[]{name})) {
            var info=new System.Diagnostics.ProcessStartInfo(Assembly.GetExecutingAssembly().Location,
                "--buffer-mutex-child \""+bin+"\" \""+marker+"\" \""+name+"\"") {UseShellExecute=false,CreateNoWindow=true};
            using(var child=System.Diagnostics.Process.Start(info)) {
                try {
                    var until=DateTime.UtcNow.AddSeconds(10);
                    while(!File.Exists(marker) && !child.HasExited && DateTime.UtcNow<until) Thread.Sleep(20);
                    Check(File.Exists(marker),"second process reached the buffer mutex");
                    Thread.Sleep(200);
                    Check(File.ReadAllText(marker)=="waiting","buffer mutex excludes a second process");
                    gate.ReleaseMutex();
                    Check(child.WaitForExit(10000) && child.ExitCode==0 && File.ReadAllText(marker)=="acquired","second process acquires the mutex after release");
                } finally { if(!child.HasExited) child.Kill(); }
            }
        }
    }
    static void LanguageBuiltinWrites() {
        var xml=new System.Xml.XmlDocument(); xml.LoadXml("<Language id='fixture'><Text key='test'>first</Text></Language>");
        var save=typeof(Language).GetMethod("SaveBuiltinIfChanged",shared);
        string path=Path.Combine(root,"builtin-fixture.xml");
        save.Invoke(null,new object[]{xml,path});
        DateTime sentinel=new DateTime(2020,1,2,3,4,5,DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path,sentinel);
        save.Invoke(null,new object[]{xml,path});
        Check(File.GetLastWriteTimeUtc(path)==sentinel,"unchanged builtin language file is not rewritten");
        xml.DocumentElement.FirstChild.InnerText="updated";
        save.Invoke(null,new object[]{xml,path});
        var read=new System.Xml.XmlDocument(); read.Load(path);
        Check(read.DocumentElement.FirstChild.InnerText=="updated","changed builtin language file is updated");
    }
    static void DownloadScheduling() {
        var day=new DateTime(2026,10,28);
        var s=new DownloadScheduleSettings();
        Check(!s.Enabled && s.AllowsDownload(day),"legacy configurations do not enable scheduling");
        s.Enabled=true; s.StartMinute=60; s.EndMinute=120;
        Check(!s.AllowsDownload(day.AddMinutes(59)),"daily window excludes time before start");
        Check(s.AllowsDownload(day.AddHours(1)),"daily window includes exact start");
        Check(s.AllowsDownload(day.AddHours(2).AddTicks(-1)),"daily window includes final tick");
        Check(!s.AllowsDownload(day.AddHours(2)),"daily window excludes exact end");
        s.StartMinute=23*60; s.EndMinute=7*60;
        Check(!s.AllowsDownload(day.AddHours(12)),"overnight window excludes daytime");
        Check(s.AllowsDownload(day.AddHours(23)),"overnight window includes evening start");
        Check(s.ActiveWindow(day.AddHours(23))==s.ActiveWindow(day.AddDays(1).AddHours(6)),"overnight window identity survives midnight");
        Check(!s.AllowsDownload(day.AddDays(1).AddHours(7)),"overnight window stops in the morning");
        var c=new DownloadScheduleController();
        Check(c.Evaluate(s,day.AddHours(22),true)==DownloadScheduleController.ScheduleAction.Pause,"enabling a schedule outside its window pauses a running queue");
        Check(c.Evaluate(s,day.AddHours(23),false)==DownloadScheduleController.ScheduleAction.Start,"window boundary starts the queue");
        Check(c.Evaluate(s,day.AddHours(23).AddSeconds(1),false)==DownloadScheduleController.ScheduleAction.None,"scheduler does not repeatedly start every tick");
        c.MarkManualPause(s,day.AddHours(23));
        Check(c.Evaluate(s,day.AddDays(1).AddHours(1),false)==DownloadScheduleController.ScheduleAction.None,"midnight does not undo manual pause");
        Check(c.Evaluate(s,day.AddDays(1).AddHours(7),false)==DownloadScheduleController.ScheduleAction.None,"paused queue stays paused at window end");
        Check(c.Evaluate(s,day.AddDays(1).AddHours(23),false)==DownloadScheduleController.ScheduleAction.Start,"next daily window can restart a manually paused queue");
        c.Reset();c.MarkManualPause(s,day.AddHours(23));
        Check(c.Evaluate(s,day.AddHours(23),false)==DownloadScheduleController.ScheduleAction.None,"manual pause before first timer tick is respected");
        s.Daily=false;s.OnceStart=day.AddDays(1).AddHours(12);s.OnceEnd=s.OnceStart.AddHours(2);
        Check(!s.AllowsDownload(s.OnceStart.AddTicks(-1)) && s.AllowsDownload(s.OnceStart),"one-time window has exact start semantics");
        Check(!s.AllowsDownload(s.OnceEnd) && !s.AllowsDownload(s.OnceStart.AddDays(1)),"one-time window never repeats");
        var xml=new System.Xml.XmlDocument();xml.AppendChild(s.ToXml(xml));
        var restored=DownloadScheduleSettings.FromXml(xml.DocumentElement);
        Check(restored.Enabled && !restored.Daily && restored.OnceStart==s.OnceStart && restored.OnceEnd==s.OnceEnd,"one-time schedule round-trips through XML");
        Check(restored.StartMinute==s.StartMinute && restored.EndMinute==s.EndMinute,"one-time mode retains the saved daily window after reload");
        s.Daily=true;s.StartMinute=1380;s.EndMinute=420;
        xml=new System.Xml.XmlDocument();xml.AppendChild(s.ToXml(xml));restored=DownloadScheduleSettings.FromXml(xml.DocumentElement);
        Check(restored.Enabled && restored.Daily && restored.StartMinute==1380 && restored.EndMinute==420,"overnight schedule round-trips through XML");
        Check(restored.OnceStart==s.OnceStart && restored.OnceEnd==s.OnceEnd,"daily mode retains the saved one-time window after reload");
        xml.DocumentElement.SetAttribute("OnceStart","invalid");
        Check(DownloadScheduleSettings.FromXml(xml.DocumentElement).Enabled,"inactive malformed one-time fields do not disable a valid daily window");
        xml.DocumentElement.SetAttribute("StartMinute","1440");
        Check(!DownloadScheduleSettings.FromXml(xml.DocumentElement).Enabled,"invalid persisted minutes disable schedule safely");
        xml.DocumentElement.SetAttribute("StartMinute","420");
        Check(!DownloadScheduleSettings.FromXml(xml.DocumentElement).Enabled,"equal daily start and end cannot silently create an always-on schedule");
        Check(!DownloadScheduleSettings.FromXml(null).Enabled,"missing schedule node remains backwards compatible");
        s.Daily=false;s.OnceEnd=s.OnceStart;
        Check(!s.IsValid(),"zero-length one-time schedule is invalid");
        s.OnceEnd=s.OnceStart.AddMinutes(-1);
        Check(!s.IsValid(),"backwards one-time interval is invalid");
        s.Enabled=false;
        Check(c.Evaluate(s,day,false)==DownloadScheduleController.ScheduleAction.None,"disabling scheduling never starts a stopped queue");
    }
    static void BatchPreflight() {
        string k=new string('A',43);
        Func<string,URLProcessor.FileURL> url=id=>new URLProcessor.FileURL("https://mega.nz/file/"+id+"#"+k,"");
        var a=url("AAAA0001");
        var duplicate=new URLProcessor.FileURL("https://mega.co.nz/#!AAAA0001!"+k,"");
        var queued=url("AAAA0002");var invalid=url("AAAA0003");var offline=url("AAAA0004");var zero=url("AAAA0005");
        var folder=url("AAAA0006");folder.DisplayName="already-decrypted.mkv";folder.SizeBytes=4096;
        var cases=new[]{a,duplicate,queued,invalid,offline,zero,folder};
        var existing=new HashSet<string>(StringComparer.Ordinal){BatchLinkPreflight.Identity(queued)};
        int calls=0;
        Func<URLProcessor.FileURL,CancellationToken,Conexion.InformacionFichero> reader=(source,token)=>{
            Interlocked.Increment(ref calls);
            if(source==invalid) return new Conexion.InformacionFichero {Err=Conexion.TipoError.Otros,Errtxt="ENOENT [-9]"};
            if(source==offline) return new Conexion.InformacionFichero {Err=Conexion.TipoError.ErrorConexion,Errtxt="Network unavailable"};
            return new Conexion.InformacionFichero {Nombre=source==zero?"empty.txt":"movie.mp4",Tamano=source==zero?0:1024};
        };
        var rows=BatchLinkPreflight.InspectFiles(cases,existing,reader,CancellationToken.None);
        Check(rows.Count==7,"preflight retains an entry for every expanded link");
        Check(calls==4,"preflight skips network calls for folder metadata, queue duplicates and batch duplicates");
        Check(rows[0].CanAdd && rows[0].Selected && rows[0].Name=="movie.mp4" && rows[0].SizeBytes==1024,"valid metadata is previewed and selected");
        Check(rows[1].State==BatchLinkPreflight.LinkState.Duplicate && !rows[1].CanAdd,"old and new link formats deduplicate by identity");
        Check(rows[1].Name==rows[0].Name && rows[1].SizeBytes==rows[0].SizeBytes,"duplicate preview rows reuse the inspected name and size");
        Check(rows[2].State==BatchLinkPreflight.LinkState.AlreadyQueued && !rows[2].Selected,"queued links are not silently imported again");
        Check(rows[3].State==BatchLinkPreflight.LinkState.Invalid && !rows[3].CanAdd,"permanent MEGA errors are distinguished from network errors");
        Check(rows[4].State==BatchLinkPreflight.LinkState.Unavailable && rows[4].CanAdd && !rows[4].Selected,"offline links remain manually addable without being marked dead or selected by default");
        Check(rows[5].SizeBytes==0 && rows[5].State==BatchLinkPreflight.LinkState.Ready,"empty files remain valid preview entries");
        Check(rows[6].Name=="already-decrypted.mkv" && rows[6].SizeBytes==4096,"folder metadata does not need a second API request");
        Check(rows[0].Category=="Video" && rows[5].Category=="Documents","type filters classify the previewed filename");
        Check(BatchLinkPreflight.Identity("caseID01",k)!=BatchLinkPreflight.Identity("CASEID01",k),"file identity comparison remains case-sensitive");
        Check(BatchLinkPreflight.Identity("caseID01",k)==BatchLinkPreflight.Identity("caseID01",k+"="),"base64 padding does not create duplicate identities");
        Check(BatchLinkPreflight.Identity("caseID01",k)!=BatchLinkPreflight.Identity("caseID01",new string('B',43)),"different decryption keys are not silently conflated");
        using(var cancel=new CancellationTokenSource()) {
            cancel.Cancel();bool rejected=false;
            try { BatchLinkPreflight.InspectFiles(new[]{url("CANCEL01")},existing,reader,cancel.Token); } catch(OperationCanceledException) {rejected=true;}
            Check(rejected,"cancelled inspection stops without adding tasks");
        }
        var failure=BatchLinkPreflight.InspectFiles(new[]{url("ERROR001"),url("AFTER001")},existing,(source,token)=>{
            if(source.URL.Contains("ERROR001"))throw new IOException("temporary fixture error");
            return new Conexion.InformacionFichero {Nombre="after.zip",Tamano=123};
        },CancellationToken.None);
        Check(failure[0].State==BatchLinkPreflight.LinkState.Unavailable && failure[1].State==BatchLinkPreflight.LinkState.Ready,"a transient failure does not discard the rest of the batch");
        Check(BatchLinkPreflight.AvailableSpace(root).GetValueOrDefault()>0,"free-space lookup works on the isolated local test volume");
    }
    static void PreflightSelectionCases() {
        var source=new URLProcessor.FileURL("https://mega.nz/file/SELECT01#"+new string('A',43),"");
        var video=new BatchLinkPreflight.Entry {Source=source,Name="movie.mkv",SizeBytes=4096,State=BatchLinkPreflight.LinkState.Ready,Selected=true};
        var archive=new BatchLinkPreflight.Entry {Source=source,Name="archive.zip",SizeBytes=1024,State=BatchLinkPreflight.LinkState.Ready,Selected=true};
        var offline=new BatchLinkPreflight.Entry {Source=source,Name="unknown.mp4",State=BatchLinkPreflight.LinkState.Unavailable};
        var invalid=new BatchLinkPreflight.Entry {Source=source,Name="gone.mkv",SizeBytes=99,State=BatchLinkPreflight.LinkState.Invalid};
        var duplicate=new BatchLinkPreflight.Entry {Source=source,Name="duplicate.zip",State=BatchLinkPreflight.LinkState.Duplicate};
        var model=new PreflightSelection(new List<BatchLinkPreflight.Entry>{video,archive,offline,invalid,duplicate});
        Check(model.SelectedCount==2 && model.SelectedBytes==5120 && model.UnknownSizes==0,"selection model initializes totals once");
        model.Filter("","Video");model.Apply(PreflightSelection.SelectionOperation.Invert);
        Check(!video.Selected && archive.Selected && offline.Selected && !invalid.Selected,"filtered inversion keeps hidden choices and excludes invalid links");
        Check(model.SelectedCount==2 && model.SelectedBytes==1024 && model.UnknownSizes==1,"inversion updates known bytes and unknown count together");
        model.Apply(PreflightSelection.SelectionOperation.SelectAll,false);
        Check(model.SelectedCount==3 && !invalid.Selected && !duplicate.Selected,"global select-all includes filtered-out eligible files only");
        model.Apply(PreflightSelection.SelectionOperation.Clear,false);
        Check(model.SelectedCount==0 && model.SelectedBytes==0 && model.UnknownSizes==0,"global clear resets every selection total");
        model.SetSelected(offline,true);model.SetSelected(offline,true);model.SetSelected(invalid,true);
        Check(model.SelectedCount==1 && model.UnknownSizes==1,"repeated and invalid checkbox writes cannot corrupt totals");
        video.Name="changed.zip";model.Filter("changed","Archives");
        Check(model.Visible.Count==1,"type classification refreshes if a filename changes");
        var big=new List<BatchLinkPreflight.Entry>();
        for(int i=0;i<100000;i++)big.Add(new BatchLinkPreflight.Entry {Source=source,Name="large-"+i+".zip",SizeBytes=long.MaxValue,State=BatchLinkPreflight.LinkState.Ready});
        var timer=System.Diagnostics.Stopwatch.StartNew();var stress=new PreflightSelection(big);
        stress.Apply(PreflightSelection.SelectionOperation.SelectAll,false);
        Check(stress.SelectedCount==100000 && stress.SelectedBytes==(decimal)long.MaxValue*100000,"large batches use non-overflowing aggregate byte counts");
        double allMs=timer.Elapsed.TotalMilliseconds;timer.Restart();
        for(int i=0;i<10000;i++)stress.SetSelected(big[i],false);
        Check(stress.SelectedCount==90000 && stress.SelectedBytes==(decimal)long.MaxValue*90000,"ten thousand checkbox changes retain incremental totals");
        Print("preflight_selection","entries=100000","initializeAndSelectAllMs="+allMs.ToString("F2"),"toggle10000Ms="+timer.Elapsed.TotalMilliseconds.ToString("F2"));
    }
    static void PreflightTreeCases() {
        string key=new string('A',43);
        var video=new BatchLinkPreflight.Entry {Source=new URLProcessor.FileURL("https://mega.nz/file/TREE0001#"+key,"video") {FolderName="Course"},Name="lesson.mp4",SizeBytes=10,State=BatchLinkPreflight.LinkState.Ready,Selected=true};
        var audio=new BatchLinkPreflight.Entry {Source=new URLProcessor.FileURL("https://mega.nz/file/TREE0002#"+key,"audio") {FolderName="Course"},Name="lesson.mp3",SizeBytes=20,State=BatchLinkPreflight.LinkState.Ready,Selected=true};
        var duplicate=new BatchLinkPreflight.Entry {Source=video.Source,Name=video.Name,SizeBytes=10,State=BatchLinkPreflight.LinkState.Duplicate};
        var selection=new PreflightSelection(new List<BatchLinkPreflight.Entry>{video,audio,duplicate});
        var tree=new PreflightTree(selection);
        Check(tree.Rows.Count==1 && tree.Rows[0].IsFolder,"shared folders start collapsed instead of flattening their children");
        var folder=tree.Rows[0];
        Check(folder.EligibleCount==2 && folder.SelectedCount==2 && folder.CheckState==System.Windows.Forms.CheckState.Checked,"folder check state excludes ineligible duplicate files");
        tree.ExpandAll(true);
        var leaf=tree.Rows.First(n=>object.ReferenceEquals(n.Entry,video));
        tree.Apply(new[]{folder,leaf},PreflightSelection.SelectionOperation.Invert);
        Check(selection.SelectedCount==0 && !duplicate.Selected,"overlapping parent and child targets never invert a file twice");
        selection.Apply(PreflightSelection.SelectionOperation.SelectAll,false);
        tree.Toggle(folder);
        Check(selection.SelectedCount==2,"collapsing a folder preserves all selected descendants");
        selection.Filter("video","");tree.Filter(true);
        tree.Apply(new[]{folder},PreflightSelection.SelectionOperation.Clear);
        Check(!video.Selected && audio.Selected && selection.SelectedCount==1,"filtered folder operations preserve hidden selections");
        selection.Filter("","");tree.Filter(false);
        Check(folder.CheckState==System.Windows.Forms.CheckState.Indeterminate && folder.SelectedCount==1,"clearing a filter restores the full folder's mixed check state");
    }
    static void QueueViewSnapshotCases() {
        var form=(Main)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Main));
        var publish=typeof(Main).GetMethod("PublishQueueOverview",hidden);
        var current=typeof(Main).GetField("queueOverviewSnapshot",hidden);
        Func<string,long> revision=name=>Convert.ToInt64(current.GetValue(form).GetType().GetField(name,hidden).GetValue(current.GetValue(form)));
        try {
            using(var a=new Fichero())using(var b=new Fichero()) {
                a.NombreFichero="unresolved";b.NombreFichero="audio.mp3";
                a.EstadoDescarga=Estado.Descargando;b.EstadoDescarga=Estado.EnCola;
                var package=new Paquete {Nombre="Course"};package.AgregarFichero(a);package.AgregarFichero(b);
                var items=new List<KeyValuePair<Paquete,List<Fichero>>>{new KeyValuePair<Paquete,List<Fichero>>(package,package.SnapshotFicheros())};
                publish.Invoke(form,new object[]{items});
                long scope=revision("ScopeVersion"),search=revision("SearchVersion");
                publish.Invoke(form,new object[]{items});
                Check(revision("ScopeVersion")==scope && revision("SearchVersion")==search,"unchanged queue snapshots do not trigger list rebuilds or searches");
                a.Porcentaje=50;publish.Invoke(form,new object[]{items});
                Check(revision("ScopeVersion")==scope && revision("SearchVersion")==search,"progress-only updates do not invalidate filter membership");
                a.EstadoDescarga=Estado.EnCola;b.EstadoDescarga=Estado.Descargando;
                publish.Invoke(form,new object[]{items});
                Check(revision("ScopeVersion")>scope && revision("SearchVersion")==search,"swapping file states updates membership even when group totals are unchanged");
                scope=revision("ScopeVersion");a.NombreFichero="lesson.mp4";
                publish.Invoke(form,new object[]{items});
                Check(revision("SearchVersion")>search && revision("ScopeVersion")==scope,"newly resolved filenames invalidate search without rebuilding unrelated groups");
                search=revision("SearchVersion");a.RutaRelativa="renamed/path";
                publish.Invoke(form,new object[]{items});
                Check(revision("SearchVersion")>search,"relative path changes invalidate active searches");
            }
        } finally {GC.SuppressFinalize(form);}
    }
    static void PreflightWarmupCases() {
        string key=new string('A',43);string a="https://mega.nz/file/CACHE001#"+key;string b="https://mega.nz/file/CACHE002#"+key;
        int calls=0;int caller=Thread.CurrentThread.ManagedThreadId;bool offUi=false;
        Func<string,CancellationToken,List<BatchLinkPreflight.Entry>> loader=(url,token)=>{
            Interlocked.Increment(ref calls);offUi=Thread.CurrentThread.ManagedThreadId!=caller;
            token.ThrowIfCancellationRequested();
            return new List<BatchLinkPreflight.Entry>{new BatchLinkPreflight.Entry {Source=new URLProcessor.FileURL(url,""),Name=Fichero.ExtraerFileID(url)+".zip",SizeBytes=1024,State=BatchLinkPreflight.LinkState.Ready,Selected=true}};
        };
        using(var session=new PreflightSession(loader)) {
            var first=session.Prepare(a);var firstResult=first.Results.GetAwaiter().GetResult();
            Check(offUi && calls==1,"speculative metadata work runs off the caller thread");
            Check(object.ReferenceEquals(first,session.Prepare(a)),"opening preview reuses the same in-flight or completed request");
            var second=session.Prepare(a+"\n"+b);var secondResult=second.Results.GetAwaiter().GetResult();
            Check(calls==2 && secondResult.Count==2,"appending one link reuses already inspected sources");
            string old="https://mega.co.nz/#!CACHE001!"+key;
            var aliases=session.Prepare(a+"\n"+old);var aliasResult=aliases.Results.GetAwaiter().GetResult();
            Check(calls==2 && aliasResult.Count==2,"old and new URL aliases share one metadata cache entry");
            var empty=new Dictionary<string,URLProcessor.FileURL>();var reconciled=PreflightSession.Reconcile(aliasResult,empty);
            Check(reconciled[0].State==BatchLinkPreflight.LinkState.Ready && reconciled[1].State==BatchLinkPreflight.LinkState.Duplicate,"cached sources are deduplicated again when composing the batch");
            reconciled[0].Selected=false;
            Check(aliasResult[0].Selected,"preview checkbox edits do not mutate cached results");
            empty.Add(BatchLinkPreflight.Identity(aliasResult[0].Source),new URLProcessor.FileURL("","") {DisplayName="already-downloaded.zip",SizeBytes=2048});
            var queued=PreflightSession.Reconcile(aliasResult,empty);
            Check(queued.All(entry=>!entry.CanAdd) && queued[0].Name=="already-downloaded.zip","cached results are reconciled with the latest queue snapshot");
            session.Prepare(a,true).Results.GetAwaiter().GetResult();
            Check(calls==3,"explicit recheck bypasses the metadata cache");
            session.Invalidate();
            session.Prepare(a).Results.GetAwaiter().GetResult();
            Check(calls==3,"completed source metadata survives a cancelled input generation");
            session.Invalidate(true);
            session.Prepare(a).Results.GetAwaiter().GetResult();
            Check(calls==4,"clearing the link inbox discards obsolete cached metadata");
        }
        using(var entered=new ManualResetEventSlim()) {
            using(var session=new PreflightSession((url,token)=>{
                if(url.Contains("SLOW0001")){entered.Set();token.WaitHandle.WaitOne(5000);token.ThrowIfCancellationRequested();}
                return loader(url,token);
            })) {
                var stale=session.Prepare("https://mega.nz/file/SLOW0001#"+key);
                Check(entered.Wait(5000),"slow fixture entered background metadata lookup");
                var timer=System.Diagnostics.Stopwatch.StartNew();var latest=session.Prepare(b);
                Check(timer.ElapsedMilliseconds<1000,"changing input does not wait for a slow obsolete lookup");
                Check(latest.Results.GetAwaiter().GetResult()[0].Name=="CACHE002.zip","new input cannot display stale metadata");
                bool cancelled=false;try{stale.Results.GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}
                Check(cancelled,"obsolete speculative work is cancelled");
            }
        }
        int retries=0;
        using(var session=new PreflightSession((url,token)=>{retries++;var entries=loader(url,token);if(retries==1)entries[0].State=BatchLinkPreflight.LinkState.Unavailable;return entries;})) {
            session.Prepare(a).Results.GetAwaiter().GetResult();
            session.Prepare(a+"\n"+b).Results.GetAwaiter().GetResult();
            Check(retries==3,"transient metadata failures are retried on the next input generation");
        }
    }
    static void PackageImportCases() {
        string destination=Path.Combine(root,"package-builder");
        var files=new List<URLProcessor.FileURL>();string key=new string('A',43);
        for(int i=0;i<20;i++)files.Add(new URLProcessor.FileURL("https://mega.nz/file/"+i.ToString("D8")+"#"+key,"nested") {DisplayName="file-"+i+".zip",SizeBytes=(long)1024*1024*1024*10,FolderName="Fixture folder"});
        var package=DownloadPackageBuilder.Build(files,"",destination,true,true,"fixture-password",CancellationToken.None);
        try {
            Check(package.Nombre=="Fixture folder" && package.ListaFicheros.Count==20,"background package builder preserves folder naming and all selected files");
            Check(Directory.GetDirectories(destination,"*",SearchOption.AllDirectories).Length==2 && Directory.GetFiles(destination,"*",SearchOption.AllDirectories).Length==0,"large-file import creates directories but does not read or download file content");
            Check(package.ListaFicheros.All(file=>file.TamanoBytes==(long)1024*1024*1024*10 && !file.DescargaProcesada),"preview metadata is retained without bypassing pre-download verification");
        } finally {foreach(var file in package.ListaFicheros)file.Dispose();}
        using(var cancel=new CancellationTokenSource()) {
            cancel.Cancel();bool cancelled=false;string path=Path.Combine(root,"cancelled-package-builder");
            try{DownloadPackageBuilder.Build(files,"",path,true,false,"",cancel.Token);}catch(OperationCanceledException){cancelled=true;}
            Check(cancelled && !Directory.Exists(path),"already cancelled import performs no directory creation");
        }
        var traversal=new List<URLProcessor.FileURL>{new URLProcessor.FileURL(files[0].URL,"../outside")};
        ExpectRejected(()=>DownloadPackageBuilder.Build(traversal,"",destination,false,false,"",CancellationToken.None),"background import still rejects remote path traversal");
    }
    static void ScheduledQueueIntegration() {
        var form=(Main)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Main));
        var config=(Configuracion)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Configuracion));
        config.DownloadSchedule=new DownloadScheduleSettings {Enabled=true,Daily=false,OnceStart=DateTime.Now.AddMinutes(-1),OnceEnd=DateTime.Now.AddMinutes(10)};
        form.Config=config;
        typeof(Main).GetField("scheduleController",hidden).SetValue(form,new DownloadScheduleController());
        typeof(Main).GetField("scheduleActionLock",hidden).SetValue(form,new object());
        var stateField=typeof(Main).GetField("EstadoAplicacion",hidden);
        stateField.SetValue(form,Enum.Parse(stateField.FieldType,"Pausa"));
        using(var paused=new Fichero()) {
            paused.PausaIndividual=true;
            var pack=new Paquete();pack.AgregarFichero(paused);
            typeof(Main).GetField("ListaPaquetes",hidden).SetValue(form,new List<Paquete>{pack});
            typeof(Main).GetMethod("ScheduleTick",hidden).Invoke(form,new object[]{null,EventArgs.Empty});
            Check(stateField.GetValue(form).ToString()=="Descargando","scheduled start reaches the real queue state");
            Check(paused.PausaIndividual,"scheduled start preserves individual manual pause");
            paused.EstadoDescarga=Estado.Pausado; paused.DescargaProcesada=true; paused.FileID="PAUSED01";
            typeof(Paquete).GetMethod("MarcarFicherosComoParados",shared).Invoke(null,new object[]{new List<Paquete>{pack}});
            Check(paused.EstadoDescarga==Estado.EnCola && paused.PausaIndividual,"restart normalizes transient state while preserving the manual pause");
            config.ConexionesPorFichero=1; config.DescargasSimultaneas=1;
            typeof(Main).GetField("NumeroConexionesMaxima",hidden).SetValue(form,1);
            typeof(Main).GetMethod("PonerFicherosADescargar",hidden).Invoke(form,null);
            Check(paused.EstadoDescarga==Estado.EnCola && !paused.DescargaComenzada,"scheduler does not launch an individually paused task restored to queue");
            form.PauseDownload();
            typeof(Main).GetMethod("ScheduleTick",hidden).Invoke(form,new object[]{null,EventArgs.Empty});
            Check(stateField.GetValue(form).ToString()=="Pausa","real manual queue pause is not undone by timer");
            config.DownloadSchedule=new DownloadScheduleSettings {Enabled=true,Daily=false,OnceStart=DateTime.Now.AddHours(1),OnceEnd=DateTime.Now.AddHours(2)};
            stateField.SetValue(form,Enum.Parse(stateField.FieldType,"Descargando"));
            paused.DescargaIndividual=true;
            typeof(Main).GetMethod("ScheduleTick",hidden).Invoke(form,new object[]{null,EventArgs.Empty});
            Check(stateField.GetValue(form).ToString()=="Pausa" && !paused.DescargaIndividual,"scheduled stop also stops individually forced downloads");
        }
        // This uninitialized test object has no native window or real configuration.
        GC.SuppressFinalize(form);
        ThrottledStreamController.GetController().Continuar();
    }
    static void FeatureDialogs() {
        Exception failure=null;
        var thread=new Thread(()=>{
            var languageFields=new[]{"LanguageFileDisk","LanguageFileInternal","LanguageFileEnUs"};
            var before=languageFields.Select(n=>typeof(Language).GetField(n,shared).GetValue(null)).ToArray();
            try {
                System.Windows.Forms.Application.EnableVisualStyles();
                var lang=new System.Xml.XmlDocument();
                string resource=typeof(Main).Assembly.GetManifestResourceNames().First(n=>n.EndsWith("zh-CN-Language.xml",StringComparison.Ordinal));
                using(var stream=typeof(Main).Assembly.GetManifestResourceStream(resource))lang.Load(stream);
                foreach(string name in languageFields)typeof(Language).GetField(name,shared).SetValue(null,lang);
                var config=(Configuracion)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Configuracion));
                WarmInboxUiCases(config);
                WorkbenchUiCases();
                var first=new URLProcessor.FileURL("https://mega.nz/file/FIXTURE1#"+new string('A',43),"");
                var second=new URLProcessor.FileURL("https://mega.nz/file/FIXTURE2#"+new string('A',43),"");
                var rows=new List<BatchLinkPreflight.Entry>{
                    new BatchLinkPreflight.Entry {Source=first,Name="课程视频.mkv",SizeBytes=4096,State=BatchLinkPreflight.LinkState.Ready,Selected=true},
                    new BatchLinkPreflight.Entry {Source=second,Name="练习素材.zip",SizeBytes=1024,State=BatchLinkPreflight.LinkState.Ready,Selected=true},
                    new BatchLinkPreflight.Entry {Source=first,Name="课程视频.mkv",SizeBytes=4096,State=BatchLinkPreflight.LinkState.Duplicate}
                };
                using(var preview=new BatchPreflightView(new List<string>(),config,new Dictionary<string,URLProcessor.FileURL>(),root)) {
                    var type=typeof(BatchPreflightView);
                    type.GetField("entries",hidden).SetValue(preview,rows);
                    var bind=type.GetMethod("BindRows",hidden);
                    bind.Invoke(preview,null);
                    var grid=(System.Windows.Forms.DataGridView)type.GetField("grid",hidden).GetValue(preview);
                    var filter=(System.Windows.Forms.ComboBox)type.GetField("category",hidden).GetValue(preview);
                    var search=(System.Windows.Forms.TextBox)type.GetField("search",hidden).GetValue(preview);
                    var add=(System.Windows.Forms.Button)type.GetField("addButton",hidden).GetValue(preview);
                    Check(typeof(System.Windows.Forms.UserControl).IsAssignableFrom(type),"file preview is an embedded page rather than another top-level window");
                    Check(grid.Rows.Count==3 && add.Enabled,"native preview binds metadata and enables confirmation");
                    Check(grid.VirtualMode && grid.MultiSelect,"preview uses a virtual multi-select grid");
                    grid.CurrentCell=grid[3,0];
                    var right=new System.Windows.Forms.KeyEventArgs(System.Windows.Forms.Keys.Right);
                    type.GetMethod("GridKeyDown",hidden).Invoke(preview,new object[]{grid,right});
                    Check(!right.Handled && !right.SuppressKeyPress,"arrow keys retain normal cell navigation outside the tree column");
                    var edit=new System.Windows.Forms.DataGridViewCellCancelEventArgs(0,2);
                    type.GetMethod("BeginCellEdit",hidden).Invoke(preview,new object[]{grid,edit});
                    Check(edit.Cancel,"duplicate checkboxes cannot enter edit mode");
                    filter.SelectedIndex=1;
                    Check(grid.Rows.Count==2,"video filter uses actual preview filenames");
                    type.GetMethod("SelectVisible",hidden).Invoke(preview,new object[]{false});
                    Check(!rows[0].Selected && rows[1].Selected,"clear-visible preserves hidden selections");
                    filter.SelectedIndex=0;search.Text="素材";bind.Invoke(preview,null);
                    Check(grid.Rows.Count==1,"native search filters Unicode filenames");
                    search.Text="";bind.Invoke(preview,null);
                    Check(filter.SelectedIndex==0 && filter.Text==Language.GetText("Preflight_All"),"default type filter displays its localized label");
                    grid.Rows[0].Cells[0].Value=true;
                    Check(rows[0].Selected,"native checkbox changes update the confirmation model");
                    grid.Rows[0].Selected=true;grid.Rows[1].Selected=true;
                    type.GetMethod("ChangeHighlighted",hidden).Invoke(preview,new object[]{PreflightSelection.SelectionOperation.Clear});
                    Check(!rows[0].Selected && !rows[1].Selected,"multi-row action clears both highlighted files");
                    type.GetMethod("GridKeyDown",hidden).Invoke(preview,new object[]{grid,new System.Windows.Forms.KeyEventArgs(System.Windows.Forms.Keys.Space)});
                    Check(rows[0].Selected && rows[1].Selected && !rows[2].Selected,"space toggles highlighted files without selecting duplicates");
                    type.GetField("freeSpace",hidden).SetValue(preview,(long)1);bind.Invoke(preview,null);
                    Check(!add.Enabled,"insufficient disk space prevents confirmation");
                    type.GetField("freeSpace",hidden).SetValue(preview,null);bind.Invoke(preview,null);
                    Check(add.Enabled,"unknown free space does not incorrectly block downloads");
                    bool confirmed=false;
                    preview.ConfirmRequested+=(sender,args)=>confirmed=true;
                    preview.SetImporting(true);
                    preview.ShowImportProgress(1,2,false,false);
                    Check(!add.Enabled && !grid.Enabled && preview.DismissButton.Enabled,"building tasks freezes selection and keeps cancellation available");
                    type.GetMethod("ApplySelection",hidden).Invoke(preview,new object[]{PreflightSelection.SelectionOperation.Clear,false});
                    Check(rows[0].Selected && rows[1].Selected,"pending selection callbacks cannot mutate the batch while it is being built");
                    preview.ShowImportProgress(2,2,true,false);
                    Check(!preview.DismissButton.Enabled,"publication cannot be cancelled after the queue hand-off begins");
                    preview.SetImporting(false);
                    Check(grid.Enabled && add.Enabled,"ending a cancelled build restores the file selection controls");
                    ApplyFeatureTheme(preview,ConfiguracionUI.ThemeModeType.Light);
                    DrawFeatureDialog(preview,"batch-preview-light.png");
                    ApplyFeatureTheme(preview,ConfiguracionUI.ThemeModeType.Dark);
                    DrawFeatureDialog(preview,"batch-preview-dark.png");
                    Check(grid.DefaultCellStyle.BackColor==ThemeManager.GetColor("Back"),"native preview updates grid colors when the theme changes");
                    Check(filter.DrawMode==System.Windows.Forms.DrawMode.OwnerDrawFixed && filter.ForeColor==ThemeManager.GetColor("Fore"),"dark dropdown draws its selected value with themed colors");
                    preview.Size=new System.Drawing.Size(640,430);
                    DrawFeatureDialog(preview,"batch-preview-narrow.png");
                    var filters=filter.Parent;
                    Check(filters.Controls.Cast<System.Windows.Forms.Control>().All(control=>control.Right<=filters.ClientSize.Width),"preview filters wrap within the minimum window width");
                    type.GetMethod("ConfirmSelection",hidden).Invoke(preview,new object[]{null,EventArgs.Empty});
                    Check(preview.SelectedFiles.Count==2,"confirmation returns only selected eligible files");
                    Check(confirmed && !preview.IsDisposed,"confirmation notifies the host and keeps the page alive until submission succeeds");
                }
                using(var preview=new BatchPreflightView(new List<string>(),config,new Dictionary<string,URLProcessor.FileURL>(),root)) {
                    var type=typeof(BatchPreflightView);var large=new List<BatchLinkPreflight.Entry>();
                    for(int i=0;i<10000;i++)large.Add(new BatchLinkPreflight.Entry {Source=first,Name="fixture-"+i+".mkv",SizeBytes=1024,State=BatchLinkPreflight.LinkState.Ready});
                    type.GetField("entries",hidden).SetValue(preview,large);
                    var sw=System.Diagnostics.Stopwatch.StartNew();type.GetMethod("BindRows",hidden).Invoke(preview,null);sw.Stop();
                    var grid=(System.Windows.Forms.DataGridView)type.GetField("grid",hidden).GetValue(preview);
                    Check(grid.RowCount==10000 && grid.Rows.SharedRow(9999).Index==-1,"ten thousand files retain shared virtual grid rows");
                    double bindMs=sw.Elapsed.TotalMilliseconds;sw.Restart();
                    type.GetMethod("ApplySelection",hidden).Invoke(preview,new object[]{PreflightSelection.SelectionOperation.SelectAll,false});sw.Stop();
                    Check(grid.Rows.SharedRow(9999).Index==-1 && large.All(entry=>entry.Selected),"bulk checking does not materialize ten thousand native rows");
                    Print("preflight_virtual_grid","rows=10000","bindMs="+bindMs.ToString("F2"),"selectAllMs="+sw.Elapsed.TotalMilliseconds.ToString("F2"));
                }
                using(var schedule=new DownloadScheduleForm(new DownloadScheduleSettings {Enabled=true,StartMinute=1380,EndMinute=420})) {
                    var once=(System.Windows.Forms.RadioButton)typeof(DownloadScheduleForm).GetField("onceButton",hidden).GetValue(schedule);
                    var daily=(System.Windows.Forms.RadioButton)typeof(DownloadScheduleForm).GetField("dailyButton",hidden).GetValue(schedule);
                    var start=(System.Windows.Forms.DateTimePicker)typeof(DownloadScheduleForm).GetField("startPicker",hidden).GetValue(schedule);
                    once.Checked=true;
                    Check(start.Value.Date==DateTime.Today.AddDays(1),"one-time booking defaults to a future date");
                    var booking=DateTime.Today.AddDays(2).AddHours(12);start.Value=booking;
                    daily.Checked=true;once.Checked=true;
                    Check(start.Value==booking,"switching schedule modes preserves one-time edits");
                    daily.Checked=true;
                    ThemeManager.ApplyTheme(schedule,ConfiguracionUI.ThemeModeType.Light);
                    DrawFeatureDialog(schedule,"download-schedule.png");
                    typeof(DownloadScheduleForm).GetMethod("SaveSettings",hidden).Invoke(schedule,new object[]{null,EventArgs.Empty});
                    Check(schedule.Settings.Enabled && schedule.Settings.StartMinute==1380 && schedule.Settings.EndMinute==420,"native schedule form returns the edited overnight settings");
                    Check(schedule.Settings.OnceStart==booking,"saving daily mode preserves the inactive booking draft");
                }
            } catch(Exception ex) {failure=ex;}
            finally {
                for(int i=0;i<languageFields.Length;i++)typeof(Language).GetField(languageFields[i],shared).SetValue(null,before[i]);
                using(var blank=new System.Windows.Forms.Form())ThemeManager.ApplyTheme(blank,ConfiguracionUI.ThemeModeType.Auto);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();
        if(failure!=null)throw new Exception("Native feature dialog regression failed",failure);
    }
    static void WorkbenchUiCases() {
        using(var main=new Main()) {
            var type=typeof(Main);
            type.GetMethod("InitWorkbench",hidden).Invoke(main,null);
            type.GetMethod("InitNavList",hidden).Invoke(main,null);
            type.GetMethod("InitNavRail",hidden).Invoke(main,null);
            var panel=(System.Windows.Forms.Panel)type.GetProperty("navPanel",hidden).GetValue(main,null);
            panel.Size=new System.Drawing.Size(184,220);
            type.GetMethod("LayoutNavRail",hidden).Invoke(main,null);
            Check(panel.AutoScroll && panel.AutoScrollMinSize.Height>panel.ClientSize.Height,"short sidebar keeps overview and shortcuts reachable by scrolling");
            var nav=(System.Windows.Forms.ListBox)type.GetProperty("navListBox",hidden).GetValue(main,null);
            Check(nav.Top==8,"sidebar navigation retains its unscrolled position after layout");
            var play=(System.Windows.Forms.Button)type.GetProperty("btnPlay",hidden).GetValue(main,null);
            main.ClientSize=new System.Drawing.Size(700,440);
            type.GetMethod("LayoutWorkbenchToolbar",hidden).Invoke(main,null);
            Check(play.Text=="" && !String.IsNullOrEmpty(play.AccessibleName),"compact icon button keeps an accessible name");
            foreach(var mode in new[]{ConfiguracionUI.ThemeModeType.Light,ConfiguracionUI.ThemeModeType.Dark}) {
                ThemeManager.ApplyTheme(main,mode);
                main.ApplyCurrentTheme();
                var quick=(System.Windows.Forms.Button)type.GetProperty("navQuickLogsButton",hidden).GetValue(main,null);
                Check(play.BackColor==ThemeManager.GetColor("ButtonBack") && quick.BackColor==play.BackColor,"toolbar and sidebar use the same button surface in "+mode);
                Check(play.FlatAppearance.BorderColor==ThemeManager.GetColor("ButtonBorder"),"button boundary follows "+mode+" theme");
                main.ClientSize=new System.Drawing.Size(1100,700);
                type.GetMethod("InitDownloadAreaLayout",hidden).Invoke(main,null);
                DrawFeatureDialog(main,"workbench-"+mode+".png");
            }
        }
    }
    static void ApplyFeatureTheme(System.Windows.Forms.Control control,ConfiguracionUI.ThemeModeType mode) {
        var form=control as System.Windows.Forms.Form;
        if(form!=null) {ThemeManager.ApplyTheme(form,mode);return;}
        using(var host=new System.Windows.Forms.Form {Font=control.Font,ClientSize=control.Size,AutoScaleMode=System.Windows.Forms.AutoScaleMode.None}) {
            host.Controls.Add(control);
            try {ThemeManager.ApplyTheme(host,mode);}
            finally {host.Controls.Remove(control);}
        }
    }
    static void DrawFeatureDialog(System.Windows.Forms.Control form,string name) {
        // Create a hidden top-level handle first. Mark only the managed visibility
        // state for child rendering; never call Show/SetVisibleCore/OnShown, which
        // would start real network inspection or display a user-facing window.
        var handle=form.Handle;
        var setState=typeof(System.Windows.Forms.Control).GetMethod("SetState",hidden);
        var visibleFlag=typeof(System.Windows.Forms.Control).GetField("STATE_VISIBLE",shared);
        if(setState==null || visibleFlag==null)throw new Exception("Hidden rendering support unavailable");
        int flag=(int)visibleFlag.GetRawConstantValue();
        setState.Invoke(form,new object[]{flag,true});
        try {
            CreateChildHandles(form);
            form.PerformLayout();
            using(var bitmap=new System.Drawing.Bitmap(form.Width,form.Height)) {
                form.DrawToBitmap(bitmap,new System.Drawing.Rectangle(0,0,form.Width,form.Height));
                bitmap.Save(Path.Combine(root,name),System.Drawing.Imaging.ImageFormat.Png);
            }
        } finally {
            setState.Invoke(form,new object[]{flag,false});
        }
        Check(form.Width>0 && form.Height>0,"hidden native dialog renders: "+name);
    }
    static void WarmInboxUiCases(Configuracion config) {
        int calls=0;string key=new string('A',43);
        string a="https://mega.nz/file/INBOX001#"+key,b="https://mega.nz/file/INBOX002#"+key,c="https://mega.nz/file/INBOX003#"+key;
        var session=new PreflightSession((url,token)=>{
            token.ThrowIfCancellationRequested();Interlocked.Increment(ref calls);
            return new List<BatchLinkPreflight.Entry>{new BatchLinkPreflight.Entry {Source=new URLProcessor.FileURL(url,""),Name="inbox.zip",State=BatchLinkPreflight.LinkState.Ready,SizeBytes=1024,Selected=true}};
        });
        using(var inbox=new AddLinks()) {
            inbox.Config=config;
            typeof(AddLinks).GetField("preflightSession",hidden).SetValue(inbox,session);
            var count=typeof(AddLinks).GetMethod("UpdateLinkCount",hidden);
            var field=typeof(AddLinks).GetField("warmRequest",hidden);
            var text=(System.Windows.Forms.RichTextBox)typeof(AddLinks).GetProperty("txtLinks",hidden).GetValue(inbox,null);
            var label=(System.Windows.Forms.Label)typeof(AddLinks).GetProperty("lblLinkCount",hidden).GetValue(inbox,null);
            inbox.AgregarEnlaces(a,true,true,false,new List<string>{a});
            count.Invoke(inbox,null);
            var prepared=(PreflightSession.Request)field.GetValue(inbox);
            PumpUntil(()=>prepared.Results.IsCompleted && label.Text.Contains("已预检"));
            Check(calls==1 && prepared.Results.Result.Count==1,"pasted links start background metadata lookup before confirmation");
            using(var preview=new BatchPreflightView(prepared.Urls.Result,config,new Dictionary<string,URLProcessor.FileURL>(),root,session,prepared)) {
                typeof(BatchPreflightView).GetMethod("BeginInspection",hidden).Invoke(preview,new object[]{false});
                var loading=typeof(BatchPreflightView).GetField("loading",hidden);
                PumpUntil(()=>!(bool)loading.GetValue(preview));
                var grid=(System.Windows.Forms.DataGridView)typeof(BatchPreflightView).GetField("grid",hidden).GetValue(preview);
                Check(calls==1 && grid.RowCount==1,"native preview reuses the inbox warm-up instead of issuing a second metadata request");
                var page=typeof(AddLinks).GetField("preflightView",hidden);
                var host=(System.Windows.Forms.Panel)typeof(AddLinks).GetField("addLinksPageHost",hidden).GetValue(inbox);
                var path=(System.Windows.Forms.TextBox)typeof(AddLinks).GetProperty("txtRuta",hidden).GetValue(inbox,null);
                path.Text=root;
                page.SetValue(inbox,preview);
                host.Controls.Add(preview);
                try {
                    var next=typeof(AddLinks).GetMethod("ShowSelectionPage",hidden);
                    var back=typeof(AddLinks).GetMethod("ShowInputPage",hidden);
                    var search=(System.Windows.Forms.TextBox)typeof(BatchPreflightView).GetField("search",hidden).GetValue(preview);
                    next.Invoke(inbox,null);
                    Check(preview.DismissButton.DialogResult==System.Windows.Forms.DialogResult.None,"escape cancels import in place instead of implicitly closing the modal host");
                    search.Text="inbox";
                    typeof(BatchPreflightView).GetMethod("SelectVisible",hidden).Invoke(preview,new object[]{false});
                    back.Invoke(inbox,new object[]{true});
                    next.Invoke(inbox,null);
                    var model=(PreflightSelection)typeof(BatchPreflightView).GetField("selection",hidden).GetValue(preview);
                    Check(Object.ReferenceEquals(page.GetValue(inbox),preview) && search.Text=="inbox" && model.SelectedCount==0 && calls==1,"back and next retain search and unchecked files without another metadata request");
                    Check(!inbox.AgregarEnlaces(c,false,true,false,new List<string>{c}) && !text.Text.Contains("INBOX003") && Object.ReferenceEquals(page.GetValue(inbox),preview) && !preview.IsDisposed && model.SelectedCount==0,"incoming links stay deferred on the file-selection page without discarding unchecked files");
                    back.Invoke(inbox,new object[]{true});
                    prepared=(PreflightSession.Request)field.GetValue(inbox);
                    PumpUntil(()=>prepared.Results.IsCompleted && label.Text.Contains("2 个文件"));
                    Check(text.Text.Contains("INBOX001") && text.Text.Contains("INBOX003") && calls==2,"returning to links merges the deferred batch and reuses prior metadata");
                } finally {
                    page.SetValue(inbox,null);
                    host.Controls.Remove(preview);
                }
            }
            inbox.AgregarEnlaces(b,false,true,false,new List<string>{b});
            count.Invoke(inbox,null);prepared=(PreflightSession.Request)field.GetValue(inbox);
            PumpUntil(()=>prepared.Results.IsCompleted && label.Text.Contains("3 个文件"));
            Check(calls==3 && text.Text.Contains("INBOX001") && text.Text.Contains("INBOX002") && text.Text.Contains("INBOX003"),"appending a clipboard batch keeps earlier text and cached metadata");
            var busy=typeof(AddLinks).GetField("importBusy",hidden);busy.SetValue(inbox,true);
            string beforeBusy=text.Text;
            Check(!inbox.AgregarEnlaces(c,false,true,false,new List<string>{c}) && text.Text==beforeBusy,"new clipboard input is deferred while confirming the current batch");
            busy.SetValue(inbox,false);
            typeof(AddLinks).GetMethod("RestoreDeferredInputs",hidden).Invoke(inbox,new object[]{true});
            prepared=(PreflightSession.Request)field.GetValue(inbox);
            PumpUntil(()=>prepared.Results.IsCompleted && label.Text.Contains("1 个文件"));
            Check(!text.Text.Contains("INBOX001") && text.Text.Contains("INBOX003") && calls==4,"after a committed batch deferred links remain in the inbox for separate confirmation");
            text.Clear();System.Windows.Forms.Application.DoEvents();
            Check(field.GetValue(inbox)==null && label.Text=="","clearing pasted text removes stale warm-up status and request references");
        }
        bool disposed=false;try{session.Prepare(a);}catch(ObjectDisposedException){disposed=true;}
        Check(disposed,"closing the inbox disposes its background preflight session");
    }
    static void PumpUntil(Func<bool> done) {
        var timer=System.Diagnostics.Stopwatch.StartNew();
        while(!done()) {
            if(timer.ElapsedMilliseconds>5000)throw new Exception("Native async fixture did not complete");
            System.Windows.Forms.Application.DoEvents();Thread.Sleep(1);
        }
    }
    static void CreateChildHandles(System.Windows.Forms.Control parent) {
        foreach(System.Windows.Forms.Control child in parent.Controls) {
            var handle=child.Handle;
            CreateChildHandles(child);
            child.PerformLayout();
        }
    }
    static void ProcessResourceMonitoring() {
        Type monitorType=typeof(Main).Assembly.GetType("MegaDownloader.ProcessResourceMonitor",true);
        MethodInfo read=monitorType.GetMethod("ReadSnapshot");
        var elapsed=System.Diagnostics.Stopwatch.StartNew();
        using(var monitor=(IDisposable)Activator.CreateInstance(monitorType,true)) {
            object[] first={0.0,null};
            read.Invoke(monitor,first);
            double firstMs=elapsed.Elapsed.TotalMilliseconds;
            Check((double)first[0]>0,"resource monitor reports the current process working set");
            Check(first[1]==null,"first CPU sample waits for an interval instead of reporting lifetime usage");
            elapsed.Restart();
            while(elapsed.ElapsedMilliseconds<150) Thread.SpinWait(10000);
            object[] second={0.0,null};
            read.Invoke(monitor,second);
            Check(second[1] is double,"CPU usage becomes available after a second sample");
            double cpu=(double)second[1];
            Check(!double.IsNaN(cpu) && cpu>0 && cpu<=100,"CPU usage detects work and is normalized to total machine capacity");
            Check((double)second[0]>0,"memory sampling continues on subsequent reads");
            Print("process_resource_monitor","firstSampleMs="+firstMs.ToString("F3"),"cpu="+cpu.ToString("F3"),"logicalProcessors="+Environment.ProcessorCount);
        }
    }
    static void ExpectRejected(Action action,string name) {
        bool rejected=false;
        try { action(); } catch(ArgumentException) { rejected=true; } catch(UnauthorizedAccessException) { rejected=true; }
        Check(rejected,name);
    }
    static void MakeZip(string path,string name,string text) {
        using(var zip=ZipFile.Open(path,ZipArchiveMode.Create)) {
            var e=zip.CreateEntry(name);
            using(var stream=e.Open()) { byte[] b=Encoding.UTF8.GetBytes(text); stream.Write(b,0,b.Length); }
        }
    }
    static string MakeKey(byte[] plain) {
        byte[] chunkMac=new byte[16], fileMac;
        using(var aes=Aes.Create()) {
            aes.Mode=CipherMode.ECB; aes.Padding=PaddingMode.None; aes.Key=new byte[16];
            using(var t=aes.CreateEncryptor()) {
                for(int i=0;i<plain.Length;i+=16) {
                    for(int j=0;j<16 && i+j<plain.Length;j++) chunkMac[j]^=plain[i+j];
                    chunkMac=t.TransformFinalBlock(chunkMac,0,16);
                }
                fileMac=t.TransformFinalBlock(chunkMac,0,16);
            }
        }
        byte[] key=new byte[32];
        for(int i=0;i<4;i++) { key[8+i]=key[24+i]=(byte)(fileMac[i]^fileMac[i+4]); key[12+i]=key[28+i]=(byte)(fileMac[i+8]^fileMac[i+12]); }
        return Convert.ToBase64String(key).TrimEnd('=').Replace('+','-').Replace('/','_');
    }
    static byte[] Encrypt(byte[] plain,string key) {
        var cipher=(Criptografia.SicSeekableBlockCipher)typeof(Criptografia).GetMethod("GetInstaceCipher",shared).Invoke(null,new object[]{key});
        byte[] padded=new byte[(plain.Length+15)/16*16]; Array.Copy(plain,padded,plain.Length);
        byte[] result=new byte[padded.Length];
        for(int i=0;i<padded.Length;i+=16) cipher.ProcessBlock(padded,i,result,i);
        return result.Take(plain.Length).ToArray();
    }
    static void DownloadCase(LocalFileServer server,byte[] plain,string key,string name,int index,bool createPart) {
        string d=Path.Combine(root,name); Directory.CreateDirectory(d);
        string path=Path.Combine(d,"data.bin");
        bool modern=name.Contains("modern");
        string partialName=modern?FileDownloader.CreatePartialFileName():null;
        string partialPath=FileDownloader.GetPartialFilePath(d,"data.bin",partialName);
        if(createPart && index>0) { byte[] part=new byte[plain.Length]; Array.Copy(plain,part,index); File.WriteAllBytes(partialPath,part); }
        byte[] foreign=Enumerable.Repeat((byte)0xCC,plain.Length).ToArray();
        if(name=="missing-modern-with-foreign-legacy") File.WriteAllBytes(path+".part",foreign);
        var dp=new FileDownloader.DataPart(plain.Length,1); dp.ChunkList[0].Index=index; dp.ChunkList[0].SyncedIndex=index;
        if(index==plain.Length) { File.WriteAllBytes(path,name=="resume-corrupt-final"?foreign:plain); dp.AllFinished=true; }
        var done=new ManualResetEvent(false);
        string failure="", finalName="data.bin"; bool success=false;
        using(var fd=new FileDownloader(false)) {
            fd.LocalDirectory=d; fd.AddFileInfo("TESTFILE",key,server.Url,"data.bin",dp,partialName);
            fd.FileRenamed+=(s,e)=>finalName=e.FileName;
            fd.FileDownloadFailed+=(s,e)=>{ failure=e.Message; };
            fd.FileDownloadSucceeded+=(s,e)=>{ success=true; };
            fd.Completed+=(s,e)=>done.Set();
            fd.Canceled+=(s,e)=>done.Set();
            fd.Start();
            bool finished=done.WaitOne(15000);
            if(!finished) fd.Stop();
            Thread.Sleep(100);
            string ranges=server.TakeRanges();
            Check(finished && success && failure=="","download completes: "+name+" "+failure);
            Check(File.ReadAllBytes(Path.Combine(d,finalName)).SequenceEqual(plain),"downloaded plaintext matches: "+name);
            if(name=="resume-final") Check(ranges=="" && !File.Exists(Path.Combine(d,"data (2).bin")),"completed file reused with no chunk transfer or duplicate");
            else Check(ranges==(createPart && index>0?"bytes=2048-4095":"bytes=0-4095"),"correct requested range: "+name+" "+ranges);
            if(name=="missing-modern-with-foreign-legacy" || name=="resume-corrupt-final") Check(File.ReadAllBytes(name=="resume-corrupt-final"?path:path+".part").SequenceEqual(foreign),"unrelated existing file preserved: "+name);
            Print("download",name,"success",ranges);
        }
        done.Dispose();
    }
    static int FreePort() {
        var p=new TcpListener(IPAddress.Loopback,0); p.Start(); int port=((IPEndPoint)p.LocalEndpoint).Port; p.Stop(); return port;
    }
    static void StreamingCases(LocalFileServer upstream,byte[] plain,string key) {
        var cfg=(Configuracion)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Configuracion));
        cfg.ServidorStreamingPassword="";
        var info=new Conexion.InformacionFichero(); info.FileID="TESTFILE"; info.FileKey=key; info.Nombre="data.bin"; info.Tamano=plain.Length; info.URL=upstream.Url;
        var cache=(Dictionary<string,KeyValuePair<DateTime,Conexion.InformacionFichero>>)typeof(StreamingModule).GetField("Urls",shared).GetValue(null);
        cache["TESTFILE|"+key]=new KeyValuePair<DateTime,Conexion.InformacionFichero>(DateTime.Now.AddMinutes(3),info);
        int port=FreePort(); var server=new HttpServer.HttpServer(); server.Add(new StreamingModule(ref cfg)); server.Start(IPAddress.Loopback,port);
        try {
            long[][] cases={null,new long[]{0,0},new long[]{1,1},new long[]{15,16},new long[]{16,16},new long[]{plain.Length-2,plain.Length-1},new long[]{15,Math.Min(plain.Length-1,50000)}};
            foreach(var range in cases) foreach(int fragment in new[]{0,2}) {
                upstream.FragmentNext=fragment;
                var req=(HttpWebRequest)WebRequest.Create("http://127.0.0.1:"+port+"/streaming?mega=!TESTFILE!"+key);
                req.Proxy=null; req.Timeout=5000; req.ReadWriteTimeout=5000;
                if(range!=null) req.AddRange(range[0],range[1]);
                    using(var resp=(HttpWebResponse)req.GetResponse()) using(var m=new MemoryStream()) {
                        resp.GetResponseStream().CopyTo(m); byte[] got=m.ToArray();
                        byte[] expected=range==null?plain:plain.Skip((int)range[0]).Take((int)(range[1]-range[0]+1)).ToArray();
                        Check(resp.StatusCode==(range==null?HttpStatusCode.OK:HttpStatusCode.PartialContent),"streaming status");
                        Check(resp.ContentLength==got.Length && got.SequenceEqual(expected),"streaming exact bytes: "+plain.Length+" "+(range==null?"full":range[0]+"-"+range[1])+" fragment="+fragment);
                    }
            }
        } finally { server.Stop(); }
    }
    static void SameNameDownloads(byte[] plainA,string keyA,byte[] encryptedA,bool duplicateLink=false) {
        byte[] plainB=duplicateLink?plainA:plainA.Select(x=>(byte)(x^0x5A)).ToArray(); string keyB=MakeKey(plainB);
        string d=Path.Combine(root,"parallel-same-name-"+duplicateLink); Directory.CreateDirectory(d);
        using(var serverA=new LocalFileServer(encryptedA)) using(var serverB=new LocalFileServer(Encrypt(plainB,keyB)))
        using(var a=new FileDownloader(false)) using(var b=new FileDownloader(false)) {
            serverB.FragmentNext=2048; serverB.ReleaseFragment=new ManualResetEvent(false);
            a.LocalDirectory=b.LocalDirectory=d;
            a.setBufferAndPackageSize(2048,1024); b.setBufferAndPackageSize(2048,1024);
            a.AddFileInfo("FILEAAAA",keyA,serverA.Url,"same.bin",null);
            b.AddFileInfo(duplicateLink?"FILEAAAA":"FILEBBBB",keyB,serverB.Url,"same.bin",null);
            Check(a.File.PartialFileName!=b.File.PartialFileName,"parallel tasks have independent partial paths, duplicate="+duplicateLink);
            var doneA=new ManualResetEvent(false); var doneB=new ManualResetEvent(false);
            string failA="",failB="",nameA="",nameB=""; bool okA=false,okB=false;
            a.FileRenamed+=(s,e)=>nameA=e.FileName; b.FileRenamed+=(s,e)=>nameB=e.FileName;
            a.FileDownloadFailed+=(s,e)=>failA=e.Message; b.FileDownloadFailed+=(s,e)=>failB=e.Message;
            a.FileDownloadSucceeded+=(s,e)=>okA=true; b.FileDownloadSucceeded+=(s,e)=>okB=true;
            a.Completed+=(s,e)=>doneA.Set(); b.Completed+=(s,e)=>doneB.Set();
            b.Start();
            DateTime limit=DateTime.UtcNow.AddSeconds(5);
            while(DateTime.UtcNow<limit && (!b.File.DataPartInitialized || b.File.GetDataPart.GetTotalProgress()<2048)) Thread.Sleep(10);
            Check(b.File.DataPartInitialized && b.File.GetDataPart.GetTotalProgress()==2048,"parallel fixture overlaps actual disk writes");
            a.Start(); bool finishedA=doneA.WaitOne(5000);
            serverB.ReleaseFragment.Set(); bool finishedB=doneB.WaitOne(5000);
            if(!finishedA) a.Stop(); if(!finishedB) b.Stop(); Thread.Sleep(100);
            Check(finishedA && finishedB && okA && okB && failA=="" && failB=="","parallel same-name downloads both succeed: "+failA+" "+failB);
            Check(nameA!=nameB && File.ReadAllBytes(Path.Combine(d,nameA)).SequenceEqual(plainA) && File.ReadAllBytes(Path.Combine(d,nameB)).SequenceEqual(plainB),"parallel final names and plaintext preserved");
            Print("parallel_same_name",duplicateLink?"duplicate link":"different links","both successful");
            doneA.Dispose(); doneB.Dispose(); serverB.ReleaseFragment.Dispose();
        }
    }
    static void ConcurrentFinalNames() {
        string d=Path.Combine(root,"concurrent-finalize"); Directory.CreateDirectory(d);
        var names=new System.Collections.Concurrent.ConcurrentBag<string>();
        System.Threading.Tasks.Parallel.For(0,8,i=> {
            using(var fd=new FileDownloader(false)) {
                fd.LocalDirectory=d;
                string part=Path.Combine(d,"task-"+i+".part"); File.WriteAllText(part,i.ToString());
                string name=(string)typeof(FileDownloader).GetMethod("RenamePartToReal",hidden).Invoke(fd,new object[]{part,Path.Combine(d,"same")});
                names.Add(name);
                Check(File.ReadAllText(Path.Combine(d,name))==i.ToString(),"concurrent rename preserves task content");
            }
        });
        Check(names.Distinct().Count()==8,"simultaneous finalization chooses eight distinct names");
    }
    static void PartialPersistenceAndCleanup(byte[] plain,string key) {
        string d=Path.Combine(root,"partial-persistence"); Directory.CreateDirectory(d);
        using(var original=new Fichero("https://mega.nz/file/TESTFILE#"+key)) using(var restored=new Fichero()) {
            original.FileID="TESTFILE"; original.FileKey=key; original.RutaLocal=d; original.NombreFichero="data.bin";
            original.TamanoBytes=plain.Length; original.DatosPartes=new FileDownloader.DataPart(plain.Length,1);
            original.DatosPartes.ChunkList[0].Index=original.DatosPartes.ChunkList[0].SyncedIndex=2048;
            var xml=new System.Xml.XmlDocument(); var node=original.GuardarXML(xml,true); restored.CargarXML(node);
            Check(restored.PartialFileName==original.PartialFileName,"task partial filename survives queue serialization");
            string own=FileDownloader.GetPartialFilePath(d,"data.bin",restored.PartialFileName);
            string legacy=Path.Combine(d,"data.bin.part"); string real=Path.Combine(d,"data.bin");
            File.WriteAllBytes(own,plain); File.WriteAllText(legacy,"foreign partial"); File.WriteAllText(real,"existing final");
            restored.ResetearDescarga();
            Check(!File.Exists(own) && File.ReadAllText(legacy)=="foreign partial" && File.ReadAllText(real)=="existing final","reset removes only this task's partial file");
            node.SelectSingleNode("PartialFileName").InnerText=@"..\outside.part";
            restored.CargarXML(node);
            Check(restored.PartialFileName!="" && !restored.PartialFileName.Contains("\\"),"unsafe persisted partial path rejected");
            node.SelectSingleNode("PartialFileName").InnerText="data.bin";
            restored.CargarXML(node);
            Check(restored.PartialFileName!="data.bin", "partial identity cannot target a final filename");
            node.RemoveChild(node.SelectSingleNode("PartialFileName")); restored.CargarXML(node);
            Check(restored.PartialFileName=="","legacy queue recognized without partial identity");
            restored.ResetearDescarga(); Check(!File.Exists(legacy),"legacy queue can reset its legacy partial");
        }
        using(var fd=new FileDownloader(false)) {
            fd.LocalDirectory=d; fd.AddFileInfo("TESTFILE",key,"http://127.0.0.1/unused","data.bin",null);
            string own=FileDownloader.GetPartialFilePath(d,"data.bin",fd.File.PartialFileName); File.WriteAllBytes(own,plain);
            typeof(FileDownloader).GetMethod("cleanUpFile",hidden).Invoke(fd,null);
            Check(!File.Exists(own) && File.ReadAllText(Path.Combine(d,"data.bin"))=="existing final","cancel cleanup removes partial and preserves existing final");
        }
    }
    static void ArchiveEdges() {
        string d=Path.Combine(root,"archive-edges"); Directory.CreateDirectory(d);
        string zip=Path.Combine(root,"locked.zip"); MakeZip(zip,"data.txt","REPLACED");
        string target=Path.Combine(d,"data.txt"); File.WriteAllText(target,"ORIGINAL");
        using(var locked=new FileStream(target,FileMode.Open,FileAccess.Read,FileShare.Read)) {
            string error=DescompresorController.ExtractArchiveSync(zip,d,"");
            Check(error=="" && File.ReadAllText(target)=="ORIGINAL" && File.ReadAllText(Path.Combine(d,"data (2).txt"))=="REPLACED","ZIP locked original remains intact; incoming file gets suffix");
        }
        string traversal=Path.Combine(root,"traversal.zip"); MakeZip(traversal,"../escape/","");
        Check(DescompresorController.ExtractArchiveSync(traversal,d,"")!="" && !Directory.Exists(Path.Combine(root,"escape")),"archive directory entries cannot escape root");
        string nested=Path.Combine(root,"nested.zip"); MakeZip(nested,"one/two/","");
        Check(DescompresorController.ExtractArchiveSync(nested,d,"")=="" && Directory.Exists(Path.Combine(d,"one","two")),"nested empty ZIP directories restored");

        string input=Path.Combine(root,"seven-input"); Directory.CreateDirectory(input);
        File.WriteAllText(Path.Combine(input,"data.txt"),"REPLACED");
        Directory.CreateDirectory(Path.Combine(input,"empty","nested"));
        string archive=Path.Combine(root,"fixture.7z");
        var psi=new System.Diagnostics.ProcessStartInfo(Path.Combine(projectRoot,"Resources","Binaries","7zr.exe"),"a -t7z -y \""+archive+"\" .\\*");
        psi.WorkingDirectory=input; psi.UseShellExecute=false; psi.CreateNoWindow=true;
        psi.RedirectStandardOutput=true; psi.RedirectStandardError=true;
        using(var proc=System.Diagnostics.Process.Start(psi)) {
            var stdout=proc.StandardOutput.ReadToEndAsync(); var stderr=proc.StandardError.ReadToEndAsync();
            if(!proc.WaitForExit(15000)) { proc.Kill(); throw new Exception("7zr fixture creation timed out"); }
            Check(proc.ExitCode==0,"7z fixture creation: "+stderr.Result); stdout.Wait();
        }
        string output=Path.Combine(root,"seven-output"); Directory.CreateDirectory(output);
        string sevenTarget=Path.Combine(output,"data.txt"); File.WriteAllText(sevenTarget,"ORIGINAL");
        Check(DescompresorController.ExtractArchiveSync(archive,output,"")=="","7z extraction succeeds");
        Check(File.ReadAllText(sevenTarget)=="REPLACED" && File.ReadAllText(Path.Combine(output,"data (2).txt"))=="ORIGINAL","7z preserves equal-length original bytes");
        Check(Directory.Exists(Path.Combine(output,"empty","nested")),"7z empty directories retained");
        using(var locked=new FileStream(sevenTarget,FileMode.Open,FileAccess.Read,FileShare.Read)) {
            Check(DescompresorController.ExtractArchiveSync(archive,output,"")!="" && File.ReadAllText(sevenTarget)=="REPLACED","7z fails safely when an original cannot be preserved");
        }
    }
    static string EncryptedName(string name) {
        byte[] text=Encoding.UTF8.GetBytes("MEGA{\"n\":\""+name+"\"}");
        byte[] padded=new byte[(text.Length+15)/16*16]; Array.Copy(text,padded,text.Length);
        using(var aes=Aes.Create()) {
            aes.Key=new byte[16]; aes.IV=new byte[16]; aes.Mode=CipherMode.CBC; aes.Padding=PaddingMode.None;
            using(var t=aes.CreateEncryptor()) return Convert.ToBase64String(t.TransformFinalBlock(padded,0,padded.Length)).TrimEnd('=').Replace('+','-').Replace('/','_');
        }
    }
    static void PreserveCompletedName(byte[] plain,string key,byte[] encrypted) {
        using(var api=new LocalFileServer(encrypted)) using(var file=new Fichero()) {
            var configField=typeof(InternalConfiguration).GetField("XML_CONFIG",shared); object previous=configField.GetValue(null);
            var xml=new System.Xml.XmlDocument();
            xml.LoadXml("<Config><URL_MEGA_API_DOWN_GET_G>"+api.Url+"</URL_MEGA_API_DOWN_GET_G><URL_MEGA_API_DOWN_GET_EU>"+api.Url+"</URL_MEGA_API_DOWN_GET_EU></Config>");
            api.ApiResponse="[{\"s\":"+plain.Length+",\"at\":\""+EncryptedName("same.bin")+"\",\"g\":\""+api.Url+"\"}]";
            configField.SetValue(null,xml);
            try {
                var cfg=(Configuracion)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Configuracion));
                file.FileID="TESTFILE"; file.FileKey=key; file.NombreFichero="same (2).bin";
                file.DatosPartes=new FileDownloader.DataPart(plain.Length,1); file.DatosPartes.SetProgress(0,plain.Length);
                Conexion.TipoError error=Conexion.TipoError.SinErrores;
                file.ActualizarInformacionFichero(cfg,ref error,true);
                Check(error==Conexion.TipoError.SinErrores && file.DescargaProcesada,"file-info fixture is valid");
                Check(file.NombreFichero=="same (2).bin","recovery retains the actual completed collision name");
                file.NombreFichero=""; file.ActualizarInformacionFichero(cfg,ref error,true);
                Check(file.NombreFichero=="same.bin","missing completed name receives a safe remote name");
                file.DatosPartes.AllFinished=false; file.NombreFichero="placeholder";
                file.ActualizarInformacionFichero(cfg,ref error,true);
                Check(file.NombreFichero=="same.bin","unfinished task still receives remote name");
            } finally { configField.SetValue(null,previous); }
        }
    }
    class LocalFileServer:IDisposable {
        HttpListener l; Thread thread; byte[] data;
        List<string> ranges=new List<string>();
        public string Url;
        public int FragmentNext;
        public ManualResetEvent ReleaseFragment;
        public string ApiResponse;
        public LocalFileServer(byte[] content) {
            data=content;
            var probe=new TcpListener(IPAddress.Loopback,0); probe.Start(); int port=((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
            Url="http://127.0.0.1:"+port+"/data";
            l=new HttpListener(); l.Prefixes.Add("http://127.0.0.1:"+port+"/"); l.Start();
            thread=new Thread(Loop); thread.IsBackground=true; thread.Start();
        }
        void Loop() {
            while(l.IsListening) {
                try {
                    var c=l.GetContext();
                    string r=c.Request.Headers["Range"];
                    if(c.Request.HttpMethod=="POST" && ApiResponse!=null) {
                        c.Request.InputStream.CopyTo(Stream.Null);
                        byte[] json=Encoding.UTF8.GetBytes(ApiResponse); c.Response.ContentLength64=json.Length;
                        c.Response.OutputStream.Write(json,0,json.Length); c.Response.Close(); continue;
                    }
                    if(c.Request.HttpMethod=="HEAD") { c.Response.ContentLength64=data.Length; c.Response.Close(); continue; }
                    long start=0,end=data.Length-1;
                    if(r!=null) { string[] p=r.Substring(6).Split('-'); start=long.Parse(p[0]); if(p[1]!="") end=Math.Min(end,long.Parse(p[1])); c.Response.StatusCode=206; c.Response.Headers.Add("Content-Range","bytes "+start+"-"+end+"/"+data.Length); }
                    lock(ranges) ranges.Add(r??"no-range");
                    c.Response.ContentLength64=end-start+1;
                    int length=(int)(end-start+1); int fragment=Interlocked.Exchange(ref FragmentNext,0);
                    if(fragment>0 && fragment<length) {
                        c.Response.OutputStream.Write(data,(int)start,fragment); c.Response.OutputStream.Flush();
                        if(ReleaseFragment!=null) ReleaseFragment.WaitOne(10000); else Thread.Sleep(250);
                        c.Response.OutputStream.Write(data,(int)start+fragment,length-fragment);
                    } else c.Response.OutputStream.Write(data,(int)start,length);
                    c.Response.Close();
                } catch(HttpListenerException) { break; } catch(ObjectDisposedException) { break; }
            }
        }
        public string TakeRanges() { lock(ranges) { string result=string.Join(",",ranges); ranges.Clear(); return result; } }
        public void Dispose() { l.Stop(); l.Close(); thread.Join(1000); }
    }
}
