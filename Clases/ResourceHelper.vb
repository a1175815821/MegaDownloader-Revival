Public Class ResourceHelper


    Public Shared Function GetResourceName(EndsWith As String) As String
        If String.IsNullOrEmpty(EndsWith) Then Return ""
        For Each resname As String In Reflection.Assembly.GetExecutingAssembly().GetManifestResourceNames
            If resname.ToUpper.EndsWith(EndsWith.ToUpper) Then
                Return resname
            End If
        Next
        Return ""
    End Function

End Class
