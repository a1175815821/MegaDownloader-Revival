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
    ' P-08: Contains() is O(1) here. With List it made every recalculation O(N_streams^2),
    ' and AddStream/RemoveStream fire once per chunk on reconnects.
    Private _StreamList As Generic.HashSet(Of ThrottledStream)
    Private _htId As Generic.Dictionary(Of String, Generic.List(Of ThrottledStream))

    ' Velocidad máxima
    Private _htMaxSpeed As Generic.Dictionary(Of String, Long)
    Private _globalMaxSpeed As Long
    Private ReadOnly _accountDownloads As New Generic.HashSet(Of AccountDownloadBudget)()

    ' The SDK applies its limit to the entire client, including concurrent downloads.
    Friend NotInheritable Class AccountDownloadBudget
        Friend Transfers As Integer
        Private _bytesPerSecond As Long

        Friend Property BytesPerSecond As Long
            Get
                Return Threading.Volatile.Read(_bytesPerSecond)
            End Get
            Set(value As Long)
                Threading.Volatile.Write(_bytesPerSecond, value)
            End Set
        End Property
    End Class

    Private Sub New()
        SyncLock Mutex
            _StreamList = New Generic.HashSet(Of ThrottledStream)
            _htId = New Generic.Dictionary(Of String, Generic.List(Of ThrottledStream))
            _htMaxSpeed = New Generic.Dictionary(Of String, Long)
            _globalMaxSpeed = 0
        End SyncLock
    End Sub

    ' P1-5: AddStream/RemoveStream fire per chunk connection, so this recalculation ran
    ' for every chunk even when the user never configured a speed limit. Remember whether
    ' the previous pass actually assigned a limit: the one transition from "limited" to
    ' "unlimited" must still run once to reset the streams' MaximumBytesPerSecond to 0.
    Private _lastAssignmentHadLimit As Boolean

    ' Caller must hold Mutex.
    Private Function HasConfiguredLimit() As Boolean
        If _globalMaxSpeed > 0 Then Return True
        For Each max As Long In _htMaxSpeed.Values
            If max > 0 Then Return True
        Next
        Return False
    End Function

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

        Dim str As System.Text.StringBuilder = Nothing

        SyncLock Mutex

            Dim hasLimit As Boolean = HasConfiguredLimit()
            If Not hasLimit Then
                ' P1-5 快速路径：没有任何限速配置，且所有仍注册的参与者当前都已是
                ' 无限速时，整段重算可以跳过。仍要检查参与者当前值：从"有限速"
                ' 切到"无限速"的那一次必须完整跑一遍清零；被移出后再加入的流/
                ' 预算也不能带着旧上限复活。
                Dim alreadyUnlimited As Boolean = Not _lastAssignmentHadLimit
                If alreadyUnlimited Then
                    For Each stream As ThrottledStream In _StreamList
                        If stream.MaximumBytesPerSecond <> 0 Then
                            alreadyUnlimited = False
                            Exit For
                        End If
                    Next
                    If alreadyUnlimited Then
                        For Each budget As AccountDownloadBudget In _accountDownloads
                            If budget.BytesPerSecond <> 0 Then
                                alreadyUnlimited = False
                                Exit For
                            End If
                        Next
                    End If
                End If
                If alreadyUnlimited Then Exit Sub
                _lastAssignmentHadLimit = False
            Else
                _lastAssignmentHadLimit = True
            End If

            Dim participants = _StreamList.Count + _accountDownloads.Count
            If participants = 0 Then Exit Sub

            ' Velocidad maxima global de cada stream
            Dim SpeedGlobalIndividual As Long = _globalMaxSpeed \ participants

            Dim MaxSpeed As Long


            Dim SpeedAsignada As Long = 0
            Dim StreamsConLimiteIndividual As New Generic.HashSet(Of ThrottledStream)
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

            Dim StreamsRestantes = participants - StreamsConLimiteIndividual.Count
            If StreamsRestantes > 0 Then
                MaxSpeed = Math.Max(0L, _globalMaxSpeed - SpeedAsignada) \ StreamsRestantes

                For Each stream As ThrottledStream In _StreamList
                    If Not StreamsConLimiteIndividual.Contains(stream) Then
                        stream.MaximumBytesPerSecond = MaxSpeed
                    End If
                Next
                For Each budget In _accountDownloads
                    budget.BytesPerSecond = MaxSpeed
                Next
            End If


            ' The log string is only built when Debug logging is actually enabled.
            If Log.IsDebugEnabled Then
                str = New System.Text.StringBuilder
                For Each stream As ThrottledStream In _StreamList
                    If str.Length > 0 Then str.Append(", ")
                    str.Append("[").Append(CInt(stream.MaximumBytesPerSecond / 1024)).Append("]")
                Next
            End If

        End SyncLock

        If str IsNot Nothing Then Log.WriteDebug("Speed limit recalculated: " & str.ToString)
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

    Friend Sub BeginAccountDownload(budget As AccountDownloadBudget)
        SyncLock Mutex
            budget.Transfers += 1
            If budget.Transfers > 1 Then Return
            _accountDownloads.Add(budget)
            _RecalcularVelocidad()
        End SyncLock
    End Sub

    Friend Sub EndAccountDownload(budget As AccountDownloadBudget)
        SyncLock Mutex
            budget.Transfers -= 1
            If budget.Transfers > 0 Then Return
            _accountDownloads.Remove(budget)
            _RecalcularVelocidad()
        End SyncLock
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

    Public Sub Continuar(Id As String)
        If String.IsNullOrEmpty(Id) Then Throw New ArgumentNullException("Id")
        SyncLock Mutex
            If _htId.ContainsKey(Id) Then
                For Each stream As ThrottledStream In _htId(Id)
                    stream.Continue()
                Next
            End If
        End SyncLock
    End Sub

#End Region
   
End Class
