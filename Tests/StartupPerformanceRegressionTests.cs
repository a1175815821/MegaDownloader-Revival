using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using MegaDownloader;

public partial class RegressionTests {
    static void StartupPerformanceCases() {
        var previousProxy=System.Net.WebRequest.DefaultWebProxy;
        try {
            SecureStringFactoryCases();
            LoadedCiphertextCacheCases();
            ParallelQueueRestoreCases();
            CancellableQueueReadCases();
            BackgroundStartupCases();
            NonblockingLibraryManagerCases();
        }finally{System.Net.WebRequest.DefaultWebProxy=previousProxy;}
        Print("startup_performance","secure strings, current/legacy ciphertext, ordered parallel restore, cancellable IO and UI lifecycle passed");
    }
    static void SecureStringFactoryCases() {
        foreach(string value in new[]{null,"","ascii","中文密码","a\0b","\uD83D\uDE03","\uD800",new string('x',65536)})
            using(var secure=Criptografia.ToSecureString(value))
                Check(secure.IsReadOnly()&&Criptografia.ToInsecureString(secure)==(value??""),"bulk secure-string factory preserves UTF-16 and read-only semantics");
        byte[][] chain=(byte[][])typeof(Criptografia).GetField("decryptionEntropies",shared).GetValue(null);
        var decrypt=typeof(Criptografia).GetMethod("DecryptStringWithCacheInfo",shared);
        for(int i=0;i<chain.Length;i++) {
            string blob=Convert.ToBase64String(ProtectedData.Protect(Encoding.Unicode.GetBytes("兼容\0value"),chain[i],DataProtectionScope.CurrentUser));
            object[] args={blob,null};
            using(var secure=(SecureString)decrypt.Invoke(null,args))
                Check(Criptografia.ToInsecureString(secure)=="兼容\0value"&&secure.IsReadOnly(),"historical entropy remains readable: "+i);
            Check((i==0&&Equals(args[1],blob))||(i!=0&&args[1]==null),"only current entropy may reuse ciphertext: "+i);
        }
        foreach(string blob in new[]{null,"","not base64!",Convert.ToBase64String(new byte[]{1,2,3,4})}) {
            object[] args={blob,"stale"};using(var secure=(SecureString)decrypt.Invoke(null,args))
                Check(secure.Length==0&&args[1]==null,"empty/corrupt ciphertext is never marked reusable");
        }
    }
    static XmlDocument FileXml(Fichero file,bool encrypted=true) {
        var doc=new XmlDocument();doc.AppendChild(file.GuardarXML(doc,encrypted));return doc;
    }
    static void LoadedCiphertextCacheCases() {
        using(var original=new Fichero("https://mega.nz/file/TESTFILE#example")) {
            original.FileID="PERF0001";original.FileKey="queue-key";original.URLFichero="https://127.0.0.1/download";original.NombreFichero="performance.bin";original.RutaLocal=root;
            var saved=FileXml(original);string[] names={"FileID","FileKey","URL","URLFichero"};
            using(var loaded=new Fichero()) {
                loaded.CargarXML(saved.DocumentElement);var reused=FileXml(loaded);
                Check(names.All(n=>saved.DocumentElement[n].InnerText==reused.DocumentElement[n].InnerText),"first save reuses all verified current ciphertext without encryption");
                foreach(string name in names) {
                    string replacement="changed-"+name;typeof(Fichero).GetProperty(name).SetValue(loaded,replacement,null);
                    var changed=FileXml(loaded);using(var secure=Criptografia.DecryptString_DPAPI(changed.DocumentElement[name].InnerText))
                        Check(Criptografia.ToInsecureString(secure)==replacement,"setter invalidates reused ciphertext: "+name);
                }
                loaded.LinkVisible=false;var hiddenXml=FileXml(loaded);
                using(var roundTrip=new Fichero()) {
                    roundTrip.CargarXML(hiddenXml.DocumentElement);
                    Check(!roundTrip.LinkVisible&&roundTrip.URL==loaded.URL,"hidden marker survives a cached save and load");
                    roundTrip.LinkVisible=true;var visibleXml=FileXml(roundTrip);roundTrip.CargarXML(visibleXml.DocumentElement);
                    Check(roundTrip.LinkVisible&&roundTrip.URL==loaded.URL,"visibility changes invalidate cache and repeated load resets hidden state");
                    roundTrip._FileKey=Criptografia.ToSecureString("direct-field-replacement");var direct=FileXml(roundTrip);
                    using(var secure=Criptografia.DecryptString_DPAPI(direct.DocumentElement["FileKey"].InnerText))
                        Check(Criptografia.ToInsecureString(secure)=="direct-field-replacement","public secure-field replacement cannot retain a stale cache");
                }
            }
            byte[][] chain=(byte[][])typeof(Criptografia).GetField("decryptionEntropies",shared).GetValue(null);
            foreach(byte[] entropy in chain.Skip(1)) {
                var old=(XmlDocument)saved.CloneNode(true);
                foreach(string name in names)using(var plain=Criptografia.DecryptString_DPAPI(saved.DocumentElement[name].InnerText))
                    old.DocumentElement[name].InnerText=Convert.ToBase64String(ProtectedData.Protect(Encoding.Unicode.GetBytes(Criptografia.ToInsecureString(plain)),entropy,DataProtectionScope.CurrentUser));
                using(var loaded=new Fichero()) {
                    loaded.CargarXML(old.DocumentElement);var migrated=FileXml(loaded);
                    Check(names.All(n=>old.DocumentElement[n].InnerText!=migrated.DocumentElement[n].InnerText),"historical queue ciphertext is migrated on its first save");
                    foreach(string name in names) {
                        byte[] bytes=ProtectedData.Unprotect(Convert.FromBase64String(migrated.DocumentElement[name].InnerText),chain[0],DataProtectionScope.CurrentUser);
                        try {using(var expected=Criptografia.DecryptString_DPAPI(saved.DocumentElement[name].InnerText))Check(Encoding.Unicode.GetString(bytes)==Criptografia.ToInsecureString(expected),"migration keeps field content: "+name);}finally{Array.Clear(bytes,0,bytes.Length);}
                    }
                }
            }
        }
    }
    static void ParallelQueueRestoreCases() {
        var source=new XmlDocument();source.LoadXml("<Paquete><Nombre>ordered</Nombre><RutaLocal></RutaLocal><ListaFicheros/></Paquete>");
        for(int i=0;i<160;i++)using(var file=new Fichero("https://mega.nz/file/TESTFILE#example")) {
            file.FileID="ORDER"+i.ToString("D4");file.FileKey="key-"+i;file.URLFichero="https://127.0.0.1/"+i;file.NombreFichero="file-"+i;file.RutaLocal=root;
            file.EstadoDescarga=i%2==0?Estado.EnCola:Estado.Erroneo;source.DocumentElement["ListaFicheros"].AppendChild(file.GuardarXML(source,true));
        }
        var restore=typeof(Paquete).GetMethod("CargarXMLCancelable",hidden|BindingFlags.Public);
        var serial=new Paquete();var parallel=new Paquete();
        try {
            restore.Invoke(serial,new object[]{source.DocumentElement,CancellationToken.None,1});restore.Invoke(parallel,new object[]{source.DocumentElement,CancellationToken.None,4});
            Check(parallel.ListaFicheros.Count==160&&parallel.ListaFicheros.Select(f=>f.FileID).SequenceEqual(serial.ListaFicheros.Select(f=>f.FileID)),"parallel restore preserves exact file order and identity");
            var a=new XmlDocument();a.AppendChild(serial.GuardarXML(a,true));var b=new XmlDocument();b.AppendChild(parallel.GuardarXML(b,true));
            Check(a.OuterXml==b.OuterXml,"parallel restore preserves every serialized field, chunk and ciphertext");
            using(var canceled=new CancellationTokenSource()) {
                canceled.Cancel();var unpublished=new Paquete();bool stopped=false;
                try {restore.Invoke(unpublished,new object[]{source.DocumentElement,canceled.Token,4});}catch(Exception ex){stopped=Unwrap(ex) is OperationCanceledException;}
                Check(stopped&&unpublished.ListaFicheros==null,"canceled restore never publishes a partial file list");
            }
            var empty=new XmlDocument();empty.LoadXml("<Paquete><ListaFicheros/></Paquete>");var emptyPackage=new Paquete();emptyPackage.CargarXML(empty.DocumentElement);
            Check(emptyPackage.ListaFicheros.Count==0,"empty queues still restore without array-bound errors");
        } finally {typeof(Paquete).GetMethod("DisposeLoadedQueue",shared).Invoke(null,new object[]{new[]{serial,parallel}});}
    }
    static void CancellableQueueReadCases() {
        string path=Path.Combine(root,"startup-locked-queue.xml");const string content="<ListaPaquetes/>";File.WriteAllText(path,content);
        using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None))using(var cancel=new CancellationTokenSource()) {
            cancel.CancelAfter(60);var watch=Stopwatch.StartNew();bool stopped=false;
            try {typeof(Paquete).GetMethod("CargarDesdeRutaCancelable",shared).Invoke(null,new object[]{path,cancel.Token});}catch(Exception ex){stopped=Unwrap(ex) is OperationCanceledException;}
            Check(stopped&&watch.ElapsedMilliseconds<1500,"startup lock retry is promptly cancellable");
        }
        Check(File.ReadAllText(path)==content&&!File.Exists(path+".bak"),"canceling startup does not write or rotate the queue");
        string configPath=Path.Combine(root,"startup-fast-config.xml");const string configXml="<Configuration><CondicionesAceptadas>true</CondicionesAceptadas></Configuration>";File.WriteAllText(configPath,configXml);
        using(var locked=new FileStream(configPath,FileMode.Open,FileAccess.Read,FileShare.None)) {
            var watch=Stopwatch.StartNew();var config=(Configuracion)Activator.CreateInstance(typeof(Configuracion),hidden,null,new object[]{configPath,1},System.Globalization.CultureInfo.InvariantCulture);
            Check(config.LoadTransientError&&watch.ElapsedMilliseconds<1500,"foreground config read defers a lock without the three-second sleep");
            config.GuardarXML(true);
        }
        Check(File.ReadAllText(configPath)==configXml&&!File.Exists(configPath+".bak"),"quick config loading retains write protection");
    }
    sealed class StartupPerformanceMain:Main {
        protected override void OnLoad(EventArgs e){}
        protected override void OnShown(EventArgs e){}
        protected override void OnFormClosing(FormClosingEventArgs e){}
        protected override void OnFormClosed(FormClosedEventArgs e){}
        protected override bool ShowWithoutActivation {get{return true;}}
    }
    static bool PumpStartupUntil(Func<bool> condition,int timeout=5000) {
        var watch=Stopwatch.StartNew();while(!condition()&&watch.ElapsedMilliseconds<timeout){Application.DoEvents();Thread.Sleep(10);}Application.DoEvents();return condition();
    }
    static void NonblockingLibraryManagerCases() {
        string path=Path.Combine(root,"startup-library-status.xml");File.WriteAllText(path,"<XML><Elements nextID='100'/></XML>");
        var library=FileBacked<StreamingLibrary>(path);typeof(StreamingLibrary).GetField("_LoadTransientError",hidden).SetValue(library,true);
        var field=typeof(StreamingLibraryManager).GetField("_StreamingLibrary",shared);object previous=field.GetValue(null);object gate=typeof(StreamingLibraryManager).GetField("_StreamingLibraryLock",shared).GetValue(null);
        using(var entered=new ManualResetEvent(false))using(var release=new ManualResetEvent(false)) {
            var owner=Task.Run(()=>{lock(gate){entered.Set();release.WaitOne();}});
            try {
                Check(entered.WaitOne(1000),"manager lock fixture holds an actual initialization lock");field.SetValue(null,library);
                var status=Task.Run(()=>StreamingLibraryManager.HasPendingRecovery());
                Check(status.Wait(1000)&&status.Result,"recovery status never waits for singleton initialization IO");
                var recovery=Task.Run(()=>StreamingLibraryManager.TryRecoverPending());
                Check(recovery.Wait(1000)&&recovery.Result&&!StreamingLibraryManager.HasPendingRecovery(),"library recovery uses its own load lock without holding the manager lock");
            }finally{field.SetValue(null,previous);release.Set();owner.Wait();}
        }
    }
    static void BackgroundStartupCases() {
        var oldContext=SynchronizationContext.Current;object oldInternal=typeof(InternalConfiguration).GetField("XML_CONFIG",shared).GetValue(null);
        var internalXml=new XmlDocument();internalXml.LoadXml("<Config><VERSION_UPDATE>0.0.0.0</VERSION_UPDATE></Config>");typeof(InternalConfiguration).GetField("XML_CONFIG",shared).SetValue(null,internalXml);
        try {
            foreach(string mode in new[]{"success","cancel","failure"})using(var main=new StartupPerformanceMain())using(var entered=new ManualResetEvent(false))using(var release=new ManualResetEvent(false)) {
                string configPath=Path.Combine(root,"startup-pipeline-"+mode+".xml");File.WriteAllText(configPath,"<Configuration><CondicionesAceptadas>true</CondicionesAceptadas><ComenzarDescargando>false</ComenzarDescargando><ResetearErroresMigratedV25>true</ResetearErroresMigratedV25></Configuration>");main.Config=FileBacked<Configuracion>(configPath);
                foreach(string method in new[]{"Translate","CrearMenus","InitWorkbench","InitBatchMenu","InitQuotaBanner","InitDownloadAreaLayout","DefinirColumnas","InitNavRail","PrepareStartupQueue"})InvokeMain(main,method);
                var workers=new List<InertStartupWorker>();foreach(string name in new[]{"_bgwComprobarMaxConexiones","_bgwActualizadorListaDescargas","_bgwActualizadorDatosDisco","_bgwDescompresor"}){var worker=new InertStartupWorker();workers.Add(worker);typeof(Main).GetField(name,hidden).SetValue(main,worker);}
                main.StartPosition=FormStartPosition.Manual;main.Location=new System.Drawing.Point(-20000,-20000);main.ShowInTaskbar=false;main.Show();main.Refresh();
                var persisted=new Paquete{Nombre="persisted",ListaFicheros=new List<Fichero>()};var pending=new Paquete{Nombre="pending",ListaFicheros=new List<Fichero>()};
                Func<CancellationToken,List<Paquete>> loader=token=>{entered.Set();WaitHandle.WaitAny(new[]{release,token.WaitHandle});token.ThrowIfCancellationRequested();if(mode=="failure")throw new InvalidDataException("fixture");return new List<Paquete>{persisted};};
                var task=(Task)InvokeMain(main,"LoadStartupQueueAsync",loader);
                try {
                    Check(entered.WaitOne(1000)&&!task.IsCompleted,"queue recovery runs in background: "+mode);
                    int beats=0;using(var timer=new System.Windows.Forms.Timer{Interval=10}){timer.Tick+=(s,e)=>beats++;timer.Start();Check(PumpStartupUntil(()=>beats>=3,1500),"UI remains responsive while queue recovery is blocked: "+mode);}
                    var flood=Task.Run(()=>{for(int i=0;i<100;i++)InvokeMain(main,"RefreshListaDescargas",false);});
                    Check(flood.Wait(1500),"periodic refresh never synchronously waits for the UI thread: "+mode);
                    Check((int)typeof(Main).GetField("_periodicListRefreshPending",hidden).GetValue(main)==1,"periodic UI updates coalesce to one pending callback: "+mode);
                    Check(PumpStartupUntil(()=>(int)typeof(Main).GetField("_periodicListRefreshPending",hidden).GetValue(main)==0),"coalesced refresh callback is released: "+mode);
                    main.StartDownload();InvokeMain(main,"ScheduleTick",null,EventArgs.Empty);InvokeMain(main,"CompleteStartup");
                    Check(workers.All(w=>w.Starts==0)&&!(bool)typeof(Main).GetField("_startupReady",hidden).GetValue(main),"loading gates download scheduling and all business workers: "+mode);
                    bool oldTransient=(bool)typeof(Paquete).GetField("_LoadTransientError",shared).GetValue(null);typeof(Paquete).GetField("_LoadTransientError",shared).SetValue(null,true);
                    try {InvokeMain(main,"GuardarFicheroDescargas");Check((DateTime)typeof(Main).GetField("UltimoGuardadoFichero",hidden).GetValue(main)==DateTime.MinValue,"loading cannot save an incomplete queue: "+mode);}finally{typeof(Paquete).GetField("_LoadTransientError",shared).SetValue(null,oldTransient);}
                    ((List<Paquete>)typeof(Main).GetField("ListaPaquetes",hidden).GetValue(main)).Add(pending);main.ProcessArgs(new[]{"pending-startup-argument"});
                    Check(((List<string[]>)typeof(Main).GetField("_pendingStartupArguments",hidden).GetValue(main)).Count==1,"incoming arguments are deferred during startup: "+mode);
                    if(mode=="cancel"){typeof(Main).GetField("Cerrando",hidden).SetValue(main,true);InvokeMain(main,"CancelStartupQueueLoad");}else release.Set();
                    Check(PumpStartupUntil(()=>task.IsCompleted),"startup continuation finishes without blocking UI: "+mode);task.GetAwaiter().GetResult();
                    var live=(List<Paquete>)typeof(Main).GetField("ListaPaquetes",hidden).GetValue(main);
                    if(mode=="success") {
                        Check(live.SequenceEqual(new[]{persisted,pending}),"publishing the persisted queue retains concurrent additions in order");
                        Check(PumpStartupUntil(()=>workers.All(w=>w.Starts==1)),"queue readiness starts each worker once");InvokeMain(main,"CompleteStartup");
                        Check(workers.All(w=>w.Starts==1)&&!(bool)typeof(Main).GetField("_startupQueueLoading",hidden).GetValue(main),"completed startup is idempotent");
                        Check(((List<string[]>)typeof(Main).GetField("_pendingStartupArguments",hidden).GetValue(main)).Count==0,"deferred arguments are drained after the real queue is ready");
                    }else if(mode=="cancel") Check(workers.All(w=>w.Starts==0)&&live.SequenceEqual(new[]{pending})&&(bool)typeof(Main).GetField("_startupQueueLoading",hidden).GetValue(main),"canceled startup cannot publish data, save or start workers: "+mode);
                    else Check(workers.All(w=>w.Starts==0)&&live.SequenceEqual(new[]{pending})&&!(bool)typeof(Main).GetField("_startupQueueLoading",hidden).GetValue(main)&&(bool)typeof(Paquete).GetField("_LoadTransientError",shared).GetValue(null),"failed startup releases the loading gate, arms write protection and cannot publish data, save or start workers: "+mode);
                }finally {
                    release.Set();InvokeMain(main,"CancelStartupQueueLoad");PumpStartupUntil(()=>task.IsCompleted);
                    var timer=typeof(Main).GetField("_recoveryTimer",hidden).GetValue(main) as System.Windows.Forms.Timer;if(timer!=null)timer.Dispose();
                    var completed=typeof(DescompresorController).GetEvent("DescompresionFinalizada");var handler=Delegate.CreateDelegate(completed.EventHandlerType,main,typeof(Main).GetMethod("DescompresionFinalizada_EventHandler",hidden));completed.RemoveEventHandler(DescompresorController.GetController(),handler);
                    foreach(var worker in workers)worker.Dispose();
                }
            }
        }finally{typeof(InternalConfiguration).GetField("XML_CONFIG",shared).SetValue(null,oldInternal);SynchronizationContext.SetSynchronizationContext(oldContext);}
    }
}
