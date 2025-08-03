using System.IO;
namespace CleanSpaceShared.Util
{
    public static class CompressionUtil
    {
        public static byte[] Compress(byte[] data)
        {
            var ms = new MemoryStream();
            using (var gzip = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Compress))
            {
                gzip.Write(data, 0, data.Length);
            }
            byte[] res = ms.ToArray();
            ms.Close();
            return res;
        }

        public static byte[] Decompress(byte[] compressedData)
        {
            var input = new MemoryStream(compressedData);
            var gzip = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress);
            var output = new MemoryStream();
            gzip.CopyTo(output);
            gzip.Close();
            input.Close();
            byte[] res = output.ToArray();
            return res;
        }
    }

}
