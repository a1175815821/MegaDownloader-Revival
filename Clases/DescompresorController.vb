Imports System.ComponentModel
Imports SharpCompress.Archive
Imports SharpCompress.Archive.IArchiveEntryExtensions
Imports SharpCompress.Reader
Imports SharpCompress.Archive.Rar.RarArchiveExtensions
Imports SharpCompress.Reader.Rar
Imports SharpCompress.Common
Imports System.IO


Public Class DescompresorController

    Public Class QueueItem
        Public Path As String
        Public Password As String
        Public CreateDirectory As Boolean
    End Class

#Region "Región Shared"
    Friend Shared Mutex As New System.Threading.Mutex()

    Private Shared _Controller As DescompresorController
    Public Shared Function GetController() As DescompresorController
        Mutex.WaitOne()
        If _Controller Is Nothing Then
            _Controller = New DescompresorController
        End If
        Mutex.ReleaseMutex()
        Return _Controller
    End Function

#End Region

#Region "Región privada"

    ''' <summary>
    ''' Cola de rutas de elementos a descomprimir (pj "C:\temp\a.rar", "C:\temp\b.rar", etc))
    ''' </summary>
    ''' <remarks>La clave es el código DEN, el valor indica la ruta y si ha de crear directorio o no</remarks>
    Private _colaElementos As Generic.Dictionary(Of String, QueueItem)
    Private _codigoElementoActual As String
    Private _pathElementoActual As String
    Private _passwordElementoActual As String
    Private _crearDirectorio As Boolean
    Private _cancelRequested As Boolean

    Private _ExtensionesSoportadas As Generic.List(Of String)


    ' En estas variables (_TamanoTotalExtraido, etc) se guardará el progreso actual

    Friend _TamanoTotal As System.Nullable(Of Long) ' Tamaño total de los ficheros cuando se descompriman
    Friend _TamanoTotalExtraido As System.Nullable(Of Long) ' Tamaño total de los ficheros ya descomprimidos completamente
    Friend _FicActTamanoTotal As System.Nullable(Of Long) ' Tamaño total del fichero que se está descomprimiendo
    Friend _FicActExtraido As System.Nullable(Of Long) ' Bytes extraidos del fichero que se está descomprimiendo
    Friend _FicActNombre As String ' Nombre del fichero que se está descomprimiendo



    Private Sub New()
        Mutex.WaitOne()
        _colaElementos = New Generic.Dictionary(Of String, QueueItem)()
        _codigoElementoActual = Nothing
        _pathElementoActual = Nothing
        _passwordElementoActual = Nothing
        _ExtensionesSoportadas = New Generic.List(Of String)
        With _ExtensionesSoportadas
            .Add("7z")
            .Add("rar")
            .Add("tar")
            .Add("zip")
        End With
        Mutex.ReleaseMutex()
    End Sub

    Public Sub RequestCancel()
        Mutex.WaitOne()
        Try
            _cancelRequested = True
        Finally
            Mutex.ReleaseMutex()
        End Try
    End Sub

    Private Function IsCancelRequested() As Boolean
        Mutex.WaitOne()
        Try
            Return _cancelRequested
        Finally
            Mutex.ReleaseMutex()
        End Try
    End Function

    Private Function PonerElementoAProcesar() As Boolean
        Mutex.WaitOne()
        Try
            If Not String.IsNullOrEmpty(_pathElementoActual) Or _colaElementos.Count = 0 Then
                Return False ' Ya hay un elemento procesando o la cola está vacía
            Else
                _codigoElementoActual = _colaElementos.Keys(0)
                _crearDirectorio = _colaElementos(_codigoElementoActual).CreateDirectory
                _pathElementoActual = _colaElementos(_codigoElementoActual).Path
                _passwordElementoActual = _colaElementos(_codigoElementoActual).Password
                _colaElementos.Remove(_codigoElementoActual)
                Return True
            End If
        Finally
            Mutex.ReleaseMutex()
        End Try
    End Function

    Private Sub ProcesarElemento(ByRef Cancel As Boolean)
        Try
            Mutex.WaitOne()
            If String.IsNullOrEmpty(Me._pathElementoActual) Then
                Exit Sub
            End If
        Finally
            Mutex.ReleaseMutex()
        End Try

        Log.WriteInfo("Extracting '" & _codigoElementoActual & "'")
        Dim Sw As New System.Diagnostics.Stopwatch
        Sw.Start()

        Dim Fichero As String = ""
        Dim Directorio As String = ""
        Dim FicheroSinExtension As String = ""

        If Not ObtenerNombres(_pathElementoActual, Directorio, Fichero, FicheroSinExtension, False, 0) Then
            ' Elemento inválido
            Mutex.WaitOne()
            Me._pathElementoActual = Nothing
            Me._passwordElementoActual = Nothing
            Mutex.ReleaseMutex()

            Log.WriteWarning("Decompressor: invalid element, discarding: '" & _codigoElementoActual & "'")

            Exit Sub
        End If

        ' Multipart 7z volumes (.7z.001): ObtenerNombres strips only the numeric
        ' suffix, leaving "name.7z" — drop the ".7z" so the extraction folder is
        ' named after the archive like every other format.
        If System.Text.RegularExpressions.Regex.IsMatch(_pathElementoActual, "\.7z\.\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase) _
           AndAlso FicheroSinExtension.ToLower.EndsWith(".7z") Then
            FicheroSinExtension = FicheroSinExtension.Substring(0, FicheroSinExtension.Length - 4)
        End If

        Dim DirectorioExtraccion As String = Directorio
        If _crearDirectorio Then
            Dim safeFolder As String = PathGuard.SanitizeFileName(FicheroSinExtension, "extracted")
            DirectorioExtraccion = PathGuard.GetSafePathUnderRoot(Directorio, safeFolder, allowRoot:=False)
        End If
        Directory.CreateDirectory(DirectorioExtraccion)

        Dim desc As New Descompressor(_pathElementoActual, DirectorioExtraccion, _passwordElementoActual)
        Dim extractOk As Boolean = False

        Dim extractThread As New Threading.Thread(AddressOf desc.Extract)
        extractThread.Priority = Threading.ThreadPriority.BelowNormal
        extractThread.IsBackground = True
        extractThread.Start()
        While Not extractThread.Join(500)
            If Cancel OrElse IsCancelRequested() Then
                ' Cooperative cancel only — do not Thread.Abort (unsafe mid-write)
                desc.CancelRequested = True
                Log.WriteWarning("Decompressor: cancel requested for '" & _codigoElementoActual & "'; waiting for worker to stop.")
                extractThread.Join(5000)
                Exit While
            End If
        End While

        Dim extractErrorMessage As String = ""
        If desc.Exception IsNot Nothing Then
            Log.WriteError("Decompressor: Error extracting '" & _codigoElementoActual & "': " & Log.SafeException(desc.Exception))
            extractOk = False
            extractErrorMessage = desc.Exception.Message
        ElseIf Cancel OrElse desc.CancelRequested OrElse IsCancelRequested() Then
            extractOk = False
            extractErrorMessage = "Extraction cancelled."
        Else
            extractOk = True
        End If
        Sw.Stop()

        If extractOk Then
            Log.WriteInfo("Element '" & _codigoElementoActual & "' extracted in " & Sw.ElapsedMilliseconds & "ms")
        Else
            Log.WriteWarning("Element '" & _codigoElementoActual & "' extraction failed or cancelled.")
        End If

        RaiseEvent DescompresionFinalizada(_codigoElementoActual, extractOk, extractErrorMessage)

        ' Hemos terminado
        Mutex.WaitOne()
        Me._pathElementoActual = Nothing
        Me._passwordElementoActual = Nothing
        Me._codigoElementoActual = Nothing
        Mutex.ReleaseMutex()


    End Sub



    Private Shared Function ObtenerNombres(ByVal Path As String, _
                                           ByRef Directorio As String, _
                                           ByRef Fichero As String, _
                                           ByRef FicheroSinExtension As String, _
                                           ByRef IsRARPart As Boolean, _
                                           ByRef RARPartLength As Integer) As Boolean
        Dim fi As New FileInfo(Path) ' C:\MyDirectory\MySubDirectory\MyFileName.txt
        If fi.Exists Then
            Fichero = fi.Name '  MyFileName.txt
            If Fichero.Contains(".") Then
                FicheroSinExtension = Fichero.Substring(0, Fichero.LastIndexOf("."c)) ' MyFileName
            Else ' No se debería dar...
                FicheroSinExtension = Fichero
            End If
            Directorio = fi.DirectoryName  ' C:\MyDirectory\MySubDirectory

            ' Caso fichero.part01.rar!!
            If Fichero.ToLower.EndsWith(".rar") And FicheroSinExtension.Contains(".") Then
                Dim fin As String = "" & FicheroSinExtension.Substring(FicheroSinExtension.LastIndexOf("."c) + 1)
                If fin.Length > 4 AndAlso fin.ToLower.Substring(0, 4) = "part" AndAlso IsNumeric(fin.ToLower.Substring(4)) Then
                    FicheroSinExtension = FicheroSinExtension.Substring(0, FicheroSinExtension.LastIndexOf("."c))
                    RARPartLength = fin.ToLower.Substring(4).Length
                    IsRARPart = True
                Else
                    IsRARPart = False
                End If
            Else
                IsRARPart = False
            End If

            Return True
        Else
            Return False
        End If
    End Function


#End Region

#Region "Región pública"

    Public Event DescompresionFinalizada(ByVal Code As String, ByVal Success As Boolean, ByVal ErrorMessage As String)

    ' Tamaño total de los ficheros dentro del elemento que se está descomprimiendo
    Public ReadOnly Property EleActual_TamanoTotal As System.Nullable(Of Long)
        Get
            Mutex.WaitOne()
            Try
                Return _TamanoTotal
            Finally
                Mutex.ReleaseMutex()
            End Try
        End Get
    End Property


    ' Tamaño total de los ficheros ya descomprimidos completamente dentro del elemento que se está descomprimiendo
    Public ReadOnly Property EleActual_TamanoTotalExtraido As System.Nullable(Of Long)
        Get
            Mutex.WaitOne()
            Try
                Return _TamanoTotalExtraido
            Finally
                Mutex.ReleaseMutex()
            End Try
        End Get
    End Property

    ' Ruta del elemento que se está descomprimiendo
    Public ReadOnly Property EleActual_Ruta As String
        Get
            Mutex.WaitOne()
            Try
                Return _pathElementoActual
            Finally
                Mutex.ReleaseMutex()
            End Try
        End Get
    End Property

    ' Codigo del elemento que se está descomprimiendo
    Public ReadOnly Property EleActual_Codigo As String
        Get
            Mutex.WaitOne()
            Try
                Return _codigoElementoActual
            Finally
                Mutex.ReleaseMutex()
            End Try
        End Get
    End Property

    ' Tamaño total del fichero que se está descomprimiendo
    Public ReadOnly Property EleActual_FicActTamano As System.Nullable(Of Long)
        Get
            Mutex.WaitOne()
            Try
                Return _FicActTamanoTotal
            Finally
                Mutex.ReleaseMutex()
            End Try
        End Get
    End Property

    ' Bytes extraidos del fichero que se está descomprimiendo
    Public ReadOnly Property EleActual_FicActExtraido As System.Nullable(Of Long)
        Get
            Mutex.WaitOne()
            Try
                Return _FicActExtraido
            Finally
                Mutex.ReleaseMutex()
            End Try
        End Get
    End Property

    ' Nombre del fichero que se está descomprimiendo
    Public ReadOnly Property EleActual_FicActNombre As String
        Get
            Mutex.WaitOne()
            Try
                Return _FicActNombre
            Finally
                Mutex.ReleaseMutex()
            End Try
        End Get
    End Property



    Public Function AgregarElemento(ByVal Code As String, ByVal Path As String, ByVal CrearDirectorio As Boolean, Password As String) As Boolean
        ' Comprobamos que el path no sea nulo, que el fichero exista, y tenga una extensión soportada
        If String.IsNullOrEmpty(Path) Then Return False
        If Not File.Exists(Path) Then Return False
        ' Solo admite 7z, rar, tar, zip
        Dim FicheroSoportado As Boolean = False
        For Each extension As String In _ExtensionesSoportadas
            If Path.ToLower.EndsWith("." & extension) Then
                FicheroSoportado = True
            End If
        Next
        ' Multipart 7z volumes (.7z.001, .7z.002, ...) are also supported via the
        ' 7-Zip CLI — queue the first volume.
        If Not FicheroSoportado AndAlso System.Text.RegularExpressions.Regex.IsMatch(Path.ToLower, "\.7z\.\d+$") Then
            FicheroSoportado = True
        End If
        If Not FicheroSoportado Then Return False


        Dim Fichero As String = ""
        Dim Directorio As String = ""
        Dim FicheroSinExtension As String = ""
        Dim IsPartRAR As Boolean = False
        Dim RARPartLength As Integer = 0

        If ObtenerNombres(Path, Directorio, Fichero, FicheroSinExtension, IsPartRAR, RARPartLength) Then

            If IsPartRAR Then
                ' Si es un RAR multivolumen intentamos poner en la cola tan solo el primer rar, si existe, claro
                Dim Path2 As String = IO.Path.Combine(Directorio, FicheroSinExtension) & ".part" & "1".PadLeft(RARPartLength, "0"c) & ".rar"
                If File.Exists(Path2) Then Path = Path2 ' Si no existe ya dará error al intentar descomprimir...
            End If

            ' Multipart 7z: queue the first volume, like the RAR case above.
            Dim m7z As System.Text.RegularExpressions.Match = System.Text.RegularExpressions.Regex.Match(Path, "\.7z\.(\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            If m7z.Success Then
                Dim primera7z As String = Path.Substring(0, m7z.Index) & ".7z.001"
                If Not String.Equals(Path, primera7z, StringComparison.OrdinalIgnoreCase) AndAlso File.Exists(primera7z) Then
                    Path = primera7z
                End If
            End If

            ' Comprobamos si ya existe en la cola
            Mutex.WaitOne()
            Try
                For Each key As String In _colaElementos.Keys
                    If _colaElementos(key).Path = Path Then
                        Log.WriteInfo("File '" & Path & "' for element '" & Code & "' is already in queue.")
                        Return False
                    End If
                Next
            Finally
                Mutex.ReleaseMutex()
            End Try

            Log.WriteInfo("Adding to decompression queue element '" & Code & "' (file '" & Path & "')")

            Mutex.WaitOne()
            If Not _colaElementos.ContainsKey(Code) Then _colaElementos.Add(Code, New QueueItem With {.Path = Path, .CreateDirectory = CrearDirectorio, .Password = Password})
            Mutex.ReleaseMutex()

            Return True
        Else
            Return False
        End If

    End Function

    Public Shared Sub DescompresorController_DoWork(sender As Object, e As DoWorkEventArgs)
        Try
            Log.WriteWarning("Starting worker bgwDescompresor")
            Dim worker As BackgroundWorker = CType(sender, BackgroundWorker)

            While Not worker.CancellationPending

                If GetController.PonerElementoAProcesar Then

                    GetController.ProcesarElemento(worker.CancellationPending)

                End If

                If worker.CancellationPending Then
                    Exit While
                End If

                System.Threading.Thread.Sleep(600)
            End While

            Log.WriteWarning("Finishing worker bgwDescompresor")
        Catch ex As Exception
            Log.WriteError("Error on worker bgwDescompresor: " & ex.ToString)
        End Try
    End Sub

    Public Function GetCola() As Generic.List(Of String)
        Mutex.WaitOne()
        Try
            Dim l As New Generic.List(Of String)
            For Each key As String In _colaElementos.Keys
                l.Add(_colaElementos(key).Path)
            Next
            Return l
        Finally
            Mutex.ReleaseMutex()
        End Try
    End Function

    ''' <summary>
    ''' Indica si el proceso está ocupado descomprimiendo un fichero o tiene elementos en cola pendientes de ser procesados
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function Ocupado() As Boolean
        Mutex.WaitOne()
        Try
            Return Not String.IsNullOrEmpty(_pathElementoActual) Or _colaElementos.Count > 0
        Finally
            Mutex.ReleaseMutex()
        End Try
    End Function

#End Region

#Region "Clase descompresora"




    Private Class Descompressor


        Public Password As String
        Private PathFichero As String
        Private PathExtraccion As String
        Public Exception As Exception
        Public CancelRequested As Boolean

        Public Sub New(ByVal _Fichero As String, ByVal _PathExtraccion As String, ByVal _Password As String)

            PathFichero = _Fichero
            PathExtraccion = _PathExtraccion
            Password = _Password
            CancelRequested = False

            If String.IsNullOrEmpty(Password) Then Password = Nothing ' Evitamos string.empty
        End Sub

        Private Shared Function getIArchive(PathFichero As String, Password As String) As IArchive

            If PathFichero.ToUpper.EndsWith(".RAR") Then
                Return SharpCompress.Archive.Rar.RarArchive.Open(PathFichero, password:=Password)
            ElseIf PathFichero.ToUpper.EndsWith(".ZIP") Then
                Return SharpCompress.Archive.Zip.ZipArchive.Open(PathFichero, password:=Password)
            Else
                Return ArchiveFactory.Open(PathFichero)
            End If

        End Function


        Public Sub Extract()
            Try


                ' 7z archives (single .7z or multipart .7z.001/.002/...) are handled by
                ' the external 7-Zip CLI — SharpCompress does not support the 7z container
                ' format at all, which made every .7z extraction fail. Prefer a system
                ' 7-Zip install; fall back to the embedded 7zr.exe (public domain).
                If PathFichero.ToLower.EndsWith(".7z") OrElse PathFichero.ToLower.Contains(".7z.") Then
                    Extract7z()
                    Return
                End If

                Using archive As IArchive = getIArchive(PathFichero, Password)

                    If archive.IsComplete Then

                        If TypeOf archive Is SharpCompress.Archive.Rar.RarArchive AndAlso _
                              CType(archive, SharpCompress.Archive.Rar.RarArchive).IsMultipartVolume() AndAlso _
                              Not CType(archive, SharpCompress.Archive.Rar.RarArchive).IsFirstVolume() Then
                            Exit Sub
                        End If


                        If (archive.IsSolid Or Not String.IsNullOrEmpty(Password)) And TypeOf archive Is SharpCompress.Archive.Rar.RarArchive Then

                            ' No nos sirve el ArchiveFactory... debemos usar el reader, pero solo para ficheros solidos RAR
                            ' Debemos pasar la lista con todos los part... pero como hemos comprobado antes
                            ' que "IsComplete" entonces estamos seguros que los tenemos todos...
                            Dim ListaFicheros As New Generic.List(Of Stream)
                            ListaFicheros.Add(File.OpenRead(PathFichero))
                            Try
                                Dim LengthPart As Integer = 0
                                If PathFichero.ToLower.EndsWith("part1.rar") Then
                                    LengthPart = 1
                                ElseIf PathFichero.ToLower.EndsWith("part01.rar") Then
                                    LengthPart = 2
                                ElseIf PathFichero.ToLower.EndsWith("part001.rar") Then
                                    LengthPart = 3
                                ElseIf PathFichero.ToLower.EndsWith("part0001.rar") Then
                                    LengthPart = 4
                                End If

                                For i As Integer = 2 To CInt(Math.Pow(10, LengthPart))
                                    Dim path As String = PathFichero.ToLower.Replace("part" & "1".PadLeft(LengthPart, "0"c) & ".rar", "part" & i.ToString.PadLeft(LengthPart, "0"c) & ".rar")
                                    ' Si es solido pero no multiparte, lo excluimos
                                    If File.Exists(path) And PathFichero.ToLower <> path.ToLower Then
                                        ListaFicheros.Add(File.OpenRead(path))
                                    Else
                                        Exit For
                                    End If
                                Next

                                If LengthPart > 0 And Not String.IsNullOrEmpty(Password) Then
                                    Throw New NotSupportedException("Solid RAR multipart archives with password are not supported by the built-in extractor.")
                                End If

                                If ListaFicheros.Count = 1 Then

                                    Using reader As IReader = RarReader.Open(ListaFicheros(0), Password)
                                       
                                        Dim c As DescompresorController = DescompresorController.GetController
                                        Try

                                            Mutex.WaitOne()
                                            Try
                                                c._TamanoTotal = 0
                                                c._FicActTamanoTotal = 0
                                                c._TamanoTotalExtraido = 0
                                                c._FicActExtraido = 0
                                                c._FicActNombre = ""
                                                For Each entry As IArchiveEntry In archive.Entries
                                                    c._TamanoTotal += entry.Size
                                                Next
                                                EnsureExtractWithinQuota(c._TamanoTotal.GetValueOrDefault(), archive.Entries.Count())
                                            Finally
                                                Mutex.ReleaseMutex()
                                            End Try

                                            While reader.MoveToNextEntry
                                                If CancelRequested Then Throw New OperationCanceledException("Extraction cancelled.")
                                                If Not reader.Entry.IsDirectory Then
                                                    c._FicActNombre = reader.Entry.Key

                                                    c._TamanoTotalExtraido += c._FicActTamanoTotal
                                                    c._FicActTamanoTotal = reader.Entry.Size
                                                    c._FicActExtraido = 0

                                                    WriteSafeEntry(reader, PathExtraccion, reader.Entry.Key)
                                                End If
                                            End While
                                        Finally
                                            Mutex.WaitOne()
                                            c._TamanoTotal = Nothing
                                            c._FicActTamanoTotal = Nothing
                                            c._TamanoTotalExtraido = Nothing
                                            c._FicActExtraido = Nothing
                                            c._FicActNombre = Nothing
                                            Mutex.ReleaseMutex()
                                           
                                        End Try

                                    End Using


                                Else
                                    Using reader As IReader = RarReader.Open(ListaFicheros)
                                 
                                        Dim c As DescompresorController = DescompresorController.GetController
                                        Try
                                            Mutex.WaitOne()
                                            Try
                                                c._TamanoTotal = 0
                                                c._FicActTamanoTotal = 0
                                                c._TamanoTotalExtraido = 0
                                                c._FicActExtraido = 0
                                                c._FicActNombre = ""
                                                For Each entry As IArchiveEntry In archive.Entries
                                                    c._TamanoTotal += entry.Size
                                                Next
                                                EnsureExtractWithinQuota(c._TamanoTotal.GetValueOrDefault(), archive.Entries.Count())
                                            Finally
                                                Mutex.ReleaseMutex()
                                            End Try

                                            While reader.MoveToNextEntry
                                                If CancelRequested Then Throw New OperationCanceledException("Extraction cancelled.")
                                                If Not reader.Entry.IsDirectory Then
                                                    c._FicActNombre = reader.Entry.Key

                                                    c._TamanoTotalExtraido += c._FicActTamanoTotal
                                                    c._FicActTamanoTotal = reader.Entry.Size
                                                    c._FicActExtraido = 0

                                                    WriteSafeEntry(reader, PathExtraccion, reader.Entry.Key)
                                                End If
                                            End While
                                        Finally
                                            Mutex.WaitOne()
                                            c._TamanoTotal = Nothing
                                            c._FicActTamanoTotal = Nothing
                                            c._TamanoTotalExtraido = Nothing
                                            c._FicActExtraido = Nothing
                                            c._FicActNombre = Nothing
                                            Mutex.ReleaseMutex()
                                          
                                        End Try

                                    End Using
                                End If





                            Finally
                                ' Cerramos los stream
                                For Each s As Stream In ListaFicheros
                                    s.Close()
                                    s.Dispose()
                                Next
                            End Try

                        Else

                            'AddHandler archive.FilePartExtractionBegin, AddressOf archive_FilePartExtractionBegin
                            AddHandler archive.CompressedBytesRead, AddressOf archive_CompressedBytesRead
                            AddHandler archive.EntryExtractionBegin, AddressOf archive_EntryExtractionBegin
                            Dim c As DescompresorController = DescompresorController.GetController
                            Try

                                Mutex.WaitOne()
                                Try
                                    c._TamanoTotal = 0
                                    c._FicActTamanoTotal = 0
                                    c._TamanoTotalExtraido = 0
                                    c._FicActExtraido = 0
                                    c._FicActNombre = ""
                                    For Each entry As IArchiveEntry In archive.Entries
                                        c._TamanoTotal += entry.Size
                                    Next
                                    EnsureExtractWithinQuota(c._TamanoTotal.GetValueOrDefault(), archive.Entries.Count())
                                Finally
                                    Mutex.ReleaseMutex()
                                End Try

                                Dim entryKeys As New Generic.List(Of String)
                                For Each entry As IArchiveEntry In archive.Entries
                                    If Not entry.IsDirectory Then
                                        entryKeys.Add(entry.Key)
                                    End If
                                Next
                                PathGuard.ValidateArchiveEntries(PathExtraccion, entryKeys)

                                For Each entry As IArchiveEntry In archive.Entries
                                    If CancelRequested Then
                                        Throw New OperationCanceledException("Extraction cancelled.")
                                    End If
                                    If Not entry.IsDirectory Then
                                        c._FicActNombre = entry.Key
                                        WriteSafeEntry(entry, PathExtraccion)
                                    End If
                                Next
                            Finally
                                Mutex.WaitOne()
                                c._TamanoTotal = Nothing
                                c._FicActTamanoTotal = Nothing
                                c._TamanoTotalExtraido = Nothing
                                c._FicActExtraido = Nothing
                                c._FicActNombre = Nothing
                                Mutex.ReleaseMutex()
                                'RemoveHandler archive.FilePartExtractionBegin, AddressOf archive_FilePartExtractionBegin
                                RemoveHandler archive.CompressedBytesRead, AddressOf archive_CompressedBytesRead
                                RemoveHandler archive.EntryExtractionBegin, AddressOf archive_EntryExtractionBegin
                            End Try


                        End If
                    End If

                End Using
            Catch ex As Exception
                Exception = ex
            End Try
        End Sub

        Private Const MaxExtractTotalBytes As Long = 50L * 1024L * 1024L * 1024L ' 50 GiB hard cap
        Private Const MaxExtractEntries As Integer = 100000

#Region "7z CLI support"

        ''' <summary>
        ''' Extracts a .7z archive (single or multipart .7z.001) via the 7-Zip command
        ''' line. A system-installed 7-Zip is preferred; otherwise the embedded
        ''' 7zr.exe (public domain, 7z format only) is dropped next to the user's
        ''' application data and used. Entries are validated against path traversal
        ''' BEFORE extraction using the same PathGuard rules as the SharpCompress path.
        ''' </summary>
        Private Sub Extract7z()
            Dim cli As String = FindSystemSevenZip()
            If cli Is Nothing Then cli = EnsureEmbedded7zr()
            If cli Is Nothing Then
                Throw New ApplicationException("No 7-Zip executable available to extract .7z archives.")
            End If

            ' ---- Pass 1: list entries and validate them against path traversal ----
            Dim listing As String = RunSevenZip(cli, "l -ba -slt -- """ & PathFichero & """", checkCancel:=True)
            Dim entryKeys As New Generic.List(Of String)
            Dim archiveFullPath As String = IO.Path.GetFullPath(PathFichero)
            For Each line As String In listing.Split(New String() {vbCrLf, vbLf, vbCr}, StringSplitOptions.RemoveEmptyEntries)
                If line.StartsWith("Path = ", StringComparison.Ordinal) Then
                    Dim entryPath As String = line.Substring(7).Trim()
                    ' Some 7-Zip builds include the archive itself as the first
                    ' listing block ("Path = <archive>"). Skip it defensively by
                    ' comparing full paths — an entry key is always relative.
                    Dim isArchiveItself As Boolean = False
                    Try
                        isArchiveItself = String.Equals(IO.Path.GetFullPath(entryPath), archiveFullPath, StringComparison.OrdinalIgnoreCase)
                    Catch
                        ' Relative entry key — cannot be the archive file itself.
                    End Try
                    If Not isArchiveItself Then entryKeys.Add(entryPath)
                End If
            Next
            If entryKeys.Count > MaxExtractEntries Then
                Throw New InvalidOperationException("Archive rejected: too many entries (" & entryKeys.Count & ").")
            End If
            PathGuard.ValidateArchiveEntries(PathExtraccion, entryKeys)

            ' ---- Pass 2: extract ----
            If Not String.IsNullOrEmpty(Password) AndAlso Password.Contains(""""c) Then
                ' 7-Zip CLI argument quoting cannot express a double quote inside a
                ' password reliably — reject instead of silently mis-decrypting.
                Throw New NotSupportedException("7z passwords containing double quotes are not supported.")
            End If

            Dim args As New System.Text.StringBuilder()
            args.Append("x -y -bd -sccUTF-8 -o""")
            args.Append(PathExtraccion)
            args.Append(""" ")
            If Not String.IsNullOrEmpty(Password) Then
                ' -p<password>: no space after -p; no -p at all means "no password",
                ' and an empty -p would make 7-Zip prompt (impossible headless).
                args.Append("-p").Append(Password).Append(" ")
            End If
            args.Append("-- """).Append(PathFichero).Append(""""c)

            RunSevenZip(cli, args.ToString(), checkCancel:=True)

            ' Report the file name currently extracted for the UI (no byte progress
            ' from the CLI — total sizes remain unknown / null).
            Dim c As DescompresorController = DescompresorController.GetController
            Mutex.WaitOne()
            Try
                c._FicActNombre = PathFichero
            Finally
                Mutex.ReleaseMutex()
            End Try
        End Sub

        ''' <summary>
        ''' Locates a system-installed 7-Zip (7z.exe). Checks both registry views
        ''' (the process is x86, but 7-Zip is commonly installed as x64) and the
        ''' well-known install folders. Returns Nothing when not found.
        ''' </summary>
        Private Shared Function FindSystemSevenZip() As String
            Try
                For Each view As Microsoft.Win32.RegistryView In {Microsoft.Win32.RegistryView.Registry64, Microsoft.Win32.RegistryView.Default}
                    Using base As Microsoft.Win32.RegistryKey = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, view)
                        Using key As Microsoft.Win32.RegistryKey = base.OpenSubKey("SOFTWARE\7-Zip")
                            If key IsNot Nothing Then
                                Dim dir As Object = key.GetValue("Path")
                                If dir IsNot Nothing Then
                                    Dim exe As String = IO.Path.Combine(CStr(dir), "7z.exe")
                                    If File.Exists(exe) Then Return exe
                                End If
                                Dim root As Object = key.GetValue("Root")
                                If root IsNot Nothing Then
                                    Dim exe2 As String = IO.Path.Combine(CStr(root), "7z.exe")
                                    If File.Exists(exe2) Then Return exe2
                                End If
                            End If
                        End Using
                    End Using
                Next
            Catch ex As Exception
                Log.WriteWarning("7z lookup: registry probe failed: " & Log.SafeException(ex))
            End Try

            For Each candidate As String In New String() {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) & "\7-Zip\7z.exe",
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) & "\7-Zip\7z.exe"
            }
                If File.Exists(candidate) Then Return candidate
            Next
            Return Nothing
        End Function

        ''' <summary>
        ''' Extracts the embedded 7zr.exe (resource "MegaDownloader.7zr.exe", public
        ''' domain) to %LOCALAPPDATA%\MegaDownloader\bin and returns its path. The file
        ''' is (re)written whenever the embedded payload differs in size, so upgrades
        ''' replace the stale binary automatically.
        ''' </summary>
        Private Shared Function EnsureEmbedded7zr() As String
            Dim dir As String = IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MegaDownloader\bin")
            Dim target As String = IO.Path.Combine(dir, "7zr.exe")

            Try
                Dim asm As System.Reflection.Assembly = System.Reflection.Assembly.GetExecutingAssembly()
                Using src As Stream = asm.GetManifestResourceStream("MegaDownloader.7zr.exe")
                    If src Is Nothing Then
                        Log.WriteError("7z extraction: embedded 7zr.exe resource is missing.")
                        Return Nothing
                    End If

                    Dim rewrite As Boolean = True
                    If File.Exists(target) Then
                        Try
                            rewrite = (New FileInfo(target).Length <> src.Length)
                        Catch
                            rewrite = True
                        End Try
                    End If

                    If rewrite Then
                        Directory.CreateDirectory(dir)
                        Using dst As New FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None)
                            src.CopyTo(dst)
                        End Using
                        Log.WriteInfo("7z extraction: embedded 7zr.exe deployed to " & target)
                    End If
                End Using
                Return target
            Catch ex As Exception
                Log.WriteError("7z extraction: failed to deploy embedded 7zr.exe: " & Log.SafeException(ex))
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Runs the 7-Zip CLI with the given arguments and returns its stdout.
        ''' Exit code 0/1 (OK/warnings) pass; 2 (fatal, e.g. wrong password) and any
        ''' other code throw with the tail of stderr/stdout for the error message.
        ''' Honors CancelRequested by killing the process.
        ''' </summary>
        Private Function RunSevenZip(ByVal cliPath As String, ByVal arguments As String, ByVal checkCancel As Boolean) As String
            Dim psi As New ProcessStartInfo()
            psi.FileName = cliPath
            psi.Arguments = arguments
            psi.UseShellExecute = False
            psi.CreateNoWindow = True
            psi.RedirectStandardOutput = True
            psi.RedirectStandardError = True
            psi.StandardOutputEncoding = System.Text.Encoding.UTF8
            psi.StandardErrorEncoding = System.Text.Encoding.UTF8

            ' Never log the raw arguments: they can carry "-p<password>".
            Dim safeArgs As String = System.Text.RegularExpressions.Regex.Replace(arguments, "-p\S+", "-p[redacted]")
            Log.WriteInfo("7z CLI: " & IO.Path.GetFileName(cliPath) & " " & safeArgs)

            Using proc As New Process()
                proc.StartInfo = psi
                proc.Start()

                ' Read stderr asynchronously to avoid the classic pipe deadlock:
                ' if 7-Zip fills the stderr buffer while we are still blocked on
                ' ReadToEnd(stdout), the child stalls and both sides wait forever.
                Dim stderrTask As Threading.Tasks.Task(Of String) = proc.StandardError.ReadToEndAsync()
                Dim stdout As String = proc.StandardOutput.ReadToEnd()
                Dim stderr As String = stderrTask.Result

                ' Cooperative cancellation while waiting for the CLI to finish.
                While Not proc.WaitForExit(250)
                    If checkCancel AndAlso CancelRequested Then
                        Try
                            proc.Kill()
                        Catch
                        End Try
                        proc.WaitForExit(5000)
                        Throw New OperationCanceledException("Extraction cancelled.")
                    End If
                End While

                If proc.ExitCode <> 0 AndAlso proc.ExitCode <> 1 Then
                    Dim tail As String = (stderr & " " & stdout).Trim()
                    If tail.Length > 300 Then tail = tail.Substring(tail.Length - 300)
                    Throw New ApplicationException("7-Zip failed with exit code " & proc.ExitCode & ": " & tail)
                End If
                Return stdout
            End Using
        End Function

