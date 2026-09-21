using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Crash.Helper.Updates
{
    internal sealed class ReleaseUpdater
    {
        internal const string LatestUrl = "https://api.github.com/repos/oimobatake/Crash.Helper/releases/latest";
        private readonly HttpClient client;
        internal ReleaseUpdater(HttpClient client) { this.client = client; }

        internal static HttpClient CreateClient()
        {
            var result = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            result.DefaultRequestHeaders.UserAgent.ParseAdd("CrashHelper/" + typeof(Program).Assembly.GetName().Version);
            result.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            result.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            return result;
        }

        internal async Task<UpdateRelease> CheckAsync(string executable, CancellationToken cancellation)
        {
            UpdateRelease release;
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                using (var response = await client.GetAsync(LatestUrl, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                {
                    if (response.StatusCode == HttpStatusCode.NotFound) return null;
                    response.EnsureSuccessStatusCode();
                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var json = new MemoryStream())
                    {
                        await CopyLimitedAsync(stream, json, 2 * 1024 * 1024, timeout.Token).ConfigureAwait(false);
                        json.Position = 0;
                        release = (UpdateRelease)new DataContractJsonSerializer(typeof(UpdateRelease)).ReadObject(json);
                    }
                }
            }
            if (release == null) return null;
            var asset = release.GetNewerAsset(AssemblyName.GetAssemblyName(executable).Version);
            if (asset == null) return null;
            return release;
        }

        internal async Task<UpdatePackage> DownloadAsync(UpdateRelease release, string executable, string stagingRoot, CancellationToken cancellation)
        {
            var asset = release.GetNewerAsset(AssemblyName.GetAssemblyName(executable).Version);
            if (asset == null) throw new InvalidOperationException("The selected update is no longer applicable.");
            var package = new UpdatePackage
            {
                TargetPath = Path.GetFullPath(executable), Tag = release.Tag, Size = asset.Size,
                Sha256 = asset.Digest.Substring(7), PreviousSha256 = UpdatePackage.Hash(executable),
                DirectoryPath = Path.Combine(stagingRoot, Guid.NewGuid().ToString("N"))
            };
            Directory.CreateDirectory(package.DirectoryPath);
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                timeout.CancelAfter(TimeSpan.FromMinutes(3));
                try
                {
                    using (var response = await client.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var target = new FileStream(package.PayloadPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                            await CopyLimitedAsync(source, target, asset.Size, timeout.Token).ConfigureAwait(false);
                    }
                    package.Validate(package.PayloadPath);
                    cancellation.ThrowIfCancellationRequested();
                    return package;
                }
                catch
                {
                    try { File.Delete(package.PayloadPath); Directory.Delete(package.DirectoryPath); } catch { }
                    throw;
                }
            }
        }

        private static async Task CopyLimitedAsync(Stream source, Stream destination, long maximum, CancellationToken cancellation)
        {
            var buffer = new byte[81920]; long total = 0; int count;
            while ((count = await source.ReadAsync(buffer, 0, buffer.Length, cancellation).ConfigureAwait(false)) != 0)
            {
                total += count;
                if (total > maximum) throw new InvalidDataException("The update response exceeds its expected size.");
                await destination.WriteAsync(buffer, 0, count, cancellation).ConfigureAwait(false);
            }
        }
    }
}
