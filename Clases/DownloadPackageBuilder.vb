Imports System.Threading

''' <summary>Construct an unpublished package off the UI thread; create each directory once.</summary>
Public NotInheritable Class DownloadPackageBuilder
    Public Shared Function Build(URLs2 As Generic.List(Of URLProcessor.FileURL), packageName As String, destination As String,
                                 createDirectory As Boolean, extractArchives As Boolean, password As String, cancellation As CancellationToken,
                                 Optional progress As IProgress(Of Integer) = Nothing) As Paquete
        cancellation.ThrowIfCancellationRequested()

        ' Creamos el paquete
        Dim oPaquete As New Paquete
        oPaquete.ListaFicheros = New Generic.List(Of Fichero)()
        Dim directories As New Generic.HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim ensureDirectory As Action(Of String) = Sub(path)
                                                      If directories.Add(path) Then System.IO.Directory.CreateDirectory(path)
                                                  End Sub
        Try
            With oPaquete
                .Nombre = packageName
                .RutaLocal = destination
                .CrearSubdirectorio = createDirectory
                .PendienteNombrePaquete = String.IsNullOrEmpty(packageName)

                ' 文件夹分享的真名(MegaFolderHelper 展开时就解密带出了,此前被丢掉)。
                ' 用户填了包名不动;单条目也不动——那种包验名后按文件名命名并建目录是既有行为。
                Dim folderName As String = URLProcessor.GetCommonFolderName(URLs2)
                Dim folderNamePending As Boolean = .PendienteNombrePaquete AndAlso _
                                                   Not String.IsNullOrEmpty(folderName) AndAlso _
                                                   URLs2 IsNot Nothing AndAlso URLs2.Count > 1

                ' 散装多文件(所有条目都无相对子目录)+勾选创建目录+包名空:自动用"新建包"
                ' 建子目录,否则多文件无名包会直接散落下载目录(此前取首个文件名建目录即 issue #4)。
                ' 文件夹分享(任一 Path 非空)保持现状,不额外套顶层;单文件走验名流程。
                If .PendienteNombrePaquete AndAlso .CrearSubdirectorio AndAlso URLs2 IsNot Nothing AndAlso URLs2.Count > 1 Then
                    Dim allFlat As Boolean = True
                    For Each fu As URLProcessor.FileURL In URLs2
                        If Not String.IsNullOrEmpty(fu.Path) Then
                            allFlat = False
                            Exit For
                        End If
                    Next
                    If allFlat Then
                        ' 扁平文件夹分享也走这里:目录照建,只是名字从"新建包"换成真名(落盘形状不变)。
                        Dim autoName As String = folderName
                        If String.IsNullOrEmpty(autoName) Then
                            autoName = Language.GetText("New package")
                            If String.IsNullOrEmpty(autoName) Then autoName = "New package"
                        End If
                        .Nombre = autoName
                        .PendienteNombrePaquete = False
                    End If
                End If

                ' 带子目录的文件夹分享:真名同样先赋值再建目录,与 Main.ControlRemotoAgregarLinks 形状统一
                ' (此前建目录之后才回写显示名、刻意不套顶层目录,同一文件夹链接两个入口落盘形状不一致:
                ' Web 推送多套一层真名目录而 AddLinks 不套)。赋值必须在建目录之前,建目录分支才会用真名建顶层目录。
                If folderNamePending AndAlso .PendienteNombrePaquete Then
                    .Nombre = folderName
                    .PendienteNombrePaquete = False
                End If

                ' Creamos el directorio
                If .CrearSubdirectorio And Not String.IsNullOrEmpty(.Nombre) Then
                    Dim packageSegment As String = PathGuard.SanitizeFileName(.Nombre, "package")
                    .RutaLocal = PathGuard.GetSafePathUnderRoot(.RutaLocal, packageSegment, allowRoot:=False)
                    ensureDirectory(.RutaLocal)
                End If

                Log.WriteWarning("Adding package in " & .RutaLocal)

                .SetDescargaExtraccionAutomatica(password) = extractArchives
                Dim completed As Integer = 0
                For Each URL In URLs2
                    cancellation.ThrowIfCancellationRequested()

                    Dim ruta As String = PathGuard.GetSafePathUnderRoot(oPaquete.RutaLocal, If(URL.Path, String.Empty), allowRoot:=True)
                    ensureDirectory(ruta)

                    Dim URLFile As String = URL.URL
                    Dim Visible As Boolean = True
                    If Not String.IsNullOrEmpty(URLFile) AndAlso URLFile.Contains(Fichero.HIDDEN_LINK) Then
                        Visible = False
                        URLFile = URLFile.Replace(Fichero.HIDDEN_LINK, "")
                    End If

                    Dim oFichero As New Fichero(URLFile)
                    .ListaFicheros.Add(oFichero)
                    With oFichero
                        .LinkVisible = Visible
                        .RutaLocal = ruta
                        .RutaRelativa = URL.Path
                        .NombreFichero = If(Visible, URLFile, Fichero.HIDDEN_LINK_DESC)
                        If Not String.IsNullOrEmpty(URL.DisplayName) Then .NombreFichero = URL.DisplayName
                        If URL.SizeBytes.HasValue Then .TamanoBytes = URL.SizeBytes.Value
                        .FileID = Fichero.ExtraerFileID(URLFile)
                        .FileKey = Fichero.ExtraerFileKey(URLFile)
                        .SetDescargaExtraccionAutomatica(password) = extractArchives
                        Log.WriteWarning("Adding file to the new package: " & .FileID)
                    End With
                    completed += 1
                    ' Publish counters, not one marshalled UI callback per imported file.
                    If progress IsNot Nothing AndAlso (completed Mod 64 = 0 OrElse completed = URLs2.Count) Then progress.Report(completed)
                Next
            End With

            cancellation.ThrowIfCancellationRequested()
            Return oPaquete
        Catch
            For Each file As Fichero In oPaquete.ListaFicheros
                file.Dispose()
            Next
            Throw
        End Try
    End Function
End Class
