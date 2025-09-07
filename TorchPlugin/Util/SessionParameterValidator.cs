using Shared.Plugin;
using System;
using System.Collections.Generic;
using static SessionParameterFactory;

namespace TorchPlugin.Util
{
    public class SessionParameterValidator
    {
      
        public static Dictionary<RequestType, ChunkValidationProvider> Providers => SessionParameterFactory.providers;
        public static bool ValidateResponse(SessionParameters challenge, SessionParameters response, ulong serverSteamID, ulong clientSteamID, uint clientOriginIP)
        {
            if (challenge.chatterLength != response.chatterLength)
                return false;

            if (response.sessionSalt == null || response.sessionSalt.Length != 16)
                return false;

            if (!ValidateChunk(challenge.randomRequestType1, response, 0, challenge, serverSteamID, clientSteamID, clientOriginIP)) return false;
            if (!ValidateChunk(challenge.randomRequestType2, response, 4, challenge, serverSteamID, clientSteamID, clientOriginIP)) return false;
            if (!ValidateChunk(challenge.randomRequestType3, response, 8, challenge, serverSteamID, clientSteamID, clientOriginIP)) return false;
            if (!ValidateChunk(challenge.randomRequestType4, response, 12, challenge, serverSteamID, clientSteamID, clientOriginIP)) return false;

            return true; 
        }

        private static bool ValidateChunk(byte requestType, byte[] clientBuffer, int offset, SessionParameters originalParameters, ulong serverSteamID, ulong clientSteamID, uint clientOriginIP)
        {
            var rt = (RequestType)requestType;

            if (!Providers.TryGetValue(rt, out var provider))
                return false;

            byte[] expected = provider.Invoke(originalParameters, serverSteamID, clientSteamID, clientOriginIP);

            for (int i = 0; i < 4; i++){
                if (clientBuffer[offset + i] != expected[i])
                    return false;
            }

            return true;
        }

      
    }
}


