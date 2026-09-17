Public Class ThrottledStreamController

#Region "Región Shared"
    Private Shared Mutex As New Object()

    Private Shared _Controller As ThrottledStreamController
    Public Shared Function GetController() As ThrottledStreamController
        SyncLock Mutex
            If _Controller Is Nothing Then
                _Controller = New ThrottledStreamController
            End If
        End SyncLock
        Return _Controller
    End Function

#End Region

#Region "Región privada"
    ' Streams activas
    Private _StreamList As Generic.List(Of ThrottledStream)
    Private _htId As Generic.Dictionary(Of String, Generic.List(Of ThrottledStream))

    ' Velocidad máxima
    Private _htMaxSpeed As Generic.Dictionary(Of String, Long)
    Private _globalMaxSpeed As Long

    Private Sub New()
        SyncLock Mutex
            _StreamList = New Generic.List(Of ThrottledStream)
            _htId = New Generic.Dictionary(Of String, Generic.List(Of ThrottledStream))
            _htMaxSpeed = New Generic.Dictionary(Of String, Long)
            _globalMaxSpeed = 0
        End SyncLock
    End Sub

    Private Sub _RecalcularVelocidad()
        ' La velocidad asignada es la menor entre la global y la individual

        ' Ejemplos:
        ' * Global: 500, individual 50 a 2 IDs, cada uno con 2 streams, total 6 streams:
        ' ** Stream 1: 25
        ' ** Stream 2: 25
        ' ** Stream 3: 25
        ' ** Stream 4: 25
        ' ** Stream 5: 200 -> Stream sin velocidad individual
        ' ** Stream 6: 200 -> Stream sin velocidad individual

        ' * Global: 500, individual 150 a 3 IDs, cada uno con 2 streams, total 10 streams:
        ' ** La global es menor a la individual (75 vs 50), aplicamos el límite global
        ' ** Stream 1: 50
        ' ** Stream 2: 50
        ' ** Stream 3: 50
        ' ** Stream 4: 50
        ' ** Stream 5: 50
        ' ** Stream 6: 50
        ' ** Stream 7: 50 -> Stream sin velocidad individual
        ' ** Stream 8: 50 -> Stream sin velocidad individual
        ' ** Stream 9: 50 -> Stream sin velocidad individual
        ' ** Stream 10: 50 -> Stream sin velocidad individual

        Dim str As New System.Text.StringBuilder

        SyncLock Mutex

            If _StreamList.Count = 0 Then Exit Sub

            ' Velocidad maxima global de cada stream
            Dim SpeedGlobalIndividual As Long = CLng(_globalMaxSpeed / _StreamList.Count)

            Dim MaxSpeed As Long


            Dim SpeedAsignada As Long = 0
            Dim StreamsConLimiteIndividual As New Generic.List(Of ThrottledStream)
            For Each Id As String In _htMaxSpeed.Keys
                MaxSpeed = _htMaxSpeed(Id)
                If MaxSpeed > 0 And _htId.ContainsKey(Id) Then

                    Dim i As Integer = 0
                    For Each stream As ThrottledStream In _htId(Id)
                        If _StreamList.Contains(stream) Then
                            i += 1
                        End If
                    Next
                    If i = 0 Then Continue For

                    ' Se aplica el limite de velocidad

                    ' Velocidad maxima individual de cada stream
                    MaxSpeed = CLng(MaxSpeed / i)

                    ' Miramos que límite aplicar, si el individual o el global
                    If MaxSpeed > SpeedGlobalIndividual And SpeedGlobalIndividual > 0 Then MaxSpeed = SpeedGlobalIndividual

                    For Each stream As ThrottledStream In _htId(Id)
                        If _StreamList.Contains(stream) Then
                            StreamsConLimiteIndividual.Add(stream)
                            stream.MaximumBytesPerSecond = MaxSpeed
                            SpeedAsignada += MaxSpeed
                        End If
                    Next

                End If
            Next

            Dim StreamsRestantes As Integer = 0
            For Each stream As ThrottledStream In _StreamList
                If Not StreamsConLimiteIndividual.Contains(stream) Then
                    StreamsRestantes += 1
                End If
            Next
            If StreamsRestantes > 0 Then

                MaxSpeed = CLng((_globalMaxSpeed - SpeedAsignada) / StreamsRestantes)
                If MaxSpeed < 0 Then MaxSpeed = 0

                For Each stream As ThrottledStream In _StreamList
                    If Not StreamsConLimiteIndividual.Contains(stream) Then
                        stream.MaximumBytesPerSecond = MaxSpeed
                    End If
                Next

            End If


            For Each stream As ThrottledStream In _StreamList
                If str.Length > 0 Then str.Append(", ")
                str.Append("[").Append(CInt(stream.MaximumBytesPerSecond / 1024)).Append("]")
            Next

        End SyncLock

        Log.WriteDebug("Speed limit recalculated: " & str.ToString)
    End Sub
