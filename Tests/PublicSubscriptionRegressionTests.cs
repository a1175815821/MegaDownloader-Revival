using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using MegaDownloader;

public partial class RegressionTests {
    const string PublicShareId = "TESTSHAR";
    const string PublicShareKey = "AAAAAAAAAAAAAAAAAAAAAA";
    const string ShareRoot = "AAAAAAAA";
    const string NotesFolder = "AAAAAAAB";
    const string EmptyFolder = "AAAAAAAC";
    const string VideoFolder = "AAAAAAAD";
    const string OldNotes = "AAAAAAAE";
    const string NewNotes = "AAAAAAAF";
    const string NewImage = "AAAAAAAG";
    const string VideoFile = "AAAAAAAH";
    const string LearningAccount = "learning-subscription@example.invalid";
    const string PersonalAccount = "personal-subscription@example.invalid";

    static Type SubscriptionType(string name) { return typeof(Main).Assembly.GetType("MegaDownloader."+name,true); }
    static object NewSubscriptionObject(string name,params object[] args) {
        return Activator.CreateInstance(SubscriptionType(name), BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,args,null);
    }
    static object SubscriptionProperty(object target,string name) { return target.GetType().GetProperty(name).GetValue(target,null); }
    static void SetSubscriptionProperty(object target,string name,object value) { target.GetType().GetProperty(name).SetValue(target,value,null); }
    static object CallSubscription(object target,string name,params object[] args) {
        return target.GetType().GetMethod(name,hidden).Invoke(target,args);
    }
    static object[] PublicReports(object settings) { return ((IEnumerable)SubscriptionProperty(settings,"Files")).Cast<object>().ToArray(); }

    static string PublicFolderPath(string fragment) { return "https://mega.nz/folder/"+PublicShareId+"#"+PublicShareKey+fragment; }

    static string PublicFixtureNode(string handle,string parent,string name,bool isFolder,int revision) {
        // Folder/file names are decrypted with a zero content key (EncryptedName), so the
        // wrapped key and its MetaMAC words stay zero; a revised file is distinguished by size.
        byte[] key = new byte[isFolder?16:32];
        string wrapped;
        using(var aes=Aes.Create()) {
            aes.Key=new byte[16]; aes.Mode=CipherMode.ECB; aes.Padding=PaddingMode.None;
            using(var encryptor=aes.CreateEncryptor()) wrapped=Criptografia.base64urlencode(encryptor.TransformFinalBlock(key,0,key.Length));
        }
        return "{\"h\":\""+handle+"\",\"p\":\""+parent+"\",\"t\":"+(isFolder?1:0)+",\"a\":\""+EncryptedName(name)+
            "\",\"k\":\""+ShareRoot+":"+wrapped+"\",\"s\":"+(isFolder?0:3+revision)+"}";
    }

    static object ParsePublicFixture(string subfolder,int firstRevision,bool newFiles) {
        var nodes=new List<string> {
            PublicFixtureNode(ShareRoot,"","Course",true,0),
            PublicFixtureNode(NotesFolder,ShareRoot,"Notes",true,0),
            PublicFixtureNode(EmptyFolder,NotesFolder,"Week 2",true,0),
            PublicFixtureNode(VideoFolder,ShareRoot,"Videos",true,0),
            PublicFixtureNode(OldNotes,NotesFolder,"First.pdf",false,firstRevision),
            PublicFixtureNode(VideoFile,VideoFolder,"Other.pdf",false,0)
        };
        if(newFiles) {
            nodes.Add(PublicFixtureNode(NewNotes,EmptyFolder,"Second.pdf",false,0));
            nodes.Add(PublicFixtureNode(NewImage,EmptyFolder,"Image.png",false,0));
        }
        var listing=NewSubscriptionObject("PublicFolderListing",PublicShareId,PublicShareKey);
        var parse=typeof(MegaFolderHelper).GetMethod("ParseFolderResponse",shared);
        var files=parse.Invoke(null,new object[]{PublicShareId,PublicShareKey,subfolder,"","[{\"f\":["+String.Join(",",nodes)+"]}]",false,null,CancellationToken.None,listing});
        listing.GetType().GetField("Files",hidden).SetValue(listing,files);
        return listing;
    }

