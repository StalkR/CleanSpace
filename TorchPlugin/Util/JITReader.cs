using System;
using System.Collections.Generic;
using Iced.Intel;
using Decoder = Iced.Intel.Decoder;



namespace TorchPlugin.Util
{
  
    public static class JitSemanticNormalizer
    {

        private static readonly HashSet<string> PushInstructions = new HashSet<string>() { "push" };
        private static readonly HashSet<string> PopInstructions = new HashSet<string>() { "pop" };

        public static string[] ExtractMnemonicSequence(byte[] jitBytes)
        {
            if (jitBytes == null || jitBytes.Length == 0)
                return Array.Empty<string>();

            var mnemonics = new List<string>();
            var reader = new ByteArrayCodeReader(jitBytes);
            var decoder = Decoder.Create(64, reader);
            decoder.IP = 0; 

            try
            {
                foreach(var instruction in decoder)
                {
                    mnemonics.Add(instruction.Mnemonic.ToString().ToLowerInvariant());
                }
            }
            catch
            {
               
            }

            return mnemonics.ToArray();
        }
        public static IEnumerable<string[]> BuildNGrams(string[] mnemonics, int n = 2)
        {
            if (mnemonics == null || mnemonics.Length < n)
                yield break;

            for (int i = 0; i <= mnemonics.Length - n; i++)
            {
                var ngram = new string[n];
                Array.Copy(mnemonics, i, ngram, 0, n);
                yield return ngram;
            }
        }

        public static int CountPushedArguments(IEnumerable<string> instructions)
        {
            int count = 0;
            foreach (var instr in instructions)
            {
                string mnemonic = instr.Split(' ')[0].ToLowerInvariant();
                if (PushInstructions.Contains(mnemonic))
                    count++;
            }
            return count;
        }

        // Main entry point for normalization
        public static (IEnumerable<string[]> ngrams, int pushedArgs) Normalize(byte[] instructions, int nGramSize = 3)
        {
            var seq = ExtractMnemonicSequence(instructions);
            var ngrams = BuildNGrams(seq, nGramSize);
            var pushed = CountPushedArguments(seq);
            return (ngrams, pushed);
        }

    }

}
