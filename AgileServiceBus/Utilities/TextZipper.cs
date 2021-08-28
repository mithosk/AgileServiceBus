using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

namespace AgileServiceBus.Utilities
{
    public class TextZipper
    {
        public async Task<byte[]> CompressAsync(string text)
        {
            byte[] textBytes = Encoding.UTF8.GetBytes(text);

            using (MemoryStream zippedStream = new MemoryStream())
            using (MemoryStream textStream = new MemoryStream(textBytes))
            using (GZipStream zipperStream = new GZipStream(zippedStream, CompressionMode.Compress))
            {
                await textStream.CopyToAsync(zipperStream);

                return zippedStream.ToArray();
            }
        }

        public async Task<string> DecompressAsync(byte[] zipped)
        {
            using (MemoryStream textStream = new MemoryStream())
            using (MemoryStream zippedStream = new MemoryStream(zipped))
            using (GZipStream zipperStream = new GZipStream(zippedStream, CompressionMode.Decompress))
            {
                await zipperStream.CopyToAsync(textStream);

                return Encoding.UTF8.GetString(textStream.ToArray());
            }
        }
    }
}