#End Region

#Region "Región pública"

    ' Llamar cuando creamos un stream nuevo
    Public Sub AddStream(ByRef Stream As ThrottledStream, ByVal Id As String)
        If String.IsNullOrEmpty(Id) Or Stream Is Nothing Then
            Throw New ArgumentNullException
        End If

        SyncLock Mutex
            If Not _StreamList.Contains(Stream) Then
                _StreamList.Add(Stream)
                If Not _htId.ContainsKey(Id) Then
                    _htId(Id) = New Generic.List(Of ThrottledStream)
                End If
                If Not _htMaxSpeed.ContainsKey(Id) Then
                    _htMaxSpeed(Id) = 0
                End If
                _htId(Id).Add(Stream)
            End If
        End SyncLock
        _RecalcularVelocidad()
    End Sub

    ' Llamar cuando dejamos de usar un stream
    Public Sub RemoveStream(ByRef Stream As ThrottledStream)
        If Stream Is Nothing Then
            Throw New ArgumentNullException
        End If


        SyncLock Mutex
            If _StreamList.Contains(Stream) Then
                _StreamList.Remove(Stream)
                Dim RemoveKey As String = Nothing
                For Each key As String In _htId.Keys
                    If _htId(key).Contains(Stream) Then
                        _htId(key).Remove(Stream)
                        If _htId(key).Count = 0 Then
                            RemoveKey = key
                        End If
                    End If
                Next
                If Not String.IsNullOrEmpty(RemoveKey) Then
                    _htId.Remove(RemoveKey)
                End If
            End If
        End SyncLock
        _RecalcularVelocidad()
    End Sub

    ' Llamar cuando se establece velocidad máxima global
    Public Sub SetMaxGlobalSpeed(ByVal Bps As Long)
        SyncLock Mutex
            _globalMaxSpeed = Bps
        End SyncLock
        _RecalcularVelocidad()
    End Sub

    ' Llamar cuando se establece velocidad máxima individual
    Public Sub SetMaxSpeed(ByVal Id As String, ByVal Bps As Long)
        SyncLock Mutex
            _htMaxSpeed(Id) = Bps
        End SyncLock
        _RecalcularVelocidad()
    End Sub

    ' Llamar cuando se elimina un fichero de la cola
    Public Sub RemoveId(ByVal Id As String)
        If Id Is Nothing Then
            Throw New ArgumentNullException
        End If


        SyncLock Mutex
            If _htMaxSpeed.ContainsKey(Id) Then
                _htMaxSpeed.Remove(Id)
            End If
        End SyncLock
        _RecalcularVelocidad()
    End Sub

    ' Llamar cuando se pausen o detengan las descargas, o se cierre el programa
    ' Simplemente aborta las pausas introducidas para adecuar la velocidad
    Public Sub Abortar()
        SyncLock Mutex
            For Each stream As ThrottledStream In _StreamList
                stream.Abort()
            Next
        End SyncLock
    End Sub

    ' Llamar cuando se haga una pausa individual
    ' Simplemente aborta las pausas introducidas para adecuar la velocidad
    Public Sub Abortar(Id As String)
        If String.IsNullOrEmpty(Id) Then
            Throw New ArgumentNullException
        End If

        SyncLock Mutex
            If _htId.ContainsKey(Id) Then
                For Each stream As ThrottledStream In _StreamList
                    If _htId(Id).Contains(stream) Then
                        stream.Abort()
                    End If
                Next
            End If
        End SyncLock
    End Sub

    ' Llamar cuando se vuelven a iniciar las descargas 
    ' (para reiniciar el bit de "abortar", sino se queda siempre activo!)
    Public Sub Continuar()
        SyncLock Mutex
            For Each stream As ThrottledStream In _StreamList
                stream.Continue()
            Next
        End SyncLock
    End Sub

#End Region
   
End Class
