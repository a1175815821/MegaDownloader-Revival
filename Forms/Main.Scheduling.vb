Imports System.Collections.Generic
Imports System.Windows.Forms

Partial Public Class Main
    Private ReadOnly scheduleController As New DownloadScheduleController()
    Private ReadOnly scheduleActionLock As New Object()
    Private scheduleTimer As System.Windows.Forms.Timer
    Private scheduleMenuItem As ToolStripMenuItem

    Private Sub InitDownloadScheduler()
        scheduleTimer = New System.Windows.Forms.Timer(Me.components) With {.Interval = 1000}
        AddHandler scheduleTimer.Tick, AddressOf ScheduleTick
        ScheduleTick(Nothing, EventArgs.Empty)
        scheduleTimer.Start()
    End Sub

    Private Sub ScheduleTick(sender As Object, e As EventArgs)
        SyncLock scheduleActionLock
            If Cerrando OrElse Config Is Nothing Then Return
            If scheduleMenuItem IsNot Nothing Then scheduleMenuItem.Checked = Config.DownloadSchedule.Enabled
            Select Case scheduleController.Evaluate(Config.DownloadSchedule, DateTime.Now, EstadoAplicacion = TipoEstadoAplicacion.Descargando)
                Case DownloadScheduleController.ScheduleAction.Pause
                    QuitarDescargasIndividuales()
                    EstadoAplicacion = TipoEstadoAplicacion.Pausa
                    ThrottledStreamController.GetController.Abortar()
                    Log.WriteInfo("Download schedule paused the queue.")
                Case DownloadScheduleController.ScheduleAction.Start
                    ' Leave PausaIndividual intact. Quota gating is also enforced in
                    ' PonerFicherosADescargar, so an active quota wait remains in force.
                    EstadoAplicacion = TipoEstadoAplicacion.Descargando
                    ThrottledStreamController.GetController.Continuar()
                    Log.WriteInfo("Download schedule started the queue.")
            End Select
        End SyncLock
    End Sub

    Private Function ScheduleAllowsStart(Optional showMessage As Boolean = False) As Boolean
        Dim allowed As Boolean = Config.DownloadSchedule.AllowsDownload(DateTime.Now)
        If Not allowed AndAlso showMessage Then ToastForm.ShowToast(Me, Language.GetText("Schedule_Outside"))
        Return allowed
    End Function

    Private Sub OpenDownloadSchedule(sender As Object, e As EventArgs)
        Using dialog As New DownloadScheduleForm(Config.DownloadSchedule)
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            SyncLock scheduleActionLock
                Config.DownloadSchedule = dialog.Settings
                scheduleController.Reset()
            End SyncLock
            Config.GuardarXML(True)
            If Config.ErrorConfig = Configuracion.ErrorConfigClass.Fichero_No_Creado OrElse Config.LoadTransientError Then
                MessageBox.Show(Me, Language.GetText("Schedule_SaveFailed"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End If
            ScheduleTick(Nothing, EventArgs.Empty)
        End Using
    End Sub

    Friend Function GetQueuedLinkSnapshot() As Dictionary(Of String, URLProcessor.FileURL)
        Dim result As New Dictionary(Of String, URLProcessor.FileURL)(StringComparer.Ordinal)
        Dim files As New List(Of Fichero)()
        SyncLock Mutex.ListaDescargas
            For Each package As Paquete In ListaPaquetes
                files.AddRange(package.SnapshotFicheros())
            Next
        End SyncLock
        For Each file As Fichero In files
            Try
                Dim key As String = BatchLinkPreflight.Identity(file.FileID, file.FileKey)
                If key Is Nothing Then Continue For
                result(key) = New URLProcessor.FileURL("", "") With {.DisplayName = If(file.DescargaProcesada, file.NombreFichero, Nothing),
                    .SizeBytes = If(file.DescargaProcesada, CType(file.TamanoBytes, Long?), Nothing)}
            Catch ex As ObjectDisposedException
                ' A task can be removed after the short reference snapshot.
            End Try
        Next
        Return result
    End Function
End Class
