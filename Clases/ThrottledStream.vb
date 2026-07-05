Imports System
Imports System.IO
Imports System.Threading
Imports System.Diagnostics

''' <summary>
''' Class for streaming data with throttling support.
''' </summary>
Public Class ThrottledStream
    Inherits Stream
    ''' <summary>
    ''' A constant used to specify an infinite number of bytes that can be transferred per second.
    ''' </summary>
    Public Const Infinite As Long = 0

#Region "Private members"
    ''' <summary>
    ''' The base stream.
    ''' </summary>
    Private _baseStream As Stream

    ''' <summary>
    ''' The maximum bytes per second that can be transferred through the base stream.
    ''' </summary>
    Private _maximumBytesPerSecond As Long

    ''' <summary>
    ''' The number of bytes that has been transferred since the last throttle.
    ''' </summary>
    Private _byteCount As Long

    ''' <summary>
    ''' The start time in milliseconds of the last throttle.
    ''' </summary>
    Private _start As System.Diagnostics.Stopwatch

    ''' <summary>
    ''' Abort flag
    ''' </summary>
    ''' <remarks></remarks>
    Private _abort As Boolean

#End Region

#Region "Properties"
   

    ''' <summary>
    ''' Gets or sets the maximum bytes per second that can be transferred through the base stream.
    ''' </summary>
    ''' <value>The maximum bytes per second.</value>
    Public Property MaximumBytesPerSecond() As Long
        Get
            Return _maximumBytesPerSecond
        End Get
        Set(value As Long)
            If _maximumBytesPerSecond <> value Then
                _maximumBytesPerSecond = value
                Reset()
            End If
        End Set
    End Property

    ''' <summary>
    ''' Gets a value indicating whether the current stream supports reading.
    ''' </summary>
    ''' <returns>true if the stream supports reading; otherwise, false.</returns>
    Public Overrides ReadOnly Property CanRead() As Boolean
        Get
            Return _baseStream.CanRead
        End Get
    End Property

    ''' <summary>
    ''' Gets a value indicating whether the current stream supports seeking.
    ''' </summary>
    ''' <value></value>
    ''' <returns>true if the stream supports seeking; otherwise, false.</returns>
    Public Overrides ReadOnly Property CanSeek() As Boolean
        Get
            Return _baseStream.CanSeek
        End Get
    End Property

    ''' <summary>
    ''' Gets a value indicating whether the current stream supports writing.
    ''' </summary>
    ''' <value></value>
    ''' <returns>true if the stream supports writing; otherwise, false.</returns>
    Public Overrides ReadOnly Property CanWrite() As Boolean
        Get
            Return _baseStream.CanWrite
        End Get
    End Property

    ''' <summary>
    ''' Gets the length in bytes of the stream.
    ''' </summary>
    ''' <value></value>
    ''' <returns>A long value representing the length of the stream in bytes.</returns>
    ''' <exception cref="T:System.NotSupportedException">The base stream does not support seeking. </exception>
    ''' <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
    Public Overrides ReadOnly Property Length() As Long
        Get
            Return _baseStream.Length
        End Get
    End Property

    ''' <summary>
    ''' Gets or sets the position within the current stream.
    ''' </summary>
    ''' <value></value>
    ''' <returns>The current position within the stream.</returns>
    ''' <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
    ''' <exception cref="T:System.NotSupportedException">The base stream does not support seeking. </exception>
    ''' <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
    Public Overrides Property Position() As Long
        Get
            Return _baseStream.Position
        End Get
        Set(value As Long)
            _baseStream.Position = value
        End Set
    End Property
#End Region

#Region "Ctor"
    ''' <summary>
    ''' Initializes a new instance of the <see cref="T:ThrottledStream"/> class with an
    ''' infinite amount of bytes that can be processed.
    ''' </summary>
    ''' <param name="baseStream">The base stream.</param>
    Public Sub New(baseStream As Stream)
        ' Nothing todo.
        Me.New(baseStream, ThrottledStream.Infinite)
    End Sub

    ''' <summary>
    ''' Initializes a new instance of the <see cref="T:ThrottledStream"/> class.
    ''' </summary>
    ''' <param name="baseStream">The base stream.</param>
    ''' <param name="maximumBytesPerSecond">The maximum bytes per second that can be transferred through the base stream.</param>
    ''' <exception cref="ArgumentNullException">Thrown when baseStream is a null reference.</exception>
    ''' <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="maximumBytesPerSecond"/> is a negative value.</exception>
    Public Sub New(baseStream As Stream, maximumBytesPerSecond As Long)
        If baseStream Is Nothing Then
            Throw New ArgumentNullException("baseStream")
        End If

        If maximumBytesPerSecond < 0 Then
            Throw New ArgumentOutOfRangeException("maximumBytesPerSecond", maximumBytesPerSecond, "The maximum number of bytes per second can't be negative.")
        End If

        _baseStream = baseStream
        _maximumBytesPerSecond = maximumBytesPerSecond
        _start = New System.Diagnostics.Stopwatch
        _start.Start()
        _byteCount = 0
        _abort = False
    End Sub
#End Region

#Region "Public methods"
    ''' <summary>
    ''' Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
    ''' </summary>
    ''' <exception cref="T:System.IO.IOException">An I/O error occurs.</exception>
    Public Overrides Sub Flush()
        _baseStream.Flush()
    End Sub

    ''' <summary>
    ''' Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
    ''' </summary>
    ''' <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between offset and (offset + count - 1) replaced by the bytes read from the current source.</param>
    ''' <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream.</param>
    ''' <param name="count">The maximum number of bytes to be read from the current stream.</param>
    ''' <returns>
    ''' The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.
    ''' </returns>
    ''' <exception cref="T:System.ArgumentException">The sum of offset and count is larger than the buffer length. </exception>
    ''' <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
    ''' <exception cref="T:System.NotSupportedException">The base stream does not support reading. </exception>
    ''' <exception cref="T:System.ArgumentNullException">buffer is null. </exception>
    ''' <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
    ''' <exception cref="T:System.ArgumentOutOfRangeException">offset or count is negative. </exception>
    Public Overrides Function Read(buffer As Byte(), offset As Integer, count As Integer) As Integer
        Dim int As Integer = _baseStream.Read(buffer, offset, count)
        Throttle(int)
        Return int
    End Function

    ''' <summary>
    ''' Sets the position within the current stream.
    ''' </summary>
    ''' <param name="offset">A byte offset relative to the origin parameter.</param>
    ''' <param name="origin">A value of type <see cref="T:System.IO.SeekOrigin"></see> indicating the reference point used to obtain the new position.</param>
    ''' <returns>
    ''' The new position within the current stream.
    ''' </returns>
    ''' <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
    ''' <exception cref="T:System.NotSupportedException">The base stream does not support seeking, such as if the stream is constructed from a pipe or console output. </exception>
    ''' <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
    Public Overrides Function Seek(offset As Long, origin As SeekOrigin) As Long
        Return _baseStream.Seek(offset, origin)
    End Function

    ''' <summary>
    ''' Sets the length of the current stream.
    ''' </summary>
    ''' <param name="value">The desired length of the current stream in bytes.</param>
    ''' <exception cref="T:System.NotSupportedException">The base stream does not support both writing and seeking, such as if the stream is constructed from a pipe or console output. </exception>
    ''' <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
    ''' <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
    Public Overrides Sub SetLength(value As Long)
        _baseStream.SetLength(value)
    End Sub

    ''' <summary>
    ''' Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
    ''' </summary>
    ''' <param name="buffer">An array of bytes. This method copies count bytes from buffer to the current stream.</param>
    ''' <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the current stream.</param>
    ''' <param name="count">The number of bytes to be written to the current stream.</param>
    ''' <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
    ''' <exception cref="T:System.NotSupportedException">The base stream does not support writing. </exception>
    ''' <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
    ''' <exception cref="T:System.ArgumentNullException">buffer is null. </exception>
    ''' <exception cref="T:System.ArgumentException">The sum of offset and count is greater than the buffer length. </exception>
    ''' <exception cref="T:System.ArgumentOutOfRangeException">offset or count is negative. </exception>
    Public Overrides Sub Write(buffer As Byte(), offset As Integer, count As Integer)
        Throttle(count)

        _baseStream.Write(buffer, offset, count)
    End Sub

    ''' <summary>
    ''' Returns a <see cref="T:System.String"></see> that represents the current <see cref="T:System.Object"></see>.
    ''' </summary>
    ''' <returns>
    ''' A <see cref="T:System.String"></see> that represents the current <see cref="T:System.Object"></see>.
    ''' </returns>
    Public Overrides Function ToString() As String
        Return _baseStream.ToString()
    End Function

    Public Sub Abort()
        Me._abort = True
    End Sub

    Public Sub [Continue]()
        Me._abort = False
    End Sub
