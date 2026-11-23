Imports System.IO

Friend Enum MegaDriveSubscriptionFileState
    PendingReview
    Queued
    Downloaded
End Enum

Friend NotInheritable Class MegaDriveSubscriptionFile
    Public Property Handle As ULong
    Public Property Name As String
    Public Property RelativePath As String
    Public Property Bytes As Long
    Public Property Modified As Long
    Public Property Updated As Boolean

    <Newtonsoft.Json.JsonIgnore>
    Public Property Status As MegaDriveSubscriptionFileState

    Friend Function SameVersion(node As MegaDriveNode) As Boolean
        Return node IsNot Nothing AndAlso Not node.IsFolder AndAlso node.Id = Handle AndAlso
            node.Bytes = Bytes AndAlso node.Modified = Modified AndAlso node.Name = Name
    End Function

    Friend Function Copy() As MegaDriveSubscriptionFile
        Return DirectCast(MemberwiseClone(), MegaDriveSubscriptionFile)
    End Function
End Class

Friend NotInheritable Class MegaDriveSubscription
    Public Property Folder As ULong
    Public Property Name As String
    Public Property Incoming As Boolean?
    Public Property Destination As String
    Public Property CheckMinutes As Integer?
    Public Property Extensions As String = ""
    Public Property AutoQueue As Boolean
    Public Property ScheduleFromUtc As Date
    Public Property LastCheckedUtc As Date?
    Public Property LastError As Integer
    Public Property Files As New List(Of MegaDriveSubscriptionFile)()
    Public Property FolderPaths As New Dictionary(Of ULong, String)()

    Friend Function Copy() As MegaDriveSubscription
        Dim result = DirectCast(MemberwiseClone(), MegaDriveSubscription)
        result.Files = Files.Select(Function(file) file.Copy()).ToList()
        result.FolderPaths = New Dictionary(Of ULong, String)(FolderPaths)
        Return result
    End Function

    Friend Shared Function NormalizeExtensions(value As String) As String
        Dim extensions = If(value, "").Split({","c, ";"c, " "c, ChrW(9)}, StringSplitOptions.RemoveEmptyEntries).
            Select(Function(extension) extension.TrimStart("*"c, "."c).ToLowerInvariant()).
            Where(Function(extension) extension.Length > 0).Distinct(StringComparer.Ordinal)
        Return String.Join(", ", extensions)
    End Function

    Friend Function NextCheckLocal() As Date?
        If Not CheckMinutes.HasValue Then Return Nothing
        Dim reference = ScheduleFromUtc
        If LastCheckedUtc.HasValue AndAlso LastCheckedUtc.Value > reference Then reference = LastCheckedUtc.Value
        Dim local = reference.ToLocalTime()
        Dim nextCheck = local.Date.AddMinutes(CheckMinutes.Value)
        Return If(nextCheck > local, nextCheck, nextCheck.AddDays(1))
    End Function

    Friend Sub ApplyCheck(snapshot As MegaDriveSnapshot, changes As MegaDriveChanges)
        Dim extensions As New HashSet(Of String)(Me.Extensions.Split({", "}, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase)
        Dim discovered = changes.Plan.Files.Where(Function(file) MatchesExtension(file.Key.Name, extensions)).
            Select(Function(file) New MegaDriveSubscriptionFile With {
                .Handle = file.Key.Id, .Name = file.Key.Name, .Bytes = file.Key.Bytes, .Modified = file.Key.Modified,
                .RelativePath = file.Value, .Updated = changes.Updated.Contains(file.Key.Id)}).ToList()
        Dim previous = Files.ToDictionary(Function(file) file.Handle)
        Dim hasNew = discovered.Any(Function(file)
                                        Dim prior As MegaDriveSubscriptionFile = Nothing
                                        Return Not previous.TryGetValue(file.Handle, prior) OrElse prior.Modified <> file.Modified OrElse
                                            prior.Bytes <> file.Bytes OrElse prior.Name <> file.Name
                                    End Function)
        If Not hasNew Then
            Dim active = New HashSet(Of ULong)(discovered.Select(Function(file) file.Handle))
            discovered.AddRange(Files.Where(Function(file)
                If active.Contains(file.Handle) Then Return False
                Dim node = snapshot.Find(file.Handle)
                Return file.SameVersion(node) AndAlso snapshot.Breadcrumbs(node.ParentId).Any(Function(parent) parent.Id = Folder) AndAlso
                    MatchesExtension(file.Name, extensions)
            End Function))
        End If
        Files = discovered
    End Sub

    Private Shared Function MatchesExtension(name As String, extensions As HashSet(Of String)) As Boolean
        If extensions.Count = 0 Then Return True
        Dim dot = name.LastIndexOf("."c)
        Return dot >= 0 AndAlso extensions.Contains(name.Substring(dot + 1))
    End Function

    Friend Function PlanSelected(selection As IEnumerable(Of MegaDriveSubscriptionFile)) As MegaDriveDownload
        Dim plan As New MegaDriveDownload()
        Dim directories As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each file In selection
            Dim node As New MegaDriveNode(file.Handle, Folder, file.Name, False, file.Bytes, file.Modified)
            plan.Files.Add(New KeyValuePair(Of MegaDriveNode, String)(node, file.RelativePath))
            plan.TotalBytes += file.Bytes
            Dim directory = file.RelativePath
            While directory <> "" AndAlso directories.Add(directory)
                plan.Directories.Add(directory)
                directory = Path.GetDirectoryName(directory)
            End While
        Next
        Return plan
    End Function

    Friend Function MatchesSettings(other As MegaDriveSubscription) As Boolean
        Return String.Equals(Destination, other.Destination, StringComparison.OrdinalIgnoreCase) AndAlso
            CheckMinutes.Equals(other.CheckMinutes) AndAlso AutoQueue = other.AutoQueue AndAlso Extensions = other.Extensions AndAlso
            ScheduleFromUtc = other.ScheduleFromUtc
    End Function

    Friend ReadOnly Property Caption As String
        Get
            Return If(String.IsNullOrEmpty(Name), Destination, Name)
        End Get
    End Property
End Class
