using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using MegaDownloader;

public partial class RegressionTests {
    static object InvokeMain(Main main,string method,params object[] args) {
        return typeof(Main).GetMethod(method,hidden).Invoke(main,args);
    }
    static T FileBacked<T>(string path) {
        return (T)Activator.CreateInstance(typeof(T),hidden,null,new object[]{path},CultureInfo.InvariantCulture);
    }
    static void DeliveryAuditFixCases(byte[] plain,string key,byte[] encrypted) {
        SpeedLimitRoundTrips();
        StopWhileRequestIsPending(plain,key,encrypted);
        DeferredConfigurationStartup();
        RecoverConcurrentLibraryEdits();
        RecoverLockedBackups();
        Print("delivery_audit_fixes","locale round trips, stop/restart, deferred startup, live services, concurrent library edits and backup recovery passed");
    }

    static void SpeedLimitRoundTrips() {
        var previous=Thread.CurrentThread.CurrentCulture;
        var format=typeof(Utils).GetMethod("FormatSpeedLimit",shared);
        var parse=typeof(Utils).GetMethod("TryParseSpeedLimit",shared);
        try {
            foreach(string culture in new[]{"de-DE","es-ES","fr-FR","en-US","zh-CN"}) {
                Thread.CurrentThread.CurrentCulture=CultureInfo.GetCultureInfo(culture);
                foreach(int kb in new[]{1,2,3,128,384,640,896,1152,1536,2048,104857600}) {
                    string text=(string)format.Invoke(null,new object[]{kb});
                    object[] args={text,0};
                    Check((bool)parse.Invoke(null,args)&&(int)args[1]==kb,"saved speed round-trips unchanged: "+culture+" "+kb+" KB/s");
                }
                foreach(bool packageMode in new[]{false,true})using(var file=new Fichero())using(var dialog=new PropiedadesDescarga()) {
                    file.FileID="SPEED128";file.NombreFichero="speed.bin";file.RutaLocal=root;file.LimiteVelocidad=128;
                    var package=new Paquete{Nombre="speed",RutaLocal=root};package.AgregarFichero(file);
                    dialog.Descarga=packageMode?(IDescarga)package:file;
                    InvokePrivate(dialog,"PropiedadesDescarga_Load",null,EventArgs.Empty);
                    string shown=dialog.Controls.Find("txtLimiteVelocidad",true).Single().Text;
                    object[] args={shown,0};
                    Check((bool)parse.Invoke(null,args)&&(int)args[1]==128,"properties show a saveable speed: "+culture+" package="+packageMode);
                    InvokePrivate(dialog,"btnGuardar_Click",null,EventArgs.Empty);
                    Check(file.LimiteVelocidad==128,"saving untouched properties preserves the speed: "+culture+" package="+packageMode);
                }
            }
        } finally { Thread.CurrentThread.CurrentCulture=previous; }
    }

