using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shared.Events;
using Shared.Hasher;
using Shared.Logging;
using Shared.Struct;
using Shared.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CleanSpace
{
    public struct PendingNonce
    {
        public ulong sender;
        public string nonce;
        public DateTime time;
        public override bool Equals(object obj)
        {
            if (!(obj is PendingNonce))
                return false;

            PendingNonce mys = (PendingNonce)obj;
            return (nonce == mys.nonce);
        }

        public override int GetHashCode() => nonce.GetHashCode();
    }

    public static class ValidationManager
    {
        public static IPluginLogger Log;
              
        
        private readonly static ConcurrentDictionary<ulong, PendingNonce> expectedNonces = new ConcurrentDictionary<ulong, PendingNonce>();

        public static bool NonceExistsForPlayer(ulong steamId)
        {  return expectedNonces.ContainsKey(steamId);  }

        public static string GetNonceForPlayer(ulong steamId)
        {  return NonceExistsForPlayer(steamId) ? expectedNonces[steamId].nonce : null;  }
           
        public static bool ValidNonceExistsForPlayer(ulong steamId)
        {  return NonceExistsForPlayer(steamId) && !TokenUtility.IsTokenValid(GetNonceForPlayer(steamId), "auth");  }

        public static bool NonceValidForPlayer(ulong steamId, string nonce)
        {  return ValidNonceExistsForPlayer(steamId) && nonce.Equals(GetNonceForPlayer(steamId)); }

        public static string RegisterNonceForPlayer(ulong steamId, string nonce, bool force = false)
        {
            if(!force && NonceExistsForPlayer(steamId)) 
                return null;
            
            expectedNonces[steamId] = new PendingNonce { nonce = nonce, sender = steamId, time = DateTime.UtcNow };
            EventHub.OnNonceRegistered(typeof(ValidationManager), steamId, expectedNonces[steamId]);
            return nonce;
        }

        private static string Secret => Shared.Plugin.Common.InstanceSecret;

        // Every thousand or so nonces we cleanup. Its not a big deal, they aren't very big and aren't going to pile up too much. Maybe.
        private static int pruneIntervalCounter = 0;
        private static int pruneInterval = 1000;
        public static string RegisterNonceForPlayer(ulong steamId, bool force = false)
        {           
            pruneIntervalCounter++;
            if (pruneIntervalCounter % pruneInterval == 0)
                PruneStaleEntries();
            var token = TokenUtility.GenerateToken(Secret, DateTime.UtcNow.AddSeconds(Shared.Plugin.Common.Config.TokenValidTimeSeconds));
            return RegisterNonceForPlayer(steamId, token, force);
        }

        public static ValidationResultCode ValidateToken(ulong steamId, string receivedNonce, bool removeOnValidate = true)
        {
            if (receivedNonce == null) return ValidationResultCode.MALFORMED_TOKEN;
            if (!NonceExistsForPlayer(steamId)) return ValidationResultCode.UNEXPECTED_TOKEN;
            if (!TokenUtility.IsTokenValid(receivedNonce, Secret)) return ValidationResultCode.EXPIRED_TOKEN;     
            if (NonceValidForPlayer(steamId, receivedNonce))
            {
                if (removeOnValidate)
                    RemoveNonceFromId(steamId);
                  
                return ValidationResultCode.VALID_TOKEN;
            }
            else return ValidationResultCode.INVALID_TOKEN;             
        }

        private static bool RemoveNonceFromId(ulong steamId, bool pruned = false)
        {
            PendingNonce r;
            if (expectedNonces.TryRemove(steamId, out r)) { 
                if (!pruned)
                    EventHub.OnNonceRemoved(typeof(ValidationManager), steamId, r);
                return true;
            }
            return false;
        }

        public static void LogValidationResult(ulong steamId, ValidationResultData data)
        {
            string successString = (data.Success ? "SUCCESS" : "FAILURE");
            string pluginListString = (data.PluginList.Count() > 0 ? $" Hashes: {data.PluginList.ToString()}" : "No Conflicts");
            Log.Info($"{Shared.Plugin.Common.PluginName}: Validation for {steamId} result {successString} with {data.Code.ToString()} {pluginListString}");
        }

        public static void PruneStaleEntries()
        {
            var gen = expectedNonces.GetEnumerator();
            while (gen.MoveNext())
            {
                ulong k = gen.Current.Key;
                var d = gen.Current.Value.time;
                if (DateTime.UtcNow.CompareTo(d.AddMinutes(1)) > 0)
                    RemoveNonceFromId(k, true);
            }
        }

        public static List<string> GetCleanSpaceHashList()
        {
            return (Shared.Plugin.Common.Plugin.Config.AnalyzedPlugins)
                .Where( (a)=>a.AssemblyName.Contains("CleanSpace") )
                .Select<PluginListEntry, string>((b)=>b.Hash).ToList();
        }
        public static List<PluginListEntry> GetCleanSpaceAssemblyList()
        {
            return (Shared.Plugin.Common.Plugin.Config.AnalyzedPlugins)
                .Where((a) => a.AssemblyName.Contains("CleanSpace")).ToList();
        }
        public static ValidationResultData Validate(ulong steamId, string receivedNonce, string securedSignature, byte[] signatureTransformer, List<String> hashList)
        {

            var tokenValidationState = ValidateToken(steamId, receivedNonce);
            if(tokenValidationState!= ValidationResultCode.VALID_TOKEN)
            {
                return new ValidationResultData()
                {
                    Code = tokenValidationState,
                    PluginList = null,
                    Success = false
                };
            }

            List<string> transformedCleanSpaceSignatures = GetCleanSpaceAssemblyList().Select(
              (element) => HasherRunner.ExecuteRunner(signatureTransformer, Assembly.LoadFile(element.Location))
              ).ToList();

          
            var currentPluginList = Shared.Plugin.Common.Plugin.Config.AnalyzedPlugins;
            var selectedPluginHashes = currentPluginList.Where(e => e.IsSelected).Select(e => e.Hash).ToList();


            var listType = Shared.Plugin.Common.Plugin.Config.PluginListType;

            var conflictingHashes = listType == PluginListType.Blacklist  
                // If it's a blacklist, then we are interested in which plugins the client has that are PRESENT in the list.
                ? selectedPluginHashes.Intersect(hashList, StringComparer.OrdinalIgnoreCase).ToList()
                // If it's a whitelist, then we are interested in the plugins that are NOT PRESENT in the list.
                : hashList.Except(selectedPluginHashes, StringComparer.OrdinalIgnoreCase).ToList();

            var action = Shared.Plugin.Common.Plugin.Config.ListMatchAction;

            if (!transformedCleanSpaceSignatures.Contains(securedSignature))
            {
                if (action == ListMatchAction.Deny)
                    return new ValidationResultData() { Code = ValidationResultCode.REJECTED_CLEANSPACE_HASH, PluginList = null, Success = false };
            }

            ValidationResultData result;
            if (conflictingHashes.Count > 0)
            {
                if (action == ListMatchAction.Accept)
                    result = new ValidationResultData() { Code = ValidationResultCode.ALLOWED, PluginList = conflictingHashes, Success = true };
                else
                    result = new ValidationResultData() { Code = ValidationResultCode.REJECTED_MATCH, PluginList = conflictingHashes, Success = false };
            }
            else
            {
                result = new ValidationResultData() { Code = ValidationResultCode.ALLOWED, PluginList = conflictingHashes, Success = true };
            }

            LogValidationResult(steamId, result);
            return result;
        }            
    }
}
