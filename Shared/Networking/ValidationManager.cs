using CleanSpaceShared;
using CleanSpaceShared.Networking;
using HarmonyLib;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Multiplayer;
using Shared.Logging;
using Shared.Struct;
using Shared.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Network;
using VRage.Utils;

namespace CleanSpace
{
    public static class ValidationManager
    {
        public static IPluginLogger Log;
        struct ExptectedNonce
        {
            public ulong sender;
            public string nonce;
            public DateTime time;
            public override bool Equals(object obj)
            {
                if (!(obj is ExptectedNonce))
                    return false;

                ExptectedNonce mys = (ExptectedNonce)obj;
                return (nonce == mys.nonce);
            }

            public override int GetHashCode() => nonce.GetHashCode();
        }       
        
        private readonly static ConcurrentDictionary<ulong, ExptectedNonce> expectedNonces = new ConcurrentDictionary<ulong, ExptectedNonce>();

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
            
            expectedNonces[steamId] = new ExptectedNonce
            {
                nonce = nonce,             
                sender = steamId,
                time = DateTime.UtcNow
            };
            Shared.Plugin.Common.Logger.Debug($"Registered nonce {nonce} for ID {steamId}");
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
            var token = Shared.Util.TokenUtility.GenerateToken(Secret, DateTime.UtcNow.AddTicks((long)Shared.Plugin.Common.Config.TokenValidTimeTicks));
            return RegisterNonceForPlayer(steamId, token, force);
        }

        public static ValidationResultCode ValidateToken(ulong steamId, string receivedNonce, bool removeOnValidate = true)
        {
            if (receivedNonce == null) return ValidationResultCode.MALFORMED_TOKEN;
            if (!NonceExistsForPlayer(steamId)) return ValidationResultCode.UNEXPECTED_TOKEN;
            if (!TokenUtility.IsTokenValid(receivedNonce, Secret)) return ValidationResultCode.EXPIRED_TOKEN;     
            if (NonceValidForPlayer(steamId, receivedNonce))
            {
                if (removeOnValidate) {
                    expectedNonces.Remove(steamId);
                }
                return ValidationResultCode.VALID_TOKEN;
            }
            else return ValidationResultCode.INVALID_TOKEN;             
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
                    expectedNonces.Remove(k);
            }
        }

        public static List<string> GetCleanSpaceHashList()
        {
            return (Shared.Plugin.Common.Plugin.Config.AnalyzedPlugins)
                .Where( (a)=>a.AssemblyName.Contains("CleanSpace") )
                .Select<PluginListEntry, string>((b)=>b.Hash).ToList();
        }

        public static ValidationResultData Validate(ulong steamId, string receivedNonce, List<String> hashList)
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

            var cleanSpaceHashPresence = GetCleanSpaceHashList().Intersect(hashList, StringComparer.OrdinalIgnoreCase).ToList();
            var currentPluginList = Shared.Plugin.Common.Plugin.Config.AnalyzedPlugins;
            var selectedPluginHashes = currentPluginList.Where(e => e.IsSelected).Select(e => e.Hash).ToList();


            var listType = Shared.Plugin.Common.Plugin.Config.PluginListType;

            var conflictingHashes = listType == PluginListType.Blacklist  
                // If it's a blacklist, then we are interested in which plugins the client has that are PRESENT in the list.
                ? selectedPluginHashes.Where(e => !cleanSpaceHashPresence.Contains(e, StringComparer.OrdinalIgnoreCase)).Intersect(hashList, StringComparer.OrdinalIgnoreCase).ToList()
                // If it's a whitelist, then we are interested in the plugins that are NOT PRESENT in the list.
                : hashList.Where(e => !cleanSpaceHashPresence.Contains(e, StringComparer.OrdinalIgnoreCase)).Except(selectedPluginHashes, StringComparer.OrdinalIgnoreCase).ToList();

            var action = Shared.Plugin.Common.Plugin.Config.ListMatchAction;

            if (cleanSpaceHashPresence.Count == 0)
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
