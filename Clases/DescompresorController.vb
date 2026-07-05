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

        Dim DirectorioExtraccion As String = Directorio
        If _crearDirectorio Then
            DirectorioExtraccion = Path.Combine(Directorio, FicheroSinExtension)
        End If

        Dim forceEnd As Boolean = False


        Dim desc As New Descompressor(_pathElementoActual, DirectorioExtraccion, _passwordElementoActual)

        Dim Thread As New Threading.Thread(AddressOf desc.Extract)
        Thread.Priority = Threading.ThreadPriority.BelowNormal
        Thread.Start()
        While Not Thread.Join(500) And Not forceEnd
            If Cancel Then
                Thread.Abort()
                System.Threading.Thread.Sleep(300)
                forceEnd = True
            End If
        End While

        If desc.Exception IsNot Nothing Then
            ' Guardar logs
            Log.WriteError("Decompressor: Error extracting '" & _codigoElementoActual & "' (file '" & _pathElementoActual & "'): " & desc.Exception.ToString)
        End If
        Sw.Stop()


        Log.WriteInfo("Element '" & _codigoElementoActual & "' extracted in " & Sw.ElapsedMilliseconds & "ms")

        RaiseEvent DescompresionFinalizada(_codigoElementoActual)

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

    Public Event DescompresionFinalizada(ByVal Code As String)

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

        Public Sub New(ByVal _Fichero As String, ByVal _PathExtraccion As String, ByVal _Password As String)

            PathFichero = _Fichero
            PathExtraccion = _PathExtraccion
            Password = _Password

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


                ' No tenemos soporte para 7zip con multipart... (.7z.001, .7z.002, etc)
                If PathFichero.ToLower.Contains(".7z.") Then
                    Throw New NotImplementedException
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
                                    Throw New NotImplementedException ' De momento no soportamos multipartes con contraseña
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
                                            Finally
                                                Mutex.ReleaseMutex()
                                            End Try

                                            While reader.MoveToNextEntry
                                                If Not reader.Entry.IsDirectory Then
                                                    c._FicActNombre = reader.Entry.Key

                                                    c._TamanoTotalExtraido += c._FicActTamanoTotal
                                                    c._FicActTamanoTotal = reader.Entry.Size
                                                    c._FicActExtraido = 0

                                                    reader.WriteEntryToDirectory(PathExtraccion, SharpCompress.Common.ExtractOptions.ExtractFullPath Or SharpCompress.Common.ExtractOptions.Overwrite)
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
                                            Finally
                                                Mutex.ReleaseMutex()
                                            End Try

                                            While reader.MoveToNextEntry
                                                If Not reader.Entry.IsDirectory Then
                                                    c._FicActNombre = reader.Entry.Key

                                                    c._TamanoTotalExtraido += c._FicActTamanoTotal
                                                    c._FicActTamanoTotal = reader.Entry.Size
                                                    c._FicActExtraido = 0

                                                    reader.WriteEntryToDirectory(PathExtraccion, SharpCompress.Common.ExtractOptions.ExtractFullPath Or SharpCompress.Common.ExtractOptions.Overwrite)
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
                                Finally
                                    Mutex.ReleaseMutex()
                                End Try

                                For Each entry As IArchiveEntry In archive.Entries
                                    If Not entry.IsDirectory Then
                                        c._FicActNombre = entry.Key

                                        entry.WriteToDirectory(PathExtraccion, SharpCompress.Common.ExtractOptions.ExtractFullPath Or SharpCompress.Common.ExtractOptions.Overwrite)
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
