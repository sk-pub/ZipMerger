using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;

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
            var file1 = (Name: "50MB.bin", Url: "https://sabnzbd.org/tests/internetspeed/50MB.bin", Compress: false);
            var file2 = (Name: "100MB.bin", Url: "https://speed.hetzner.de/100MB.bin", Compress: false);
            var file3 = (Name: "1GB.bin", Url: "https://speed.hetzner.de/1GB.bin", Compress: true);

            var files = new [] { file1, file2, file3 };

            // TODO: Stop downloading when the client gets disconnected
            return new FileCallbackResult("application/zip", async (outputStream, _) =>
            {
                using (var zipArchive = new ZipArchive(outputStream, ZipArchiveMode.Create))
                {
                    var downloads = files.Select(async (file) =>
                        (file.Name, file.Compress, Stream: await httpClient.GetStreamAsync(file.Url))
                    ).ToList();

                    while (downloads.Any())
                    {
                        var finishedDownload = await Task.WhenAny(downloads);
                        downloads.Remove(finishedDownload);

                        var file = await finishedDownload;

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
                }
            })
            {
                FileDownloadName = "merged.zip"
            };
        }
    }
}
