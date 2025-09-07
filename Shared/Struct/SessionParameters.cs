using System;
using System.IO;

[Serializable]
public struct SessionParameters
{
    public byte randomRequestType1;
    public byte randomRequestType2;
    public byte chatterLength;
    public byte[] sessionSalt;
    public byte randomRequestType3;
    public byte randomRequestType4;

    public byte[] ToBytes()
    {
        if (sessionSalt == null || sessionSalt.Length != 16)
            throw new InvalidOperationException("sessionSalt must be exactly 16 bytes.");

        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(randomRequestType1);
            bw.Write(randomRequestType2);
            bw.Write(chatterLength);
            bw.Write(sessionSalt);
            bw.Write(randomRequestType3);
            bw.Write(randomRequestType4);

            return ms.ToArray();
        }
    }

    public static SessionParameters FromBytes(byte[] data)
    {
        if (data == null || data.Length != 21)
            throw new ArgumentException("Data must be exactly 19 bytes.", nameof(data));

        var result = new SessionParameters();
        using (var ms = new MemoryStream(data))
        using (var br = new BinaryReader(ms))
        {
            result.randomRequestType1 = br.ReadByte();
            result.randomRequestType2 = br.ReadByte();
            result.chatterLength = br.ReadByte();
            result.sessionSalt = br.ReadBytes(16);
            result.randomRequestType3 = br.ReadByte();
            result.randomRequestType4 = br.ReadByte();
        }

        return result;
    }

    public static implicit operator byte[](SessionParameters p) => p.ToBytes();
    public static implicit operator SessionParameters(byte[] data) => FromBytes(data);
    
}