#End Region

#Region "Protected methods"
    ''' <summary>
    ''' Throttles for the specified buffer size in bytes.
    ''' </summary>
    ''' <param name="bufferSizeInBytes">The buffer size in bytes.</param>
    Protected Sub Throttle(bufferSizeInBytes As Integer)
        ' Make sure the buffer isn't empty.
        If _maximumBytesPerSecond <= 0 OrElse bufferSizeInBytes <= 0 Then
            Return
        End If

        _byteCount += bufferSizeInBytes
        Dim elapsedMilliseconds As Double = 1000 * _start.ElapsedTicks / Stopwatch.Frequency

        If elapsedMilliseconds > 0 Then
            ' Calculate the current bps.
            Dim bps As Long = CLng(_byteCount * 1000L / elapsedMilliseconds)

            ' If the bps are more then the maximum bps, try to throttle.
            If bps > _maximumBytesPerSecond Then
                ' Calculate the time to sleep.
                Dim sw As New System.Diagnostics.Stopwatch
                sw.Start()
                Dim wakeElapsed As Long = CLng(_byteCount * 1000L / _maximumBytesPerSecond)
                Dim toSleep As Integer = CInt(wakeElapsed - elapsedMilliseconds)
                If toSleep > 0 Then
                    'Try
                    '    ' Dividimos el tiempo que dormimos para poder pausar o detener correctamente
                    '    ' Ejemplo: si dormimos 2 segundos seguidos y nos piden parar, 
                    '    ' tenemos que esperar 2 segundos antes que finalicemos, lo cual penaliza
                    '    ' la responsividad de la UI. Si en cambio dormimos 10 veces 200ms cada vez
                    '    ' podemos abortar de manera mas "agil".
                    '    While toSleep > 150
                    '        Thread.Sleep(100)

                    '        ' ¿¿No debería reiniciarse sw en cada bucle para descontar el tiempo que ha dormido??
                    '        ' Si se reinicia en cada bucle no funciona bien, en cambio así sí... (???)
                    '        toSleep = CInt(toSleep - sw.ElapsedMilliseconds)
                    '        If _abort Then toSleep = 0
                    '    End While
                    '    If toSleep > 0 Then Thread.Sleep(toSleep)
                    '    'Thread.Sleep(toSleep)

                    '    ' Eatup ThreadAbortException.
                    'Catch generatedExceptionName As ThreadAbortException
                    'End Try

                    ' New system
                    Try
                        Dim SleepUntil As DateTime = DateTime.Now.ToUniversalTime.AddMilliseconds(toSleep)
                        While SleepUntil.Subtract(DateTime.Now.ToUniversalTime).TotalMilliseconds > 150
                            Thread.Sleep(100)
                            If _abort Then
                                SleepUntil = DateTime.Now.ToUniversalTime
                            End If
                        End While
                        toSleep = CInt(SleepUntil.Subtract(DateTime.Now.ToUniversalTime).TotalMilliseconds)
                        If toSleep > 0 Then Thread.Sleep(toSleep)
                    Catch generatedExceptionName As ThreadAbortException
                    End Try

                End If
                sw.Stop()

            End If
            Reset()
        End If
    End Sub


    ''' <summary>
    ''' Will reset the bytecount to 0 and reset the start time to the current time.
    ''' </summary>
    Protected Sub Reset()
        Dim difference As Double = 1000 * _start.ElapsedTicks / Stopwatch.Frequency

        ' Only reset counters when a known history is available of more then 1 second.
        If difference > 1000 Then
            _byteCount = 0
            _start.Stop()
            _start.Reset()
            _start.Start()
        End If
    End Sub
#End Region
End Class


'=======================================================
'Service provided by Telerik (www.telerik.com)
'Conversion powered by NRefactory.
'Twitter: @telerik, @toddanglin
'Facebook: facebook.com/telerik
'=======================================================
