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
    Private ReadOnly selection As PreflightSelection
    Private filtered As Boolean
    Public ReadOnly Rows As New List(Of Node)()

    Public Sub New(model As PreflightSelection)
        selection = model
        For Each entry As BatchLinkPreflight.Entry In selection.Entries
            Dim parent As Node = root
            Dim source As URLProcessor.FileURL = entry.Source
            If source IsNot Nothing Then
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
        AddHandler selection.SelectionChanged, AddressOf SelectionChanged
        Filter(False)
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
        filtered = isFiltered
        For Each node As Node In nodes
            node.SearchCollapsed = False
            node.Matches = False
            node.MatchCount = 0
            node.EligibleCount = 0
            node.SelectedCount = 0
            node.SizeBytes = 0
            node.UnknownSizes = 0
        Next
        For Each entry As BatchLinkPreflight.Entry In selection.Visible
            Dim leaf As Node = leaves(entry)
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
            If node.MatchCount = 0 Then Continue While
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
