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

    Friend Shared Function OpenBundledDll(fileName As String) As IO.Stream
        Dim resourceName = GetResourceName(".Dependencies." & fileName & ".gz")
        If resourceName = "" Then Return Nothing
        Dim source = GetType(ResourceHelper).Assembly.GetManifestResourceStream(resourceName)
        Return New IO.Compression.GZipStream(source, IO.Compression.CompressionMode.Decompress)
    End Function

End Class
