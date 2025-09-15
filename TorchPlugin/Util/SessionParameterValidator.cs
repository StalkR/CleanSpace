using Shared.Plugin;
using Shared.Struct;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using static SessionParameterFactory;
using static SteamKit2.Internal.CMsgClientPersonaState.Friend;

namespace CleanSpaceTorch.Util
{
    public static class NGramComparer
    {
        public static double CompareNGrams(IEnumerable<string[]> ngrams1, IEnumerable<string[]> ngrams2)
        {
            if (ngrams1 == null || ngrams2 == null)
                return 0.0;

            var set1 = new HashSet<string>(ngrams1.Select(ng => string.Join(" ", ng)));
            var set2 = new HashSet<string>(ngrams2.Select(ng => string.Join(" ", ng)));

            if (set1.Count == 0 && set2.Count == 0)
                return 1.0;

            int intersection = set1.Intersect(set2).Count();
            int union = set1.Union(set2).Count();

            return union > 0 ? (double)intersection / union : 0.0;
        }
    }

    public class SessionParameterValidator
    {        

        public static double ByteListComparison(byte[] list1, byte[] list2)
        {
            if (list1.IsNullOrEmpty() && list2.IsNullOrEmpty())
                return 100.0;

            int commonLength = Math.Max(list1.Length, list2.Length);
            int matchingBytes = 0;
            for (int i = 0; i < commonLength; i++)
            {
                byte byte1 = (i < list1.Length) ? list1[i] : (byte)0; 
                byte byte2 = (i < list2.Length) ? list2[i] : (byte)0;

                if (byte1 == byte2)
                {
                    matchingBytes++;
                }
            }
            return (double)matchingBytes / commonLength * 100.0;
        }
        public static Dictionary<RequestType, ChunkValidationProvider> Providers => SessionParameterFactory.providers;

        public static int ValidateResponse(SessionParameters challenge, byte[] response, params object[] args)
        {
            for (int i = 0; i < args.Length; i++)
                Common.Logger.Debug($"Args[{i}] = {args[i]?.GetType().FullName ?? "null"}");

            var MyResponse = SessionParameterFactory.AnswerChallenge(challenge, args);
            Common.Logger.Debug($"Local AnswerChallenge produced {MyResponse?.Length ?? 0} bytes");
            Common.Logger.Debug($"Remote response length {response?.Length ?? 0} bytes");

            var mySlices = UnpackChallengeResponse(MyResponse);
            var theirSlices = UnpackChallengeResponse(response);

            // Normalize slices
            double totalScore = 0.0;
            int sliceCount = 0;

            foreach (var kv in mySlices.Keys.ToList())
            {
                byte[] localBytes = mySlices[kv];
                byte[] remoteBytes = theirSlices.ContainsKey(kv) ? theirSlices[kv] : Array.Empty<byte>();

                byte[] normalizedLocal;
                byte[] normalizedRemote;
                double sliceScore = 0;
            /*    if (kv == RequestType.JitAttestation)
                {
                    // Deserialize and normalize JIT WindowBytes
                    var localResp = ProtoUtil.Deserialize<JittestResponse>(localBytes);
                    var remoteResp = ProtoUtil.Deserialize<JittestResponse>(remoteBytes);

                    var (localNgrams, localPushes) = JitSemanticNormalizer.Normalize(localResp.WindowBytes);
                    var (remoteNgrams, remotePushes) = JitSemanticNormalizer.Normalize(remoteResp.WindowBytes);
                    
                    // Compare n-grams
                    sliceScore = NGramComparer.CompareNGrams(localNgrams, remoteNgrams);
                    Common.Logger.Debug($"Slice {kv}: {sliceScore:F2}% similarity");
                        
                    // aww man this didn't make it in

                }
                else*/ if (kv == RequestType.MethodIL /*|| kv == RequestType.TypeIL*/)
                {
                    normalizedLocal = Encoding.UTF8.GetBytes(IlSemanticNormalizer.NormalizeIL(localBytes, null));
                    mySlices[kv] = normalizedLocal;

                    normalizedRemote = Encoding.UTF8.GetBytes(IlSemanticNormalizer.NormalizeIL(remoteBytes, null));
                    theirSlices[kv] = normalizedRemote;

                    sliceScore = ByteListComparison(normalizedLocal, normalizedRemote);
                    Common.Logger.Debug($"Slice {kv}: {sliceScore:F2}% similarity");
                }
                else
                {                  
                    normalizedLocal = localBytes;
                    normalizedRemote = remoteBytes;

                    sliceScore = ByteListComparison(normalizedLocal, normalizedRemote);
                    Common.Logger.Debug($"Slice {kv}: {sliceScore:F2}% similarity");
                }

               
                totalScore += sliceScore;
                sliceCount++;
            }

            double finalScore = sliceCount > 0 ? totalScore / sliceCount : 0.0;
            Common.Logger.Debug($"Average slice similarity: {finalScore:F2}%");

            return finalScore > 90.0 ? 1 : 0;
        }




        public static Dictionary<RequestType, byte[]> UnpackChallengeResponse(byte[] data)
        {
            var result = new Dictionary<RequestType, byte[]>();
            
            if (data == null)
            {
                Common.Logger.Debug("UnpackChallengeResponse called with null data");
                return result;
            }

            using (var ms = new MemoryStream(data))
            using (var br = new BinaryReader(ms))
            {
                while (ms.Position < ms.Length)
                {
                    RequestType rt = (RequestType)br.ReadByte();
                    int length = br.ReadInt32();
                    byte[] slice = br.ReadBytes(length);
                    result[rt] = slice;
                    Common.Logger.Debug($"Unpacked slice {rt} ({length} bytes)");
                }
            }

            return result;
        }

        public static byte[] RepackChallengeResponse(Dictionary<RequestType, byte[]> slices)
        {
            using (var ms = new MemoryStream())
            {
                foreach (var kv in slices)
                {
                    ms.WriteByte((byte)kv.Key);
                    var lenBytes = BitConverter.GetBytes(kv.Value.Length);
                    ms.Write(lenBytes, 0, lenBytes.Length);
                    ms.Write(kv.Value, 0, kv.Value.Length);
                }
                return ms.ToArray();
            }
        }


    }
}


