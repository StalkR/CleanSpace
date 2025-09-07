using Shared.Util;
using System;
using System.Collections.Generic;
using System.Net;

public enum RequestType : byte
{
    // IPv4 or lower bytes of IPv6
    ClientIpBytes1 = 1,
    ClientIpBytes2 = 2,
    ClientIpBytes3 = 3,
    ClientIpBytes4 = 4,

    // SteamIDs (8 bytes = 2 possible challenges each)
    ClientSteamId1 = 21, 
    ClientSteamId2 = 22, 
    ServerSteamId1 = 31,
    ServerSteamId2 = 32,

    // Assemblies
    LoadedAssembliesCount = 40,

    // Salt (16 bytes = 4 chunks of possibilities)
    EchoSalt1 = 50,
    EchoSalt2 = 51,
    EchoSalt3 = 52,
    EchoSalt4 = 53,
}

public static class SessionParameterFactory
{
    private static readonly Random _rng = new Random();

    public delegate byte[] ChunkValidationProvider(SessionParameters originalParameters, ulong serverSteamID, ulong clientSteamID, uint clientOriginIP);

    public static Dictionary<RequestType, ChunkValidationProvider> providers = new Dictionary<RequestType, ChunkValidationProvider>();

    public static void RegisterProvider(RequestType requestType, ChunkValidationProvider requestDelegate)
    {
        providers[requestType] = requestDelegate;
    }

    public static void RegisterProviders()
    {

        RegisterProvider(RequestType.ClientIpBytes1, (orig, serverId, clientId, clientIp) =>
        {
            var ipBytes = BitConverter.GetBytes(clientIp);
            return new byte[] { ipBytes[0], 0, 0, 0 };
        });

        RegisterProvider(RequestType.ClientIpBytes2, (orig, serverId, clientId, clientIp) =>
        {
            var ipBytes = BitConverter.GetBytes(clientIp);
            return new byte[] { ipBytes[1], 0, 0, 0 };
        });

        RegisterProvider(RequestType.ClientIpBytes3, (orig, serverId, clientId, clientIp) =>
        {
            var ipBytes = BitConverter.GetBytes(clientIp);
            return new byte[] { ipBytes[2], 0, 0, 0 };
        });

        RegisterProvider(RequestType.ClientIpBytes4, (orig, serverId, clientId, clientIp) =>
        {
            var ipBytes = BitConverter.GetBytes(clientIp);
            return new byte[] { ipBytes[3], 0, 0, 0 };
        });

        RegisterProvider(RequestType.ClientSteamId1, (orig, serverId, clientId, clientIp) =>
            BitConverter.GetBytes((uint)(clientId & 0xFFFFFFFF)));

        RegisterProvider(RequestType.ClientSteamId2, (orig, serverId, clientId, clientIp) =>
            BitConverter.GetBytes((uint)(clientId >> 32)));

        RegisterProvider(RequestType.ServerSteamId1, (orig, serverId, clientId, clientIp) =>
            BitConverter.GetBytes((uint)(serverId & 0xFFFFFFFF)));

        RegisterProvider(RequestType.ServerSteamId2, (orig, serverId, clientId, clientIp) =>
            BitConverter.GetBytes((uint)(serverId >> 32)));

        RegisterProvider(RequestType.LoadedAssembliesCount, (orig, serverId, clientId, clientIp) =>
            BitConverter.GetBytes(AppDomain.CurrentDomain.GetAssemblies().Length));

        RegisterProvider(RequestType.EchoSalt1, (orig, serverId, clientId, clientIp) =>
        {
            var slice = new byte[4];
            Array.Copy(orig.sessionSalt, 0, slice, 0, 4);
            return slice;
        });

        RegisterProvider(RequestType.EchoSalt2, (orig, serverId, clientId, clientIp) =>
        {
            var slice = new byte[4];
            Array.Copy(orig.sessionSalt, 4, slice, 0, 4);
            return slice;
        });

        RegisterProvider(RequestType.EchoSalt3, (orig, serverId, clientId, clientIp) =>
        {
            var slice = new byte[4];
            Array.Copy(orig.sessionSalt, 8, slice, 0, 4);
            return slice;
        });

        RegisterProvider(RequestType.EchoSalt4, (orig, serverId, clientId, clientIp) =>
        {
            var slice = new byte[4];
            Array.Copy(orig.sessionSalt, 12, slice, 0, 4);
            return slice;
        });
        Shared.Plugin.Common.Logger.Info($"{Shared.Plugin.Common.Logger}: Session parameter validation providers registered.");
    }

    public static SessionParameters CreateSessionParameters(byte[] salt)
    {
        var ret = new SessionParameters
        {
            randomRequestType1 = GetRandomRequestType(),
            randomRequestType2 = GetRandomRequestType(),
            randomRequestType3 = GetRandomRequestType(),         
            chatterLength = (byte)_rng.Next(2, 10),
            sessionSalt = salt
        };
        ret.randomRequestType4 = (ret.randomRequestType1 != 40 
                                && ret.randomRequestType2 != 40 
                                && ret.randomRequestType3 != 40) 
                                ? (byte)RequestType.LoadedAssembliesCount 
                                : GetRandomRequestType();
        return ret;
    }

    public static SessionParameters AnswerChallenge(SessionParameters challenge, uint clientIp, ulong clientSteamId, ulong serverSteamId, int loadedAssemblies)
    {
        byte[] buffer = new byte[16];

        WriteResponse(buffer, 0, challenge.randomRequestType1, challenge.sessionSalt,
            clientIp, clientSteamId, serverSteamId, loadedAssemblies, challenge);
        WriteResponse(buffer, 4, challenge.randomRequestType2, challenge.sessionSalt,
            clientIp, clientSteamId, serverSteamId, loadedAssemblies, challenge);
        WriteResponse(buffer, 8, challenge.randomRequestType3, challenge.sessionSalt,
            clientIp, clientSteamId, serverSteamId, loadedAssemblies, challenge);
        WriteResponse(buffer, 12, challenge.randomRequestType3, challenge.sessionSalt,
           clientIp, clientSteamId, serverSteamId, loadedAssemblies, challenge);

        challenge.sessionSalt = buffer;
        return challenge;
    }

    private static void WriteResponse( byte[] buffer, int offset, byte requestType, byte[] salt, uint clientIp, ulong clientSteamId,  ulong serverSteamId, int loadedAssemblies, SessionParameters challengeIn)
    {
        var rt = (RequestType)requestType;
        byte[] slice = new byte[4];

        if (!providers.TryGetValue(rt, out var provider))
            return;

        slice = provider.Invoke(challengeIn, serverSteamId, clientSteamId, clientIp);
        Array.Copy(slice, 0, buffer, offset, 4);
    }

    private static byte GetRandomRequestType()
    {
        var values = (RequestType[])Enum.GetValues(typeof(RequestType));
        return (byte)values[_rng.Next(values.Length)];
    }

}