    static void StopWhileRequestIsPending(byte[] plain,string key,byte[] encrypted) {
        object previousConfig=typeof(InternalConfiguration).GetField("XML_CONFIG",shared).GetValue(null);
        var previousContext=SynchronizationContext.Current;
        var pump=new CallbackPump();SynchronizationContext.SetSynchronizationContext(pump);
        using(var entered=new ManualResetEvent(false))using(var release=new ManualResetEvent(false))
        using(var slow=new LocalFileServer(encrypted))using(var api=new LocalFileServer(encrypted))using(var file=new Fichero()) {
            slow.HeadEntered=entered;slow.HeadRelease=release;
            api.ApiResponse="[{\"s\":"+plain.Length+",\"at\":\""+EncryptedName("retry.bin")+"\",\"g\":\""+api.Url+"\"}]";
            var internalXml=new XmlDocument();
            internalXml.LoadXml("<Config><URL_MEGA_API_DOWN_GET_G>"+api.Url+"</URL_MEGA_API_DOWN_GET_G><URL_MEGA_API_DOWN_GET_EU>"+api.Url+"</URL_MEGA_API_DOWN_GET_EU></Config>");
            typeof(InternalConfiguration).GetField("XML_CONFIG",shared).SetValue(null,internalXml);
            var config=(Configuracion)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Configuracion));
            config.ListaPreSharedKeys=new List<SecureString>();config.DownloadSchedule=new DownloadScheduleSettings();
            config.TamanoBufferKB=750;config.TamanoPaqueteKB=50;config.ConexionesPorFichero=1;config.DescargasSimultaneas=1;
            string directory=Path.Combine(root,"stop-pending-http");Directory.CreateDirectory(directory);
            file.FileID="TESTFILE";file.FileKey=key;file.RutaLocal=directory;file.NombreFichero="retry.bin";file.DescargaProcesada=true;
            var old=new FileDownloader(false);old.LocalDirectory=directory;old.DeleteFilesAfterCancel=false;
            old.AddFileInfo(file.FileID,key,slow.Url,file.NombreFichero,null,file.PartialFileName);
            typeof(Fichero).GetField("_Downloader",hidden).SetValue(file,old);
            int cancellations=0;file.CancellationComplete+=(s,e)=>Interlocked.Increment(ref cancellations);
            old.Started+=(s,e)=>InvokePrivate(file,"downloader_Started",s,e);
            old.CancelRequested+=(s,e)=>InvokePrivate(file,"downloader_CancelRequested",s,e);
            old.Canceled+=(s,e)=>{
                Check(!old.CanStart,"a stopped worker remains unavailable while cancellation callbacks are running");
                file.Stop();
                Check(cancellations==0&&file.EstadoDescarga!=Estado.EnCola,"a stop during the cancellation callback cannot publish a premature queue state");
            };
            old.Canceled+=(s,e)=>InvokePrivate(file,"downloader_Canceled",s,e);
            var main=QueueFixture(file);main.Config=config;
            typeof(Main).GetField("scheduleController",hidden).SetValue(main,new DownloadScheduleController());
            typeof(Main).GetField("scheduleActionLock",hidden).SetValue(main,new object());
            typeof(Main).GetField("NumeroConexionesMaxima",hidden).SetValue(main,10);
            try {
                old.Start();Check(entered.WaitOne(5000),"stop/restart fixture entered an actual HTTP request");
                main.StopDownload();InvokeMain(main,"PararDescargaFicheros");InvokeMain(main,"PararDescargaFicheros");
                Check(!old.CanStart&&cancellations==0,"repeated stop waits for the pending worker instead of announcing completion");
                main.StartDownload();InvokeMain(main,"PonerFicherosADescargar");
                Check(file.EstadoDescarga!=Estado.Erroneo&&api.ApiCalls==0,"play during cancellation does not start a second generation");
                release.Set();
                Check(pump.Until(()=>typeof(Fichero).GetField("_Downloader",hidden).GetValue(file)==null&&file.EstadoDescarga==Estado.EnCola),"cancelled downloader is detached before becoming queueable");
                Check(cancellations==1,"pending cancellation completes once");
                InvokeMain(main,"PonerFicherosADescargar");
                Check(pump.Until(()=>file.EstadoDescarga==Estado.Completado),"play resumes after cancellation has actually finished");
                Check(File.ReadAllBytes(Path.Combine(directory,"retry.bin")).SequenceEqual(plain),"restarted download delivers exact content");
                Check(api.ApiCalls==1,"completed cancellation permits one metadata refresh for the new generation");
                Check(pump.Until(()=>typeof(Fichero).GetField("_bgArranque",hidden).GetValue(file)==null),"restarted startup worker is drained");
            } finally {
                release.Set();
                typeof(InternalConfiguration).GetField("XML_CONFIG",shared).SetValue(null,previousConfig);
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }
    }

    sealed class InertStartupWorker:BackgroundWorker {
        public int Starts;
        protected override void OnDoWork(DoWorkEventArgs e) { Interlocked.Increment(ref Starts); }
    }
    static void DeferredConfigurationStartup() {
        object previousInternal=typeof(InternalConfiguration).GetField("XML_CONFIG",shared).GetValue(null);
        var previousProxy=WebRequest.DefaultWebProxy;
        var xml=new XmlDocument();xml.LoadXml("<Config><VERSION_UPDATE>0.0.0.0</VERSION_UPDATE></Config>");
        typeof(InternalConfiguration).GetField("XML_CONFIG",shared).SetValue(null,xml);
        try {
            foreach(bool autoStart in new[]{false,true}) {
                string path=Path.Combine(root,"deferred-config-"+autoStart+".xml");int port=FreePort();
                File.WriteAllText(path,"<Configuration><CondicionesAceptadas>true</CondicionesAceptadas><ComenzarDescargando>"+autoStart.ToString()+"</ComenzarDescargando><ServidorStreamingActivo>true</ServidorStreamingActivo><ServidorStreamingPuerto>"+port+"</ServidorStreamingPuerto><MaxConexionesGuardadas>7</MaxConexionesGuardadas><ResetearErroresMigratedV25>true</ResetearErroresMigratedV25></Configuration>");
                using(var main=new PresentationMain()) {
                    var workers=new List<InertStartupWorker>();
                    foreach(string name in new[]{"_bgwComprobarMaxConexiones","_bgwActualizadorListaDescargas","_bgwActualizadorDatosDisco","_bgwDescompresor"}) {
                        var worker=new InertStartupWorker();workers.Add(worker);typeof(Main).GetField(name,hidden).SetValue(main,worker);
                    }
                    typeof(Main).GetField("ListaPaquetes",hidden).SetValue(main,new List<Paquete>());
                    try {
                        using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None)) {
                            main.Config=FileBacked<Configuracion>(path);
                            Check(main.Config.LoadTransientError,"locked configuration remains pending");
                            InvokeMain(main,"CompleteStartup");main.StartDownload();InvokeMain(main,"ScheduleTick",null,EventArgs.Empty);
                            Check(!(bool)typeof(Main).GetField("_startupReady",hidden).GetValue(main)&&workers.All(w=>w.Starts==0),"no business workers start before configuration recovery");
                            Check(typeof(Main).GetField("EstadoAplicacion",hidden).GetValue(main).ToString()!="Descargando","unknown startup preferences do not start the queue");
                        }
                        Check(main.Config.TryRecoverFromTransientLock(),"actual configuration reload succeeds after unlock");
                        InvokeMain(main,"ResumeAfterConfigurationRecovery");
                        Check(SpinWait.SpinUntil(()=>workers.All(w=>w.Starts==1),5000),"recovery starts each worker once");
                        Check((typeof(Main).GetField("EstadoAplicacion",hidden).GetValue(main).ToString()=="Descargando")==autoStart,"recovery honors the saved automatic-start preference: "+autoStart);
                        Check((int)typeof(Main).GetField("NumeroConexionesMaxima",hidden).GetValue(main)==7,"recovery applies the saved connection budget");
                        var listeners=System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
                        Check(main.Config.ServidorStreamingActivo&&listeners.Any(p=>p.Port==port),"recovered streaming settings create the actual listener");
                        InvokeMain(main,"CompleteStartup");
                        Check(workers.All(w=>w.Starts==1),"startup completion is idempotent after recovery");
                    } finally {
                        ServidorWebController.StopWebServer();
                        var timer=typeof(Main).GetField("_recoveryTimer",hidden).GetValue(main) as System.Windows.Forms.Timer;
                        if(timer!=null)timer.Dispose();
                        foreach(var worker in workers)worker.Dispose();
                    }
                }
            }
        } finally {
            WebRequest.DefaultWebProxy=previousProxy;
            typeof(InternalConfiguration).GetField("XML_CONFIG",shared).SetValue(null,previousInternal);
        }
    }

    static LibraryElement LibraryEntry(string name) { return new LibraryElement{Name=name,Link=new SecureString()}; }
    static void RecoverConcurrentLibraryEdits() {
        string path=Path.Combine(root,"concurrent-library.xml");
        File.WriteAllText(path,"<XML><Elements nextID='102'><Element><ID>100</ID><Name>disk-a</Name></Element><Element><ID>101</ID><Name>disk-b</Name></Element></Elements></XML>");
        var library=FileBacked<StreamingLibrary>(path);
        using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None))library.LoadXML();
        Check(library.LoadTransientError,"library enters recovery for a locked source");
        bool responsive;
        lock(typeof(StreamingLibrary).GetField("_sync",hidden).GetValue(library)) {
            var status=Task.Run(()=>library.LoadTransientError);
            responsive=status.Wait(1000);
        }
        Check(responsive,"the UI recovery heartbeat does not wait for library serialization");
        var before=LibraryEntry("before");var removed=LibraryEntry("removed");library.AddNewElement(before);library.AddNewElement(removed);
        using(var entered=new ManualResetEvent(false))using(var release=new ManualResetEvent(false)) {
            var document=new XmlDocument();document.Load(path);
            Func<XmlDocument> loader=()=>{entered.Set();release.WaitOne(5000);return document;};
            var recovery=Task.Run(()=>(bool)typeof(StreamingLibrary).GetMethod("RecoverUsing",hidden).Invoke(library,new object[]{loader}));
            try {
                Check(entered.WaitOne(5000),"library recovery is suspended during the document load");
                var during=LibraryEntry("during");library.AddNewElement(during);
                Check(library.RemoveElement(removed),"library removal during recovery succeeds");
                library.SaveXML();
                Check(File.ReadAllText(path).Contains("disk-a")&&!File.ReadAllText(path).Contains("during"),"pending edits cannot replace the locked-source snapshot before recovery commits");
                release.Set();Check(recovery.Wait(5000)&&recovery.Result,"concurrent library recovery completes");
                string[] expected={"before","disk-a","disk-b","during"};
                Check(library.Elements().Select(e=>e.Name).OrderBy(x=>x).SequenceEqual(expected.OrderBy(x=>x)),"recovery merges live additions and preserves live removals");
                var saved=new XmlDocument();saved.Load(path);
                Check(saved.SelectNodes("XML/Elements/Element/Name").Cast<XmlNode>().Select(n=>n.InnerText).OrderBy(x=>x).SequenceEqual(expected.OrderBy(x=>x)),"the merged library is persisted without losing the concurrent edit");
                var after=LibraryEntry("after");library.AddNewElement(after);library.SaveXML();
                Check(library.Elements().Select(e=>e.ID).Distinct().Count()==5&&int.Parse(after.ID)>int.Parse(during.ID),"new entries use unique IDs after the merged library is committed");
            } finally { release.Set();recovery.Wait(5000); }
        }
    }

    static void RecoverLockedBackups() {
        Type atomic=typeof(Main).Assembly.GetType("MegaDownloader.AtomicFile");
        var load=atomic.GetMethod("TryLoadXmlWithRecoveryInfo",shared);
        var reload=atomic.GetMethod("TryReloadAfterTransientFailure",shared);
        foreach(bool missingPrimary in new[]{false,true}) {
            string path=Path.Combine(root,"backup-only-"+missingPrimary+".xml");
            if(!missingPrimary)File.WriteAllText(path,"<State>broken");
            File.WriteAllText(path+".bak","<State source='backup'/>");
            object[] args={path,null,false,false,null,1,0};
            using(var locked=new FileStream(path+".bak",FileMode.Open,FileAccess.Read,FileShare.None)) {
                Check(!(bool)load.Invoke(null,args)&&(bool)args[3],"a backup lock is classified as transient: missing="+missingPrimary);
            }
            object[] recovered={path,args[4],null};
            Check((bool)reload.Invoke(null,recovered)&&((XmlDocument)recovered[2]).DocumentElement.GetAttribute("source")=="backup","backup recovery retries the source that was actually locked: missing="+missingPrimary);
        }
        {
            string path=Path.Combine(root,"primary-lock-policy.xml");
            File.WriteAllText(path,"<State source='primary'/>");File.WriteAllText(path+".bak","<State source='stale'/>");
            object[] args={path,null,false,false,null,1,0};
            using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None))Check(!(bool)load.Invoke(null,args)&&(bool)args[3],"primary lock is classified as transient");
            File.WriteAllText(path,"<State>broken");object[] recovered={path,args[4],null};
            Check(!(bool)reload.Invoke(null,recovered),"a previously locked primary never silently falls back to a stale backup");
        }
        var previousProxy=WebRequest.DefaultWebProxy;
        try {
            string path=Path.Combine(root,"config-backup-recovery.xml");
            File.WriteAllText(path,"<Configuration>broken");
            File.WriteAllText(path+".bak","<Configuration><RutaDefecto>backup-path</RutaDefecto><ResetearErroresMigratedV25>true</ResetearErroresMigratedV25></Configuration>");
            Configuracion config;
            using(var locked=new FileStream(path+".bak",FileMode.Open,FileAccess.Read,FileShare.None))config=FileBacked<Configuracion>(path);
            Check(config.LoadTransientError&&config.TryRecoverFromTransientLock()&&config.RutaDefecto=="backup-path","configuration recovers from an unlocked backup through its public recovery path");
            config.GuardarXML(true);var saved=new XmlDocument();saved.Load(path);
            Check(saved.DocumentElement.SelectSingleNode("RutaDefecto").InnerText=="backup-path","recovered configuration can be saved without writing defaults");
            string libraryPath=Path.Combine(root,"library-backup-recovery.xml");
            File.WriteAllText(libraryPath,"<XML>broken");File.WriteAllText(libraryPath+".bak","<XML><Elements nextID='2'><Element><ID>1</ID><Name>backup-movie</Name></Element></Elements></XML>");
            var library=FileBacked<StreamingLibrary>(libraryPath);
            using(var locked=new FileStream(libraryPath+".bak",FileMode.Open,FileAccess.Read,FileShare.None))library.LoadXML();
            library.AddNewElement(LibraryEntry("pending-movie"));
            Check(library.TryRecoverFromTransientLock()&&!library.LoadTransientError&&library.Elements().Count==2,"library backup recovery retains pending additions");
            var persisted=new XmlDocument();persisted.Load(libraryPath);
            Check(persisted.SelectNodes("XML/Elements/Element").Count==2,"library backup recovery writes both saved and pending entries");
        } finally { WebRequest.DefaultWebProxy=previousProxy; }
        var fields=new[]{"_LoadTransientError","_LoadCorruptError","_PreserveBackupOnNextSave","_LastSavedXML","_TransientSkipLogged"}.Select(n=>typeof(Paquete).GetField(n,shared)).ToArray();
        object[] originals=fields.Select(f=>f.GetValue(null)).ToArray();
        try {
            string path=Path.Combine(root,"queue-backup-recovery.xml");
            File.WriteAllText(path,"<ListaPaquetes>broken");File.WriteAllText(path+".bak","<ListaPaquetes><Paquete><Nombre>backup-package</Nombre><ListaFicheros/></Paquete></ListaPaquetes>");
            using(var locked=new FileStream(path+".bak",FileMode.Open,FileAccess.Read,FileShare.None))typeof(Paquete).GetMethod("CargarDesdeRuta",shared).Invoke(null,new object[]{path});
            var restored=(List<Paquete>)typeof(Paquete).GetMethod("TryReloadFromPath",shared).Invoke(null,new object[]{path});
            Check(restored.Count==1&&restored[0].Nombre=="backup-package"&&Paquete.HasTransientLoadError,"queue backup is recovered with protection retained until merge");
            Paquete.CommitTransientRecovery();Check(!Paquete.HasTransientLoadError,"queue backup recovery unlocks writes only at commit");
        } finally { for(int i=0;i<fields.Length;i++)fields[i].SetValue(null,originals[i]); }
    }
}
