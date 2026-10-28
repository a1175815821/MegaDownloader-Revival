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
        ArchiveEdges();
        PreserveCompletedName(plain,key,encrypted);
        // Cross multiple streaming buffers and end in a partial AES block.
        byte[] large=Enumerable.Range(0,65539).Select(i=>(byte)(i%251+1)).ToArray();
        string largeKey=MakeKey(large);
        using(var server=new LocalFileServer(Encrypt(large,largeKey))) StreamingCases(server,large,largeKey);
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
