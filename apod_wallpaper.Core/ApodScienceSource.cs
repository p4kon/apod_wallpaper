using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    internal sealed class ApodScienceResponse : IDisposable
    {
        private readonly Action _dispose;
        private int _disposed;
        internal ApodScienceResponse(int status, Stream body, string contentType = "application/json",
            long? contentLength = null, TimeSpan? retryAfter = null, Action dispose = null)
        {
            Status = status; Body = body; ContentType = contentType;
            ContentLength = contentLength; RetryAfter = retryAfter; _dispose = dispose;
        }
        internal int Status { get; }
        internal Stream Body { get; }
        internal string ContentType { get; }
        internal long? ContentLength { get; }
        internal TimeSpan? RetryAfter { get; }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            if (_dispose != null) _dispose(); else Body?.Dispose();
        }
    }

    internal sealed class ApodScienceRequestException : IOException
    {
        internal ApodScienceRequestException(int status) : base("NASA Science HTTP " + status + ".") { Status = status; }
        internal int Status { get; }
    }

    internal sealed class ApodScienceSource
    {
        private const int MaximumBodyBytes = 1024 * 1024;
        private static readonly Lazy<HttpClient> Client = new Lazy<HttpClient>(() => new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        }) { Timeout = Timeout.InfiniteTimeSpan });
        private readonly object _gate = new object();
        private readonly Dictionary<DateTime, Flight> _flights = new Dictionary<DateTime, Flight>();
        private readonly SemaphoreSlim _slots = new SemaphoreSlim(2, 2);
        private readonly Func<Uri, CancellationToken, Task<ApodScienceResponse>> _send;
        private readonly Func<DateTime> _utcNow;
        private readonly TimeSpan _timeout;
        private DateTime _retryNotBeforeUtc;

        private sealed class Flight
        {
            internal readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
            internal readonly TaskCompletionSource<string> Completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal Task Worker;
            internal int Waiters;
            internal bool Finished;
        }

        internal ApodScienceSource(Func<Uri, CancellationToken, Task<ApodScienceResponse>> send = null,
            TimeSpan? timeout = null, Func<DateTime> utcNow = null)
        {
            _send = send ?? SendAsync;
            _timeout = timeout ?? TimeSpan.FromSeconds(8);
            if (_timeout <= TimeSpan.Zero || _timeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        internal async Task<ApodScienceRecord> GetEntryAsync(DateTime date, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            date = date.Date;
            Flight flight;
            lock (_gate)
            {
                if (!_flights.TryGetValue(date, out flight))
                {
                    flight = new Flight { Waiters = 1 };
                    _flights.Add(date, flight);
                    // The worker owns completion/error observation even if every consumer cancels.
                    flight.Worker = RunFlightAsync(date, flight);
                }
                else flight.Waiters++;
            }
            try
            {
                var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (token.Register(() => cancelled.TrySetResult(true)))
                {
                    await Task.WhenAny(flight.Completion.Task, cancelled.Task).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    var json = await flight.Completion.Task.ConfigureAwait(false);
                    // Parse per consumer: ApodEntry is mutable and must not escape as shared state.
                    return ApodScienceParser.Parse(json, date);
                }
            }
            finally
            {
                lock (_gate)
                {
                    if (--flight.Waiters == 0)
                    {
                        RemoveFlight(date, flight);
                        if (flight.Finished) flight.Cancellation.Dispose();
                        else flight.Cancellation.Cancel();
                    }
                }
            }
        }

        private void RemoveFlight(DateTime date, Flight flight)
        {
            if (_flights.TryGetValue(date, out var current) && ReferenceEquals(current, flight))
                _flights.Remove(date);
        }

        private async Task RunFlightAsync(DateTime date, Flight flight)
        {
            try
            {
                var json = await ReadJsonAsync(date, flight.Cancellation.Token).ConfigureAwait(false);
                flight.Completion.TrySetResult(json);
            }
            catch (OperationCanceledException) { flight.Completion.TrySetCanceled(); }
            catch (Exception ex)
            {
                flight.Completion.TrySetException(ex);
                // A cancelled last waiter may no longer observe this exception.
                var observed = flight.Completion.Task.Exception;
            }
            finally
            {
                lock (_gate)
                {
                    RemoveFlight(date, flight);
                    flight.Finished = true;
                    if (flight.Waiters == 0) flight.Cancellation.Dispose();
                }
            }
        }

        private async Task<string> ReadJsonAsync(DateTime date, CancellationToken callerToken)
        {
            using (var budget = CancellationTokenSource.CreateLinkedTokenSource(callerToken))
            {
                budget.CancelAfter(_timeout);
                var token = budget.Token;
                var entered = false;
                try
                {
                    await _slots.WaitAsync(token).ConfigureAwait(false);
                    entered = true;
                    lock (_gate)
                        if (_utcNow() < _retryNotBeforeUtc) throw new ApodScienceRequestException(429);

                    using (var response = await _send(new Uri(ApodScienceParser.BuildUrl(date)), token).ConfigureAwait(false))
                    {
                        token.ThrowIfCancellationRequested();
                        if (response.Status == 429)
                        {
                            var delay = response.RetryAfter ?? TimeSpan.FromMinutes(1);
                            if (delay < TimeSpan.FromSeconds(1)) delay = TimeSpan.FromSeconds(1);
                            lock (_gate)
                            {
                                var now = _utcNow();
                                var until = delay > DateTime.MaxValue - now ? DateTime.MaxValue : now.Add(delay);
                                if (until > _retryNotBeforeUtc) _retryNotBeforeUtc = until;
                            }
                        }
                        if (response.Status == 404)
                            throw new ApodEntryUnavailableException(date, "NASA Science publication was not found.");
                        if (response.Status != 200) throw new ApodScienceRequestException(response.Status);
                        var type = response.ContentType ?? string.Empty;
                        if (!string.Equals(type, "application/json", StringComparison.OrdinalIgnoreCase)
                            && !(type.StartsWith("application/", StringComparison.OrdinalIgnoreCase) && type.EndsWith("+json", StringComparison.OrdinalIgnoreCase)))
                            throw new InvalidDataException("NASA Science did not return JSON.");
                        if (response.ContentLength > MaximumBodyBytes)
                            throw new InvalidDataException("NASA Science JSON exceeds the size limit.");

                        // ResponseHeadersRead does not cover body timeout/size. Bound decompressed bytes too.
                        using (token.Register(response.Dispose))
                        using (var bytes = new MemoryStream())
                        {
                            var buffer = new byte[8192];
                            int count;
                            while ((count = await response.Body.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) != 0)
                            {
                                token.ThrowIfCancellationRequested();
                                if (bytes.Length + count > MaximumBodyBytes)
                                    throw new InvalidDataException("NASA Science JSON exceeds the size limit.");
                                bytes.Write(buffer, 0, count);
                            }
                            token.ThrowIfCancellationRequested();
                            return new UTF8Encoding(false, true).GetString(bytes.ToArray()).TrimStart('\uFEFF');
                        }
                    }
                }
                catch (Exception ex) when (token.IsCancellationRequested &&
                    (ex is OperationCanceledException || ex is IOException || ex is ObjectDisposedException))
                {
                    callerToken.ThrowIfCancellationRequested();
                    throw new TimeoutException("NASA Science metadata request timed out.", ex);
                }
                finally { if (entered) _slots.Release(); }
            }
        }

        private static async Task<ApodScienceResponse> SendAsync(Uri uri, CancellationToken token)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
            {
                request.Headers.Accept.ParseAdd("application/json");
                var response = await Client.Value.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                try
                {
                    TimeSpan? retry = response.Headers.RetryAfter?.Delta;
                    if (!retry.HasValue && response.Headers.RetryAfter?.Date != null)
                        retry = response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow;
                    var body = response.StatusCode == HttpStatusCode.OK
                        ? await response.Content.ReadAsStreamAsync().ConfigureAwait(false) : Stream.Null;
                    return new ApodScienceResponse((int)response.StatusCode, body,
                        response.Content.Headers.ContentType?.MediaType, response.Content.Headers.ContentLength,
                        retry, response.Dispose);
                }
                catch { response.Dispose(); throw; }
            }
        }
    }
}
