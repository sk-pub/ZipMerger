using Microsoft.AspNetCore.Mvc;
using SuperLinq.Async;
using System.IO.Compression;
using TeilOne.FastZip;

namespace ZipMerger
{
    [Route("api/[controller]")]
    [ApiController]
    public class MergeController : ControllerBase
    {
        // GET: api/<MergeController>
        [HttpGet]
        public FileResult Get([FromServices] HttpClient httpClient)
        {
            var file1 = (Name: "50MB.bin", Url: "https://sabnzbd.org/tests/internetspeed/50MB.bin", Compress: false, Unzip: false);
            var file2 = (Name: "100MB.bin", Url: "https://speed.hetzner.de/100MB.bin", Compress: false, Unzip: false);
            var file3 = (Name: "1GB.bin", Url: "https://speed.hetzner.de/1GB.bin", Compress: true, Unzip: false);

            var zipFile1 = (Name: "should-unzip", Url: "https://teil-one.s3.eu-central-1.amazonaws.com/zip-mixed.zip", Compress: true, Unzip: true);

            var files = new[] { file1, file2, file3, zipFile1 };

            // TODO: Stop downloading when the client gets disconnected
            return new FileCallbackResult("application/zip", async (outputStream, _) =>
            {
                using var zipArchive = new ZipArchive(outputStream, ZipArchiveMode.Create);

                var downloads = files.Select(file => DownloadFiles(httpClient, file)).ConcurrentMerge();

                await foreach (var file in downloads)
                {
                    await using (var objectToDispose = file.ObjectToDispose)
                    {
                        var zipEntry = zipArchive.CreateEntry(
                            file.Name,
                            file.Compress ? CompressionLevel.Fastest : CompressionLevel.NoCompression
                        );

                        using (var sourceStream = file.Stream)
                        using (var zipStream = zipEntry.Open())
                        {
                            await sourceStream.CopyToAsync(zipStream);
                        }
                    }

                    //await outputStream.FlushAsync();
                }

                GC.Collect();
            })
            {
                FileDownloadName = "merged.zip"
            };
        }

        private static async IAsyncEnumerable<(string Name, bool Compress, Stream Stream, IAsyncDisposable ObjectToDispose)> DownloadFiles(HttpClient httpClient, (string Name, string Url, bool Compress, bool Unzip) file)
        {
            var url = new Uri(file.Url);

            var fileStream = url.IsFile ? new FileStream(file.Url, FileMode.Open, FileAccess.Read) : await httpClient.GetStreamAsync(url);

            if (file.Unzip)
            {
                await using var zipStreamReader = new ZipStreamReader(fileStream);

                await foreach (var entry in zipStreamReader.GetEntriesAsync())
                {
                    yield return (Name: entry.FullName, file.Compress, Stream: entry.Stream, entry);
                }
            }
            else
            {
                yield return (file.Name, file.Compress, Stream: fileStream, fileStream);
            }
        }
    }
}
