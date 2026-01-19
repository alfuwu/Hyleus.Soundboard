using System.Runtime.InteropServices;
using Hyleus.Soundboard.Framework.Enums;

namespace Hyleus.Soundboard.Framework;
internal static unsafe class FdkAacNative {
    private const string LibName = "fdk-aac";

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint aacDecoder_Open(
        AacTransportType transportType,
        int nrOfLayers
    );

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern AacDecoderError aacDecoder_Close(
        nint handle
    );

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern AacDecoderError aacDecoder_Fill(
        nint handle,
        ref nint buffer,
        ref int bufferSize,
        ref int bytesValid
    );

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern AacDecoderError aacDecoder_DecodeFrame(
        nint handle,
        short* pcmBuffer,
        int pcmBufferSize,
        int flags
    );

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int aacDecoder_GetParam(
        nint handle,
        AacDecoderParam param
    );
}
