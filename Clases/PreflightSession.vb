Imports System.Collections.Generic
Imports System.Threading
Imports System.Threading.Tasks

''' <summary>One add-links window owns a speculative, cancellable metadata session.
''' Results never enqueue a download. Finished sources are reused when more links arrive.</summary>
Public NotInheritable Class PreflightSession
    Implements IDisposable

    Public NotInheritable Class Request
        Public ReadOnly Text As String
        Public ReadOnly Urls As Task(Of List(Of String))
        Public ReadOnly Results As Task(Of List(Of BatchLinkPreflight.Entry))
        Friend ReadOnly Cancellation As CancellationTokenSource
        Public ReadOnly Started As DateTime = DateTime.UtcNow
        Private latestProgress As New PreflightProgress(PreflightProgress.Phase.Parsing)

        Public ReadOnly Property Progress As PreflightProgress
            Get
                Return Volatile.Read(latestProgress)
            End Get
        End Property

        Private Sub Publish(value As PreflightProgress)
            Volatile.Write(latestProgress, value)
        End Sub

        Friend Sub New(textValue As String, parser As Func(Of List(Of String)),
                       resolver As Func(Of List(Of String), CancellationToken, Action(Of PreflightProgress), List(Of BatchLinkPreflight.Entry)), cancellationValue As CancellationTokenSource)
            Text = textValue
            Cancellation = cancellationValue
            Dim token As CancellationToken = Cancellation.Token
            Urls = Task.Run(Function()
                                token.ThrowIfCancellationRequested()
                                Dim parsed As List(Of String) = parser()
                                token.ThrowIfCancellationRequested()
                                Return parsed
                            End Function, token)
            Results = RunAsync(resolver, token)
            ' Speculative work may finish after its window closed; always observe faults.
            Results.ContinueWith(Sub(t)
                                     Dim observed As AggregateException = t.Exception
                                 End Sub, TaskContinuationOptions.OnlyOnFaulted)
        End Sub

        Private Async Function RunAsync(resolver As Func(Of List(Of String), CancellationToken, Action(Of PreflightProgress), List(Of BatchLinkPreflight.Entry)), token As CancellationToken) As Task(Of List(Of BatchLinkPreflight.Entry))
            Try
                Dim parsed As List(Of String) = Await Urls.ConfigureAwait(False)
                Publish(New PreflightProgress(PreflightProgress.Phase.Waiting, total:=parsed.Count))
                Dim resolved As List(Of BatchLinkPreflight.Entry) = Await Task.Run(Function() resolver(parsed, token, AddressOf Publish), token).ConfigureAwait(False)
                Publish(New PreflightProgress(PreflightProgress.Phase.Completed, parsed.Count, parsed.Count, found:=resolved.Count))
                Return resolved
            Catch ex As OperationCanceledException
                Publish(New PreflightProgress(PreflightProgress.Phase.Cancelled))
                Throw
            Catch ex As Exception
                Publish(New PreflightProgress(PreflightProgress.Phase.Failed))
                Throw
            End Try
        End Function
    End Class

    Private NotInheritable Class CachedSource
        Public Entries As List(Of BatchLinkPreflight.Entry)
        Public Completed As DateTime
    End Class

    Private ReadOnly gate As New Object()
    Private ReadOnly workerGate As New SemaphoreSlim(1, 1)
    Private ReadOnly cache As New Dictionary(Of String, CachedSource)(StringComparer.Ordinal)
    Private ReadOnly loader As Func(Of String, CancellationToken, IProgress(Of PreflightProgress), List(Of BatchLinkPreflight.Entry))
    Private current As Request
    Private disposed As Boolean

    Public Sub New(configuration As Configuracion)
        loader = Function(url, token, progress) BatchLinkPreflight.Resolve(New List(Of String) From {url}, configuration, New HashSet(Of String)(StringComparer.Ordinal), token, detailedProgress:=progress)
    End Sub

    ''' <summary>Injectable source loader for offline cache/cancellation regressions.</summary>
    Public Sub New(sourceLoader As Func(Of String, CancellationToken, List(Of BatchLinkPreflight.Entry)))
        loader = Function(url, token, progress) sourceLoader(url, token)
    End Sub

    Public Function Prepare(text As String, Optional force As Boolean = False) As Request
        SyncLock gate
            If disposed Then Throw New ObjectDisposedException(NameOf(PreflightSession))
            If Not force AndAlso current IsNot Nothing AndAlso Not current.Cancellation.IsCancellationRequested AndAlso
                String.Equals(current.Text, text, StringComparison.Ordinal) AndAlso Not current.Results.IsFaulted AndAlso Not current.Results.IsCanceled AndAlso
                (Not current.Results.IsCompleted OrElse DateTime.UtcNow - current.Started < TimeSpan.FromMinutes(3)) Then Return current
            CancelCurrent()
            If force Then cache.Clear()
            Dim source As New CancellationTokenSource()
            current = New Request(text, Function() URLExtractor.ExtraerURLs(text), AddressOf ResolveSources, source)
            Return current
        End SyncLock
    End Function

    Public Sub Invalidate(Optional clearCache As Boolean = False)
        SyncLock gate
            CancelCurrent()
            If clearCache Then cache.Clear()
        End SyncLock
    End Sub

    Private Sub CancelCurrent()
        Dim previous As Request = current
        current = Nothing
        If previous Is Nothing Then Return
        previous.Cancellation.Cancel()
        previous.Results.ContinueWith(Sub(t) previous.Cancellation.Dispose(), TaskScheduler.Default)
    End Sub

    Private Function ResolveSources(urls As List(Of String), token As CancellationToken, report As Action(Of PreflightProgress)) As List(Of BatchLinkPreflight.Entry)
        workerGate.Wait(token)
        Try
            token.ThrowIfCancellationRequested()
            Dim sourceKeys As New Dictionary(Of String, String)(StringComparer.Ordinal)
            Dim active As New HashSet(Of String)(StringComparer.Ordinal)
            For Each url As String In urls
                token.ThrowIfCancellationRequested()
                Dim key As String = url
                If Not URLExtractor.IsMegaFolder(url) AndAlso Not URLExtractor.IsELC(url) Then
                    key = If(BatchLinkPreflight.Identity(New URLProcessor.FileURL(url, "")), url)
                End If
                sourceKeys(url) = key
                active.Add(key)
            Next
            SyncLock gate
                For Each key As String In New List(Of String)(cache.Keys)
                    If Not active.Contains(key) Then cache.Remove(key)
                Next
            End SyncLock
            Dim result As New List(Of BatchLinkPreflight.Entry)()
            Dim completed As Integer = 0
            For Each url As String In urls
                token.ThrowIfCancellationRequested()
                Dim known As CachedSource = Nothing
                Dim sourceKey As String = sourceKeys(url)
                SyncLock gate
                    cache.TryGetValue(sourceKey, known)
                End SyncLock
                If known Is Nothing OrElse DateTime.UtcNow - known.Completed >= TimeSpan.FromMinutes(3) Then
                    Dim sourceProgress As New PreflightProgress.Reporter(Sub(value) report(New PreflightProgress(value.Stage, completed, urls.Count, value.ItemsProcessed, value.ItemsTotal, result.Count)))
                    report(New PreflightProgress(PreflightProgress.Phase.Reading, completed, urls.Count, found:=result.Count))
                    Dim inspected As List(Of BatchLinkPreflight.Entry) = loader(url, token, sourceProgress)
                    token.ThrowIfCancellationRequested()
                    known = New CachedSource With {.Entries = inspected, .Completed = DateTime.UtcNow}
                    ' Transient failures are retried on the next generation, not cached.
                    If Not inspected.Exists(Function(entry) entry.State = BatchLinkPreflight.LinkState.Unavailable) Then
                        SyncLock gate
                            If Not disposed AndAlso Not token.IsCancellationRequested Then cache(sourceKey) = known
                        End SyncLock
                    End If
                End If
                result.AddRange(known.Entries)
                completed += 1
                report(New PreflightProgress(PreflightProgress.Phase.Checking, completed - 1, urls.Count, known.Entries.Count, known.Entries.Count, result.Count))
            Next
            Return result
        Finally
            workerGate.Release()
        End Try
    End Function

    Public Shared Function Reconcile(input As List(Of BatchLinkPreflight.Entry), queued As Dictionary(Of String, URLProcessor.FileURL)) As List(Of BatchLinkPreflight.Entry)
        Dim seen As New HashSet(Of String)(StringComparer.Ordinal)
        Dim result As New List(Of BatchLinkPreflight.Entry)(input.Count)
        For Each source As BatchLinkPreflight.Entry In input
            Dim entry As New BatchLinkPreflight.Entry With {.Source = source.Source, .OriginalUrl = source.OriginalUrl, .Name = source.Name, .SizeBytes = source.SizeBytes, .State = source.State, .Detail = source.Detail}
            Dim key As String = BatchLinkPreflight.Identity(entry.Source)
            If key IsNot Nothing Then
                Dim existing As URLProcessor.FileURL = Nothing
                If queued.TryGetValue(key, existing) Then
                    entry.State = BatchLinkPreflight.LinkState.AlreadyQueued
                    If Not String.IsNullOrEmpty(existing.DisplayName) Then entry.Name = existing.DisplayName
                    If existing.SizeBytes.HasValue Then entry.SizeBytes = existing.SizeBytes
                ElseIf Not seen.Add(key) Then
                    entry.State = BatchLinkPreflight.LinkState.Duplicate
                End If
            End If
            entry.Selected = entry.State = BatchLinkPreflight.LinkState.Ready
            result.Add(entry)
        Next
        Return result
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        SyncLock gate
            If disposed Then Return
            disposed = True
            CancelCurrent()
            cache.Clear()
        End SyncLock
        ' workerGate can still have a cancelled worker unwinding. It owns no OS
        ' wait handle unless requested, so let GC release it after that worker exits.
    End Sub
End Class
