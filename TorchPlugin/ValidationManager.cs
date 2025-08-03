using CleanSpaceShared;
using CleanSpaceShared.Networking;
using HarmonyLib;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Multiplayer;
using Shared.Logging;
using Shared.Struct;
using System;
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
            public long expiry;
            public override bool Equals(object obj)
            {
                if (!(obj is ExptectedNonce))
                    return false;

                ExptectedNonce mys = (ExptectedNonce)obj;
                return (nonce == mys.nonce);
            }

            public override int GetHashCode() => nonce.GetHashCode();
        }
       

        private static Dictionary<ulong, ExptectedNonce> expectedNonces = new Dictionary<ulong, ExptectedNonce>();

        public static bool NonceExistsForPlayer(ulong steamId)
        {
            return expectedNonces.ContainsKey(steamId);
        }

        public static string GetNonceForPlayer(ulong steamId)
        {
            return NonceExistsForPlayer(steamId) ? expectedNonces[steamId].nonce : null;
        }


        public static bool NonceExpiredForPlayer(ulong steamId)
        {
            return (expectedNonces[steamId].expiry <= DateTime.UtcNow.Ticks);
        }

        public static bool ValidNonceExistsForPlayer(ulong steamId)
        {
            return NonceExistsForPlayer(steamId) && !NonceExpiredForPlayer(steamId);
        }

        public static bool NonceValidForPlayer(ulong steamId, string nonce)
        {
            return ValidNonceExistsForPlayer(steamId) && nonce.Equals(expectedNonces[steamId].nonce);
        }

        public static bool RegisterNonceForPlayer(ulong steamId, string nonce, bool force = false)
        {

            if(!force && NonceExistsForPlayer(steamId)) 
                return false;
            
            expectedNonces[steamId] = new ExptectedNonce
            {
                nonce = nonce,
                expiry = (long)(DateTime.UtcNow.Ticks + CleanSpaceTorchPlugin.Instance.Config.TokenValidTimeTicks),
                sender = steamId
            };
            return true;
        }

        public static bool RegisterNonceForPlayer(ulong steamId)
        {
            var secret = CleanSpaceTorchPlugin.Instance.Config.Secret;
            var token = TokenUtility.GenerateToken(secret, DateTime.UtcNow.AddTicks((long)CleanSpaceTorchPlugin.Instance.Config.TokenValidTimeTicks), "auth");
            return RegisterNonceForPlayer(steamId, token);
        }


        public static ValidationResultCode ValidateToken(ulong steamId, string receivedNonce, bool removeOnValidate = true)
        {
            if (receivedNonce == null) return ValidationResultCode.MALFORMED_TOKEN;
            if (!NonceExistsForPlayer(steamId)) return ValidationResultCode.INVALID_TOKEN;
            if (NonceExpiredForPlayer(steamId)) return ValidationResultCode.EXPIRED_TOKEN;

            if (NonceValidForPlayer(steamId, receivedNonce))
            {
                if (removeOnValidate) expectedNonces.Remove(steamId);
                return ValidationResultCode.VALID_TOKEN;
               
            }
            else return ValidationResultCode.UNEXPECTED_TOKEN;             
        }


        public static void LogValidationResult(ulong steamId, ValidationResultData data)
        {
            string successString = (data.Success ? "SUCCESS" : "FAILURE");
            string pluginListString = (data.PluginList.Count() > 0 ? $" Hashes: {data.PluginList.ToString()}" : "FAILURE");
            Log.Info($"{CleanSpaceTorchPlugin.PluginName}: result {successString} with {data.Code.ToString()} {pluginListString}");
        }

        public static void pruneStaleEntries()
        {

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

            var currentPluginList = CleanSpaceTorchPlugin.Instance.Config.AnalyzedPlugins;
            var selectedPluginHashes = currentPluginList.Where(e => e.IsSelected).Select(e => e.Hash).ToList();

            var conflictingHashes = selectedPluginHashes.Intersect(hashList, StringComparer.OrdinalIgnoreCase).ToList();
            var action = CleanSpaceTorchPlugin.Instance.Config.ListMatchAction;

            ValidationResultData result;
            if (conflictingHashes.Count > 0)
            {
                switch (action)
                {
                    case ListMatchAction.Accept:
                        result = new ValidationResultData()
                        {
                            Code = ValidationResultCode.ALLOWED,
                            PluginList = conflictingHashes,
                            Success = true
                        };
                        break;
                    case ListMatchAction.Deny:
                        result = new ValidationResultData()
                        {
                            Code = ValidationResultCode.REJECTED_MATCH,
                            PluginList = conflictingHashes,
                            Success = false
                        };
                        break;
                    default:
                        result = new ValidationResultData()
                        {
                            Code = ValidationResultCode.ALLOWED,
                            PluginList = conflictingHashes,
                            Success = true
                        };
                        break;

                }
            }
            else
            {

                result = new ValidationResultData()
                {
                    Code = ValidationResultCode.ALLOWED,
                    PluginList = conflictingHashes,
                    Success = true
                };

            }

            LogValidationResult(steamId, result);
            return result;
        }

        
    
    }
}
