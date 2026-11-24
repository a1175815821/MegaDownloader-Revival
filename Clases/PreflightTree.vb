Imports System.Collections.Generic
Imports System.Windows.Forms

''' <summary>Folder hierarchy with a virtual projection; collapsing a folder never changes its selection.</summary>
Public NotInheritable Class PreflightTree
    Public NotInheritable Class Node
        Public Name As String
        Public Parent As Node
        Public Entry As BatchLinkPreflight.Entry
        Public Expanded As Boolean
        Public Depth As Integer
        Public MatchCount As Integer
        Public EligibleCount As Integer
        Public SelectedCount As Integer
        Public SizeBytes As Decimal
        Public UnknownSizes As Integer
        Friend FolderListing As PublicFolderListing
        Friend FolderHandle As String
        Friend HasVisibleChildren As Boolean
        Friend Matches As Boolean
        Friend SearchCollapsed As Boolean
        Friend ReadOnly Folders As Dictionary(Of String, Node)
        Friend ReadOnly FolderChildren As List(Of Node)
        Friend ReadOnly FileChildren As List(Of Node)

        Public Sub New(Optional file As BatchLinkPreflight.Entry = Nothing)
            Entry = file
            If file Is Nothing Then
                Folders = New Dictionary(Of String, Node)(StringComparer.Ordinal)
                FolderChildren = New List(Of Node)()
                FileChildren = New List(Of Node)()
            End If
        End Sub

        Public ReadOnly Property IsFolder As Boolean
            Get
                Return Entry Is Nothing
            End Get
        End Property

        Public ReadOnly Property CheckState As CheckState
            Get
                If EligibleCount = 0 OrElse SelectedCount = 0 Then Return System.Windows.Forms.CheckState.Unchecked
                Return If(SelectedCount = EligibleCount, System.Windows.Forms.CheckState.Checked, System.Windows.Forms.CheckState.Indeterminate)
            End Get
        End Property
    End Class

    Private ReadOnly root As New Node With {.Depth = -1}
    Private ReadOnly nodes As New List(Of Node)()
    Private ReadOnly leaves As New Dictionary(Of BatchLinkPreflight.Entry, Node)()
    Private selection As PreflightSelection
    Private filtered As Boolean
    Public ReadOnly Rows As New List(Of Node)()
    Friend ReadOnly HasPublicFolders As Boolean
    Friend ReadOnly InitialPublicFolder As Node

    Friend ReadOnly Property FileRowCount As Integer
        Get
            Return leaves.Count
        End Get
    End Property

    Public Sub New(model As PreflightSelection)
        selection = model
        Dim listings As New Dictionary(Of PublicFolderListing, Dictionary(Of String, Node))()
        For Each entry As BatchLinkPreflight.Entry In selection.Entries
            Dim parent As Node = root
            Dim source As URLProcessor.FileURL = entry.Source
            If entry.FolderListing IsNot Nothing Then
                Dim folders As Dictionary(Of String, Node) = Nothing
                If Not listings.TryGetValue(entry.FolderListing, folders) Then
                    folders = BuildPublicFolders(entry.FolderListing)
                    listings.Add(entry.FolderListing, folders)
                End If
                If entry.IsFolderMarker Then Continue For
                If Not folders.TryGetValue(source.PublicFolderParent, parent) Then parent = folders(entry.FolderListing.RootHandle)
            ElseIf source IsNot Nothing Then
                If Not String.IsNullOrEmpty(source.FolderName) Then
                    Dim groupKey As String = "share:" & If(source.PreflightOrigin, "") & ChrW(0) & source.FolderName
                    parent = Folder(parent, groupKey, source.FolderName)
                End If
                For Each segment As String In If(source.Path, "").Split(New Char() {"\"c, "/"c}, StringSplitOptions.RemoveEmptyEntries)
                    parent = Folder(parent, "path:" & segment, segment)
                Next
            End If
            Dim leaf As New Node(entry) With {.Name = entry.Name, .Parent = parent, .Depth = parent.Depth + 1}
            parent.FileChildren.Add(leaf)
            nodes.Add(leaf)
            leaves(entry) = leaf
        Next
        HasPublicFolders = listings.Count > 0
        If listings.Count = 1 AndAlso root.FolderChildren.Count = 1 AndAlso root.FileChildren.Count = 0 Then
            InitialPublicFolder = root.FolderChildren(0)
            InitialPublicFolder.Expanded = True
        End If
        AddHandler selection.SelectionChanged, AddressOf SelectionChanged
        Filter(False)
    End Sub

    Private Function BuildPublicFolders(listing As PublicFolderListing) As Dictionary(Of String, Node)
        ' Link by remote handles, not by the potentially identical display paths.
        Dim folders As New Dictionary(Of String, Node)(listing.Directories.Count, StringComparer.Ordinal)
        For Each directory As PublicFolderDirectory In listing.Directories.Values
            Dim folder As New Node With {.Name = directory.Name, .FolderListing = listing, .FolderHandle = directory.Handle}
            folders.Add(directory.Handle, folder)
            nodes.Add(folder)
        Next
        For Each directory As PublicFolderDirectory In listing.Directories.Values
            Dim folder As Node = folders(directory.Handle)
            Dim parent As Node = If(directory.Handle = listing.RootHandle, root, folders(directory.ParentHandle))
            folder.Parent = parent
            parent.FolderChildren.Add(folder)
        Next
        Dim pending As New Stack(Of Node)()
        pending.Push(folders(listing.RootHandle))
        While pending.Count > 0
            Dim folder As Node = pending.Pop()
            folder.Depth = folder.Parent.Depth + 1
            For Each child As Node In folder.FolderChildren
                pending.Push(child)
            Next
        End While
        Return folders
    End Function

    Friend Sub RefreshSelection(model As PreflightSelection)
        ' Queue reconciliation emits the same entries in the same order. Keep the
        ' actual nodes so highlighting, expansion and the scroll anchor survive.
        RemoveHandler selection.SelectionChanged, AddressOf SelectionChanged
        For index As Integer = 0 To selection.Entries.Count - 1
            Dim previous As BatchLinkPreflight.Entry = selection.Entries(index)
            If previous.IsFolderMarker Then Continue For
            Dim leaf As Node = leaves(previous)
            Dim updated As BatchLinkPreflight.Entry = model.Entries(index)
            leaves.Remove(previous)
            leaves.Add(updated, leaf)
            leaf.Entry = updated
            leaf.Name = updated.Name
        Next
        selection = model
        AddHandler selection.SelectionChanged, AddressOf SelectionChanged
    End Sub

    Private Function Folder(parent As Node, key As String, name As String) As Node
        Dim result As Node = Nothing
        If Not parent.Folders.TryGetValue(key, result) Then
            result = New Node With {.Name = name, .Parent = parent, .Depth = parent.Depth + 1}
            parent.Folders.Add(key, result)
            parent.FolderChildren.Add(result)
            nodes.Add(result)
        End If
        Return result
    End Function

    Public Sub Filter(isFiltered As Boolean)
        Filter(isFiltered, "", False)
    End Sub

    Friend Sub Filter(isFiltered As Boolean, folderSearch As String, preserveSearchExpansion As Boolean)
        filtered = isFiltered
        For Each node As Node In nodes
            If Not preserveSearchExpansion Then node.SearchCollapsed = False
            node.HasVisibleChildren = False
            node.Matches = Not filtered AndAlso node.FolderListing IsNot Nothing
            node.MatchCount = 0
            node.EligibleCount = 0
            node.SelectedCount = 0
            node.SizeBytes = 0
            node.UnknownSizes = 0
        Next
        For Each entry As BatchLinkPreflight.Entry In selection.Visible
            Dim leaf As Node = Nothing
            If Not leaves.TryGetValue(entry, leaf) Then Continue For ' Empty listing marker.
            leaf.Matches = True
            Dim node As Node = leaf
            While node IsNot root
                node.MatchCount += 1
                If entry.CanAdd Then node.EligibleCount += 1
                If entry.Selected Then node.SelectedCount += 1
                If entry.SizeBytes.HasValue Then
                    node.SizeBytes += CDec(entry.SizeBytes.Value)
                Else
                    node.UnknownSizes += 1
                End If
                node = node.Parent
            End While
        Next
        If folderSearch.Length > 0 Then
            For Each folder As Node In nodes
                If folder.FolderListing Is Nothing OrElse folder.Name.IndexOf(folderSearch, StringComparison.CurrentCultureIgnoreCase) < 0 Then Continue For
                Dim ancestor As Node = folder
                While ancestor IsNot root AndAlso Not ancestor.Matches
                    ancestor.Matches = True
                    ancestor = ancestor.Parent
                End While
            Next
        End If
        For Each node As Node In nodes
            If (node.Matches OrElse node.MatchCount > 0) AndAlso node.Parent IsNot root Then node.Parent.HasVisibleChildren = True
        Next
        RebuildRows()
    End Sub

    Private Sub SelectionChanged(entry As BatchLinkPreflight.Entry, delta As Integer)
        Dim node As Node = Nothing
        If Not leaves.TryGetValue(entry, node) OrElse Not node.Matches Then Return
        While node IsNot root
            node.SelectedCount += delta
            node = node.Parent
        End While
    End Sub

    Public Sub Toggle(node As Node)
        If node Is Nothing OrElse Not node.IsFolder Then Return
        If filtered Then
            node.SearchCollapsed = Not node.SearchCollapsed
        Else
            node.Expanded = Not node.Expanded
        End If
        RebuildRows()
    End Sub

    Public Sub ExpandAll(expand As Boolean)
        For Each node As Node In nodes
            If Not node.IsFolder Then Continue For
            If filtered Then
                node.SearchCollapsed = Not expand
            Else
                node.Expanded = expand
            End If
        Next
        RebuildRows()
    End Sub

    Public Function IsExpanded(node As Node) As Boolean
        Return If(filtered, Not node.SearchCollapsed, node.Expanded)
    End Function

    Private Sub RebuildRows()
        Rows.Clear()
        Dim pending As New Stack(Of Node)()
        PushChildren(root, pending)
        While pending.Count > 0
            Dim node As Node = pending.Pop()
            If node.MatchCount = 0 AndAlso Not node.Matches Then Continue While
            Rows.Add(node)
            If node.IsFolder AndAlso IsExpanded(node) Then PushChildren(node, pending)
        End While
    End Sub

    Private Shared Sub PushChildren(parent As Node, pending As Stack(Of Node))
        For index As Integer = parent.FileChildren.Count - 1 To 0 Step -1
            pending.Push(parent.FileChildren(index))
        Next
        For index As Integer = parent.FolderChildren.Count - 1 To 0 Step -1
            pending.Push(parent.FolderChildren(index))
        Next
    End Sub

    Public Sub Apply(targets As IEnumerable(Of Node), operation As PreflightSelection.SelectionOperation)
        ' A folder and a highlighted child must never invert the same file twice.
        Dim highlighted As New HashSet(Of Node)(targets)
        Dim pending As New Stack(Of Node)()
        For Each node As Node In highlighted
            Dim ancestor As Node = node.Parent
            While ancestor IsNot Nothing AndAlso Not highlighted.Contains(ancestor)
                ancestor = ancestor.Parent
            End While
            If ancestor Is Nothing Then pending.Push(node)
        Next
        While pending.Count > 0
            Dim node As Node = pending.Pop()
            If node.MatchCount = 0 Then Continue While
            If node.IsFolder Then
                PushChildren(node, pending)
            ElseIf node.Matches Then
                Dim entry As BatchLinkPreflight.Entry = node.Entry
                selection.SetSelected(entry, If(operation = PreflightSelection.SelectionOperation.Invert, Not entry.Selected, operation = PreflightSelection.SelectionOperation.SelectAll))
            End If
        End While
    End Sub
End Class