#End Region

        Private Shared Sub EnsureExtractWithinQuota(ByVal totalUncompressed As Long, ByVal entryCount As Integer)
            If entryCount > MaxExtractEntries Then
                Throw New InvalidOperationException("Archive rejected: too many entries (" & entryCount & ").")
            End If
            If totalUncompressed < 0 OrElse totalUncompressed > MaxExtractTotalBytes Then
                Throw New InvalidOperationException("Archive rejected: uncompressed size exceeds safety limit.")
            End If
        End Sub

        Private Shared Sub WriteSafeEntry(ByVal entry As IArchiveEntry, ByVal extractionRoot As String)
            Dim dest As String = PathGuard.GetSafeArchiveEntryPath(extractionRoot, entry.Key)
            Dim parentDir As String = Path.GetDirectoryName(dest)
            If Not String.IsNullOrEmpty(parentDir) Then
                Directory.CreateDirectory(parentDir)
            End If
            entry.WriteToFile(dest, SharpCompress.Common.ExtractOptions.Overwrite)
        End Sub

        Private Shared Sub WriteSafeEntry(ByVal reader As IReader, ByVal extractionRoot As String, ByVal entryKey As String)
            Dim dest As String = PathGuard.GetSafeArchiveEntryPath(extractionRoot, entryKey)
            Dim parentDir As String = Path.GetDirectoryName(dest)
            If Not String.IsNullOrEmpty(parentDir) Then
                Directory.CreateDirectory(parentDir)
            End If
            reader.WriteEntryToFile(dest, SharpCompress.Common.ExtractOptions.Overwrite)
        End Sub

        Private Sub archive_CompressedBytesRead(sender As Object, e As CompressedBytesReadEventArgs)
            Dim c As DescompresorController = DescompresorController.GetController
            Mutex.WaitOne()
            c._FicActExtraido = e.CompressedBytesRead
            Mutex.ReleaseMutex()
            'Dim percentage As String = If(FicActTamanoTotal.HasValue, CreatePercentage(e.CompressedBytesRead, FicActTamanoTotal.Value).ToString(), "Unknown")
            'Console.WriteLine("Read Compressed File Entry Bytes: {0} ({1}%) " & " - Total: " & (TamanoTotalExtraido.Value + FicActExtraido.Value) & " / " & TamanoTotal & " ({2}%)", e.CompressedBytesRead, percentage, CInt(100 * (TamanoTotalExtraido.Value + FicActExtraido.Value) / TamanoTotal))
        End Sub



        Private Sub archive_EntryExtractionBegin(sender As Object, e As ArchiveExtractionEventArgs(Of IArchiveEntry))
            Dim c As DescompresorController = DescompresorController.GetController
            Mutex.WaitOne()
            c._TamanoTotalExtraido += c._FicActTamanoTotal
            c._FicActTamanoTotal = e.Item.Size
            c._FicActExtraido = 0
            Mutex.ReleaseMutex()
            'Console.WriteLine("Initializing File Entry Extraction: " + e.Item.FilePath + "; size: " & e.Item.Size)
            'Console.WriteLine("Extracted: " & TamanoTotalExtraido & " / " & TamanoTotal)
        End Sub

        'Private Function CreatePercentage(n As Long, d As Long) As Integer
        '    Return CInt((CDbl(n) / CDbl(d)) * 100)
        'End Function
    End Class


#End Region

End Class
