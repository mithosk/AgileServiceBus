using AgileServiceBus.Utilities;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace AgileServiceBus.Test.Unit
{
    public class TextZipperTest
    {
        [Fact]
        public async Task TransformationCoherence()
        {
            string text = "This is my text";

            TextZipper textZipper = new TextZipper();
            byte[] compressed = await textZipper.CompressAsync(text);
            string decompressed = await textZipper.DecompressAsync(compressed);

            Assert.Equal(text, decompressed);
        }

        [Fact]
        public async Task DataReduction()
        {
            string text = "This is my text";

            TextZipper textZipper = new TextZipper();
            byte[] compressed = await textZipper.CompressAsync(text);
            byte[] textBytes = Encoding.UTF8.GetBytes(text);

            Assert.True(compressed.Length < textBytes.Length);
        }
    }
}