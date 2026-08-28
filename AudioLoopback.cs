using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

internal enum EDataFlow { Render, Capture, All }
internal enum ERole { Console, Multimedia, Communications }

[Flags]
internal enum ClsCtx : uint { InprocServer = 0x1, InprocHandler = 0x2, LocalServer = 0x4, All = 0x17 }

[Flags]
internal enum AudioClientStreamFlags : uint { Loopback = 0x00020000 }

[Flags]
internal enum AudioClientBufferFlags : uint { DataDiscontinuity = 0x1, Silent = 0x2, TimestampError = 0x4 }

[StructLayout(LayoutKind.Sequential, Pack = 2)]
internal struct WaveFormatEx
{
    public ushort FormatTag;
    public ushort Channels;
    public uint SamplesPerSec;
    public uint AvgBytesPerSec;
    public ushort BlockAlign;
    public ushort BitsPerSample;
    public ushort ExtraSize;
}

[StructLayout(LayoutKind.Sequential, Pack = 2)]
internal struct WaveFormatExtensible
{
    public WaveFormatEx Format;
    public ushort ValidBitsPerSample;
    public uint ChannelMask;
    public Guid SubFormat;
}

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumeratorComObject { }

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out IntPtr devices);
    [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
    [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
    [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig] int Activate(ref Guid interfaceId, ClsCtx clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
    [PreserveSig] int OpenPropertyStore(uint access, out IntPtr properties);
    [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    [PreserveSig] int GetState(out uint state);
}

[ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioClient
{
    [PreserveSig] int Initialize(int shareMode, AudioClientStreamFlags streamFlags, long bufferDuration, long periodicity, IntPtr format, ref Guid sessionGuid);
    [PreserveSig] int GetBufferSize(out uint bufferFrames);
    [PreserveSig] int GetStreamLatency(out long latency);
    [PreserveSig] int GetCurrentPadding(out uint paddingFrames);
    [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closestMatch);
    [PreserveSig] int GetMixFormat(out IntPtr deviceFormat);
    [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
    [PreserveSig] int Start();
    [PreserveSig] int Stop();
    [PreserveSig] int Reset();
    [PreserveSig] int SetEventHandle(IntPtr eventHandle);
    [PreserveSig] int GetService(ref Guid interfaceId, [MarshalAs(UnmanagedType.IUnknown)] out object service);
}

[ComImport, Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioCaptureClient
{
    [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out AudioClientBufferFlags flags, out ulong devicePosition, out ulong qpcPosition);
    [PreserveSig] int ReleaseBuffer(uint frames);
    [PreserveSig] int GetNextPacketSize(out uint frames);
}

internal sealed class LoopbackCapture : IDisposable
{
    const ushort WaveFormatPcm = 1;
    const ushort WaveFormatIeeeFloat = 3;
    const ushort WaveFormatExtensible = 0xFFFE;
    static readonly Guid AudioClientId = new Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");
    static readonly Guid AudioCaptureClientId = new Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317");
    static readonly Guid PcmSubtype = new Guid("00000001-0000-0010-8000-00AA00389B71");
    static readonly Guid FloatSubtype = new Guid("00000003-0000-0010-8000-00AA00389B71");

    IMMDeviceEnumerator enumerator;
    IMMDevice device;
    IAudioClient audioClient;
    IAudioCaptureClient captureClient;
    IntPtr formatPointer;
    WaveFormatEx format;
    bool started;

    public string FfmpegFormat { get; private set; }
    public int SampleRate { get { return (int)format.SamplesPerSec; } }
    public int Channels { get { return format.Channels; } }

    public LoopbackCapture()
    {
        enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        Check(enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out device), "DefaultAudioEndpoint");
        object client;
        Guid iid = AudioClientId;
        Check(device.Activate(ref iid, ClsCtx.All, IntPtr.Zero, out client), "Activate");
        audioClient = (IAudioClient)client;
        Check(audioClient.GetMixFormat(out formatPointer), "GetMixFormat");
        format = (WaveFormatEx)Marshal.PtrToStructure(formatPointer, typeof(WaveFormatEx));
        FfmpegFormat = ResolveFormat(formatPointer, format);
    }

    static string ResolveFormat(IntPtr pointer, WaveFormatEx value)
    {
        Guid subtype = Guid.Empty;
        if (value.FormatTag == WaveFormatExtensible)
            subtype = ((WaveFormatExtensible)Marshal.PtrToStructure(pointer, typeof(WaveFormatExtensible))).SubFormat;
        bool isFloat = value.FormatTag == WaveFormatIeeeFloat || subtype == FloatSubtype;
        bool isPcm = value.FormatTag == WaveFormatPcm || subtype == PcmSubtype;
        if (isFloat && value.BitsPerSample == 32) return "f32le";
        if (isPcm && value.BitsPerSample == 16) return "s16le";
        if (isPcm && value.BitsPerSample == 24) return "s24le";
        if (isPcm && value.BitsPerSample == 32) return "s32le";
        throw new InvalidOperationException("Unsupported Windows mix format: tag=" + value.FormatTag + ", bits=" + value.BitsPerSample);
    }

    public void Start()
    {
        Guid session = Guid.Empty;
        Check(audioClient.Initialize(0, AudioClientStreamFlags.Loopback, 10000000, 0, formatPointer, ref session), "Initialize");
        object capture;
        Guid iid = AudioCaptureClientId;
        Check(audioClient.GetService(ref iid, out capture), "GetService");
        captureClient = (IAudioCaptureClient)capture;
        Check(audioClient.Start(), "Start");
        started = true;
    }

    public void CopyTo(Stream output)
    {
        byte[] silence = null;
        while (true)
        {
            uint packetFrames;
            Check(captureClient.GetNextPacketSize(out packetFrames), "GetNextPacketSize");
            if (packetFrames == 0) { Thread.Sleep(3); continue; }
            while (packetFrames > 0)
            {
                IntPtr data; uint frames; AudioClientBufferFlags flags; ulong devicePosition, qpcPosition;
                Check(captureClient.GetBuffer(out data, out frames, out flags, out devicePosition, out qpcPosition), "GetBuffer");
                int bytes = checked((int)frames * format.BlockAlign);
                try
                {
                    if ((flags & AudioClientBufferFlags.Silent) != 0)
                    {
                        if (silence == null || silence.Length < bytes) silence = new byte[bytes];
                        output.Write(silence, 0, bytes);
                    }
                    else
                    {
                        byte[] buffer = new byte[bytes]; Marshal.Copy(data, buffer, 0, bytes); output.Write(buffer, 0, bytes);
                    }
                    output.Flush();
                }
                finally { Check(captureClient.ReleaseBuffer(frames), "ReleaseBuffer"); }
                Check(captureClient.GetNextPacketSize(out packetFrames), "GetNextPacketSize");
            }
        }
    }

    static void Check(int hresult, string operation)
    {
        if (hresult < 0) Marshal.ThrowExceptionForHR(hresult, new IntPtr(-1));
    }

    public void Dispose()
    {
        if (started) { try { audioClient.Stop(); } catch { } started = false; }
        if (formatPointer != IntPtr.Zero) { Marshal.FreeCoTaskMem(formatPointer); formatPointer = IntPtr.Zero; }
        if (captureClient != null) Marshal.ReleaseComObject(captureClient);
        if (audioClient != null) Marshal.ReleaseComObject(audioClient);
        if (device != null) Marshal.ReleaseComObject(device);
        if (enumerator != null) Marshal.ReleaseComObject(enumerator);
    }
}

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            using (var capture = new LoopbackCapture())
            {
                if (args.Length > 0 && args[0] == "--info")
                {
                    Console.WriteLine(capture.FfmpegFormat + "|" + capture.SampleRate + "|" + capture.Channels);
                    return 0;
                }
                capture.Start(); capture.CopyTo(Console.OpenStandardOutput());
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }
}
