using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace MegaDownloader
{

    /// <summary>
/// Class for streaming data with throttling support.
/// </summary>
    public class ThrottledStream : Stream
    {
        /// <summary>
    /// A constant used to specify an infinite number of bytes that can be transferred per second.
    /// </summary>
        public const long Infinite = 0L;

        #region Private members
        /// <summary>
    /// The base stream.
    /// </summary>
        private Stream _baseStream;

        /// <summary>
    /// The maximum bytes per second that can be transferred through the base stream.
    /// </summary>
        private long _maximumBytesPerSecond;

        /// <summary>
    /// The number of bytes that has been transferred since the last throttle.
    /// </summary>
        private long _byteCount;

        /// <summary>
    /// The start time in milliseconds of the last throttle.
    /// </summary>
        private Stopwatch _start;

        /// <summary>
    /// Abort flag
    /// </summary>
    /// <remarks></remarks>
        private bool _abort;

        #endregion

        #region Properties


        /// <summary>
    /// Gets or sets the maximum bytes per second that can be transferred through the base stream.
    /// </summary>
    /// <value>The maximum bytes per second.</value>
        public long MaximumBytesPerSecond
        {
            get
            {
                return _maximumBytesPerSecond;
            }
            set
            {
                if (_maximumBytesPerSecond != value)
                {
                    _maximumBytesPerSecond = value;
                    Reset();
                }
            }
        }

        /// <summary>
    /// Gets a value indicating whether the current stream supports reading.
    /// </summary>
    /// <returns>true if the stream supports reading; otherwise, false.</returns>
        public override bool CanRead
        {
            get
            {
                return _baseStream.CanRead;
            }
        }

        /// <summary>
    /// Gets a value indicating whether the current stream supports seeking.
    /// </summary>
    /// <value></value>
    /// <returns>true if the stream supports seeking; otherwise, false.</returns>
        public override bool CanSeek
        {
            get
            {
                return _baseStream.CanSeek;
            }
        }

        /// <summary>
    /// Gets a value indicating whether the current stream supports writing.
    /// </summary>
    /// <value></value>
    /// <returns>true if the stream supports writing; otherwise, false.</returns>
        public override bool CanWrite
        {
            get
            {
                return _baseStream.CanWrite;
            }
        }

        /// <summary>
    /// Gets the length in bytes of the stream.
    /// </summary>
    /// <value></value>
    /// <returns>A long value representing the length of the stream in bytes.</returns>
    /// <exception cref="T:System.NotSupportedException">The base stream does not support seeking. </exception>
    /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
        public override long Length
        {
            get
            {
                return _baseStream.Length;
            }
        }

        /// <summary>
    /// Gets or sets the position within the current stream.
    /// </summary>
    /// <value></value>
    /// <returns>The current position within the stream.</returns>
    /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
    /// <exception cref="T:System.NotSupportedException">The base stream does not support seeking. </exception>
    /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
        public override long Position
        {
            get
            {
                return _baseStream.Position;
            }
            set
            {
                _baseStream.Position = value;
            }
        }
        #endregion

        #region Ctor
        /// <summary>
    /// Initializes a new instance of the <see cref="T:ThrottledStream"/> class with an
    /// infinite amount of bytes that can be processed.
    /// </summary>
    /// <param name="baseStream">The base stream.</param>
        // Nothing todo.
        public ThrottledStream(Stream baseStream) : this(baseStream, Infinite)
        {
        }

        /// <summary>
    /// Initializes a new instance of the <see cref="T:ThrottledStream"/> class.
    /// </summary>
    /// <param name="baseStream">The base stream.</param>
    /// <param name="maximumBytesPerSecond">The maximum bytes per second that can be transferred through the base stream.</param>
    /// <exception cref="ArgumentNullException">Thrown when baseStream is a null reference.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="maximumBytesPerSecond"/> is a negative value.</exception>
        public ThrottledStream(Stream baseStream, long maximumBytesPerSecond)
        {
            if (baseStream is null)
            {
                throw new ArgumentNullException("baseStream");
            }

            if (maximumBytesPerSecond < 0L)
            {
                throw new ArgumentOutOfRangeException("maximumBytesPerSecond", maximumBytesPerSecond, "The maximum number of bytes per second can't be negative.");
            }

            _baseStream = baseStream;
            _maximumBytesPerSecond = maximumBytesPerSecond;
            _start = new Stopwatch();
            _start.Start();
            _byteCount = 0L;
            Volatile.Write(ref _abort, false);
        }
        #endregion

        #region Public methods
        /// <summary>
    /// Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
    /// </summary>
    /// <exception cref="T:System.IO.IOException">An I/O error occurs.</exception>
        public override void Flush()
        {
            _baseStream.Flush();
        }

        /// <summary>
    /// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
    /// </summary>
    /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between offset and (offset + count - 1) replaced by the bytes read from the current source.</param>
    /// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream.</param>
    /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
    /// <returns>
    /// The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.
    /// </returns>
    /// <exception cref="T:System.ArgumentException">The sum of offset and count is larger than the buffer length. </exception>
    /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
    /// <exception cref="T:System.NotSupportedException">The base stream does not support reading. </exception>
    /// <exception cref="T:System.ArgumentNullException">buffer is null. </exception>
    /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">offset or count is negative. </exception>
        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = _baseStream.Read(buffer, offset, count);
            Throttle(bytesRead);
            return bytesRead;
        }

        /// <summary>
    /// Sets the position within the current stream.
    /// </summary>
    /// <param name="offset">A byte offset relative to the origin parameter.</param>
    /// <param name="origin">A value of type <see cref="T:System.IO.SeekOrigin"></see> indicating the reference point used to obtain the new position.</param>
    /// <returns>
    /// The new position within the current stream.
    /// </returns>
    /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
    /// <exception cref="T:System.NotSupportedException">The base stream does not support seeking, such as if the stream is constructed from a pipe or console output. </exception>
    /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
        public override long Seek(long offset, SeekOrigin origin)
        {
            return _baseStream.Seek(offset, origin);
        }

        /// <summary>
    /// Sets the length of the current stream.
    /// </summary>
    /// <param name="value">The desired length of the current stream in bytes.</param>
    /// <exception cref="T:System.NotSupportedException">The base stream does not support both writing and seeking, such as if the stream is constructed from a pipe or console output. </exception>
    /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
    /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
        public override void SetLength(long value)
        {
            _baseStream.SetLength(value);
        }

        /// <summary>
    /// Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
    /// </summary>
    /// <param name="buffer">An array of bytes. This method copies count bytes from buffer to the current stream.</param>
    /// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the current stream.</param>
    /// <param name="count">The number of bytes to be written to the current stream.</param>
    /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
    /// <exception cref="T:System.NotSupportedException">The base stream does not support writing. </exception>
    /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
    /// <exception cref="T:System.ArgumentNullException">buffer is null. </exception>
    /// <exception cref="T:System.ArgumentException">The sum of offset and count is greater than the buffer length. </exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">offset or count is negative. </exception>
        public override void Write(byte[] buffer, int offset, int count)
        {
            Throttle(count);

            _baseStream.Write(buffer, offset, count);
        }

        /// <summary>
    /// Returns a <see cref="T:System.String"></see> that represents the current <see cref="T:System.Object"></see>.
    /// </summary>
    /// <returns>
    /// A <see cref="T:System.String"></see> that represents the current <see cref="T:System.Object"></see>.
    /// </returns>
        public override string ToString()
        {
            return _baseStream.ToString();
        }

        public void Abort()
        {
            Volatile.Write(ref _abort, true);
        }

        public void Continue()
        {
            Volatile.Write(ref _abort, false);
        }
        #endregion

        #region Protected methods
        /// <summary>
    /// Throttles for the specified buffer size in bytes.
    /// </summary>
    /// <param name="bufferSizeInBytes">The buffer size in bytes.</param>
        protected void Throttle(int bufferSizeInBytes)
        {
            // Make sure the buffer isn't empty.
            if (_maximumBytesPerSecond <= 0L || bufferSizeInBytes <= 0)
            {
                return;
            }

            _byteCount += bufferSizeInBytes;
            double elapsedMilliseconds = 1000L * _start.ElapsedTicks / (double)Stopwatch.Frequency;

            if (elapsedMilliseconds > 0d)
            {
                // Calculate the current bps.
                long bps = (long)Math.Round(_byteCount * 1000L / elapsedMilliseconds);

                // If the bps are more then the maximum bps, try to throttle.
                if (bps > _maximumBytesPerSecond)
                {
                    // Calculate the time to sleep.
                    var sw = new Stopwatch();
                    sw.Start();
                    long wakeElapsed = (long)Math.Round(_byteCount * 1000L / (double)_maximumBytesPerSecond);
                    int toSleep = (int)Math.Round(wakeElapsed - elapsedMilliseconds);
                    if (toSleep > 0)
                    {
                        // Try
                        // ' Dividimos el tiempo que dormimos para poder pausar o detener correctamente
                        // ' Ejemplo: si dormimos 2 segundos seguidos y nos piden parar, 
                        // ' tenemos que esperar 2 segundos antes que finalicemos, lo cual penaliza
                        // ' la responsividad de la UI. Si en cambio dormimos 10 veces 200ms cada vez
                        // ' podemos abortar de manera mas "agil".
                        // While toSleep > 150
                        // Thread.Sleep(100)

                        // ' ¿¿No debería reiniciarse sw en cada bucle para descontar el tiempo que ha dormido??
                        // ' Si se reinicia en cada bucle no funciona bien, en cambio así sí... (???)
                        // toSleep = CInt(toSleep - sw.ElapsedMilliseconds)
                        // If _abort Then toSleep = 0
                        // End While
                        // If toSleep > 0 Then Thread.Sleep(toSleep)
                        // 'Thread.Sleep(toSleep)

                        // ' Eatup ThreadAbortException.
                        // Catch generatedExceptionName As ThreadAbortException
                        // End Try

                        // New system
                        try
                        {
                            // P3-4: monotonic sleep deadline. The old code slept "until
                            // DateTime.UtcNow reached a wall-clock timestamp" — an NTP step
                            // or a manual clock change could stretch the sleep (clock jumped
                            // back) or erase it (clock jumped forward), leaving the speed
                            // limit broken for seconds or minutes afterwards. Stopwatch
                            // cannot jump, matching the monotonic _start used above.
                            var swSleep = Stopwatch.StartNew();
                            while (toSleep - swSleep.ElapsedMilliseconds > 150L)
                            {
                                Thread.Sleep(20);
                                if (Volatile.Read(ref _abort))
                                    break;
                            }
                            if (!Volatile.Read(ref _abort))
                            {
                                int remaining = (int)(toSleep - swSleep.ElapsedMilliseconds);
                                if (remaining > 0)
                                    Thread.Sleep(remaining);
                            }
                        }
                        catch (ThreadAbortException generatedExceptionName)
                        {
                        }

                    }
                    sw.Stop();

                }
                Reset();
            }
        }


        /// <summary>
    /// Will reset the bytecount to 0 and reset the start time to the current time.
    /// </summary>
        protected void Reset()
        {
            double difference = 1000L * _start.ElapsedTicks / (double)Stopwatch.Frequency;

            // Only reset counters when a known history is available of more then 1 second.
            if (difference > 1000d)
            {
                _byteCount = 0L;
                _start.Stop();
                _start.Reset();
                _start.Start();
            }
        }
        #endregion
    }
}


// =======================================================
// Service provided by Telerik (www.telerik.com)
// Conversion powered by NRefactory.
// Twitter: @telerik, @toddanglin
// Facebook: facebook.com/telerik
// =======================================================
