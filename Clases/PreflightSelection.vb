Imports System.Collections.Generic

''' <summary>Selection and totals independent of native grid rows; O(1) per checkbox.</summary>
Public NotInheritable Class PreflightSelection
    Public Enum SelectionOperation
        SelectAll
        Clear
        Invert
    End Enum

    Public ReadOnly Entries As List(Of BatchLinkPreflight.Entry)
    Public ReadOnly Visible As New List(Of BatchLinkPreflight.Entry)()
    Public ReadOnly Property SelectedCount As Integer
    Public ReadOnly Property SelectedBytes As Decimal
    Public ReadOnly Property UnknownSizes As Integer
    Public ReadOnly Property DuplicateCount As Integer
    Public ReadOnly Property InvalidCount As Integer
    Public ReadOnly Property UnavailableCount As Integer
    Public Event SelectionChanged(entry As BatchLinkPreflight.Entry, delta As Integer)

    Public Sub New(input As List(Of BatchLinkPreflight.Entry))
        Entries = input
        For Each entry As BatchLinkPreflight.Entry In Entries
            Select Case entry.State
                Case BatchLinkPreflight.LinkState.AlreadyQueued, BatchLinkPreflight.LinkState.Duplicate : _DuplicateCount += 1
                Case BatchLinkPreflight.LinkState.Invalid : _InvalidCount += 1
                Case BatchLinkPreflight.LinkState.Unavailable : _UnavailableCount += 1
            End Select
            Dim selected As Boolean = entry.Selected AndAlso entry.CanAdd
            entry.Selected = False
            SetSelected(entry, selected)
        Next
        Filter("", "")
    End Sub

    Public Sub Filter(text As String, category As String)
        Visible.Clear()
        For Each entry As BatchLinkPreflight.Entry In Entries
            If text.Length > 0 AndAlso If(entry.Name, "").IndexOf(text, StringComparison.CurrentCultureIgnoreCase) < 0 AndAlso
                (entry.Source Is Nothing OrElse
                 (If(entry.Source.FolderName, "").IndexOf(text, StringComparison.CurrentCultureIgnoreCase) < 0 AndAlso
                  If(entry.Source.Path, "").IndexOf(text, StringComparison.CurrentCultureIgnoreCase) < 0)) Then Continue For
            If category.Length > 0 AndAlso entry.Category <> category Then Continue For
            Visible.Add(entry)
        Next
    End Sub

    Public Sub SetSelected(entry As BatchLinkPreflight.Entry, selected As Boolean)
        selected = selected AndAlso entry.CanAdd
        If entry.Selected = selected Then Return
        Dim delta As Integer = If(selected, 1, -1)
        entry.Selected = selected
        _SelectedCount += delta
        If entry.SizeBytes.HasValue Then
            _SelectedBytes += CDec(entry.SizeBytes.Value) * delta
        Else
            _UnknownSizes += delta
        End If
        RaiseEvent SelectionChanged(entry, delta)
    End Sub

    Public Sub Apply(operation As SelectionOperation, Optional visibleOnly As Boolean = True)
        For Each entry As BatchLinkPreflight.Entry In If(visibleOnly, Visible, Entries)
            SetSelected(entry, If(operation = SelectionOperation.Invert, Not entry.Selected, operation = SelectionOperation.SelectAll))
        Next
    End Sub
End Class