    static void PublicSubscriptionCases() {
        var listing=ParsePublicFixture("",0,false);
        var listingType=listing.GetType();
        var isFolder=listingType.GetMethod("IsFolderLink",shared);
        Check((bool)isFolder.Invoke(null,new object[]{PublicFolderPath("")}) &&
            (bool)isFolder.Invoke(null,new object[]{"https://mega.nz/#F!"+PublicShareId+"!"+PublicShareKey}) &&
            !(bool)isFolder.Invoke(null,new object[]{PublicFolderPath("/file/"+OldNotes)}),"public root and legacy folder links are accepted, but file-only links are not subscribable");
        var directories=(IDictionary)listingType.GetField("Directories",hidden).GetValue(listing);
        Check(directories.Count==4 && directories.Contains(EmptyFolder),"public listing retains the actual node handle for an empty descendant");
        Check((string)CallSubscription(listing,"Breadcrumb",NotesFolder)=="Course / Notes","subscription title contains the share breadcrumb");
        var direct=ParsePublicFixture(NotesFolder,0,false);
        var id=(ulong)CallSubscription(listing,"SubscriptionId",NotesFolder);
        Check(id==(ulong)CallSubscription(direct,"SubscriptionId",NotesFolder) &&
            id!=(ulong)CallSubscription(listing,"SubscriptionId",VideoFolder),"root navigation and direct links identify the same folder, independently of sibling folders");
        var target=Path.Combine(root,"public-subscription-output");
        var storePath=Path.Combine(root,"public-subscription-store","subscriptions.xml");
        var item=NewSubscriptionObject("MegaDriveSubscription");
        SetSubscriptionProperty(item,"Folder",id);
        SetSubscriptionProperty(item,"Name","Course / Notes");
        SetSubscriptionProperty(item,"PublicShareUrl",PublicFolderPath("/folder/"+NotesFolder));
        SetSubscriptionProperty(item,"Destination",target);
        SetSubscriptionProperty(item,"DownloadEmail",LearningAccount);
        SetSubscriptionProperty(item,"Extensions","pdf");
        SetSubscriptionProperty(item,"CheckMinutes",540);
        SetSubscriptionProperty(item,"ScheduleFromUtc",new DateTime(2026,11,23,8,0,0,DateTimeKind.Local).ToUniversalTime());
        SetSubscriptionProperty(item,"AutoQueue",true);
        var paths=SubscriptionProperty(item,"FolderPaths");
        var scan=CallSubscription(listing,"Plan",NotesFolder,paths);
        Check(((IList)scan).Count==1 && ((IList)CallSubscription(listing,"Plan",EmptyFolder,new Dictionary<ulong,string>())).Count==0,
            "subscription scope excludes sibling files and successfully plans an empty folder");
        var store=NewSubscriptionObject("PublicFolderSubscriptions",storePath);
        var apply=store.GetType().GetMethod("ApplySnapshot",shared);
        var includeExisting=CallSubscription(item,"Copy");
        SetSubscriptionProperty(includeExisting,"IncludeExisting",true);
        apply.Invoke(null,new[]{includeExisting,scan,true});
        Check(PublicReports(includeExisting).Length==1,"including existing downloads exposes the existing match immediately");
        apply.Invoke(null,new[]{item,scan,true});
        Check(PublicReports(item).Length==0 && ((IDictionary)SubscriptionProperty(item,"KnownPublicFiles")).Count==1,
            "future-only first check sets a baseline with zero downloads");
        Check((bool)CallSubscription(store,"Create",item) && !(bool)CallSubscription(store,"Create",item) &&
            !File.ReadAllText(storePath).Contains(PublicShareKey),"subscription store deduplicates and protects the share key");
        var reopened=NewSubscriptionObject("PublicFolderSubscriptions",storePath);
        var previous=CallSubscription(reopened,"Find",id);
        Check((string)SubscriptionProperty(previous,"DownloadEmail")==LearningAccount && PublicReports(previous).Length==0,
            "file baselines and chosen identity survive restart without account-store dependency");
        var due=(DateTime)CallSubscription(previous,"NextCheckLocal");
        Check(due.Hour==9 && ((IList)CallSubscription(reopened,"Due",due.AddSeconds(-1))).Count==0 &&
            ((IList)CallSubscription(reopened,"Due",due.AddDays(2))).Count==1,"scheduled scans catch up once after the app misses a check");
        var next=CallSubscription(previous,"Copy");
        var updated=ParsePublicFixture(NotesFolder,1,true);
        scan=CallSubscription(updated,"Plan",NotesFolder,SubscriptionProperty(next,"FolderPaths"));
        apply.Invoke(null,new[]{next,scan,false});
        var discovered=PublicReports(next);
        var first=discovered.Single(file=>(string)SubscriptionProperty(file,"Name")=="First.pdf");
        var second=discovered.Single(file=>(string)SubscriptionProperty(file,"Name")=="Second.pdf");
        Check(discovered.Length==2 && (bool)SubscriptionProperty(first,"Updated") && !(bool)SubscriptionProperty(second,"Updated") &&
            (string)SubscriptionProperty(second,"RelativePath")=="Week 2","update is detected by version and new files keep their path; nonmatching PNG is excluded");
        apply.Invoke(null,new[]{next,scan,false});
        SetSubscriptionProperty(next,"LastAttemptUtc",due.AddDays(2).ToUniversalTime());
        SetSubscriptionProperty(next,"LastCheckedUtc",due.AddDays(2).ToUniversalTime());
        Check((bool)CallSubscription(reopened,"SaveCheck",previous,next) && PublicReports(next).Length==2 &&
            ((IList)CallSubscription(reopened,"Due",due.AddDays(2).AddMinutes(1))).Count==0,"repeat scan preserves pending review without duplicating discoveries");
        var oldInput=PublicSubscriptionInput(second);
        var weekTwo=PathGuard.GetSafePathUnderRoot(Path.Combine(target,"Week 2"),"");
        var package=DownloadPackageBuilder.Build(new List<URLProcessor.FileURL>{oldInput},"Notes",target,false,false,"",CancellationToken.None,null,LearningAccount);
        try {
            var queued=package.ListaFicheros.Single();
            Check(queued.RutaLocal==weekTwo,"the nested destination follows the subscription folder");
            Check(queued.MegaAccountEmail==LearningAccount,"the subscription identity is attached to the queued public file");
            Check((string)typeof(Fichero).GetProperty("MegaAccountDownloadLink",hidden).GetValue(queued,null)==PublicFolderPath("/file/"+NewNotes),
                "the account transfer uses the canonical public folder child link");
            var edit=CallSubscription(reopened,"Find",id);
            SetSubscriptionProperty(edit,"DownloadEmail",PersonalAccount);
            Check((bool)CallSubscription(reopened,"SaveSettings",edit) && queued.MegaAccountEmail==LearningAccount &&
                (string)SubscriptionProperty(CallSubscription(reopened,"Find",id),"DownloadEmail")==PersonalAccount &&
                PublicReports(CallSubscription(reopened,"Find",id)).Length==2,"changing subscription identity preserves queued tasks and pending discoveries");
            var xml=new XmlDocument();
            xml.AppendChild(queued.GuardarXML(xml,true));
            Check(!xml.OuterXml.Contains(PublicShareKey) && xml.DocumentElement["FixedDownloadIdentity"]!=null,"queue persists the identity binding without exposing the share key");
            using(var reloaded=new Fichero()) {
                reloaded.CargarXML(xml.DocumentElement);
                Check(reloaded.MegaAccountEmail==LearningAccount &&
                      (bool)typeof(Fichero).GetProperty("IsWaitingForFixedAccount",hidden).GetValue(reloaded,null) &&
                      (string)typeof(Fichero).GetProperty("MegaAccountDownloadLink",hidden).GetValue(reloaded,null)==PublicFolderPath("/file/"+NewNotes),
                    "signed-out fixed account waits and reloads the SDK link after queue restore");
            }
        } finally { foreach(var task in package.ListaFicheros) task.Dispose(); }
        var history=NewSubscriptionObject("MegaDriveHistory",Path.Combine(root,"public-subscription-deliveries"));
        var filePath=Path.Combine(weekTwo,"Second (2).pdf");
        File.WriteAllBytes(filePath,new byte[]{1,2,3});
        CallSubscription(history,"RecordPublic","fixture",SubscriptionProperty(second,"Handle"),filePath,SubscriptionProperty(second,"VersionKey"));
        var lookup=(Delegate)CallSubscription(history,"CreatePublicDeliveryLookup","fixture");
        Check((string)lookup.DynamicInvoke(second,Path.GetDirectoryName(filePath))==filePath,"report resolves the real collision-suffixed delivery path");
        using(var window=(Form)NewSubscriptionObject("MegaDriveSubscriptionsForm",null,
            NewSubscriptionObject("MegaAccountService",Path.Combine(root,"public-ui-account")),"",null)) {
            var reports=CallSubscription(reopened,"ReadAll");
            CallSubscription(window,"ShowReports",reports,id);
            var table=(DataGridView)window.GetType().GetField("report",hidden).GetValue(window);
            Check(table.RowCount==2 && Enumerable.Range(0,table.RowCount).Any(i=>Convert.ToString(table[2,i].Value)==Path.Combine("Week 2","Second.pdf")),
                "the real subscription window shows public discoveries with nested relative paths");
        }
        CallSubscription(reopened,"Remove",id);
        Check(((IList)CallSubscription(reopened,"ReadAll")).Count==0 && File.Exists(filePath),"unsubscribe removes tracking without deleting deliveries");
    }

    static URLProcessor.FileURL PublicSubscriptionInput(object report) {
        var file=new URLProcessor.FileURL((string)SubscriptionProperty(report,"PublicUrl"),(string)SubscriptionProperty(report,"RelativePath")) {
            DisplayName=(string)SubscriptionProperty(report,"Name"),SizeBytes=(long)SubscriptionProperty(report,"Bytes")
        };
        typeof(URLProcessor.FileURL).GetField("PublicFolderAccountLink",hidden).SetValue(file,SubscriptionProperty(report,"AccountLink"));
        typeof(URLProcessor.FileURL).GetField("FixedDownloadIdentity",hidden).SetValue(file,true);
        return file;
    }
}
