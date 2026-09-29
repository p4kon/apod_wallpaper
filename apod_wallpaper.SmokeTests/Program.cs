using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace apod_wallpaper.SmokeTests
{
    internal static class Program
    {
        private static int _failures;
        private static string _secretStoreDirectory;
        private static string _logDirectory;
        private static InMemorySettingsStore _settingsStore;
        private static apod_wallpaper.DpapiUserSecretStore _secretStore;

        private static string ScienceFixture(string name)
        {
            return File.ReadAllText(Path.Combine(GetRepositoryRoot(), "apod_wallpaper.SmokeTests", "Fixtures", "NasaScience", name + ".json"));
        }

        private static void NasaScienceDateUrlsArePure()
        {
            const string root = "https://science.nasa.gov/wp-json/wp/v2/apod-basic/";
            Assert(apod_wallpaper.ApodScienceParser.BuildUrl(new DateTime(2026, 9, 28)) == root + "260928", "Modern date URL must be exact.");
            Assert(apod_wallpaper.ApodScienceParser.BuildUrl(new DateTime(1995, 6, 16)) == root + "950616", "First APOD date must work.");
            Assert(apod_wallpaper.ApodScienceParser.BuildUrl(new DateTime(2000, 1, 1)) == root + "000101", "Century boundary must preserve zeroes.");
            Assert(apod_wallpaper.ApodScienceParser.BuildUrl(new DateTime(2024, 2, 29)) == root + "240229", "Leap day must work.");
        }

        private static void NasaScienceSelectsPrimaryImage()
        {
            var parsed = apod_wallpaper.ApodScienceParser.Parse(ScienceFixture("image"), new DateTime(2026, 9, 27));
            var entry = parsed.Entry;
            Assert(entry.HasImage && entry.MediaType == "image", "Primary image should be available.");
            Assert(entry.PreviewImageUrl.EndsWith("?w=800&h=800&fit=clip"), "Preview must be bounded without cropping.");
            Assert(entry.BestImageUrl == "https://assets.science.nasa.gov/content/dam/science/cds/apod/andromeda.jpg", "Original must bypass CDN default resizing.");
            Assert(parsed.PostUrl.Contains("/image-article/"), "Canonical article URL must remain separate.");
            Assert(entry.Title == "Andromeda & stars", "Title entities must be decoded.");
            Assert(entry.Copyright == "A & B", "Credits must be plain text.");
            Assert(entry.Explanation == "Stars & dust.\nSecond line.", "Explanation must remove label/scripts and preserve breaks.");
            Assert(entry.ResolvedFromSource == "nasa_science", "Source should be identifiable.");
        }

        private static void NasaSciencePreservesArchiveOriginal()
        {
            var entry = apod_wallpaper.ApodScienceParser.Parse(ScienceFixture("archive"), new DateTime(1995, 6, 16)).Entry;
            Assert(entry.BestImageUrl.EndsWith("/e_lens.gif"), "Archive must use real GIF, not news-thumbnail.");
            Assert(entry.PreviewImageUrl == entry.BestImageUrl, "Identical source URLs are valid; do not invent a thumbnail.");
        }

        private static string ScienceImageFixtureUrls(string preview, string original)
        {
            return ScienceFixture("image")
                .Replace("https://assets.science.nasa.gov/dynamicimage/assets/science/cds/apod/andromeda.jpg?fit=clip&amp;w=800", preview)
                .Replace("https://assets.science.nasa.gov/dynamicimage/assets/science/cds/apod/andromeda.jpg?fit=clip&amp;w=4298", original);
        }

        private static void NasaSciencePreviewPolicyPreservesOtherSources()
        {
            foreach (var url in new[]
            {
                "https://example.org/nebula.jpg",
                "https://assets.science.nasa.gov.evil.example/dynamicimage/assets/science/nebula.jpg",
                "https://assets.science.nasa.gov/unknown/nebula.jpg",
                "https://assets.science.nasa.gov/content/dam/science/nebula.gif",
                "https://assets.science.nasa.gov/dynamicimage/assets/science/nebula.webp",
                "https://assets.science.nasa.gov/dynamicimage/assets/science/nebula.jpg?token=signature",
                "https://assets.science.nasa.gov/dynamicimage/assets/science/nebula.jpg?fit=crop",
                "https://assets.science.nasa.gov/dynamicimage/assets/science/nebula.jpg?w=400&w=800",
                "https://assets.science.nasa.gov/content/dam/science/nebula.png?download=1"
            })
            {
                var entry = apod_wallpaper.ApodScienceParser.Parse(ScienceImageFixtureUrls(url, url), new DateTime(2026, 9, 27)).Entry;
                Assert(entry.Url == url && entry.HdUrl == url, "Unverified hosts, formats and parameters must stay unchanged: " + url);
            }
        }

        private static void NasaSciencePreviewPolicyHandlesStaticAssets()
        {
            const string original = "https://assets.science.nasa.gov/content/dam/science/missions/webb/nebula.png";
            var parsed = apod_wallpaper.ApodScienceParser.Parse(ScienceImageFixtureUrls(original, original), new DateTime(2026, 9, 27));
            Assert(parsed.Entry.HdUrl == original, "Static asset original must stay untouched.");
            Assert(parsed.Entry.Url == "https://assets.science.nasa.gov/dynamicimage/assets/science/missions/webb/nebula.png?w=800&h=800&fit=clip", "Static PNG should receive a bounded CDN preview.");
        }

        private static void NasaSciencePreviewPolicyKeepsSeparateAssets()
        {
            const string preview = "https://assets.science.nasa.gov/dynamicimage/assets/science/annotated.jpg";
            const string original = "https://assets.science.nasa.gov/dynamicimage/assets/science/original.jpg";
            var parsed = apod_wallpaper.ApodScienceParser.Parse(ScienceImageFixtureUrls(preview, original), new DateTime(2026, 9, 27));
            Assert(parsed.Entry.Url.Contains("/annotated.jpg?w=800&h=800&fit=clip"), "Keep the source preview variant.");
            Assert(parsed.Entry.HdUrl == "https://assets.science.nasa.gov/content/dam/science/original.jpg", "Never substitute preview variant for original.");
            Assert(parsed.SourcePreviewUrl == preview && parsed.SourceOriginalUrl == original, "Keep original source links for explicit integration-time fallback.");
        }

        private static void NasaScienceRejectsVideoPosters()
        {
            foreach (var kind in new[] { "video", "iframe" })
            {
                var json = ScienceFixture("video").Replace("\"media_type\": \"video\"", "\"media_type\": \"" + kind + "\"");
                var entry = apod_wallpaper.ApodScienceParser.Parse(json, new DateTime(2026, 8, 31)).Entry;
                Assert(!entry.HasImage && string.IsNullOrEmpty(entry.HdUrl), "Video poster must not be a downloadable image.");
            }
        }

        private static void NasaScienceHandlesTextOnly()
        {
            var entry = apod_wallpaper.ApodScienceParser.Parse(ScienceFixture("text-only"), new DateTime(2012, 3, 12)).Entry;
            Assert(!entry.HasImage && entry.MediaType == "other", "Verified text-only page must be unsupported.");
            Assert(string.IsNullOrEmpty(entry.Url) && string.IsNullOrEmpty(entry.HdUrl), "Unsupported page must not expose poster or article as image.");
        }

        private static void NasaScienceRejectsInvalidPayloads()
        {
            var json = ScienceFixture("image");
            foreach (var invalid in new[]
            {
                "", "{}", "[]", "<html>Error</html>", "null", "{invalid}",
                json.Replace("2026-09-27", "2026-09-26"),
                json.Replace("2026 September 27", "2026 September 26"),
                json.Replace("\"basic_html\"", "\"missing_html\""),
                json.Replace("https://science.nasa.gov/image-article/", "https://evil.example/image-article/"),
                json.Replace("https://assets.science.nasa.gov/dynamicimage/assets/science/cds/apod/andromeda.jpg", "file:///C:/private.jpg"),
                json.Replace("https://assets.science.nasa.gov/dynamicimage/assets/science/cds/apod/andromeda.jpg", "https://127.0.0.1/private.jpg"),
                json.Replace("andromeda.jpg", "news-thumbnail.png"),
                json.Replace("\"media_type\": \"image\"", "\"media_type\": \"surprise\"")
            })
            {
                var rejected = false;
                try { apod_wallpaper.ApodScienceParser.Parse(invalid, new DateTime(2026, 9, 27)); }
                catch (InvalidDataException) { rejected = true; }
                Assert(rejected, "Untrusted or unsupported payload must fail closed.");
            }
        }

        private static apod_wallpaper.ApodScienceResponse ScienceResponse(int status = 200, string json = null,
            string type = "application/json", long? length = null, TimeSpan? retry = null)
        {
            return new apod_wallpaper.ApodScienceResponse(status,
                new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json ?? ScienceFixture("image"))), type, length, retry);
        }

        private static async Task ExpectScienceFailureAsync<T>(Task task) where T : Exception
        {
            try { await task; }
            catch (T) { return; }
            throw new InvalidOperationException("Expected " + typeof(T).Name);
        }

        private static async Task NasaScienceTransportBoundsAsync()
        {
            var date = new DateTime(2026, 9, 27);
            var calls = 0;
            var source = new apod_wallpaper.ApodScienceSource((uri, ct) =>
            {
                calls++;
                Assert(uri.AbsoluteUri == apod_wallpaper.ApodScienceParser.BuildUrl(date), "Only date JSON, without key or image requests.");
                return Task.FromResult(ScienceResponse());
            });
            Assert((await source.GetEntryAsync(date)).Entry.HasImage, "Valid JSON must parse.");
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                await ExpectScienceFailureAsync<OperationCanceledException>(source.GetEntryAsync(date, cancelled.Token));
            }
            Assert(calls == 1, "Pre-cancelled request must not send.");
            foreach (var status in new[] { 301, 302, 403, 500, 503 })
            {
                source = new apod_wallpaper.ApodScienceSource((uri, ct) => Task.FromResult(ScienceResponse(status)));
                await ExpectScienceFailureAsync<apod_wallpaper.ApodScienceRequestException>(source.GetEntryAsync(date));
            }
            source = new apod_wallpaper.ApodScienceSource((uri, ct) => Task.FromResult(ScienceResponse(404)));
            await ExpectScienceFailureAsync<apod_wallpaper.ApodEntryUnavailableException>(source.GetEntryAsync(date));
            foreach (var response in new[] { ScienceResponse(type: "text/html"), ScienceResponse(json: "{}"),
                ScienceResponse(length: 1048577), ScienceResponse(json: new string('x', 1048577)),
                ScienceResponse(json: ScienceFixture("image").Replace("2026-09-27", "2026-09-26")) })
            {
                source = new apod_wallpaper.ApodScienceSource((uri, ct) => Task.FromResult(response));
                await ExpectScienceFailureAsync<InvalidDataException>(source.GetEntryAsync(date));
            }
            source = new apod_wallpaper.ApodScienceSource(async (uri, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return ScienceResponse();
            }, TimeSpan.FromMilliseconds(100));
            await ExpectScienceFailureAsync<TimeoutException>(source.GetEntryAsync(date));
            var now = new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc);
            calls = 0;
            source = new apod_wallpaper.ApodScienceSource((uri, ct) =>
            { calls++; return Task.FromResult(ScienceResponse(429, retry: TimeSpan.FromMinutes(2))); }, utcNow: () => now);
            await ExpectScienceFailureAsync<apod_wallpaper.ApodScienceRequestException>(source.GetEntryAsync(date));
            await ExpectScienceFailureAsync<apod_wallpaper.ApodScienceRequestException>(source.GetEntryAsync(date.AddDays(-1)));
            Assert(calls == 1, "Retry-After must stop requests across dates.");
            now = now.AddMinutes(2);
            await ExpectScienceFailureAsync<apod_wallpaper.ApodScienceRequestException>(source.GetEntryAsync(date));
            Assert(calls == 2, "Retry must be allowed after cooldown.");
        }

        private static async Task NasaScienceTransportSharesRequestsAsync()
        {
            var date = new DateTime(2026, 9, 27);
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;
            var source = new apod_wallpaper.ApodScienceSource(async (uri, ct) =>
            { Interlocked.Increment(ref calls); await gate.Task; ct.ThrowIfCancellationRequested(); return ScienceResponse(); });
            using (var cancel = new CancellationTokenSource())
            {
                var first = source.GetEntryAsync(date, cancel.Token);
                var second = source.GetEntryAsync(date);
                var third = source.GetEntryAsync(date);
                cancel.Cancel();
                await ExpectScienceFailureAsync<OperationCanceledException>(first);
                gate.SetResult(true);
                var records = await Task.WhenAll(second, third);
                Assert(calls == 1, "Concurrent same-date requests must share the network.");
                Assert(!ReferenceEquals(records[0].Entry, records[1].Entry), "Consumers must not share mutable entries.");
            }
            var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            source = new apod_wallpaper.ApodScienceSource(async (uri, ct) =>
            {
                try { await Task.Delay(Timeout.Infinite, ct); }
                finally { stopped.TrySetResult(true); }
                return ScienceResponse();
            });
            using (var cancel = new CancellationTokenSource())
            {
                var pending = source.GetEntryAsync(date, cancel.Token);
                cancel.Cancel();
                await ExpectScienceFailureAsync<OperationCanceledException>(pending);
                Assert(await Task.WhenAny(stopped.Task, Task.Delay(2000)) == stopped.Task, "Last waiter cancellation must stop network.");
            }
        }

        private sealed class SlowScienceStream : MemoryStream
        {
            internal bool Disposed;
            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return 0;
            }
            protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
        }

        private static async Task NasaScienceTransportBodyAndConcurrencyAsync()
        {
            var date = new DateTime(2026, 9, 27);
            var stream = new SlowScienceStream();
            var source = new apod_wallpaper.ApodScienceSource((uri, ct) => Task.FromResult(
                new apod_wallpaper.ApodScienceResponse(200, stream)), TimeSpan.FromMilliseconds(100));
            await ExpectScienceFailureAsync<TimeoutException>(source.GetEntryAsync(date));
            Assert(stream.Disposed, "Timed-out response body must be disposed.");

            var calls = 0;
            var active = 0;
            var peak = 0;
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            source = new apod_wallpaper.ApodScienceSource(async (uri, ct) =>
            {
                Interlocked.Increment(ref calls);
                var count = Interlocked.Increment(ref active);
                lock (release) peak = Math.Max(peak, count);
                try { await release.Task; }
                finally { Interlocked.Decrement(ref active); }
                return ScienceResponse(404);
            });
            var requests = Enumerable.Range(0, 5).Select(i => source.GetEntryAsync(date.AddDays(-i))).ToArray();
            Assert(calls == 2, "Only two distinct dates may enter the transport before slots release.");
            release.SetResult(true);
            foreach (var task in requests) await ExpectScienceFailureAsync<apod_wallpaper.ApodEntryUnavailableException>(task);
            Assert(calls == 5 && peak <= 2, "Queued requests must complete within concurrency limit.");
            await ExpectScienceFailureAsync<apod_wallpaper.ApodEntryUnavailableException>(source.GetEntryAsync(date));
            Assert(calls == 6, "Failed flight must not be cached forever.");

            calls = 0;
            using (var cancel = new CancellationTokenSource())
            using (var queuedCancel = new CancellationTokenSource())
            {
                source = new apod_wallpaper.ApodScienceSource(async (uri, ct) =>
                {
                    Interlocked.Increment(ref calls);
                    await Task.Delay(Timeout.Infinite, ct);
                    return ScienceResponse();
                });
                requests = Enumerable.Range(0, 2).Select(i => source.GetEntryAsync(date.AddDays(-i), cancel.Token)).ToArray();
                var queued = source.GetEntryAsync(date.AddDays(-2), queuedCancel.Token);
                queuedCancel.Cancel();
                await ExpectScienceFailureAsync<OperationCanceledException>(queued);
                cancel.Cancel();
                foreach (var task in requests) await ExpectScienceFailureAsync<OperationCanceledException>(task);
                Assert(calls == 2, "Cancelled queued requests must not send.");
            }
        }

        private sealed class RecordingApodSource : apod_wallpaper.IApodClient
        {
            internal readonly List<string> Calls = new List<string>();
            internal readonly apod_wallpaper.ApodEntry Entry = new apod_wallpaper.ApodEntry();
            internal DateTime Start;
            internal DateTime End;
            internal string Key;
            internal Exception Failure;
            internal readonly TaskCompletionSource<apod_wallpaper.ApodEntry> Pending =
                new TaskCompletionSource<apod_wallpaper.ApodEntry>(TaskCreationOptions.RunContinuationsAsynchronously);
            public apod_wallpaper.ApodEntry GetEntry(DateTime date)
            {
                Calls.Add("single"); Start = date;
                if (Failure != null) throw Failure;
                return Entry;
            }
            public Task<apod_wallpaper.ApodEntry> GetEntryAsync(DateTime date)
            { Calls.Add("single_async"); Start = date; return Pending.Task; }
            public apod_wallpaper.ApodEntry GetLatestEntry() { Calls.Add("latest"); return Entry; }
            public Task<apod_wallpaper.ApodEntry> GetLatestEntryAsync()
            { Calls.Add("latest_async"); return Task.FromResult(Entry); }
            public IReadOnlyList<apod_wallpaper.ApodEntry> GetEntries(DateTime startDate, DateTime endDate)
            { Calls.Add("range"); Start = startDate; End = endDate; return new[] { Entry }; }
            public Task<IReadOnlyList<apod_wallpaper.ApodEntry>> GetEntriesAsync(DateTime startDate, DateTime endDate)
            { Calls.Add("range_async"); Start = startDate; End = endDate; return Task.FromResult<IReadOnlyList<apod_wallpaper.ApodEntry>>(new[] { Entry }); }
            public Task<apod_wallpaper.ApiKeyValidationState> ValidateApiKeyAsync(string apiKey)
            { Calls.Add("validate"); Key = apiKey; return Task.FromResult(apod_wallpaper.ApiKeyValidationState.Unknown); }
        }

        private static async Task ApodFacadePreservesContractsAsync()
        {
            var source = new RecordingApodSource();
            apod_wallpaper.IApodClient client = new apod_wallpaper.ApodClient(source);
            var start = new DateTime(2026, 9, 1, 12, 30, 0);
            var end = start.AddDays(20);
            Assert(ReferenceEquals(client.GetEntry(start), source.Entry) && source.Start == start, "Sync date and result must pass through unchanged.");
            var pending = client.GetEntryAsync(end);
            Assert(!pending.IsCompleted && source.Start == end, "Async source must not be blocked or replaced by sync work.");
            source.Pending.SetResult(source.Entry);
            Assert(ReferenceEquals(await pending, source.Entry), "Async result must be preserved.");
            Assert(ReferenceEquals(client.GetLatestEntry(), source.Entry), "Latest sync result.");
            Assert(ReferenceEquals(await client.GetLatestEntryAsync(), source.Entry), "Latest async result.");
            Assert(ReferenceEquals(client.GetEntries(start, end)[0], source.Entry) && source.Start == start && source.End == end, "Sync range contract.");
            Assert(ReferenceEquals((await client.GetEntriesAsync(start, end))[0], source.Entry) && source.Start == start && source.End == end, "Async range contract.");
            Assert(await client.ValidateApiKeyAsync("test-key") == apod_wallpaper.ApiKeyValidationState.Unknown && source.Key == "test-key", "Validation remains source-owned.");
            Assert(string.Join(",", source.Calls) == "single,single_async,latest,latest_async,range,range_async,validate", "Every operation must route once to the matching source method.");
            var failure = new IOException("source failure");
            source.Failure = failure;
            try { client.GetEntry(start); throw new InvalidOperationException("Failure was swallowed."); }
            catch (IOException ex) { Assert(ReferenceEquals(ex, failure), "Do not replace errors or try a hidden legacy fallback."); }
            var failingSource = new RecordingApodSource();
            failingSource.Pending.SetException(failure);
            client = new apod_wallpaper.ApodClient(failingSource);
            await ExpectScienceFailureAsync<IOException>(client.GetEntryAsync(start));
            Assert(failingSource.Calls.Count == 1, "Async failure must not trigger retry/fallback.");
            try { new apod_wallpaper.ApodClient(null); throw new InvalidOperationException("Null source accepted."); }
            catch (ArgumentNullException) { }
        }

        private static void NasaScienceSyncTransportBounds()
        {
            var date = new DateTime(2026, 9, 27);
            var calls = 0;
            var source = new apod_wallpaper.ApodScienceSource(sendSync: (uri, token) =>
            {
                calls++;
                Assert(uri.AbsoluteUri == apod_wallpaper.ApodScienceParser.BuildUrl(date), "Sync must request exact date JSON.");
                return ScienceResponse();
            });
            Assert(source.GetEntry(date).Entry.HasImage, "Sync must parse valid JSON.");
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                try { source.GetEntry(date, cancel.Token); throw new InvalidOperationException("Cancellation ignored."); }
                catch (OperationCanceledException) { }
            }
            Assert(calls == 1, "Pre-cancel must not send.");
            foreach (var status in new[] { 302, 403, 500, 503 })
            {
                source = new apod_wallpaper.ApodScienceSource(sendSync: (uri, token) => ScienceResponse(status));
                try { source.GetEntry(date); throw new InvalidOperationException("HTTP error accepted."); }
                catch (apod_wallpaper.ApodScienceRequestException ex) { Assert(ex.Status == status, "Preserve HTTP status."); }
            }
            source = new apod_wallpaper.ApodScienceSource(sendSync: (uri, token) => ScienceResponse(404));
            try { source.GetEntry(date); throw new InvalidOperationException("Missing date accepted."); }
            catch (apod_wallpaper.ApodEntryUnavailableException) { }
            foreach (var response in new[] { ScienceResponse(type: "text/html"), ScienceResponse(json: "{}"),
                ScienceResponse(length: 1048577), ScienceResponse(json: new string('x', 1048577)) })
            {
                source = new apod_wallpaper.ApodScienceSource(sendSync: (uri, token) => response);
                try { source.GetEntry(date); throw new InvalidOperationException("Invalid JSON accepted."); }
                catch (InvalidDataException) { }
            }
            source = new apod_wallpaper.ApodScienceSource(timeout: TimeSpan.FromMilliseconds(100), sendSync: (uri, token) =>
            {
                token.WaitHandle.WaitOne();
                token.ThrowIfCancellationRequested();
                return ScienceResponse();
            });
            try { source.GetEntry(date); throw new InvalidOperationException("Timeout ignored."); }
            catch (TimeoutException) { }
        }

        private sealed class BlockingScienceStream : MemoryStream
        {
            private readonly ManualResetEventSlim _closed = new ManualResetEventSlim(false);
            public override int Read(byte[] buffer, int offset, int count)
            {
                if (!_closed.Wait(TimeSpan.FromSeconds(2))) throw new InvalidOperationException("Body timeout did not dispose stream.");
                throw new ObjectDisposedException(nameof(BlockingScienceStream));
            }
            protected override void Dispose(bool disposing) { _closed.Set(); base.Dispose(disposing); }
        }

        private static async Task NasaScienceSyncAndAdapterAsync()
        {
            var date = new DateTime(2026, 9, 27);
            var source = new apod_wallpaper.ApodScienceSource(timeout: TimeSpan.FromMilliseconds(100),
                sendSync: (uri, token) => new apod_wallpaper.ApodScienceResponse(200, new BlockingScienceStream()));
            try { source.GetEntry(date); throw new InvalidOperationException("Body timeout ignored."); }
            catch (TimeoutException) { }
            var now = DateTime.UtcNow;
            var calls = 0;
            source = new apod_wallpaper.ApodScienceSource(
                send: (uri, token) => { calls++; return Task.FromResult(ScienceResponse()); },
                utcNow: () => now,
                sendSync: (uri, token) => { calls++; return ScienceResponse(429, retry: TimeSpan.FromMinutes(2)); });
            try { source.GetEntry(date); throw new InvalidOperationException("429 accepted."); }
            catch (apod_wallpaper.ApodScienceRequestException) { }
            await ExpectScienceFailureAsync<apod_wallpaper.ApodScienceRequestException>(source.GetEntryAsync(date));
            Assert(calls == 1, "Sync Retry-After must also block async requests.");
            now = now.AddMinutes(2);
            Assert((await source.GetEntryAsync(date)).Entry.HasImage && calls == 2, "Async must resume after sync cooldown.");

            var legacy = new RecordingApodSource();
            source = new apod_wallpaper.ApodScienceSource(send: (uri, token) => Task.FromResult(ScienceResponse()),
                sendSync: (uri, token) => ScienceResponse());
            apod_wallpaper.IApodClient client = new apod_wallpaper.ApodClient(new apod_wallpaper.ApodScienceClient(source, legacy, () => date));
            Assert(client.GetEntry(date).ResolvedFromSource == "nasa_science", "Sync date must use new source.");
            Assert((await client.GetEntryAsync(date)).ResolvedFromSource == "nasa_science", "Async date must use new source.");
            Assert(legacy.Calls.Count == 0, "Date calls must not contact legacy/API key validation.");
            client.GetLatestEntry();
            await client.GetLatestEntryAsync();
            await client.ValidateApiKeyAsync("test-key");
            Assert(string.Join(",", legacy.Calls) == "validate", "Only legacy API key validation remains delegated.");
            source = new apod_wallpaper.ApodScienceSource(send: (uri, token) => Task.FromResult(ScienceResponse(503)),
                sendSync: (uri, token) => ScienceResponse(503));
            client = new apod_wallpaper.ApodScienceClient(source, legacy);
            try { client.GetEntry(date); throw new InvalidOperationException("Failure swallowed."); }
            catch (apod_wallpaper.ApodScienceRequestException) { }
            await ExpectScienceFailureAsync<apod_wallpaper.ApodScienceRequestException>(client.GetEntryAsync(date));
            Assert(legacy.Calls.Count == 1, "Failed dates must not trigger legacy retry chains.");
        }

        private static void ScienceMetadataLinksPersist()
        {
            var root = Path.Combine(Path.GetTempPath(), "apod_science_cache_" + Guid.NewGuid().ToString("N"));
            var snapshot = CaptureSettings();
            apod_wallpaper.FileStorage.SetApplicationDataDirectoryOverride(root);
            apod_wallpaper.FileStorage.SetSessionImagesDirectory(root);
            try
            {
                var date = new DateTime(2026, 9, 27);
                var parsed = apod_wallpaper.ApodScienceParser.Parse(ScienceFixture("image"), date);
                Assert(parsed.Entry.PostUrl == parsed.PostUrl, "Canonical URL must survive record-to-entry boundary.");
                Assert(parsed.Entry.SourcePreviewUrl == parsed.SourcePreviewUrl && parsed.Entry.SourceOriginalUrl == parsed.SourceOriginalUrl,
                    "Source image URLs must survive record-to-entry boundary.");
                var cache = new apod_wallpaper.ApodMetadataCache();
                cache.Upsert(parsed.Entry);
                var restored = new apod_wallpaper.ApodMetadataCache().Get(date).ToEntry();
                Assert(restored.PostUrl == parsed.PostUrl && restored.SourcePreviewUrl == parsed.SourcePreviewUrl && restored.SourceOriginalUrl == parsed.SourceOriginalUrl,
                    "Disk cache must roundtrip all new links.");
                var legacy = new apod_wallpaper.ApodEntry { Date = parsed.Entry.Date, Url = parsed.Entry.Url, HdUrl = parsed.Entry.HdUrl, MediaType = "image" };
                cache.Upsert(legacy);
                restored = new apod_wallpaper.ApodMetadataCache().Get(date).ToEntry();
                Assert(restored.PostUrl == parsed.PostUrl && restored.SourceOriginalUrl == parsed.SourceOriginalUrl, "Legacy refresh must not erase known links for unchanged media.");
                legacy.Url = "https://example.org/other.jpg";
                legacy.HdUrl = legacy.Url;
                cache.UpsertRange(new[] { legacy });
                restored = new apod_wallpaper.ApodMetadataCache().Get(date).ToEntry();
                Assert(restored.PostUrl == parsed.PostUrl && restored.SourceOriginalUrl == null && restored.SourcePreviewUrl == null,
                    "Do not reuse stale fallback images after primary media changes.");
                var oldJson = "[{\"Date\":\"1995-06-16\",\"Title\":\"Old cache\",\"Url\":\"https://example.org/old.gif\",\"MediaType\":\"image\",\"CachedAtUtc\":\"\\/Date(1700000000000)\\/\",\"LastVerifiedUtc\":\"\\/Date(1700000000000)\\/\"}]";
                File.WriteAllText(apod_wallpaper.FileStorage.MetadataCacheFilePath, oldJson, new System.Text.UTF8Encoding(false));
                var old = new apod_wallpaper.ApodMetadataCache().Get(new DateTime(1995, 6, 16)).ToEntry();
                Assert(old.HasImage && old.Title == "Old cache" && old.PostUrl == null, "Old disk cache without fields must still load.");
                cache = new apod_wallpaper.ApodMetadataCache();
                cache.Upsert(parsed.Entry);
                var fake = new RecordingApodSource();
                var service = new apod_wallpaper.ApodWallpaperService(fake, cache, new FakeWallpaperApplier());
                Assert(service.GetPostUrl(date) == parsed.PostUrl && service.OpenPost(date) == parsed.PostUrl,
                    "NASA action must use cached canonical URL without network.");
                Assert(service.GetPostUrl(new DateTime(1995, 6, 16)) == apod_wallpaper.ApodPageUrl.GetUrl(new DateTime(1995, 6, 16)), "Old cache fallback stays date-specific.");
                foreach (var unsafeUrl in new[] { "file:///C:/private", "https://evil.example/image-article/test/", "https://science.nasa.gov.evil.example/image-article/test/", "https://user@science.nasa.gov/image-article/test/" })
                {
                    cache.Get(date).PostUrl = unsafeUrl;
                    Assert(service.GetPostUrl(date) == apod_wallpaper.ApodPageUrl.GetUrl(date), "Never launch arbitrary cached URI.");
                }
                cache.Upsert(parsed.Entry);
                fake.Failure = new IOException("offline");
                try { service.GetEntryByDate(date, true); throw new InvalidOperationException("Refresh failure was swallowed."); }
                catch (IOException) { }
                Assert(new apod_wallpaper.ApodMetadataCache().Get(date).PostUrl == parsed.PostUrl, "Failed metadata refresh must leave disk cache intact.");
                Assert(fake.Calls.Count == 1, "URL lookup must not issue background requests.");
            }
            finally
            {
                apod_wallpaper.FileStorage.SetApplicationDataDirectoryOverride(null);
                apod_wallpaper.FileStorage.SetSessionImagesDirectory(snapshot.ImagesDirectoryPath);
                TryDeleteDirectory(root);
            }
        }

        private static async Task ScienceLatestUsesVerifiedDatesAsync()
        {
            var today = new DateTime(2026, 9, 28);
            var calls = 0;
            var legacy = new RecordingApodSource();
            Func<Uri, CancellationToken, apod_wallpaper.ApodScienceResponse> send = (uri, token) =>
            { calls++; return uri.AbsolutePath.EndsWith("260928") ? ScienceResponse(404) : ScienceResponse(); };
            var source = new apod_wallpaper.ApodScienceSource(send: (uri, token) => Task.FromResult(send(uri, token)), sendSync: send);
            var client = new apod_wallpaper.ApodScienceClient(source, legacy, () => today);
            Assert(client.GetLatestEntry().Date == "2026-09-27", "Sync latest must walk back after confirmed 404.");
            Assert((await client.GetLatestEntryAsync()).Date == "2026-09-27" && calls == 4, "Async latest must use same policy.");
            Assert(legacy.Calls.Count == 0, "Latest must not contact old API/HTML.");
            calls = 0;
            source = new apod_wallpaper.ApodScienceSource(send: (uri, token) => { calls++; return Task.FromResult(ScienceResponse(503)); });
            client = new apod_wallpaper.ApodScienceClient(source, legacy, () => today);
            await ExpectScienceFailureAsync<apod_wallpaper.ApodScienceRequestException>(client.GetLatestEntryAsync());
            Assert(calls == 1, "Transient server failure must not fan out to older dates.");
            source = new apod_wallpaper.ApodScienceSource(send: (uri, token) => Task.FromResult(ScienceResponse(json: ScienceFixture("video"))));
            client = new apod_wallpaper.ApodScienceClient(source, legacy, () => new DateTime(2026, 8, 31));
            Assert((await client.GetLatestEntryAsync()).MediaType == "video", "Latest publication need not be an image.");
            source = new apod_wallpaper.ApodScienceSource(send: async (uri, token) =>
            { await Task.Delay(Timeout.Infinite, token); return ScienceResponse(); });
            client = new apod_wallpaper.ApodScienceClient(source, legacy, () => today, TimeSpan.FromMilliseconds(100));
            await ExpectScienceFailureAsync<TimeoutException>(client.GetLatestEntryAsync());
        }

        private static async Task ScienceProbeAndRandomAsync()
        {
            var date = new DateTime(2026, 9, 27);
            var calls = 0;
            var source = new apod_wallpaper.ApodScienceSource(send: (uri, token) =>
            { calls++; return Task.FromResult(ScienceResponse()); });
            var probe = new apod_wallpaper.ApodPageAvailabilityProbe(source);
            var result = await probe.ProbeAsync(date, TimeSpan.FromSeconds(1));
            Assert(result.IsAvailable && result.Entry.PostUrl != null && result.Method == "GET", "Probe must return verified metadata.");
            result = await probe.ProbeAsync(date.AddDays(-1), TimeSpan.FromSeconds(1));
            Assert(!result.IsAvailable && !result.IsUnavailable, "Wrong JSON date must remain unknown.");
            source = new apod_wallpaper.ApodScienceSource(send: (uri, token) => Task.FromResult(ScienceResponse(404)));
            result = await new apod_wallpaper.ApodPageAvailabilityProbe(source).ProbeAsync(date, TimeSpan.FromSeconds(1));
            Assert(result.IsUnavailable && result.Entry == null, "404 must not unlock or cache an entry.");
            source = new apod_wallpaper.ApodScienceSource(send: async (uri, token) =>
            { await Task.Delay(Timeout.Infinite, token); return ScienceResponse(); });
            result = await new apod_wallpaper.ApodPageAvailabilityProbe(source).ProbeAsync(date, TimeSpan.FromMilliseconds(100));
            Assert(!result.IsAvailable && !result.IsUnavailable, "Timeout is unknown, not missing.");

            var picks = 0;
            calls = 0;
            var cache = new InMemoryApodMetadataCache();
            source = new apod_wallpaper.ApodScienceSource(send: (uri, token) =>
            { calls++; return Task.FromResult(uri.AbsolutePath.EndsWith("950617") ? ScienceResponse(404) : ScienceResponse()); });
            var random = new apod_wallpaper.RandomApodService(new apod_wallpaper.ApodPageAvailabilityProbe(source), cache,
                (start, end) => ++picks == 1 ? new DateTime(1995, 6, 17) : date);
            var selected = await random.PickGlobalAsync(true);
            Assert(selected.Date == date && calls == 2, "Random must reroll a confirmed missing page.");
            Assert(cache.Get(date)?.PostUrl != null && cache.Get(date).ToEntry().HasImage, "Random must retain metadata for preview.");
            calls = 0;
            source = new apod_wallpaper.ApodScienceSource(send: (uri, token) => { calls++; return Task.FromResult(ScienceResponse(503)); });
            random = new apod_wallpaper.RandomApodService(new apod_wallpaper.ApodPageAvailabilityProbe(source), cache, (start, end) => date);
            selected = await random.PickGlobalAsync(false);
            Assert(selected.Status != apod_wallpaper.RandomApodStatus.Success && calls == 1, "Do not multiply requests during a NASA outage.");
            calls = 0;
            source = new apod_wallpaper.ApodScienceSource(send: (uri, token) => { calls++; return Task.FromResult(ScienceResponse(404)); });
            random = new apod_wallpaper.RandomApodService(new apod_wallpaper.ApodPageAvailabilityProbe(source), cache, (start, end) => date);
            await random.PickGlobalAsync(false);
            Assert(calls == apod_wallpaper.RandomApodService.GlobalAvailabilityAttemptLimit, "Missing pages must respect attempt cap.");
        }

        private static async Task ScienceRangePagesAsync()
        {
            var date = new DateTime(2026, 9, 27);
            var page1 = "[" + ScienceFixture("image") + "]";
            var page2 = "[" + ScienceFixture("image").Replace("2026-09-27", "2026-09-26").Replace("2026 September 27", "2026 September 26") + "]";
            var calls = 0;
            Func<Uri, CancellationToken, apod_wallpaper.ApodScienceResponse> send = (uri, token) =>
            {
                calls++;
                Assert(uri.Query.Contains("date_from=260926") && uri.Query.Contains("date_to=260927"), "Range URL must preserve exact bounds.");
                var json = uri.Query.Contains("page=2") ? page2 : page1;
                return new apod_wallpaper.ApodScienceResponse(200, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)), total: 2, totalPages: 2);
            };
            var source = new apod_wallpaper.ApodScienceSource(send: (uri, token) => Task.FromResult(send(uri, token)), sendSync: send);
            var entries = source.GetEntries(date.AddDays(-1), date);
            Assert(entries.Count == 2 && entries[0].Date == "2026-09-26" && entries[1].PostUrl != null, "Sync range must collect/sort all pages and retain metadata.");
            Assert((await source.GetEntriesAsync(date.AddDays(-1), date)).Count == 2 && calls == 4, "Async must use same pagination.");
            source = new apod_wallpaper.ApodScienceSource(send: (uri, token) => Task.FromResult(uri.Query.Contains("page=2")
                ? ScienceResponse(503) : new apod_wallpaper.ApodScienceResponse(200, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(page1)), total: 2, totalPages: 2)));
            await ExpectScienceFailureAsync<apod_wallpaper.ApodScienceRequestException>(source.GetEntriesAsync(date.AddDays(-1), date));
            foreach (var json in new[] { "{}", "[null]", page2 })
            {
                source = new apod_wallpaper.ApodScienceSource(send: (uri, token) => Task.FromResult(
                    new apod_wallpaper.ApodScienceResponse(200, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)), total: 1, totalPages: 1)));
                await ExpectScienceFailureAsync<InvalidDataException>(source.GetEntriesAsync(date, date));
            }
            source = new apod_wallpaper.ApodScienceSource(send: (uri, token) => Task.FromResult(ScienceResponse(json: page1)));
            await ExpectScienceFailureAsync<InvalidDataException>(source.GetEntriesAsync(date, date));
            source = new apod_wallpaper.ApodScienceSource(send: (uri, token) => Task.FromResult(
                new apod_wallpaper.ApodScienceResponse(200, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(page1)), total: 1, totalPages: 2)));
            Assert((await source.GetEntriesAsync(date, date)).Count == 1, "Identical duplicate dates must be deduplicated.");
            source = new apod_wallpaper.ApodScienceSource(send: (uri, token) => Task.FromResult(
                new apod_wallpaper.ApodScienceResponse(200, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(page1)), total: 2, totalPages: 2)));
            await ExpectScienceFailureAsync<InvalidDataException>(source.GetEntriesAsync(date.AddDays(-1), date));
            source = new apod_wallpaper.ApodScienceSource(send: (uri, token) => Task.FromResult(
                new apod_wallpaper.ApodScienceResponse(200, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(page1)),
                    total: uri.Query.Contains("page=2") ? 1 : 2, totalPages: 2)));
            await ExpectScienceFailureAsync<InvalidDataException>(source.GetEntriesAsync(date.AddDays(-1), date));
            source = new apod_wallpaper.ApodScienceSource(send: (uri, token) => Task.FromResult(
                new apod_wallpaper.ApodScienceResponse(200, new MemoryStream(System.Text.Encoding.UTF8.GetBytes("[]")), total: 0, totalPages: 0)));
            Assert((await source.GetEntriesAsync(date, date)).Count == 0, "Confirmed empty range must return no synthesized unavailable entries.");
            var legacy = new RecordingApodSource();
            var adapter = new apod_wallpaper.ApodScienceClient(source, legacy);
            Assert((await adapter.GetEntriesAsync(date, date)).Count == 0 && legacy.Calls.Count == 0, "Adapter ranges must no longer use legacy.");
            source = new apod_wallpaper.ApodScienceSource(send: async (uri, token) =>
            { await Task.Delay(Timeout.Infinite, token); return ScienceResponse(); }, timeout: TimeSpan.FromMilliseconds(100));
            await ExpectScienceFailureAsync<TimeoutException>(source.GetEntriesAsync(date, date));
        }

        [STAThread]
        private static int Main()
        {
            _secretStoreDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_smoke_secrets_" + Guid.NewGuid().ToString("N"));
            _logDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_smoke_logs_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_secretStoreDirectory);
            Directory.CreateDirectory(_logDirectory);
            apod_wallpaper.AppLogger.SetLogDirectoryOverride(_logDirectory);
            _settingsStore = new InMemorySettingsStore(CreateDefaultSettingsSnapshot());
            _secretStore = new apod_wallpaper.DpapiUserSecretStore(_secretStoreDirectory);

            try
            {
                Run("Scheduler restarts after stop inside callback", SchedulerRestartsAfterStopInsideCallback);
                Run("Future date is unavailable sync", FutureDateIsUnavailableSync);
                Run("Future date is unavailable async", FutureDateIsUnavailableAsync);
                Run("API key change resets validation state", ApiKeyChangeResetsValidationState);
                Run("Preferred display date uses last applied date", PreferredDisplayDateUsesLastAppliedDate);
                Run("HTML extractor resolves preview and full image", HtmlExtractorResolvesImagePage);
                Run("HTML extractor rejects video page", HtmlExtractorRejectsVideoPage);
                Run("HTML extractor classifies text-only APOD page as unsupported", HtmlExtractorClassifiesTextOnlyPageAsUnsupported);
                Run("HTML extractor handles annotated image page", HtmlExtractorHandlesAnnotatedImagePage);
                Run("HTML extractor resolves title and explanation text", HtmlExtractorResolvesTextMetadata);
                Run("Runtime settings fall back to DEMO_KEY for invalid key", InvalidApiKeyFallsBackToDemoKey);
                Run("Local image is preferred for preview", LocalImageIsPreferredForPreview);
                Run("ApplyLatestPublished walks back through video days", ApplyLatestPublishedFallsBackAcrossVideoDays);
                Run("Smart composer stretches near screen ratio images", SmartComposerUsesStretchForNearScreenRatio);
                Run("Smart composer creates single focus image for square content", SmartComposerCreatesSingleFocusForSquareImages);
                Run("Smart composer preserves ultrawide images without Fill cropping", SmartComposerPreservesUltraWideImages);
                Run("Scheduler uses hourly polling for DEMO_KEY", SchedulerUsesHourlyPollingForDemoKey);
                Run("Scheduler uses 30 minute polling for personal key", SchedulerUsesThirtyMinutePollingForPersonalKey);
                Run("Wallpaper service rejects invalid local file", WallpaperServiceRejectsInvalidLocalFile);
                Run("Scheduler day lock skips repeated checks after today's image", SchedulerDayLockSkipsAfterTodaysImage);
                Run("Scheduler day lock keeps checking after yesterday fallback", SchedulerDayLockKeepsCheckingAfterYesterdayFallback);
                Run("Scheduler day lock does not skip when no applied date", SchedulerDayLockRequiresAppliedDate);
                Run("API key is stored outside plaintext settings", ApiKeyIsStoredOutsidePlaintextSettings);
                Run("Initial state snapshot returns startup data in one call", InitialStateSnapshotReturnsStartupData);
                Run("Json settings store writes non-secret settings to settings.json", JsonSettingsStoreWritesSettingsFile);
                Run("Storage layout resolves all backend paths centrally", StorageLayoutResolvesAllPathsCentrally);
                Run("Storage summary counts local library without cleanup", StorageSummaryCountsLocalLibraryWithoutCleanup);
                Run("Portable storage mode keeps app data near executable", PortableStorageModeUsesPortableLayout);
                Run("Store storage mode keeps app data inside sandbox path", StoreStorageModeUsesSandboxLayout);
                Run("Public facade methods use operation results", PublicFacadeMethodsUseOperationResults);
                Run("Public workflow payload never exposes failed status", FailedWorkflowStatusMapsToOperationError);
                Run("Backend facade does not expose diagnostics contract", BackendFacadeDoesNotExposeDiagnosticsContract);
                Run("WallpaperApplied subscription disposes cleanly", WallpaperAppliedSubscriptionDisposesCleanly);
                Run("WinUI localization literals are covered", WinUiLocalizationLiteralsAreCovered);
                Run("Translation target language normalizes values", TranslationTargetLanguageNormalizesValues);
                Run("Google Translate URL builder encodes explanation", GoogleTranslateUrlBuilderEncodesExplanation);
                Run("APOD page URL builder is deterministic", ApodPageUrlBuilderIsDeterministic);
                Run("APOD availability probe evaluates redirects", ApodAvailabilityProbeEvaluatesRedirects);
                Run("APOD availability probe retries forbidden HEAD with GET", ApodAvailabilityProbeRetriesForbiddenHeadWithGet);
                Run("Calendar availability transient override unlocks today only", CalendarAvailabilityTransientOverrideUnlocksTodayOnly);
                Run("Calendar availability throttle resets when today changes", CalendarAvailabilityThrottleResetsWhenTodayChanges);
                Run("APOD availability probe source avoids workflow side effects", ApodAvailabilityProbeSourceAvoidsWorkflowSideEffects);
                Run("Calendar year state source is cache only", CalendarYearStateSourceIsCacheOnly);
                Run("Favorite APOD store persists normalized dates", FavoriteApodStorePersistsNormalizedDates);
                Run("Update check compares release versions", UpdateCheckComparesReleaseVersions);
                Run("Update check defaults to automatic checks enabled", UpdateCheckDefaultsToAutomaticChecksEnabled);
                Run("Update reminder policy respects cooldowns", UpdateReminderPolicyRespectsCooldowns);
                Run("About update status uses persisted cache", AboutUpdateStatusUsesPersistedCache);
                Run("Update check persistence keeps cached latest after dialog choices", UpdateCheckPersistenceKeepsCachedLatestAfterDialogChoices);
                Run("About update status preserves cached updates after failures", AboutUpdateStatusPreservesCachedUpdatesAfterFailures);
                Run("Random APOD settings and sources normalize", RandomApodSettingsAndSourcesNormalize);
                Run("Downloaded APOD date scan ignores smart artifacts", DownloadedApodDateScanIgnoresSmartArtifacts);
                Run("Favorite rotation source defaults to latest", FavoriteRotationSourceDefaultsToLatest);
                Run("Favorite rotation avoids immediate repeat", FavoriteRotationAvoidsImmediateRepeat);
                Run("Display topology snapshot is read only", DisplayTopologySnapshotIsReadOnly);
                Run("Month calendar keeps stable day visuals", MonthCalendarKeepsStableDayVisuals);
                Run("Favorites preview uses a single open gesture path", FavoritesPreviewUsesSingleOpenGesturePath);
                Run("Update reminder diagnostics avoid secrets", UpdateReminderDiagnosticsAvoidSecrets);
                Run("NASA Science date URLs are pure", NasaScienceDateUrlsArePure);
                Run("NASA Science selects primary image and plain text", NasaScienceSelectsPrimaryImage);
                Run("NASA Science preserves archive originals", NasaSciencePreservesArchiveOriginal);
                Run("NASA Science rejects video posters as images", NasaScienceRejectsVideoPosters);
                Run("NASA Science handles text-only pages", NasaScienceHandlesTextOnly);
                Run("NASA Science rejects invalid payloads", NasaScienceRejectsInvalidPayloads);
                Run("NASA Science preview preserves other sources", NasaSciencePreviewPolicyPreservesOtherSources);
                Run("NASA Science preview handles static assets", NasaSciencePreviewPolicyHandlesStaticAssets);
                Run("NASA Science preview keeps separate variants", NasaSciencePreviewPolicyKeepsSeparateAssets);
                Run("NASA Science transport bounds and errors", () => NasaScienceTransportBoundsAsync().GetAwaiter().GetResult());
                Run("NASA Science shared request cancellation", () => NasaScienceTransportSharesRequestsAsync().GetAwaiter().GetResult());
                Run("NASA Science body timeout and concurrency", () => NasaScienceTransportBodyAndConcurrencyAsync().GetAwaiter().GetResult());
                Run("APOD facade preserves source contracts", () => ApodFacadePreservesContractsAsync().GetAwaiter().GetResult());
                Run("NASA Science synchronous transport bounds", NasaScienceSyncTransportBounds);
                Run("NASA Science sync body, shared cooldown and staged adapter", () => NasaScienceSyncAndAdapterAsync().GetAwaiter().GetResult());
                Run("NASA Science metadata links persist compatibly", ScienceMetadataLinksPersist);
                Run("NASA Science latest verifies dates and total budget", () => ScienceLatestUsesVerifiedDatesAsync().GetAwaiter().GetResult());
                Run("NASA Science probe and Random retain verified metadata", () => ScienceProbeAndRandomAsync().GetAwaiter().GetResult());
                Run("NASA Science range pagination is complete or fails", () => ScienceRangePagesAsync().GetAwaiter().GetResult());

                Console.WriteLine(_failures == 0
                    ? "Smoke tests passed."
                    : "Smoke tests failed: " + _failures);

                return _failures == 0 ? 0 : 1;
            }
            finally
            {
                apod_wallpaper.AppLogger.ClearLogDirectoryOverride();
                TryDeleteDirectory(_secretStoreDirectory);
                TryDeleteDirectory(_logDirectory);
            }
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine("[PASS] " + name);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _failures);
                Console.WriteLine("[FAIL] " + name + ": " + ex.Message);
            }
        }

        private static void SchedulerRestartsAfterStopInsideCallback()
        {
            using (var scheduler = new apod_wallpaper.Scheduler())
            {
                scheduler.PollingInterval = TimeSpan.FromMilliseconds(50);

                var firstRun = new ManualResetEventSlim(false);
                scheduler.Start(() =>
                {
                    scheduler.Stop();
                    firstRun.Set();
                });

                if (!firstRun.Wait(TimeSpan.FromSeconds(2)))
                    throw new InvalidOperationException("Scheduler did not fire the first callback.");

                var secondRun = new ManualResetEventSlim(false);
                scheduler.Start(() =>
                {
                    scheduler.Stop();
                    secondRun.Set();
                });

                if (!secondRun.Wait(TimeSpan.FromSeconds(2)))
                    throw new InvalidOperationException("Scheduler did not restart after stopping inside the callback.");
            }
        }

        private static void FavoriteApodStorePersistsNormalizedDates()
        {
            var directory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_favorites_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var path = Path.Combine(directory, "favorites.json");
                var store = new apod_wallpaper.FavoriteApodStore(path);
                var date = new DateTime(2026, 7, 14);

                Assert(store.SetFavorite(date, true), "Initial favorite add should change state.");
                Assert(!store.SetFavorite(date, true), "Duplicate favorite add should not change state.");
                Assert(store.IsFavorite(date), "Favorite date should be present.");

                var reloaded = new apod_wallpaper.FavoriteApodStore(path);
                var dates = reloaded.GetDates();
                Assert(dates.Count == 1 && dates[0] == date.Date, "Favorite date should survive reload once.");

                Assert(reloaded.SetFavorite(date, false), "Favorite remove should change state.");
                Assert(!reloaded.IsFavorite(date), "Favorite date should be removed.");
            }
            finally
            {
                TryDeleteDirectory(directory);
            }
        }

        private static void UpdateCheckComparesReleaseVersions()
        {
            Assert(apod_wallpaper.UpdateCheckService.NormalizeVersionText("v1.2.1") == "1.2.1", "Expected v-prefix to be ignored.");
            Assert(apod_wallpaper.UpdateCheckService.CompareReleaseVersions("v1.3.0", "1.2.1") > 0, "Expected v1.3.0 to be newer than 1.2.1.");
            Assert(apod_wallpaper.UpdateCheckService.CompareReleaseVersions("v1.3.0", "1.3.0") == 0, "Expected v-prefix not to affect equal version comparison.");
            Assert(apod_wallpaper.UpdateCheckService.CompareReleaseVersions("1.3.0", "v1.3.0") == 0, "Expected current v-prefix not to affect equal version comparison.");
            Assert(apod_wallpaper.UpdateCheckService.CompareReleaseVersions("v1.3.1", "1.3.0") > 0, "Expected v1.3.1 to be newer than 1.3.0.");
            Assert(apod_wallpaper.UpdateCheckService.CompareReleaseVersions("v1.2.2", "1.2.1") > 0, "Expected newer release to compare higher.");
            Assert(apod_wallpaper.UpdateCheckService.CompareReleaseVersions("1.2.1", "v1.2.1") == 0, "Expected equal versions to compare equal.");
            Assert(apod_wallpaper.UpdateCheckService.CompareReleaseVersions("1.2.0", "1.2.1") < 0, "Expected older release to compare lower.");
        }

        private static void UpdateCheckDefaultsToAutomaticChecksEnabled()
        {
            var directory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_update_settings_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var store = new apod_wallpaper.JsonSettingsStore(Path.Combine(directory, "settings.json"));
                var defaults = store.Load();
                Assert(defaults.AutoCheckUpdatesEnabled, "Fresh settings should enable update auto-checks.");
                Assert(!defaults.SuppressAutomaticUpdateReminder, "Fresh settings should not suppress update reminders.");

                defaults.AutoCheckUpdatesEnabled = false;
                defaults.SuppressAutomaticUpdateReminder = true;
                defaults.LastUpdateCheckUtc = new DateTime(2026, 7, 23, 0, 0, 0, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);
                defaults.LastAutomaticUpdateCheckUtc = new DateTime(2026, 7, 23, 1, 0, 0, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);
                defaults.LastUpdateReminderShownUtc = new DateTime(2026, 7, 23, 2, 0, 0, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);
                defaults.LastUpdateReminderVersion = "1.3.0";
                defaults.LastKnownLatestVersion = "1.3.0";
                defaults.LastKnownLatestReleaseUrl = "https://github.com/p4kon/apod_wallpaper/releases/tag/v1.3.0";
                store.Save(defaults);

                var reloaded = store.Load();
                Assert(!reloaded.AutoCheckUpdatesEnabled, "Saved update auto-check setting should round-trip.");
                Assert(reloaded.SuppressAutomaticUpdateReminder, "Saved reminder suppression should round-trip.");
                Assert(!string.IsNullOrWhiteSpace(reloaded.LastUpdateCheckUtc), "Last update check timestamp should round-trip.");
                Assert(!string.IsNullOrWhiteSpace(reloaded.LastAutomaticUpdateCheckUtc), "Last automatic update timestamp should round-trip.");
                Assert(!string.IsNullOrWhiteSpace(reloaded.LastUpdateReminderShownUtc), "Last reminder timestamp should round-trip.");
                Assert(reloaded.LastUpdateReminderVersion == "1.3.0", "Last reminder version should round-trip.");
                Assert(reloaded.LastKnownLatestVersion == "1.3.0", "Last known latest version should round-trip.");
                Assert(reloaded.LastKnownLatestReleaseUrl.Contains("v1.3.0"), "Last known release URL should round-trip.");
            }
            finally
            {
                TryDeleteDirectory(directory);
            }
        }

        private static void RandomApodSettingsAndSourcesNormalize()
        {
            Assert(apod_wallpaper.RandomApodSource.Normalize(null) == apod_wallpaper.RandomApodSource.Global, "Expected null random source to normalize to global.");
            Assert(apod_wallpaper.RandomApodSource.Normalize(string.Empty) == apod_wallpaper.RandomApodSource.Global, "Expected empty random source to normalize to global.");
            Assert(apod_wallpaper.RandomApodSource.Normalize("local") == apod_wallpaper.RandomApodSource.Downloaded, "Expected local alias to normalize to downloaded.");
            Assert(apod_wallpaper.RandomApodSource.Normalize("favorite") == apod_wallpaper.RandomApodSource.Favorites, "Expected favorite alias to normalize to favorites.");
            Assert(apod_wallpaper.RandomApodService.ResolveGlobalStartDate(false) == new DateTime(2015, 1, 1), "Expected default global random range to start in 2015.");
            Assert(apod_wallpaper.RandomApodService.ResolveGlobalStartDate(true) == new DateTime(1995, 6, 16), "Expected deep archive random range to start at first APOD page.");

            var defaults = apod_wallpaper.JsonSettingsStore.CreateDefaultSnapshot();
            Assert(defaults.RandomApodSource == apod_wallpaper.RandomApodSource.Global, "Expected default random source to be global.");
            Assert(!defaults.RandomApodIncludeDeepArchive, "Expected deep archive random mode to be off by default.");
        }

        private static void DownloadedApodDateScanIgnoresSmartArtifacts()
        {
            var directory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_downloaded_dates_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            Directory.CreateDirectory(Path.Combine(directory, "smart"));
            var snapshot = CaptureSettings();
            try
            {
                var localDate = new DateTime(2026, 7, 14);
                using (var bitmap = new Bitmap(4, 4))
                {
                    bitmap.Save(Path.Combine(directory, "2026-07-14.jpg"), System.Drawing.Imaging.ImageFormat.Jpeg);
                }

                File.WriteAllText(Path.Combine(directory, "notes.txt"), "ignore");
                File.WriteAllText(Path.Combine(directory, "smart", "2026-07-15.jpg"), "ignore");

                apod_wallpaper.FileStorage.SetSessionImagesDirectory(directory);
                var dates = apod_wallpaper.FileStorage.GetDownloadedImageDates();
                Assert(dates.Count == 1 && dates[0] == localDate, "Expected only top-level APOD image filename dates.");
            }
            finally
            {
                apod_wallpaper.FileStorage.SetSessionImagesDirectory(snapshot.ImagesDirectoryPath);
                TryDeleteDirectory(directory);
            }
        }

        private static void FavoriteRotationSourceDefaultsToLatest()
        {
            var defaults = apod_wallpaper.JsonSettingsStore.CreateDefaultSnapshot();
            Assert(defaults.AutoWallpaperSource == apod_wallpaper.AutoWallpaperSource.Latest, "Expected auto wallpaper source to default to latest APOD.");
            Assert(apod_wallpaper.ApplicationSettingsSnapshot.NormalizeAutoWallpaperSource(null) == apod_wallpaper.AutoWallpaperSource.Latest, "Expected null source to normalize to latest.");
            Assert(apod_wallpaper.ApplicationSettingsSnapshot.NormalizeAutoWallpaperSource("favorite") == apod_wallpaper.AutoWallpaperSource.Favorites, "Expected favorite alias to normalize to favorites.");
        }

        private static void FavoriteRotationAvoidsImmediateRepeat()
        {
            var last = new DateTime(2026, 7, 14);
            var next = apod_wallpaper.ApplicationController.SelectFavoriteRotationDate(
                new[] { last, new DateTime(2026, 7, 15) },
                last);

            Assert(next.HasValue && next.Value == new DateTime(2026, 7, 15), "Expected favorite rotation to avoid repeating last date when alternatives exist.");

            var only = apod_wallpaper.ApplicationController.SelectFavoriteRotationDate(new[] { last }, last);
            Assert(only.HasValue && only.Value == last, "Expected a single favorite to remain selectable.");
        }

        private static void UpdateReminderPolicyRespectsCooldowns()
        {
            var now = new DateTime(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);
            var settings = apod_wallpaper.JsonSettingsStore.CreateDefaultSnapshot();

            Assert(apod_wallpaper.UpdateReminderPolicy.ShouldRunAutomaticCheck(settings, now), "Fresh settings should allow automatic update check.");

            settings.LastAutomaticUpdateCheckUtc = apod_wallpaper.UpdateReminderPolicy.FormatUtc(now.AddHours(-23));
            Assert(!apod_wallpaper.UpdateReminderPolicy.ShouldRunAutomaticCheck(settings, now), "Automatic check should be throttled before one day.");

            settings.LastAutomaticUpdateCheckUtc = apod_wallpaper.UpdateReminderPolicy.FormatUtc(now.AddHours(-2));
            settings.LastAutomaticUpdateCheckFailedUtc = settings.LastAutomaticUpdateCheckUtc;
            Assert(apod_wallpaper.UpdateReminderPolicy.ShouldRunAutomaticCheck(settings, now), "Failed automatic check should retry after one hour.");

            settings.AutoCheckUpdatesEnabled = false;
            Assert(!apod_wallpaper.UpdateReminderPolicy.ShouldRunAutomaticCheck(settings, now), "Disabled automatic checks should not run.");
            settings.SuppressAutomaticUpdateReminder = true;
            Assert(!apod_wallpaper.UpdateReminderPolicy.ShouldRunUpdateCheck(settings, now, automatic: true, forceCheck: false),
                "Automatic update path should be blocked when automatic checks are disabled and reminders are suppressed.");
            Assert(apod_wallpaper.UpdateReminderPolicy.ShouldRunUpdateCheck(settings, now, automatic: false, forceCheck: true),
                "Manual force-check path should be allowed even when automatic checks are disabled and reminders are suppressed.");

            settings.AutoCheckUpdatesEnabled = true;
            settings.SuppressAutomaticUpdateReminder = false;
            settings.LastUpdateReminderShownUtc = apod_wallpaper.UpdateReminderPolicy.FormatUtc(now.AddDays(-1));
            settings.LastUpdateReminderVersion = "1.3.0";
            Assert(!apod_wallpaper.UpdateReminderPolicy.ShouldShowReminder(settings, "v1.3.0", "1.2.4", now, false), "Same version reminder should respect five day cooldown.");
            Assert(apod_wallpaper.UpdateReminderPolicy.ShouldShowReminder(settings, "v1.3.1", "1.2.4", now, false), "Newer latest version should bypass old version cooldown.");

            settings.LastUpdateReminderShownUtc = apod_wallpaper.UpdateReminderPolicy.FormatUtc(now.AddDays(-6));
            settings.LastUpdateReminderVersion = "1.3.0";
            Assert(apod_wallpaper.UpdateReminderPolicy.ShouldShowReminder(settings, "v1.3.0", "1.2.4", now, false), "Same version reminder should show after five days.");

            Assert(!apod_wallpaper.UpdateReminderPolicy.ShouldShowReminder(settings, "v1.3.0", "1.2.4", now, true), "Session guard should block a repeated reminder.");

            settings.SuppressAutomaticUpdateReminder = true;
            settings.AutoCheckUpdatesEnabled = false;
            Assert(!apod_wallpaper.UpdateReminderPolicy.ShouldShowReminder(settings, "v1.3.1", "1.2.4", now, false), "Do not remind should suppress automatic modal.");

            settings.LastKnownLatestVersion = "v1.3.0";
            Assert(apod_wallpaper.UpdateReminderPolicy.IsCachedUpdateAvailable(settings, "1.2.4"), "Cached newer version should produce About update status.");
            var cachedStatus = apod_wallpaper.UpdateReminderPolicy.GetCachedUpdateAvailability(settings, "1.2.4");
            Assert(cachedStatus.Kind == apod_wallpaper.CachedUpdateAvailabilityKind.UpdateAvailable, "Cached status model should report an available update.");
            Assert(cachedStatus.LatestVersion == "1.3.0", "Cached status model should normalize v-prefixed latest versions.");
            var reloadedCachedStatus = apod_wallpaper.UpdateReminderPolicy.GetCachedUpdateAvailability(settings.Clone(), "1.2.4");
            Assert(reloadedCachedStatus.Kind == apod_wallpaper.CachedUpdateAvailabilityKind.UpdateAvailable, "Cached status should survive a settings snapshot reload.");
            Assert(!apod_wallpaper.UpdateReminderPolicy.IsCachedUpdateAvailable(settings, "1.3.0"), "Cached equal version should not produce About update status.");
            settings.LastKnownLatestVersion = "not-a-version";
            Assert(apod_wallpaper.UpdateReminderPolicy.GetCachedUpdateAvailability(settings, "1.2.4").Kind == apod_wallpaper.CachedUpdateAvailabilityKind.None,
                "Invalid cached latest version should not produce About update status.");
            settings.LastKnownLatestVersion = "v1.3.0";
            settings.AutoCheckUpdatesEnabled = false;
            settings.SuppressAutomaticUpdateReminder = true;
            Assert(apod_wallpaper.UpdateReminderPolicy.GetCachedUpdateAvailability(settings, "1.2.4").Kind == apod_wallpaper.CachedUpdateAvailabilityKind.UpdateAvailable,
                "Do not remind should not hide cached About update status.");
        }

        private static void AboutUpdateStatusPreservesCachedUpdatesAfterFailures()
        {
            var sourcePath = Path.Combine(GetRepositoryRoot(), "apod_wallpaper.WinUI", "AboutPage.xaml.cs");
            var source = File.ReadAllText(sourcePath);
            Assert(source.Contains("RefreshUpdateStatusFromBackendAsync()"), "About page should expose a lightweight cached status refresh.");
            Assert(source.Contains("TryShowPersistentUpdateStatusAsync(hideStalePersistentStatus: true)"), "About page refresh should re-read persisted cached update state.");
            Assert(source.Contains("if (!await TryShowPersistentUpdateStatusAsync())"), "Manual check failures should keep cached update status when it is still available.");
            Assert(source.Contains("if (await TryShowPersistentUpdateStatusAsync())"), "Manual up-to-date or transient paths must not hide cached newer update status.");
            Assert(source.Contains("visualState == UpdateStatusVisualState.UpdateAvailable"), "Persistent update available text should cancel transient status dismissal.");
        }

        private static void AboutUpdateStatusUsesPersistedCache()
        {
            const string CurrentVersion = "1.3.1";
            const string LatestVersion = "1.3.2";
            const string ReleaseUrl = "https://github.com/p4kon/apod_wallpaper/releases/tag/v1.3.2";
            var now = new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);
            var settings = CreateDefaultSettingsSnapshot();
            settings.LastKnownLatestVersion = LatestVersion;
            settings.LastKnownLatestReleaseUrl = ReleaseUrl;

            AssertAboutShowsCachedUpdate(settings, CurrentVersion, LatestVersion, "Found update should show in About.");

            var later = settings.Clone();
            later.LastUpdateReminderShownUtc = apod_wallpaper.UpdateReminderPolicy.FormatUtc(now);
            later.LastUpdateReminderVersion = LatestVersion;
            AssertAboutShowsCachedUpdate(later, CurrentVersion, LatestVersion, "Later should not hide cached About update status.");

            var openRelease = settings.Clone();
            openRelease.LastUpdateReminderShownUtc = apod_wallpaper.UpdateReminderPolicy.FormatUtc(now);
            openRelease.LastUpdateReminderVersion = LatestVersion;
            AssertAboutShowsCachedUpdate(openRelease, CurrentVersion, LatestVersion, "Open release should not hide cached About update status.");

            var doNotRemind = settings.Clone();
            doNotRemind.AutoCheckUpdatesEnabled = false;
            doNotRemind.SuppressAutomaticUpdateReminder = true;
            doNotRemind.LastUpdateReminderShownUtc = apod_wallpaper.UpdateReminderPolicy.FormatUtc(now);
            doNotRemind.LastUpdateReminderVersion = LatestVersion;
            AssertAboutShowsCachedUpdate(doNotRemind, CurrentVersion, LatestVersion, "Do not remind should not hide cached About update status.");

            var autoDisabled = settings.Clone();
            autoDisabled.AutoCheckUpdatesEnabled = false;
            AssertAboutShowsCachedUpdate(autoDisabled, CurrentVersion, LatestVersion, "Auto disabled should not hide cached About update status.");

            var suppressTrue = settings.Clone();
            suppressTrue.SuppressAutomaticUpdateReminder = true;
            AssertAboutShowsCachedUpdate(suppressTrue, CurrentVersion, LatestVersion, "Suppressed reminder should not hide cached About update status.");

            var reloaded = settings.Clone();
            AssertAboutShowsCachedUpdate(reloaded, CurrentVersion, LatestVersion, "Cached update status should survive settings reload.");

            Assert(apod_wallpaper.UpdateReminderPolicy.GetCachedUpdateAvailability(settings, LatestVersion).Kind == apod_wallpaper.CachedUpdateAvailabilityKind.None,
                "Current version equal to cached latest should hide About update status.");
            Assert(apod_wallpaper.UpdateReminderPolicy.GetCachedUpdateAvailability(settings, "1.3.3").Kind == apod_wallpaper.CachedUpdateAvailabilityKind.None,
                "Current version newer than cached latest should hide About update status.");
        }

        private static void AssertAboutShowsCachedUpdate(apod_wallpaper.ApplicationSettingsSnapshot settings, string currentVersion, string latestVersion, string message)
        {
            var status = apod_wallpaper.UpdateReminderPolicy.GetCachedUpdateAvailability(settings, currentVersion);
            Assert(status.Kind == apod_wallpaper.CachedUpdateAvailabilityKind.UpdateAvailable, message);
            Assert(status.LatestVersion == latestVersion, "About cached update status should expose the normalized latest version.");
        }

        private static void UpdateCheckPersistenceKeepsCachedLatestAfterDialogChoices()
        {
            var snapshot = CaptureSettings();
            try
            {
                var controller = CreateController();
                const string CurrentVersion = "1.2.4";
                const string LatestVersion = "1.3.0";
                const string ReleaseUrl = "https://github.com/p4kon/apod_wallpaper/releases/tag/v1.3.0";
                var checkedAtUtc = new DateTime(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);

                InvokePersistUpdateCheckResult(controller, new apod_wallpaper.UpdateCheckResult
                {
                    Status = apod_wallpaper.UpdateCheckStatus.UpdateAvailable,
                    CurrentVersion = CurrentVersion,
                    LatestVersion = LatestVersion,
                    LatestReleaseUrl = ReleaseUrl,
                    CheckedAtUtc = checkedAtUtc,
                }, automatic: false);

                var afterManualCheck = ReadSettings(controller);
                AssertCachedUpdateAvailable(afterManualCheck, CurrentVersion, LatestVersion, ReleaseUrl, "Manual check should persist cached latest version.");

                var later = afterManualCheck.Clone();
                later.LastUpdateReminderShownUtc = apod_wallpaper.UpdateReminderPolicy.FormatUtc(checkedAtUtc.AddMinutes(1));
                later.LastUpdateReminderVersion = LatestVersion;
                Assert(controller.SaveSettingsAsync(later).GetAwaiter().GetResult().Succeeded, "Expected Later reminder save to succeed.");
                var afterLater = ReadSettings(controller);
                Assert(afterLater.LastUpdateReminderVersion == LatestVersion, "Later should persist reminder version.");
                AssertCachedUpdateAvailable(afterLater, CurrentVersion, LatestVersion, ReleaseUrl, "Later should keep cached latest version.");

                var openRelease = afterLater.Clone();
                openRelease.LastUpdateReminderShownUtc = apod_wallpaper.UpdateReminderPolicy.FormatUtc(checkedAtUtc.AddMinutes(2));
                openRelease.LastUpdateReminderVersion = LatestVersion;
                Assert(controller.SaveSettingsAsync(openRelease).GetAwaiter().GetResult().Succeeded, "Expected Open release reminder save to succeed.");
                var afterOpenRelease = ReadSettings(controller);
                AssertCachedUpdateAvailable(afterOpenRelease, CurrentVersion, LatestVersion, ReleaseUrl, "Open release should keep cached latest version.");

                var doNotRemind = afterOpenRelease.Clone();
                doNotRemind.AutoCheckUpdatesEnabled = false;
                doNotRemind.SuppressAutomaticUpdateReminder = true;
                doNotRemind.LastUpdateReminderShownUtc = apod_wallpaper.UpdateReminderPolicy.FormatUtc(checkedAtUtc.AddMinutes(3));
                doNotRemind.LastUpdateReminderVersion = LatestVersion;
                Assert(controller.SaveSettingsAsync(doNotRemind).GetAwaiter().GetResult().Succeeded, "Expected Do not remind save to succeed.");
                var afterDoNotRemind = ReadSettings(controller);
                Assert(!afterDoNotRemind.AutoCheckUpdatesEnabled, "Do not remind should turn automatic update checks off.");
                Assert(afterDoNotRemind.SuppressAutomaticUpdateReminder, "Do not remind should suppress automatic reminders.");
                AssertCachedUpdateAvailable(afterDoNotRemind, CurrentVersion, LatestVersion, ReleaseUrl, "Do not remind should keep cached latest version.");

                var legacySettingsSave = CreateDefaultSettingsSnapshot();
                legacySettingsSave.SuppressAutomaticUpdateReminder = true;
                legacySettingsSave.AutoCheckUpdatesEnabled = false;
                Assert(controller.SaveSettingsAsync(legacySettingsSave).GetAwaiter().GetResult().Succeeded, "Expected legacy settings save to succeed.");
                var afterLegacySave = ReadSettings(controller);
                AssertCachedUpdateAvailable(afterLegacySave, CurrentVersion, LatestVersion, ReleaseUrl, "A settings save without cached latest fields should not wipe known update status.");
            }
            finally
            {
                RestoreSettings(snapshot);
            }
        }

        private static void DisplayTopologySnapshotIsReadOnly()
        {
            var sourcePath = Path.Combine(GetRepositoryRoot(), "apod_wallpaper.Core", "DisplayTopologySnapshot.cs");
            var source = File.ReadAllText(sourcePath);
            var forbiddenTokens = new[]
            {
                "SetWallpaper",
                "SystemParametersInfo",
                "ApplyDay",
                "DownloadDay",
                "Save(",
            };

            foreach (var token in forbiddenTokens)
                Assert(!source.Contains(token), "Display topology spike must remain read-only: " + token);

            var snapshot = new apod_wallpaper.DisplayTopologySnapshot(new[]
            {
                new apod_wallpaper.DisplayMonitorInfo("secondary", -1920, 0, 0, 1080),
                new apod_wallpaper.DisplayMonitorInfo("primary", 0, 0, 2560, 1440),
            });
            Assert(snapshot.IsMultiMonitor, "Expected two monitors to be detected as multi-monitor topology.");
            Assert(snapshot.Monitors[0].DevicePath == "secondary", "Expected monitors to be sorted by desktop position.");
        }

        private static void MonthCalendarKeepsStableDayVisuals()
        {
            var sourcePath = Path.Combine(GetRepositoryRoot(), "apod_wallpaper.WinUI", "MainPage.xaml.cs");
            var source = File.ReadAllText(sourcePath);
            var xamlPath = Path.Combine(GetRepositoryRoot(), "apod_wallpaper.WinUI", "MainPage.xaml");
            var xaml = File.ReadAllText(xamlPath);

            Assert(source.IndexOf("CalendarDaysGrid.Children.Clear()", StringComparison.Ordinal) < 0,
                "Month navigation must not rebuild the date button visual tree.");
            Assert(source.IndexOf("visual.Button.IsEnabled = !isFuture", StringComparison.Ordinal) < 0,
                "Month calendar future dates must not rely on Button disabled visual state.");
            Assert(xaml.IndexOf("CalendarYearScrollViewer_PointerWheelChanged", StringComparison.Ordinal) < 0,
                "Year calendar scrolling should use the native ScrollViewer wheel handling.");

            var applyMethodIndex = source.IndexOf("private async Task ApplyCalendarMonthStateAsync", StringComparison.Ordinal);
            var requestGuardIndex = source.IndexOf("if (requestVersion != _monthRequestVersion || !IsVisibleMonth(monthState.Month))", applyMethodIndex, StringComparison.Ordinal);
            var ensureMonthIndex = source.IndexOf("EnsureCalendarMonthBuilt(monthState.Month);", applyMethodIndex, StringComparison.Ordinal);
            Assert(applyMethodIndex >= 0 && requestGuardIndex > applyMethodIndex && requestGuardIndex < ensureMonthIndex,
                "Month state application must reject stale requests before touching the calendar UI.");
        }

        private static void FavoritesPreviewUsesSingleOpenGesturePath()
        {
            var favoritesPagePath = Path.Combine(GetRepositoryRoot(), "apod_wallpaper.WinUI", "FavoritesPage.xaml.cs");
            var favoritesSource = File.ReadAllText(favoritesPagePath);
            var favoritesXamlPath = Path.Combine(GetRepositoryRoot(), "apod_wallpaper.WinUI", "FavoritesPage.xaml");
            var favoritesXaml = File.ReadAllText(favoritesXamlPath);
            var previewWindowPath = Path.Combine(GetRepositoryRoot(), "apod_wallpaper.WinUI", "FavoriteImagePreviewWindow.cs");
            var previewSource = File.ReadAllText(previewWindowPath);

            Assert(favoritesXaml.IndexOf("ItemClick=\"FavoritesGridView_ItemClick\"", StringComparison.Ordinal) < 0,
                "Favorites GridView must not use ItemClick when favorite tiles also handle Tapped.");
            Assert(favoritesSource.IndexOf("FavoritesGridView_ItemClick", StringComparison.Ordinal) < 0,
                "Favorites preview must have one authoritative tile open handler.");
            Assert(favoritesSource.IndexOf("_previewOpenInProgress", StringComparison.Ordinal) >= 0,
                "Favorites preview must guard against duplicate open requests from one input gesture.");
            Assert(favoritesSource.IndexOf("FavoriteContextMenuFactory.Create", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("FavoriteContextMenuFactory.Create", StringComparison.Ordinal) >= 0,
                "Favorites page and fullscreen preview must use the same context menu factory.");
            Assert(favoritesSource.IndexOf("SetFavoriteAsWallpaperAsync", StringComparison.Ordinal) >= 0 &&
                favoritesSource.IndexOf("ApplyDayAsync(item.Date.Date, wallpaperStyle)", StringComparison.Ordinal) >= 0,
                "Favorites context menu must apply the selected favorite through the backend workflow.");
            Assert(favoritesSource.IndexOf("updatedSnapshot.AutoRefreshEnabled = false;", StringComparison.Ordinal) >= 0,
                "Applying a favorite as wallpaper must turn off automatic wallpaper apply.");
            Assert(previewSource.IndexOf("TryCreateBlurredBackdrop", StringComparison.Ordinal) >= 0,
                "Favorites preview must build a screenshot-backed blurred backdrop before showing.");
            Assert(previewSource.IndexOf("Background = BuildBackdropBrush(_workArea)", StringComparison.Ordinal) >= 0,
                "Favorites preview must use the captured backdrop as the root background for the first visible frame.");
            Assert(previewSource.IndexOf("public async Task ShowPreviewAsync()", StringComparison.Ordinal) >= 0,
                "Favorites preview must prepare the first image frame before showing the window.");
            Assert(previewSource.IndexOf("await ShowCurrentItemAsync();", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("await ShowCurrentItemAsync();", StringComparison.Ordinal) < previewSource.IndexOf("AppWindow.Show(false);", StringComparison.Ordinal),
                "Favorites preview must load the image before the off-screen AppWindow.Show call.");
            Assert(previewSource.IndexOf("CreateHiddenWindowBounds", StringComparison.Ordinal) < 0 &&
                previewSource.IndexOf("BringToForeground(\"hidden\")", StringComparison.Ordinal) < 0,
                "Favorites preview must not show a hidden window and then visibly resize it to fullscreen.");
            Assert(previewSource.IndexOf("BackgroundFadeInMs = 100", StringComparison.Ordinal) >= 0,
                "Favorites preview background fade-in must stay at 100 ms.");
            Assert(previewSource.IndexOf("SurfaceFadeInMs = 150", StringComparison.Ordinal) >= 0,
                "Favorites preview image fade-in must stay at 150 ms.");
            Assert(previewSource.IndexOf("NativeWindowFadeInMs = 120", StringComparison.Ordinal) >= 0,
                "Favorites preview native window fade-in must smooth the first visible backdrop frame.");
            Assert(previewSource.IndexOf("CloseFadeOutMs = 80", StringComparison.Ordinal) >= 0,
                "Favorites preview close fade-out must stay short but smooth.");
            Assert(previewSource.IndexOf("_root.KeyDown += Root_KeyDown;", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("private async void Root_KeyDown", StringComparison.Ordinal) >= 0,
                "Favorites preview must handle keyboard input.");
            Assert(previewSource.IndexOf("VirtualKey.Escape", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("VirtualKey.Space", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("VirtualKey.Left", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("VirtualKey.Right", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("VirtualKey.Number0", StringComparison.Ordinal) >= 0,
                "Favorites preview must support close, previous/next, and reset keyboard shortcuts.");
            Assert(previewSource.IndexOf("e.Handled = true;\r\n        await ClosePreviewAsync();", StringComparison.Ordinal) < 0 ||
                previewSource.IndexOf("if (e.Key == VirtualKey.Escape || e.Key == VirtualKey.Space)", StringComparison.Ordinal) >= 0,
                "Favorites preview must not close on arbitrary keyboard input.");
            Assert(previewSource.IndexOf("MinZoom = 0.4", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("MaxZoom = 5.0", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("PointerWheelChanged", StringComparison.Ordinal) >= 0,
                "Favorites preview must keep bounded mouse-wheel zoom.");
            Assert(previewSource.IndexOf("MinWheelZoomStep = 0.025", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("MaxWheelZoomStep = 0.125", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("SlowWheelNotchesPerSecond = 5", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("FastWheelNotchesPerSecond = 20", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("instantaneousSpeed = Math.Abs(signedNotches) * 1000d / elapsedMs", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("_smoothedWheelSpeed += (instantaneousSpeed - _smoothedWheelSpeed) * WheelSpeedSmoothingFactor", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("(clampedSpeed - SlowWheelNotchesPerSecond) /", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("Math.Pow(1 + zoomStep, signedNotches)", StringComparison.Ordinal) >= 0,
                "Favorites preview wheel zoom must map measured wheel speed to a smooth 2.5-12.5 percent step.");
            Assert(previewSource.IndexOf("direction != _lastWheelDirection", StringComparison.Ordinal) >= 0,
                "Favorites preview wheel speed must reset when the user reverses zoom direction.");
            Assert(previewSource.IndexOf("if (!IsWithin(_imageViewport, e.OriginalSource", StringComparison.Ordinal) < 0 &&
                previewSource.IndexOf("ResolveZoomAnchor(point.Position", StringComparison.Ordinal) >= 0,
                "Favorites preview wheel zoom must work across the fullscreen viewer and resolve an image-aware anchor.");
            Assert(previewSource.IndexOf("anchor.X - ((anchor.X - _targetPanX) * zoomRatio)", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("anchor.Y - ((anchor.Y - _targetPanY) * zoomRatio)", StringComparison.Ordinal) >= 0,
                "Favorites preview zoom must preserve the image point beneath the pointer.");
            Assert(previewSource.IndexOf("CompositionTarget.Rendering += ZoomAnimation_Rendering;", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("CompositionTarget.Rendering -= ZoomAnimation_Rendering;", StringComparison.Ordinal) >= 0,
                "Favorites preview zoom smoothing must attach and detach its render callback.");
            Assert(previewSource.IndexOf("StorageFile.GetFileFromPathAsync(item.ImagePath)", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("PreviewDecodePixelWidth", StringComparison.Ordinal) < 0 &&
                previewSource.IndexOf("DecodePixelWidth =", StringComparison.Ordinal) < 0,
                "Favorites fullscreen preview must decode the full local image instead of the thumbnail or a capped 1800 px bitmap.");
            Assert(previewSource.IndexOf("CapturePointer", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("ReleasePointerCapture(e.Pointer)", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("DragThreshold = 6", StringComparison.Ordinal) >= 0,
                "Favorites preview must use a drag threshold and release only the active pointer capture.");

            var pointerReleasedIndex = previewSource.IndexOf("private async void Root_PointerReleased", StringComparison.Ordinal);
            var dragSnapshotIndex = previewSource.IndexOf("var wasDragging = _isDragging;", pointerReleasedIndex, StringComparison.Ordinal);
            var pointerReleaseIndex = previewSource.IndexOf("_root.ReleasePointerCapture(e.Pointer);", pointerReleasedIndex, StringComparison.Ordinal);
            Assert(pointerReleasedIndex >= 0 && dragSnapshotIndex > pointerReleasedIndex && pointerReleaseIndex > dragSnapshotIndex,
                "Favorites preview must snapshot drag state before releasing pointer capture so capture-lost cannot turn a drag into a click.");

            var imageViewportIndex = previewSource.IndexOf("private Grid BuildImageViewport", StringComparison.Ordinal);
            var surfaceIndex = previewSource.IndexOf("private Grid BuildSurface", StringComparison.Ordinal);
            var infoTextIndex = previewSource.IndexOf("private static TextBlock BuildInfoText", StringComparison.Ordinal);
            Assert(imageViewportIndex >= 0 && surfaceIndex > imageViewportIndex && infoTextIndex > surfaceIndex,
                "Favorites preview must use transparent grid surfaces for the image canvas.");
            var imageSurfaceSource = previewSource.Substring(imageViewportIndex, infoTextIndex - imageViewportIndex);
            Assert(imageSurfaceSource.IndexOf("FromArgb(255, 0, 0, 0)", StringComparison.Ordinal) < 0,
                "Favorites preview image canvas must not paint black letterbox bars around the image.");
            Assert(previewSource.IndexOf("UpdateSurfaceSizeForBitmap(bitmap)", StringComparison.Ordinal) >= 0,
                "Favorites preview must size its base image surface from the bitmap aspect ratio.");
            Assert(previewSource.IndexOf("_imageViewport.RenderTransform = _imageTransform;", StringComparison.Ordinal) >= 0,
                "Favorites preview must transform the full image hit-test surface when zooming and panning.");
            Assert(previewSource.IndexOf("private double ResolveViewportWidth()", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("private double ResolveViewportHeight()", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("_root.ActualWidth > 0", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("_root.ActualHeight > 0", StringComparison.Ordinal) >= 0,
                "Favorites preview fit and pan bounds must use the fullscreen XAML viewport instead of the old fixed image frame.");
            Assert(previewSource.IndexOf("menu.ShowAt(_root, e.GetPosition(_root));", StringComparison.Ordinal) >= 0,
                "Favorites preview context menu must open at the right-click pointer position.");
            var pointerMovedIndex = previewSource.IndexOf("private void Root_PointerMoved", StringComparison.Ordinal);
            var pointerMovedInfoIndex = previewSource.IndexOf("ShowInfoPanel();", pointerMovedIndex, StringComparison.Ordinal);
            var pointerMovedPressedGuardIndex = previewSource.IndexOf("if (!_pointerPressed)", pointerMovedIndex, StringComparison.Ordinal);
            Assert(pointerMovedIndex >= 0 && pointerMovedInfoIndex > pointerMovedIndex &&
                pointerMovedPressedGuardIndex > pointerMovedInfoIndex,
                "Favorites preview info panel must reappear on ordinary pointer movement, not only during a drag.");
            var infoPanelIndex = previewSource.IndexOf("private static Border BuildInfoPanel", StringComparison.Ordinal);
            var showCurrentItemIndex = previewSource.IndexOf("private async Task ShowCurrentItemAsync", StringComparison.Ordinal);
            var infoPanelSource = previewSource.Substring(infoPanelIndex, showCurrentItemIndex - infoPanelIndex);
            Assert(infoPanelIndex >= 0 && showCurrentItemIndex > infoPanelIndex &&
                infoPanelSource.IndexOf("IsHitTestVisible = false", StringComparison.Ordinal) < 0 &&
                previewSource.IndexOf("InfoPanel_PointerPressed", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("InfoPanel_PointerReleased", StringComparison.Ordinal) >= 0,
                "Favorites preview info panel must consume clicks instead of passing them through and closing the viewer.");
            Assert(previewSource.IndexOf("BuildOverlayButton", StringComparison.Ordinal) < 0 &&
                previewSource.IndexOf("CalendarButton_Click", StringComparison.Ordinal) < 0,
                "Favorites preview must not show old top corner overlay buttons.");
            Assert(previewSource.IndexOf("graphics.FillRectangle(tint", StringComparison.Ordinal) < 0,
                "Favorites preview must animate darkening instead of baking the tint into the captured backdrop.");
            Assert(previewSource.IndexOf("AppWindow.MoveAndResize(CreateWarmupBounds());", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("AppWindow.MoveAndResize(CreateWarmupBounds());", StringComparison.Ordinal) < previewSource.IndexOf("AppWindow.Show(false);", StringComparison.Ordinal),
                "Favorites preview must warm up off-screen before the first visible move.");
            Assert(previewSource.IndexOf("await WaitForRenderPassesAsync(2);", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("AppWindow.MoveAndResize(_workArea);", StringComparison.Ordinal) > previewSource.IndexOf("await WaitForRenderPassesAsync(2);", StringComparison.Ordinal),
                "Favorites preview must wait for render-ready state before moving on-screen.");
            Assert(previewSource.IndexOf("SetNativeWindowAlpha(0);", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("SetNativeWindowAlpha(0);", StringComparison.Ordinal) < previewSource.IndexOf("AppWindow.MoveAndResize(_workArea);", StringComparison.Ordinal) &&
                previewSource.IndexOf("await FadeInNativeWindowAsync();", StringComparison.Ordinal) > previewSource.IndexOf("BeginOpenAnimation();", StringComparison.Ordinal),
                "Favorites preview must fade the native window in with the XAML image animation.");
            Assert(previewSource.IndexOf("AppWindow.Show(true);", StringComparison.Ordinal) < 0,
                "Favorites preview must not show directly on-screen before warmup.");
            Assert(previewSource.IndexOf("FadeOutNativeWindowAsync", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("SetNativeWindowAlpha(alpha)", StringComparison.Ordinal) >= 0,
                "Favorites preview close must fade the native window instead of fading XAML root to black.");
            Assert(previewSource.IndexOf("WmMouseActivate", StringComparison.Ordinal) >= 0 &&
                previewSource.IndexOf("RemoveMouseActivateGuard", StringComparison.Ordinal) >= 0,
                "Favorites preview must keep a first-click guard with cleanup.");

            var menuFactoryPath = Path.Combine(GetRepositoryRoot(), "apod_wallpaper.WinUI", "FavoriteContextMenuFactory.cs");
            var menuFactorySource = File.ReadAllText(menuFactoryPath);
            Assert(menuFactorySource.IndexOf("AppStrings.Get(\"Set as wallpaper\")", StringComparison.Ordinal) >= 0 &&
                menuFactorySource.IndexOf("AppStrings.Get(\"Open favorite in Calendar\")", StringComparison.Ordinal) >= 0 &&
                menuFactorySource.IndexOf("AppStrings.Get(\"Open in folder\")", StringComparison.Ordinal) >= 0 &&
                menuFactorySource.IndexOf("AppStrings.Get(\"Remove from favorites\")", StringComparison.Ordinal) >= 0,
                "Shared favorites context menu must localize every visible command.");
        }

        private static void UpdateReminderDiagnosticsAvoidSecrets()
        {
            var shellSource = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "apod_wallpaper.WinUI", "ShellPage.xaml.cs"));
            var aboutSource = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "apod_wallpaper.WinUI", "AboutPage.xaml.cs"));
            Assert(shellSource.Contains("[UpdateReminder]"), "Automatic update reminders must write diagnostic log entries.");
            Assert(aboutSource.Contains("trigger=ManualAboutCheck"), "Manual About update checks must write diagnostic log entries.");
            Assert(!shellSource.Contains("NasaApiKey") && !aboutSource.Contains("NasaApiKey"), "Update diagnostics must not log NASA API keys.");
            Assert(!shellSource.Contains("ImagesDirectoryPath") && !aboutSource.Contains("ImagesDirectoryPath"), "Update diagnostics must not log user storage paths.");
        }

        private static void ApodPageUrlBuilderIsDeterministic()
        {
            var url = apod_wallpaper.ApodPageUrl.BuildUrl(new DateTime(2026, 7, 14));
            Assert(url == "https://apod.nasa.gov/apod/ap260714.html", "Expected APOD HTML URL to use apYYMMDD.html.");

            var firstApodUrl = apod_wallpaper.ApodPageUrl.BuildUrl(new DateTime(1995, 6, 16));
            Assert(firstApodUrl == "https://apod.nasa.gov/apod/ap950616.html", "Expected APOD HTML URL to preserve two-digit year formatting.");
        }

        private static void ApodAvailabilityProbeEvaluatesRedirects()
        {
            var requestedDate = new DateTime(2026, 7, 15);
            var expectedUrl = apod_wallpaper.ApodPageUrl.BuildUrl(requestedDate);

            var available = apod_wallpaper.ApodPageAvailabilityProbe.EvaluateResponse(
                requestedDate,
                expectedUrl,
                System.Net.HttpStatusCode.OK,
                "HEAD",
                null,
                new Uri(expectedUrl));
            Assert(available.IsAvailable, "Expected exact 200 response to be available.");

            var redirected = apod_wallpaper.ApodPageAvailabilityProbe.EvaluateResponse(
                requestedDate,
                expectedUrl,
                System.Net.HttpStatusCode.Redirect,
                "HEAD",
                new Uri("/apod/ap260714.html", UriKind.Relative),
                new Uri(expectedUrl));
            Assert(redirected.IsUnavailable, "Expected redirect to another APOD date to be unavailable.");
            Assert(!redirected.IsAvailable, "Redirect to another date must not unlock today.");
            Assert(!apod_wallpaper.ApodPageAvailabilityProbe.ShouldRetryWithGet(redirected), "Redirect responses must not retry as GET.");
        }

        private static void ApodAvailabilityProbeRetriesForbiddenHeadWithGet()
        {
            var requestedDate = new DateTime(2026, 7, 15);
            var expectedUrl = apod_wallpaper.ApodPageUrl.BuildUrl(requestedDate);

            var forbiddenHead = apod_wallpaper.ApodPageAvailabilityProbe.EvaluateResponse(
                requestedDate,
                expectedUrl,
                System.Net.HttpStatusCode.Forbidden,
                "HEAD",
                null,
                new Uri(expectedUrl));
            Assert(apod_wallpaper.ApodPageAvailabilityProbe.ShouldRetryWithGet(forbiddenHead), "Expected HEAD 403 to retry with GET.");

            var notFoundHead = apod_wallpaper.ApodPageAvailabilityProbe.EvaluateResponse(
                requestedDate,
                expectedUrl,
                System.Net.HttpStatusCode.NotFound,
                "HEAD",
                null,
                new Uri(expectedUrl));
            Assert(!apod_wallpaper.ApodPageAvailabilityProbe.ShouldRetryWithGet(notFoundHead), "HEAD 404 must not retry with GET.");
        }

        private static void CalendarAvailabilityTransientOverrideUnlocksTodayOnly()
        {
            var yesterday = new DateTime(2026, 7, 14);
            var today = new DateTime(2026, 7, 15);
            var tomorrow = new DateTime(2026, 7, 16);

            var effectiveLatest = apod_wallpaper.ApodCalendarAvailability.ResolveEffectiveLatestPublishedDate(yesterday, today);
            Assert(effectiveLatest == today, "Expected transient available date to advance effective latest published date.");
            Assert(tomorrow > effectiveLatest, "Expected tomorrow to remain future.");
        }

        private static void CalendarAvailabilityThrottleResetsWhenTodayChanges()
        {
            var previousDay = new DateTime(2026, 7, 14);
            var currentDay = new DateTime(2026, 7, 15);
            var lastProbeUtc = new DateTime(2026, 7, 14, 20, 59, 0, DateTimeKind.Utc);
            var nowUtc = new DateTime(2026, 7, 14, 21, 1, 0, DateTimeKind.Utc);
            var throttle = TimeSpan.FromMinutes(5);

            Assert(!apod_wallpaper.ApodCalendarAvailability.ShouldThrottleProbe(currentDay, previousDay, lastProbeUtc, nowUtc, throttle),
                "Expected new APOD date to bypass throttle.");
            Assert(apod_wallpaper.ApodCalendarAvailability.ShouldThrottleProbe(previousDay, previousDay, lastProbeUtc, nowUtc, throttle),
                "Expected same APOD date to remain throttled within the throttle window.");
            Assert(!apod_wallpaper.ApodCalendarAvailability.ShouldThrottleProbe(previousDay, null, lastProbeUtc, nowUtc, throttle),
                "Expected missing last probe date to skip throttle.");
        }

        private static void ApodAvailabilityProbeSourceAvoidsWorkflowSideEffects()
        {
            var probeSourcePath = Path.Combine(GetRepositoryRoot(), "apod_wallpaper.Core", "ApodPageAvailabilityProbe.cs");
            var source = File.ReadAllText(probeSourcePath);
            var forbiddenTokens = new[]
            {
                "ApplyDay",
                "ApplyLatest",
                "DownloadDay",
                "LoadDay",
                "ApodWorkflowService",
                "Scheduler",
            };

            foreach (var token in forbiddenTokens)
            {
                Assert(!source.Contains(token), "Availability probe must not call workflow side-effect path: " + token);
            }
        }

        private static void CalendarYearStateSourceIsCacheOnly()
        {
            var sourcePath = Path.Combine(GetRepositoryRoot(), "apod_wallpaper.Core", "ApodCalendarStateService.cs");
            var source = File.ReadAllText(sourcePath);
            Assert(source.Contains("GetYearState(int year)"), "Expected calendar year state method.");
            Assert(source.Contains("refreshMissingDates: false"), "Year state must render from cache without refreshing missing dates.");
            Assert(source.Contains("MonthRefreshMode.Balanced"), "Year state should use balanced cache-only status evaluation.");
        }

        private static void FutureDateIsUnavailableSync()
        {
            var workflow = new apod_wallpaper.ApodWorkflowService();
            var result = workflow.LoadDay(DateTime.Today.AddDays(1), false);
            Assert(result.Status == apod_wallpaper.ApodWorkflowStatus.Unavailable, "Expected Unavailable status for a future date.");
        }

        private static void FutureDateIsUnavailableAsync()
        {
            var workflow = new apod_wallpaper.ApodWorkflowService();
            var result = workflow.LoadDayAsync(DateTime.Today.AddDays(2), false).GetAwaiter().GetResult();
            Assert(result.Status == apod_wallpaper.ApodWorkflowStatus.Unavailable, "Expected async Unavailable status for a future date.");
        }

        private static void ApiKeyChangeResetsValidationState()
        {
            var snapshot = CaptureSettings();
            try
            {
                var controller = CreateController();
                var firstSaveResult = controller.SaveSettingsAsync(new apod_wallpaper.ApplicationSettingsSnapshot
                {
                    TrayDoubleClickAction = snapshot.TrayDoubleClickAction,
                    WallpaperStyleIndex = snapshot.WallpaperStyleIndex,
                    AutoRefreshEnabled = false,
                    StartWithWindows = snapshot.StartWithWindows,
                    ImagesDirectoryPath = snapshot.ImagesDirectoryPath,
                    NasaApiKey = "first-key",
                    NasaApiKeyValidationState = apod_wallpaper.ApiKeyValidationState.Valid.ToString(),
                    LastAutoRefreshRunDate = snapshot.LastAutoRefreshRunDate,
                    LastAutoRefreshAppliedDate = snapshot.LastAutoRefreshAppliedDate,
                }).GetAwaiter().GetResult();
                Assert(firstSaveResult.Succeeded, "Expected first settings save to succeed.");

                var secondSaveResult = controller.SaveSettingsAsync(new apod_wallpaper.ApplicationSettingsSnapshot
                {
                    TrayDoubleClickAction = snapshot.TrayDoubleClickAction,
                    WallpaperStyleIndex = snapshot.WallpaperStyleIndex,
                    AutoRefreshEnabled = false,
                    StartWithWindows = snapshot.StartWithWindows,
                    ImagesDirectoryPath = snapshot.ImagesDirectoryPath,
                    NasaApiKey = "second-key",
                    NasaApiKeyValidationState = apod_wallpaper.ApiKeyValidationState.Valid.ToString(),
                    LastAutoRefreshRunDate = snapshot.LastAutoRefreshRunDate,
                    LastAutoRefreshAppliedDate = snapshot.LastAutoRefreshAppliedDate,
                }).GetAwaiter().GetResult();
                Assert(secondSaveResult.Succeeded, "Expected second settings save to succeed.");

                Assert(GetValueOrThrow(controller.GetApiKeyValidationStateAsync().GetAwaiter().GetResult(), "Unable to read API key validation state.") == apod_wallpaper.ApiKeyValidationState.Unknown,
                    "Expected validation state to reset to Unknown after API key change.");
            }
            finally
            {
                RestoreSettings(snapshot);
            }
        }

        private static void PreferredDisplayDateUsesLastAppliedDate()
        {
            var snapshot = CaptureSettings();
            try
            {
                var controller = CreateController();
                var saveResult = controller.SaveSettingsAsync(new apod_wallpaper.ApplicationSettingsSnapshot
                {
                    TrayDoubleClickAction = snapshot.TrayDoubleClickAction,
                    WallpaperStyleIndex = snapshot.WallpaperStyleIndex,
                    AutoRefreshEnabled = snapshot.AutoRefreshEnabled,
                    StartWithWindows = snapshot.StartWithWindows,
                    ImagesDirectoryPath = snapshot.ImagesDirectoryPath,
                    NasaApiKey = snapshot.NasaApiKey,
                    NasaApiKeyValidationState = snapshot.NasaApiKeyValidationState,
                    LastAutoRefreshRunDate = snapshot.LastAutoRefreshRunDate,
                    LastAutoRefreshAppliedDate = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd"),
                }).GetAwaiter().GetResult();
                Assert(saveResult.Succeeded, "Expected settings save for preferred display date to succeed.");

                Assert(GetValueOrThrow(controller.GetPreferredDisplayDateAsync().GetAwaiter().GetResult(), "Unable to resolve preferred display date.") == DateTime.Today.AddDays(-1),
                    "Expected preferred display date to use the last auto-applied date.");
            }
            finally
            {
                RestoreSettings(snapshot);
            }
        }

        private static void HtmlExtractorResolvesImagePage()
        {
            const string html =
@"2026 April 8
<br>
<a href=""image/2604/earthset_original.jpg"">
<IMG SRC=""image/2604/earthset_700.jpg"" style=""max-width:100%""></a>
</center>";

            string previewUrl;
            string imageUrl;
            var result = apod_wallpaper.ApodPageImageExtractor.TryExtract(
                html,
                "https://apod.nasa.gov/apod/ap260408.html",
                out previewUrl,
                out imageUrl);

            Assert(result, "Expected extractor to resolve an image page.");
            Assert(previewUrl == "https://apod.nasa.gov/apod/image/2604/earthset_700.jpg", "Unexpected preview URL.");
            Assert(imageUrl == "https://apod.nasa.gov/apod/image/2604/earthset_original.jpg", "Unexpected full image URL.");
        }

        private static void HtmlExtractorRejectsVideoPage()
        {
            const string html =
@"2026 April 9
<br>
<video width=""960"" height=""540"" controls autoplay muted>
<source src=""image/2604/comet_plunge.mp4"" type=""video/mp4"">
</video>
</center>";

            string previewUrl;
            string imageUrl;
            var result = apod_wallpaper.ApodPageImageExtractor.TryExtract(
                html,
                "https://apod.nasa.gov/apod/ap260409.html",
                out previewUrl,
                out imageUrl);

            Assert(!result, "Expected extractor to reject a video page.");
            Assert(string.IsNullOrEmpty(previewUrl), "Preview URL should be empty for video pages.");
            Assert(string.IsNullOrEmpty(imageUrl), "Image URL should be empty for video pages.");
        }

        private static void HtmlExtractorClassifiesTextOnlyPageAsUnsupported()
        {
            const string html =
@"<html>
<head>
<title>APOD: 2012 March 12 - Text Only APOD</title>
</head>
<body>
<center>
2012 March 12
</center>
<p><b> Explanation: </b>
Some APOD archive pages contain text but no downloadable image or video media.
</body>
</html>";

            string videoUrl;
            var result = apod_wallpaper.ApodPageImageExtractor.TryExtractVideo(
                html,
                "https://apod.nasa.gov/apod/ap120312.html",
                out videoUrl);

            Assert(result, "Expected text-only APOD page to be classified as unsupported media.");
            Assert(videoUrl == "https://apod.nasa.gov/apod/ap120312.html", "Expected text-only media URL to point to the APOD page.");
        }

        private static void HtmlExtractorHandlesAnnotatedImagePage()
        {
            const string html =
@"2026 March 15
<br>
<a href=""image/2603/MayanMilkyWay_Fernandez_1600.jpg""
onMouseOver=""if (document.images) document.imagename1.src='image/2603/MayanMilkyWay_Fernandez_1080_annotated.jpg';""
onMouseOut=""if (document.images) document.imagename1.src='image/2603/MayanMilkyWay_Fernandez_1080.jpg';"">
<IMG SRC=""image/2603/MayanMilkyWay_Fernandez_1080.jpg"" name=imagename1 style=""max-width:100%""></a>
</center>";

            string previewUrl;
            string imageUrl;
            var result = apod_wallpaper.ApodPageImageExtractor.TryExtract(
                html,
                "https://apod.nasa.gov/apod/ap260315.html",
                out previewUrl,
                out imageUrl);

            Assert(result, "Expected extractor to resolve an annotated image page.");
            Assert(previewUrl == "https://apod.nasa.gov/apod/image/2603/MayanMilkyWay_Fernandez_1080.jpg", "Unexpected preview URL for annotated image page.");
            Assert(imageUrl == "https://apod.nasa.gov/apod/image/2603/MayanMilkyWay_Fernandez_1600.jpg", "Unexpected full image URL for annotated image page.");
        }

        private static void HtmlExtractorResolvesTextMetadata()
        {
            const string html =
@"<html>
<head>
<title>APOD: 2026 May 04 - Spiral Echoes</title>
</head>
<body>
<center>
2026 May 04
<br>
<a href=""image/2605/spiral_full.jpg"">
<img src=""image/2605/spiral_preview.jpg""></a>
</center>
<p><b> Explanation: </b>
Spiral dust lanes &amp; glowing gas reveal how galaxies evolve.
<p>
Bright clusters mark newborn stars.
</p>
<p> <center>
<b> Growing Gallery: </b><a href=""ap260503.html"">extra related link</a>
</center>
<p>Tomorrow's picture: another sky surprise.</p>
</body>
</html>";

            var title = apod_wallpaper.ApodPageImageExtractor.ExtractTitle(html);
            var explanation = apod_wallpaper.ApodPageImageExtractor.ExtractExplanation(html);

            Assert(title == "Spiral Echoes", "Expected APOD HTML title to be extracted.");
            Assert(explanation == "Spiral dust lanes & glowing gas reveal how galaxies evolve. Bright clusters mark newborn stars.",
                "Expected APOD explanation text to be extracted and normalized.");
        }

        private static void InvalidApiKeyFallsBackToDemoKey()
        {
            apod_wallpaper.AppRuntimeSettings.Configure("invalid-key", null, apod_wallpaper.ApiKeyValidationState.Invalid);
            Assert(apod_wallpaper.AppRuntimeSettings.NasaApiKey == "DEMO_KEY", "Expected DEMO_KEY fallback for invalid API key.");
            Assert(apod_wallpaper.AppRuntimeSettings.RawNasaApiKey == "invalid-key", "Expected raw API key to remain available.");
        }

        private static void LocalImageIsPreferredForPreview()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_smoke_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);

            var snapshot = CaptureSettings();
            try
            {
                var date = new DateTime(2026, 4, 18);
                var imagePath = Path.Combine(tempDirectory, "2026-04-18.jpg");
                using (var bitmap = new Bitmap(8, 8))
                {
                    bitmap.Save(imagePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                }

                apod_wallpaper.FileStorage.SetSessionImagesDirectory(tempDirectory);
                var workflow = new apod_wallpaper.ApodWorkflowService();
                var result = workflow.LoadDay(date, false);

                Assert(result.Status == apod_wallpaper.ApodWorkflowStatus.Success, "Expected local preview workflow to succeed.");
                Assert(result.IsLocalFile, "Expected local file to be used for preview.");
                Assert(string.Equals(result.PreviewLocation, imagePath, StringComparison.OrdinalIgnoreCase), "Expected preview location to be the local image path.");
            }
            finally
            {
                apod_wallpaper.FileStorage.SetSessionImagesDirectory(snapshot.ImagesDirectoryPath);
                try
                {
                    if (Directory.Exists(tempDirectory))
                        Directory.Delete(tempDirectory, true);
                }
                catch
                {
                }

                RestoreSettings(snapshot);
            }
        }

        private static void ApplyLatestPublishedFallsBackAcrossVideoDays()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_smoke_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);

            var snapshot = CaptureSettings();
            try
            {
                var today = DateTime.Today;
                var yesterday = today.AddDays(-1);
                var imageDate = today.AddDays(-2);
                var localImagePath = Path.Combine(tempDirectory, imageDate.ToString("yyyy-MM-dd") + ".jpg");

                using (var bitmap = new Bitmap(12, 12))
                {
                    bitmap.Save(localImagePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                }

                apod_wallpaper.FileStorage.SetSessionImagesDirectory(tempDirectory);

                var fakeClient = new FakeApodClient(
                    CreateVideoEntry(today),
                    new System.Collections.Generic.Dictionary<DateTime, apod_wallpaper.ApodEntry>
                    {
                        { today, CreateVideoEntry(today) },
                        { yesterday, CreateVideoEntry(yesterday) },
                        { imageDate, CreateImageEntry(imageDate) },
                    });
                var fakeCache = new InMemoryApodMetadataCache();
                var fakeWallpaperApplier = new FakeWallpaperApplier();
                var service = new apod_wallpaper.ApodWallpaperService(fakeClient, fakeCache, fakeWallpaperApplier);
                var workflow = new apod_wallpaper.ApodWorkflowService(service);

                var result = workflow.ApplyLatestPublished(apod_wallpaper.WallpaperStyle.Smart, true);

                Assert(result.Status == apod_wallpaper.ApodWorkflowStatus.Success, "Expected ApplyLatestPublished to succeed.");
                Assert(result.ResolvedDate == imageDate, "Expected ApplyLatestPublished to fall back to the nearest image date.");
                Assert(result.LatestPublishedDate == today, "Expected ApplyLatestPublished to preserve the real latest published date for calendar updates.");
                Assert(string.Equals(result.ImagePath, localImagePath, StringComparison.OrdinalIgnoreCase), "Expected fallback image path to point to the local image.");
                Assert(fakeWallpaperApplier.LastAppliedImagePath == localImagePath, "Expected wallpaper applier to receive the fallback local image.");
            }
            finally
            {
                apod_wallpaper.FileStorage.SetSessionImagesDirectory(snapshot.ImagesDirectoryPath);
                try
                {
                    if (Directory.Exists(tempDirectory))
                        Directory.Delete(tempDirectory, true);
                }
                catch
                {
                }

                RestoreSettings(snapshot);
            }
        }

        private static void SmartComposerUsesStretchForNearScreenRatio()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_smoke_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);

            try
            {
                var screenBounds = apod_wallpaper.DisplayMetrics.GetPrimaryScreenBounds();
                if (screenBounds.Width <= 0 || screenBounds.Height <= 0)
                    screenBounds = new Rectangle(0, 0, 1920, 1080);
                var imagePath = Path.Combine(tempDirectory, "near-screen.jpg");

                using (var bitmap = new Bitmap(screenBounds.Width, screenBounds.Height))
                {
                    bitmap.Save(imagePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                }

                var composition = apod_wallpaper.SmartWallpaperComposer.Prepare(imagePath);

                Assert(composition.Style == apod_wallpaper.WallpaperStyle.Stretch, "Expected near-screen-ratio image to use Stretch.");
                Assert(composition.Strategy == "stretch_near_screen_ratio", "Expected near-screen-ratio strategy.");
                Assert(string.Equals(composition.ImagePath, imagePath, StringComparison.OrdinalIgnoreCase), "Expected original image path for stretch strategy.");
            }
            finally
            {
                TryDeleteDirectory(tempDirectory);
            }
        }

        private static void SmartComposerCreatesSingleFocusForSquareImages()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_smoke_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            var snapshot = CaptureSettings();

            try
            {
                var imagePath = Path.Combine(tempDirectory, "square.jpg");

                using (var bitmap = new Bitmap(1200, 1200))
                {
                    bitmap.Save(imagePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                }

                apod_wallpaper.FileStorage.SetSessionImagesDirectory(tempDirectory);
                var composition = apod_wallpaper.SmartWallpaperComposer.Prepare(imagePath);

                Assert(composition.Style == apod_wallpaper.WallpaperStyle.Fill, "Expected square image to use Fill after smart composition.");
                Assert(composition.Strategy == "single_focus_background", "Expected square image to use single focus strategy.");
                Assert(File.Exists(composition.ImagePath), "Expected composed smart wallpaper file to exist.");
                Assert(composition.ImagePath.IndexOf(Path.Combine("smart", string.Empty), StringComparison.OrdinalIgnoreCase) >= 0, "Expected smart image to be stored in the smart subfolder.");
            }
            finally
            {
                RestoreSettings(snapshot);
                TryDeleteDirectory(tempDirectory);
            }
        }

        private static void SmartComposerPreservesUltraWideImages()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_smoke_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            var snapshot = CaptureSettings();

            try
            {
                var imagePath = Path.Combine(tempDirectory, "ultrawide.jpg");

                using (var bitmap = new Bitmap(2920, 1000))
                {
                    bitmap.Save(imagePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                }

                apod_wallpaper.FileStorage.SetSessionImagesDirectory(tempDirectory);
                var composition = apod_wallpaper.SmartWallpaperComposer.Prepare(imagePath);

                Assert(composition.Style == apod_wallpaper.WallpaperStyle.Fill, "Expected ultrawide image to use Fill only after smart composition.");
                Assert(composition.Strategy == "wide_focus_background", "Expected ultrawide image to use wide focus strategy instead of raw Fill cropping.");
                Assert(File.Exists(composition.ImagePath), "Expected composed ultrawide smart wallpaper file to exist.");
                Assert(!string.Equals(composition.ImagePath, imagePath, StringComparison.OrdinalIgnoreCase), "Expected ultrawide smart mode to create a composed wallpaper rather than applying the original image.");
            }
            finally
            {
                RestoreSettings(snapshot);
                TryDeleteDirectory(tempDirectory);
            }
        }

        private static void SchedulerUsesHourlyPollingForDemoKey()
        {
            var pollingInterval = apod_wallpaper.ApplicationController.ResolveSchedulerPollingInterval(new apod_wallpaper.ApplicationSettingsSnapshot
            {
                AutoRefreshEnabled = true,
                NasaApiKey = "DEMO_KEY",
            });

            Assert(pollingInterval == TimeSpan.FromHours(1), "Expected DEMO_KEY polling interval to be one hour.");
        }

        private static void SchedulerUsesThirtyMinutePollingForPersonalKey()
        {
            var pollingInterval = apod_wallpaper.ApplicationController.ResolveSchedulerPollingInterval(new apod_wallpaper.ApplicationSettingsSnapshot
            {
                AutoRefreshEnabled = true,
                NasaApiKey = "personal-key",
                NasaApiKeyValidationState = apod_wallpaper.ApiKeyValidationState.Valid.ToString(),
            });

            Assert(pollingInterval == TimeSpan.FromMinutes(30), "Expected personal key polling interval to be 30 minutes.");
        }

        private static void WallpaperServiceRejectsInvalidLocalFile()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_smoke_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            var invalidImagePath = Path.Combine(tempDirectory, "broken.jpg");
            File.WriteAllText(invalidImagePath, "not-an-image");

            try
            {
                var service = new apod_wallpaper.WallpaperService();
                try
                {
                    service.ApplyPreservingHistory(invalidImagePath, apod_wallpaper.WallpaperStyle.Fill);
                    throw new InvalidOperationException("Expected wallpaper service to reject an invalid local file.");
                }
                catch (InvalidOperationException ex)
                {
                    Assert(ex.Message.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0, "Expected invalid file error message.");
                }
            }
            finally
            {
                TryDeleteDirectory(tempDirectory);
            }
        }

        private static void SchedulerDayLockSkipsAfterTodaysImage()
        {
            var today = DateTime.Today;
            var shouldSkip = apod_wallpaper.ApplicationController.ShouldSkipSchedulerForToday(today, today, today);
            Assert(shouldSkip, "Expected scheduler day lock to skip repeated checks after today's image was applied.");
        }

        private static void SchedulerDayLockKeepsCheckingAfterYesterdayFallback()
        {
            var today = DateTime.Today;
            var shouldSkip = apod_wallpaper.ApplicationController.ShouldSkipSchedulerForToday(today, today.AddDays(-1), today);
            Assert(!shouldSkip, "Expected scheduler day lock to keep checking when today's early run only applied an older fallback image.");
        }

        private static void SchedulerDayLockRequiresAppliedDate()
        {
            var today = DateTime.Today;
            var shouldSkip = apod_wallpaper.ApplicationController.ShouldSkipSchedulerForToday(today, null, today);
            Assert(!shouldSkip, "Expected scheduler day lock to require a successfully applied date.");
        }

        private static void ApiKeyIsStoredOutsidePlaintextSettings()
        {
            var snapshot = CaptureSettings();
            try
            {
                ResetSecretStore();
                var controller = CreateController();
                var saveResult = controller.SaveSettingsAsync(new apod_wallpaper.ApplicationSettingsSnapshot
                {
                    TrayDoubleClickAction = snapshot.TrayDoubleClickAction,
                    WallpaperStyleIndex = snapshot.WallpaperStyleIndex,
                    AutoRefreshEnabled = snapshot.AutoRefreshEnabled,
                    StartWithWindows = snapshot.StartWithWindows,
                    ImagesDirectoryPath = snapshot.ImagesDirectoryPath,
                    NasaApiKey = "protected-test-key",
                    NasaApiKeyValidationState = apod_wallpaper.ApiKeyValidationState.Valid.ToString(),
                    LastAutoRefreshRunDate = snapshot.LastAutoRefreshRunDate,
                    LastAutoRefreshAppliedDate = snapshot.LastAutoRefreshAppliedDate,
                }).GetAwaiter().GetResult();

                Assert(saveResult.Succeeded, "Expected protected API key save to succeed.");
                Assert(_secretStore.GetNasaApiKey() == "protected-test-key", "Expected API key to be stored in protected storage.");
                Assert(GetValueOrThrow(controller.GetSettingsAsync().GetAwaiter().GetResult(), "Unable to read settings after protected save.").NasaApiKey == "protected-test-key", "Expected facade settings to surface the protected API key.");
            }
            finally
            {
                RestoreSettings(snapshot);
                ResetSecretStore();
            }
        }

        private static void InitialStateSnapshotReturnsStartupData()
        {
            var snapshot = CaptureSettings();
            var customImagesDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_initial_state_images_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(customImagesDirectory);

            try
            {
                var lastAppliedDate = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");
                var controller = CreateController();
                var saveResult = controller.SaveSettingsAsync(new apod_wallpaper.ApplicationSettingsSnapshot
                {
                    TrayDoubleClickAction = true,
                    WallpaperStyleIndex = (int)apod_wallpaper.WallpaperStyle.Fill,
                    AutoRefreshEnabled = snapshot.AutoRefreshEnabled,
                    StartWithWindows = snapshot.StartWithWindows,
                    ImagesDirectoryPath = customImagesDirectory,
                    NasaApiKey = "DEMO_KEY",
                    NasaApiKeyValidationState = apod_wallpaper.ApiKeyValidationState.Unknown.ToString(),
                    LastAutoRefreshRunDate = snapshot.LastAutoRefreshRunDate,
                    LastAutoRefreshAppliedDate = lastAppliedDate,
                }).GetAwaiter().GetResult();
                Assert(saveResult.Succeeded, "Expected initial state settings save to succeed.");

                var initialStateResult = controller.GetInitialStateAsync().GetAwaiter().GetResult();
                Assert(initialStateResult.Succeeded, "Expected initial state snapshot to succeed.");

                var initialState = initialStateResult.Value;
                Assert(initialState != null, "Expected initial state payload.");
                Assert(initialState.Settings != null, "Expected settings inside initial state.");
                Assert(initialState.StoragePaths != null, "Expected storage paths inside initial state.");
                Assert(initialState.ApiKeyValidationState == apod_wallpaper.ApiKeyValidationState.Unknown, "Expected validation state from initial state.");
                Assert(initialState.PreferredDisplayDate == DateTime.Today.AddDays(-1), "Expected preferred display date from initial state.");
                Assert(initialState.SelectedWallpaperStyle == apod_wallpaper.WallpaperStyle.Fill, "Expected selected wallpaper style from initial state.");
                Assert(initialState.LocalImageIndexReady, "Expected initial state to confirm local index readiness.");
                Assert(string.Equals(initialState.StoragePaths.ImagesDirectory, customImagesDirectory, StringComparison.OrdinalIgnoreCase), "Expected initial state to surface effective images directory.");
                Assert(string.Equals(initialState.Settings.ImagesDirectoryPath, customImagesDirectory, StringComparison.OrdinalIgnoreCase), "Expected initial state settings to keep configured images directory.");
            }
            finally
            {
                RestoreSettings(snapshot);
                TryDeleteDirectory(customImagesDirectory);
            }
        }

        private static void JsonSettingsStoreWritesSettingsFile()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_settings_store_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            var settingsPath = Path.Combine(tempDirectory, "settings.json");

            try
            {
                var store = new apod_wallpaper.JsonSettingsStore(settingsPath);
                var snapshot = new apod_wallpaper.ApplicationSettingsSnapshot
                {
                    TrayDoubleClickAction = true,
                    WallpaperStyleIndex = (int)apod_wallpaper.WallpaperStyle.Fill,
                    AutoRefreshEnabled = true,
                    StartWithWindows = false,
                    NasaApiKeyValidationState = apod_wallpaper.ApiKeyValidationState.Valid.ToString(),
                    ImagesDirectoryPath = @"C:\temp\images",
                    TranslationTargetLanguage = apod_wallpaper.TranslationTargetLanguage.Russian,
                    LastAutoRefreshRunDate = "2026-04-29",
                    LastAutoRefreshAppliedDate = "2026-04-28",
                };

                store.Save(snapshot);

                Assert(File.Exists(settingsPath), "Expected settings.json to be created.");
                var json = File.ReadAllText(settingsPath);
                Assert(json.IndexOf("TrayDoubleClickAction", StringComparison.OrdinalIgnoreCase) >= 0, "Expected non-secret settings inside settings.json.");
                Assert(json.IndexOf("ImagesDirectoryPath", StringComparison.OrdinalIgnoreCase) >= 0, "Expected images directory inside settings.json.");
                Assert(json.IndexOf("protected-test-key", StringComparison.OrdinalIgnoreCase) < 0, "Expected API key secret value not to be written into settings.json.");

                var loaded = store.Load();
                Assert(loaded.TrayDoubleClickAction, "Expected tray action to round-trip through settings.json.");
                Assert(loaded.WallpaperStyleIndex == (int)apod_wallpaper.WallpaperStyle.Fill, "Expected wallpaper style to round-trip through settings.json.");
                Assert(loaded.AutoRefreshEnabled, "Expected auto-refresh flag to round-trip through settings.json.");
                Assert(!loaded.StartWithWindows, "Expected start-with-Windows flag to round-trip through settings.json.");
                Assert(loaded.ImagesDirectoryPath == @"C:\temp\images", "Expected images directory to round-trip through settings.json.");
                Assert(loaded.TranslationTargetLanguage == apod_wallpaper.TranslationTargetLanguage.Russian, "Expected translation target language to round-trip through settings.json.");
            }
            finally
            {
                TryDeleteDirectory(tempDirectory);
            }
        }

        private static void StorageLayoutResolvesAllPathsCentrally()
        {
            var snapshot = CaptureSettings();
            var customImagesDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_storage_images_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(customImagesDirectory);

            try
            {
                apod_wallpaper.FileStorage.SetStorageModeOverride(apod_wallpaper.ApplicationStorageMode.LocalApplicationData);

                var controller = CreateController();
                var saveResult = controller.SaveSettingsAsync(new apod_wallpaper.ApplicationSettingsSnapshot
                {
                    TrayDoubleClickAction = snapshot.TrayDoubleClickAction,
                    WallpaperStyleIndex = snapshot.WallpaperStyleIndex,
                    AutoRefreshEnabled = snapshot.AutoRefreshEnabled,
                    StartWithWindows = snapshot.StartWithWindows,
                    ImagesDirectoryPath = customImagesDirectory,
                    NasaApiKey = snapshot.NasaApiKey,
                    NasaApiKeyValidationState = snapshot.NasaApiKeyValidationState,
                    LastAutoRefreshRunDate = snapshot.LastAutoRefreshRunDate,
                    LastAutoRefreshAppliedDate = snapshot.LastAutoRefreshAppliedDate,
                }).GetAwaiter().GetResult();
                Assert(saveResult.Succeeded, "Expected storage settings save to succeed.");

                var layout = GetValueOrThrow(controller.GetStoragePathsAsync().GetAwaiter().GetResult(), "Unable to read storage layout.");
                Assert(string.Equals(layout.ImagesDirectory, customImagesDirectory, StringComparison.OrdinalIgnoreCase), "Expected custom images directory in storage layout.");
                Assert(string.Equals(layout.SmartImagesDirectory, Path.Combine(customImagesDirectory, "smart"), StringComparison.OrdinalIgnoreCase), "Expected smart directory under images directory.");
                Assert(layout.CacheDirectory.IndexOf("apod_wallpaper", StringComparison.OrdinalIgnoreCase) >= 0, "Expected cache directory to be backend-defined.");
                Assert(layout.LogsDirectory.IndexOf("apod_wallpaper", StringComparison.OrdinalIgnoreCase) >= 0, "Expected logs directory to be backend-defined.");
                Assert(layout.SecretsDirectory.IndexOf("secrets", StringComparison.OrdinalIgnoreCase) >= 0, "Expected secrets directory to be backend-defined.");
                Assert(layout.SettingsFilePath.IndexOf("settings.json", StringComparison.OrdinalIgnoreCase) >= 0, "Expected settings.json path to be backend-defined.");
                Assert(layout.Mode == apod_wallpaper.ApplicationStorageMode.LocalApplicationData, "Expected local application data mode.");
                Assert(layout.UsesCustomImagesDirectory, "Expected storage layout to report custom images directory usage.");
            }
            finally
            {
                apod_wallpaper.FileStorage.SetStorageModeOverride(null);
                RestoreSettings(snapshot);
                TryDeleteDirectory(customImagesDirectory);
            }
        }

        private static void StorageSummaryCountsLocalLibraryWithoutCleanup()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "apod_wallpaper_storage_summary_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            Directory.CreateDirectory(Path.Combine(tempDirectory, "smart"));
            var snapshot = CaptureSettings();
            try
            {
                using (var bitmap = new Bitmap(4, 4))
                {
                    bitmap.Save(Path.Combine(tempDirectory, "2026-07-14.jpg"), System.Drawing.Imaging.ImageFormat.Jpeg);
                }

                File.WriteAllText(Path.Combine(tempDirectory, "smart", "2026-07-14-smart.txt"), "generated");
                apod_wallpaper.FileStorage.SetSessionImagesDirectory(tempDirectory);

                var summary = new apod_wallpaper.StorageSummaryService().GetStorageSummary();
                Assert(summary.DownloadedImageCount == 1, "Expected one valid downloaded image.");
                Assert(summary.DownloadedImageSizeBytes > 0, "Expected downloaded image size to be counted.");
                Assert(summary.SmartImages.FileCount == 1, "Expected smart variant files to be counted separately.");
                Assert(File.Exists(Path.Combine(tempDirectory, "2026-07-14.jpg")), "Storage summary must not delete original images.");
                Assert(File.Exists(Path.Combine(tempDirectory, "smart", "2026-07-14-smart.txt")), "Storage summary must not delete generated variants.");
            }
            finally
            {
                apod_wallpaper.FileStorage.SetSessionImagesDirectory(snapshot.ImagesDirectoryPath);
                TryDeleteDirectory(tempDirectory);
            }
        }

        private static void PortableStorageModeUsesPortableLayout()
        {
            apod_wallpaper.ApplicationStorageLayout.Configure(apod_wallpaper.ApplicationStorageMode.Portable);
            try
            {
                apod_wallpaper.FileStorage.SetSessionImagesDirectory(null);
                apod_wallpaper.AppRuntimeSettings.Configure(null, null, apod_wallpaper.ApiKeyValidationState.Unknown);
                var controller = CreateController();
                var layout = GetValueOrThrow(controller.GetStoragePathsAsync().GetAwaiter().GetResult(), "Unable to read portable storage layout.");
                var expectedImagesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images");
                var expectedApplicationDataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");

                Assert(layout.Mode == apod_wallpaper.ApplicationStorageMode.Portable, "Expected portable storage mode.");
                Assert(string.Equals(layout.ImagesDirectory, expectedImagesDirectory, StringComparison.OrdinalIgnoreCase), "Expected portable images directory next to executable.");
                Assert(string.Equals(layout.ApplicationDataDirectory, expectedApplicationDataDirectory, StringComparison.OrdinalIgnoreCase), "Expected portable application data directory next to executable.");
                Assert(string.Equals(layout.CacheDirectory, Path.Combine(expectedApplicationDataDirectory, "cache"), StringComparison.OrdinalIgnoreCase), "Expected portable cache directory.");
                Assert(string.Equals(layout.LogsDirectory, Path.Combine(expectedApplicationDataDirectory, "logs"), StringComparison.OrdinalIgnoreCase), "Expected portable logs directory.");
                Assert(string.Equals(layout.SecretsDirectory, Path.Combine(expectedApplicationDataDirectory, "secrets"), StringComparison.OrdinalIgnoreCase), "Expected portable secrets directory.");
                Assert(string.Equals(layout.SettingsFilePath, Path.Combine(expectedApplicationDataDirectory, "settings.json"), StringComparison.OrdinalIgnoreCase), "Expected portable settings.json path.");
            }
            finally
            {
                apod_wallpaper.ApplicationStorageLayout.ResetConfiguration();
            }
        }

        private static void StoreStorageModeUsesSandboxLayout()
        {
            var sandboxPath = Path.Combine(Path.GetTempPath(), "apod_wallpaper_store_layout_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(sandboxPath);

            try
            {
                apod_wallpaper.ApplicationStorageLayout.Configure(apod_wallpaper.ApplicationStorageMode.Store, sandboxPath);
                apod_wallpaper.FileStorage.SetSessionImagesDirectory(null);
                apod_wallpaper.AppRuntimeSettings.Configure(null, null, apod_wallpaper.ApiKeyValidationState.Unknown);

                var controller = CreateController();
                var layout = GetValueOrThrow(controller.EnsureStorageLayoutAsync().GetAwaiter().GetResult(), "Unable to prepare store storage layout.");

                Assert(layout.Mode == apod_wallpaper.ApplicationStorageMode.Store, "Expected store storage mode.");
                Assert(string.Equals(layout.ApplicationDataDirectory, sandboxPath, StringComparison.OrdinalIgnoreCase), "Expected store application data directory to use the sandbox path.");
                Assert(string.Equals(layout.ImagesDirectory, Path.Combine(sandboxPath, "images"), StringComparison.OrdinalIgnoreCase), "Expected store images directory inside sandbox path.");
                Assert(string.Equals(layout.SmartImagesDirectory, Path.Combine(sandboxPath, "images", "smart"), StringComparison.OrdinalIgnoreCase), "Expected store smart images directory inside sandbox path.");
                Assert(string.Equals(layout.CacheDirectory, Path.Combine(sandboxPath, "cache"), StringComparison.OrdinalIgnoreCase), "Expected store cache directory inside sandbox path.");
                Assert(string.Equals(layout.LogsDirectory, Path.Combine(sandboxPath, "logs"), StringComparison.OrdinalIgnoreCase), "Expected store logs directory inside sandbox path.");
                Assert(string.Equals(layout.SecretsDirectory, Path.Combine(sandboxPath, "secrets"), StringComparison.OrdinalIgnoreCase), "Expected store secrets directory inside sandbox path.");
                Assert(string.Equals(layout.SettingsFilePath, Path.Combine(sandboxPath, "settings.json"), StringComparison.OrdinalIgnoreCase), "Expected store settings.json path inside sandbox path.");

                var settingsStore = new apod_wallpaper.JsonSettingsStore();
                var storeSecret = new apod_wallpaper.DpapiUserSecretStore();
                settingsStore.Save(CreateDefaultSettingsSnapshot());
                storeSecret.SaveNasaApiKey("sandbox-key");

                Assert(File.Exists(Path.Combine(sandboxPath, "settings.json")), "Expected settings.json to be written into the sandbox path.");
                Assert(File.Exists(Path.Combine(sandboxPath, "secrets", "nasa-api-key.bin")), "Expected protected secret file to be written into the sandbox path.");
                Assert(storeSecret.GetNasaApiKey() == "sandbox-key", "Expected protected secret round-trip in store storage mode.");
            }
            finally
            {
                apod_wallpaper.ApplicationStorageLayout.ResetConfiguration();
                TryDeleteDirectory(sandboxPath);
            }
        }

        private static void PublicFacadeMethodsUseOperationResults()
        {
            var facadeType = typeof(apod_wallpaper.IApplicationBackendFacade);
            var invalidMethods = facadeType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => !UsesOperationResultContract(method.ReturnType))
                .Select(method => method.Name + ": " + method.ReturnType.FullName)
                .ToArray();

            Assert(invalidMethods.Length == 0,
                "Expected all public facade methods to use OperationResult contracts. Invalid methods: " + string.Join(", ", invalidMethods));
        }

        private static void FailedWorkflowStatusMapsToOperationError()
        {
            var failedResult = new apod_wallpaper.ApodWorkflowResult
            {
                Status = apod_wallpaper.ApodWorkflowStatus.Failed,
                Message = "Workflow-level failure should surface as an operation error.",
            };

            try
            {
                apod_wallpaper.ApplicationController.EnsureWorkflowResultSucceeded(
                    failedResult,
                    "Fallback workflow failure message.");
                throw new InvalidOperationException("Expected failed workflow status to be rejected before reaching the public facade payload.");
            }
            catch (InvalidOperationException ex)
            {
                Assert(ex.Message == failedResult.Message, "Expected workflow failure message to become the public operation error message.");
            }

            var unavailableResult = new apod_wallpaper.ApodWorkflowResult
            {
                Status = apod_wallpaper.ApodWorkflowStatus.Unavailable,
                Message = "Unavailable is a valid domain outcome.",
            };

            var mappedUnavailable = apod_wallpaper.ApplicationController.EnsureWorkflowResultSucceeded(
                unavailableResult,
                "Fallback workflow failure message.");
            Assert(object.ReferenceEquals(mappedUnavailable, unavailableResult), "Expected unavailable workflow result to remain a successful payload.");
        }

        private static void BackendFacadeDoesNotExposeDiagnosticsContract()
        {
            Assert(!typeof(apod_wallpaper.IApplicationDiagnosticsFacade).IsAssignableFrom(typeof(apod_wallpaper.IApplicationBackendFacade)),
                "Expected backend facade to stop exposing diagnostics methods directly to frontend callers.");
        }

        private static bool UsesOperationResultContract(Type returnType)
        {
            if (returnType.IsGenericType &&
                returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var taskInnerType = returnType.GetGenericArguments()[0];
                if (taskInnerType == typeof(apod_wallpaper.OperationResult))
                    return true;

                if (taskInnerType.IsGenericType &&
                    taskInnerType.GetGenericTypeDefinition() == typeof(apod_wallpaper.OperationResult<>))
                {
                    return true;
                }
            }

            return false;
        }

        private static void WallpaperAppliedSubscriptionDisposesCleanly()
        {
            var controller = CreateController();
            EventHandler<apod_wallpaper.WallpaperAppliedEventArgs> handler = delegate { };

            var subscribeResult = controller.SubscribeWallpaperAppliedAsync(handler).GetAwaiter().GetResult();
            Assert(subscribeResult.Succeeded, "Expected wallpaper subscription to succeed.");
            Assert(subscribeResult.Value != null, "Expected wallpaper subscription token.");

            var eventField = typeof(apod_wallpaper.ApplicationController).GetField(
                "WallpaperApplied",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(eventField != null, "Expected to inspect WallpaperApplied backing field.");

            var afterSubscribe = eventField.GetValue(controller) as Delegate;
            Assert(afterSubscribe != null && afterSubscribe.GetInvocationList().Length == 1,
                "Expected one wallpaper handler after subscription.");

            subscribeResult.Value.Dispose();

            var afterDispose = eventField.GetValue(controller) as Delegate;
            Assert(afterDispose == null || afterDispose.GetInvocationList().Length == 0,
                "Expected wallpaper handler to be removed after disposing subscription.");
        }

        private static apod_wallpaper.ApplicationSettingsSnapshot CaptureSettings()
        {
            return GetValueOrThrow(CreateController().GetSettingsAsync().GetAwaiter().GetResult(), "Unable to capture current application settings.");
        }

        private static void WinUiLocalizationLiteralsAreCovered()
        {
            const string AutoOnRussian = "\u0410\u0432\u0442\u043e \u0432\u043a\u043b.";
            const string AutoOffRussian = "\u0410\u0432\u0442\u043e \u0432\u044b\u043a\u043b.";

            var repoRoot = ResolveRepositoryRoot();
            var winUiDirectory = Path.Combine(repoRoot, "apod_wallpaper.WinUI");
            var appStringsPath = Path.Combine(winUiDirectory, "AppStrings.cs");
            var appStringsSource = File.ReadAllText(appStringsPath);
            var appStringKeys = ExtractAppStringKeys(appStringsSource);

            Assert(!appStringsSource.Contains("LanguageSystem"), "AppStrings must not use the removed System language.");
            Assert(!appStringsSource.Contains("CultureInfo.CurrentUICulture.TwoLetterISOLanguageName"), "AppStrings must not choose UI language from CurrentUICulture.");
            Assert(appStringKeys.Contains("CopyFailed"), "AppStrings must contain CopyFailed.");
            Assert(appStringKeys.Contains("Set as wallpaper"), "AppStrings must contain the Favorites Set as wallpaper action.");
            Assert(appStringKeys.Contains("Translation language"), "AppStrings must contain the translation language tooltip prefix.");
            foreach (var languageName in new[] { "Russian", "Spanish", "German", "French", "Italian", "Portuguese", "Japanese" })
                Assert(appStringKeys.Contains(languageName), "AppStrings must contain language name: " + languageName);

            foreach (var xamlPath in Directory.GetFiles(winUiDirectory, "*.xaml"))
                AssertXamlLiteralsHaveKeys(xamlPath, appStringKeys);

            var mainPageSource = File.ReadAllText(Path.Combine(winUiDirectory, "MainPage.xaml.cs"));
            Assert(!mainPageSource.Contains("TranslationTargetDisplay"), "Translation target display must not use internal localization keys.");
            Assert(!mainPageSource.Contains("TranslationTargetPlaceholder"), "Translation target placeholder key must not appear in MainPage UI code.");

            foreach (var path in Directory.GetFiles(winUiDirectory, "*.cs").Concat(Directory.GetFiles(winUiDirectory, "*.xaml")))
            {
                if (string.Equals(Path.GetFileName(path), "AppStrings.cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                var text = File.ReadAllText(path);
                if (text.Contains(AutoOnRussian) || text.Contains(AutoOffRussian))
                    throw new InvalidOperationException("Auto labels must only appear in AppStrings: " + path);

                if (ContainsCyrillic(text) && !IsAllowedCyrillicFile(path, text))
                    throw new InvalidOperationException("Russian UI text must live in AppStrings: " + path);

                if (text.Contains("Content=\"System\"") || text.Contains("Tag=\"system\"") || text.Contains("LanguageSystem"))
                    throw new InvalidOperationException("System language leftover found in UI file: " + path);

                if (text.Contains("CultureInfo.CurrentUICulture.TwoLetterISOLanguageName"))
                    throw new InvalidOperationException("CurrentUICulture is used as UI language source in: " + path);

                AssertNoDirectUserVisibleAssignments(path, text);
            }
        }

        private static void TranslationTargetLanguageNormalizesValues()
        {
            Assert(apod_wallpaper.TranslationTargetLanguage.Normalize(null) == apod_wallpaper.TranslationTargetLanguage.Russian, "Expected null target language to normalize to ru.");
            Assert(apod_wallpaper.TranslationTargetLanguage.Normalize(string.Empty) == apod_wallpaper.TranslationTargetLanguage.Russian, "Expected empty target language to normalize to ru.");
            Assert(apod_wallpaper.TranslationTargetLanguage.Normalize(" RU ") == apod_wallpaper.TranslationTargetLanguage.Russian, "Expected RU to normalize to ru.");
            Assert(apod_wallpaper.TranslationTargetLanguage.Normalize("es") == apod_wallpaper.TranslationTargetLanguage.Spanish, "Expected es to remain valid.");
            Assert(apod_wallpaper.TranslationTargetLanguage.Normalize("en") == apod_wallpaper.TranslationTargetLanguage.Russian, "Expected English target language to normalize to ru.");
            Assert(apod_wallpaper.TranslationTargetLanguage.Normalize("unknown") == apod_wallpaper.TranslationTargetLanguage.Russian, "Expected unknown target language to normalize to ru.");
            Assert(apod_wallpaper.TranslationTargetLanguage.GetDisplayCode("ru") == "ru", "Expected ru display code.");
            Assert(apod_wallpaper.TranslationTargetLanguage.GetDisplayCode("es") == "es", "Expected es display code.");
            Assert(apod_wallpaper.TranslationTargetLanguage.GetDisplayCode("ja") == "ja", "Expected ja display code.");
        }

        private static void GoogleTranslateUrlBuilderEncodesExplanation()
        {
            var text = "Stars & \"dust\"" + Environment.NewLine + "Unicode: Привет";
            var url = apod_wallpaper.TranslationTargetLanguage.BuildGoogleTranslateUrl(
                apod_wallpaper.TranslationTargetLanguage.Russian,
                text,
                includeText: true);

            Assert(url.StartsWith("https://translate.google.com/?sl=en&tl=ru&text=", StringComparison.Ordinal), "Expected Google Translate URL to use en source and ru target.");
            Assert(url.EndsWith("&op=translate", StringComparison.Ordinal), "Expected Google Translate URL to use translate mode.");
            Assert(url.Contains("Stars%20%26%20%22dust%22"), "Expected spaces, ampersands, and quotes to be URL encoded.");
            Assert(url.Contains("%D0%9F%D1%80%D0%B8%D0%B2%D0%B5%D1%82"), "Expected Unicode text to be URL encoded.");

            var urlWithoutText = apod_wallpaper.TranslationTargetLanguage.BuildGoogleTranslateUrl(
                apod_wallpaper.TranslationTargetLanguage.Japanese,
                text,
                includeText: false);
            Assert(urlWithoutText == "https://translate.google.com/?sl=en&tl=ja&op=translate", "Expected fallback URL without text payload.");
        }

        private static string ResolveRepositoryRoot()
        {
            foreach (var start in new[] { Environment.CurrentDirectory, AppDomain.CurrentDomain.BaseDirectory })
            {
                var directory = new DirectoryInfo(start);
                while (directory != null)
                {
                    var appStringsPath = Path.Combine(directory.FullName, "apod_wallpaper.WinUI", "AppStrings.cs");
                    if (File.Exists(appStringsPath))
                        return directory.FullName;

                    directory = directory.Parent;
                }
            }

            throw new InvalidOperationException("Unable to locate repository root for localization audit.");
        }

        private static HashSet<string> ExtractAppStringKeys(string source)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(source, "\\[\"(?<key>(?:\\\\.|[^\"\\\\])*)\"\\]\\s*=\\s*\"(?<value>(?:\\\\.|[^\"\\\\])*)\""))
            {
                keys.Add(match.Groups["key"].Value);
                keys.Add(match.Groups["value"].Value);
            }

            Assert(keys.Contains("Auto On") && keys.Contains("\u0410\u0432\u0442\u043e \u0432\u043a\u043b."), "Expected Auto On localization pair.");
            Assert(keys.Contains("Local") && keys.Contains("\u041b\u043e\u043a\u0430\u043b\u044c\u043d\u043e"), "Expected calendar legend localization pair.");
            Assert(keys.Contains("On") && keys.Contains("\u0412\u043a\u043b."), "Expected ToggleSwitch On localization pair.");
            return keys;
        }

        private static void AssertXamlLiteralsHaveKeys(string path, HashSet<string> appStringKeys)
        {
            var text = File.ReadAllText(path);
            var pattern = "(Text|Content|Header|Label|PlaceholderText|ToolTipService\\.ToolTip|AutomationProperties\\.Name|AutomationProperties\\.HelpText|OnContent|OffContent)\\s*=\\s*\"(?<value>[^\"]*)\"";
            foreach (Match match in Regex.Matches(text, pattern))
            {
                var value = match.Groups["value"].Value;
                if (ShouldIgnoreXamlLiteral(value))
                    continue;

                if (!appStringKeys.Contains(value))
                    throw new InvalidOperationException(Path.GetFileName(path) + " literal is missing from AppStrings: " + value);
            }
        }

        private static bool ShouldIgnoreXamlLiteral(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;
            if (value.StartsWith("{", StringComparison.Ordinal))
                return true;
            if (!Regex.IsMatch(value, "[A-Za-z\u0400-\u04FF]"))
                return true;

            return value == "English" || value == "\u0420\u0443\u0441\u0441\u043a\u0438\u0439";
        }

        private static bool ContainsCyrillic(string text)
        {
            return Regex.IsMatch(text, "[\u0400-\u04FF]");
        }

        private static bool IsAllowedCyrillicFile(string path, string text)
        {
            return string.Equals(Path.GetFileName(path), "SettingsPage.xaml", StringComparison.OrdinalIgnoreCase)
                && text.Contains("Content=\"\u0420\u0443\u0441\u0441\u043a\u0438\u0439\"");
        }

        private static void AssertNoDirectUserVisibleAssignments(string path, string text)
        {
            var pattern = "\\.(Text|Content|Title|Message|Header|Label)\\s*=\\s*\"(?<value>[^\"]*[A-Za-z\u0400-\u04FF][^\"]*)\"";
            foreach (Match match in Regex.Matches(text, pattern))
            {
                var value = match.Groups["value"].Value;
                if (value.StartsWith("http", StringComparison.OrdinalIgnoreCase) || value.Contains("://"))
                    continue;

                throw new InvalidOperationException(Path.GetFileName(path) + " assigns user-visible text without AppStrings: " + value);
            }
        }

        private static void RestoreSettings(apod_wallpaper.ApplicationSettingsSnapshot snapshot)
        {
            var restoreResult = CreateController().SaveSettingsAsync(snapshot).GetAwaiter().GetResult();
            Assert(restoreResult.Succeeded, "Expected settings restore to succeed.");
        }

        private static apod_wallpaper.ApplicationSettingsSnapshot ReadSettings(apod_wallpaper.ApplicationController controller)
        {
            return GetValueOrThrow(controller.GetSettingsAsync().GetAwaiter().GetResult(), "Unable to read application settings.");
        }

        private static void InvokePersistUpdateCheckResult(apod_wallpaper.ApplicationController controller, apod_wallpaper.UpdateCheckResult result, bool automatic)
        {
            var method = typeof(apod_wallpaper.ApplicationController).GetMethod(
                "PersistUpdateCheckResult",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(method != null, "Expected ApplicationController.PersistUpdateCheckResult to exist.");
            method.Invoke(controller, new object[] { result, automatic });
        }

        private static void AssertCachedUpdateAvailable(
            apod_wallpaper.ApplicationSettingsSnapshot settings,
            string currentVersion,
            string latestVersion,
            string releaseUrl,
            string message)
        {
            Assert(settings.LastKnownLatestVersion == latestVersion, message + " LastKnownLatestVersion mismatch.");
            Assert(settings.LastKnownLatestReleaseUrl == releaseUrl, message + " LastKnownLatestReleaseUrl mismatch.");
            var cachedStatus = apod_wallpaper.UpdateReminderPolicy.GetCachedUpdateAvailability(settings, currentVersion);
            Assert(cachedStatus.Kind == apod_wallpaper.CachedUpdateAvailabilityKind.UpdateAvailable, message);
            Assert(cachedStatus.LatestVersion == latestVersion, message + " Cached latest version mismatch.");
        }

        private static apod_wallpaper.ApplicationController CreateController()
        {
            return new apod_wallpaper.ApplicationController(
                _settingsStore,
                _secretStore,
                new FakeStartupRegistrationService());
        }

        private static apod_wallpaper.ApplicationSettingsSnapshot CreateDefaultSettingsSnapshot()
        {
            return new apod_wallpaper.ApplicationSettingsSnapshot
            {
                TrayDoubleClickAction = false,
                WallpaperStyleIndex = (int)apod_wallpaper.WallpaperStyle.Smart,
                AutoRefreshEnabled = false,
                StartWithWindows = true,
                NasaApiKey = "DEMO_KEY",
                NasaApiKeyValidationState = apod_wallpaper.ApiKeyValidationState.Unknown.ToString(),
                ImagesDirectoryPath = string.Empty,
                TranslationTargetLanguage = apod_wallpaper.TranslationTargetLanguage.Russian,
                LastAutoRefreshRunDate = string.Empty,
                LastAutoRefreshAppliedDate = string.Empty,
            };
        }

        private static T GetValueOrThrow<T>(apod_wallpaper.OperationResult<T> result, string fallbackMessage)
        {
            if (result == null)
                throw new InvalidOperationException(fallbackMessage);

            if (!result.Succeeded)
                throw new InvalidOperationException(result.Error != null ? result.Error.Message : fallbackMessage);

            return result.Value;
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch
            {
            }
        }

        private static void ResetSecretStore()
        {
            TryDeleteDirectory(_secretStoreDirectory);
            Directory.CreateDirectory(_secretStoreDirectory);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static string GetRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "apod_wallpaper.sln")))
                    return directory.FullName;

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Unable to locate repository root from " + AppContext.BaseDirectory);
        }

        private static apod_wallpaper.ApodEntry CreateVideoEntry(DateTime date)
        {
            return new apod_wallpaper.ApodEntry
            {
                Date = date.ToString("yyyy-MM-dd"),
                MediaType = "video",
                Url = "https://example.test/video.mp4",
                HdUrl = null,
                ResolvedFromSource = "api",
            };
        }

        private static apod_wallpaper.ApodEntry CreateImageEntry(DateTime date)
        {
            return new apod_wallpaper.ApodEntry
            {
                Date = date.ToString("yyyy-MM-dd"),
                MediaType = "image",
                Url = "https://example.test/" + date.ToString("yyyy-MM-dd") + "_preview.jpg",
                HdUrl = "https://example.test/" + date.ToString("yyyy-MM-dd") + "_full.jpg",
                ResolvedFromSource = "api",
            };
        }

        private sealed class FakeApodClient : apod_wallpaper.IApodClient
        {
            private readonly apod_wallpaper.ApodEntry _latestEntry;
            private readonly System.Collections.Generic.Dictionary<DateTime, apod_wallpaper.ApodEntry> _entries;

            public FakeApodClient(apod_wallpaper.ApodEntry latestEntry, System.Collections.Generic.Dictionary<DateTime, apod_wallpaper.ApodEntry> entries)
            {
                _latestEntry = latestEntry;
                _entries = entries;
            }

            public apod_wallpaper.ApodEntry GetEntry(DateTime date) => _entries[date.Date];
            public Task<apod_wallpaper.ApodEntry> GetEntryAsync(DateTime date) => Task.FromResult(GetEntry(date));
            public apod_wallpaper.ApodEntry GetLatestEntry() => _latestEntry;
            public Task<apod_wallpaper.ApodEntry> GetLatestEntryAsync() => Task.FromResult(_latestEntry);
            public System.Collections.Generic.IReadOnlyList<apod_wallpaper.ApodEntry> GetEntries(DateTime startDate, DateTime endDate) => new[] { _latestEntry };
            public Task<System.Collections.Generic.IReadOnlyList<apod_wallpaper.ApodEntry>> GetEntriesAsync(DateTime startDate, DateTime endDate) => Task.FromResult(GetEntries(startDate, endDate));
            public Task<apod_wallpaper.ApiKeyValidationState> ValidateApiKeyAsync(string apiKey) => Task.FromResult(apod_wallpaper.ApiKeyValidationState.Valid);
        }

        private sealed class InMemorySettingsStore : apod_wallpaper.IApplicationSettingsStore
        {
            private apod_wallpaper.ApplicationSettingsSnapshot _snapshot;

            public InMemorySettingsStore(apod_wallpaper.ApplicationSettingsSnapshot initialSnapshot)
            {
                _snapshot = (initialSnapshot ?? new apod_wallpaper.ApplicationSettingsSnapshot()).Clone();
            }

            public bool Exists()
            {
                return _snapshot != null;
            }

            public apod_wallpaper.ApplicationSettingsSnapshot Load()
            {
                return (_snapshot ?? new apod_wallpaper.ApplicationSettingsSnapshot()).Clone();
            }

            public void Save(apod_wallpaper.ApplicationSettingsSnapshot settings)
            {
                _snapshot = (settings ?? new apod_wallpaper.ApplicationSettingsSnapshot()).Clone();
            }
        }

        private sealed class FakeStartupRegistrationService : apod_wallpaper.IStartupRegistrationService
        {
            public void SetStartWithWindows(bool enabled)
            {
            }
        }

        private sealed class InMemoryApodMetadataCache : apod_wallpaper.IApodMetadataCache
        {
            private readonly System.Collections.Generic.Dictionary<DateTime, apod_wallpaper.ApodCachedEntry> _entries = new System.Collections.Generic.Dictionary<DateTime, apod_wallpaper.ApodCachedEntry>();

            public apod_wallpaper.ApodCachedEntry Get(DateTime date)
            {
                apod_wallpaper.ApodCachedEntry entry;
                _entries.TryGetValue(date.Date, out entry);
                return entry;
            }

            public System.Collections.Generic.IReadOnlyList<apod_wallpaper.ApodCachedEntry> GetRange(DateTime startDate, DateTime endDate)
            {
                return _entries.Values.ToArray();
            }

            public void Upsert(apod_wallpaper.ApodEntry entry)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Date))
                    return;

                _entries[DateTime.Parse(entry.Date).Date] = apod_wallpaper.ApodCachedEntry.FromEntry(entry);
            }

            public void UpsertRange(System.Collections.Generic.IEnumerable<apod_wallpaper.ApodEntry> entries)
            {
                foreach (var entry in entries)
                    Upsert(entry);
            }

            public void SaveLocalImagePath(DateTime date, string localImagePath)
            {
                apod_wallpaper.ApodCachedEntry entry;
                if (_entries.TryGetValue(date.Date, out entry))
                    entry.LocalImagePath = localImagePath;
            }

            public void SyncLocalImagePaths()
            {
            }
        }

        private sealed class FakeWallpaperApplier : apod_wallpaper.IWallpaperApplier
        {
            public string LastAppliedImagePath { get; private set; }
            public apod_wallpaper.WallpaperStyle LastAppliedStyle { get; private set; }

            public void ApplyPreservingHistory(string imagePath, apod_wallpaper.WallpaperStyle style)
            {
                LastAppliedImagePath = imagePath;
                LastAppliedStyle = style;
            }

            public string ReapplyCurrentWallpaperStyle(apod_wallpaper.WallpaperStyle style)
            {
                LastAppliedStyle = style;
                return LastAppliedImagePath;
            }

            public string ResolveCurrentWallpaperSourcePath()
            {
                return LastAppliedImagePath;
            }
        }
    }
}
