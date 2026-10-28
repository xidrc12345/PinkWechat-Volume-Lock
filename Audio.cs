using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace VolumeGuard {
 [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class Enumerator {}
 [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IEnum {
  int EnumAudioEndpoints(int flow, int mask, out IntPtr devices);
  int GetDefaultAudioEndpoint(int flow, int role, out IDevice device);
 }
 [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IDevice {
  int Activate(ref Guid iid, int context, IntPtr p, [MarshalAs(UnmanagedType.IUnknown)] out object obj);
  int OpenPropertyStore(int access, out IntPtr store);
  int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
 }
 [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IVolume {
  int RegisterControlChangeNotify(IntPtr p); int UnregisterControlChangeNotify(IntPtr p); int GetChannelCount(out uint n);
  int SetMasterVolumeLevel(float v, ref Guid g); int SetMasterVolumeLevelScalar(float v, ref Guid g);
  int GetMasterVolumeLevel(out float v); int GetMasterVolumeLevelScalar(out float v);
  int SetChannelVolumeLevel(uint n,float v,ref Guid g); int SetChannelVolumeLevelScalar(uint n,float v,ref Guid g);
  int GetChannelVolumeLevel(uint n,out float v); int GetChannelVolumeLevelScalar(uint n,out float v);
  int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute,ref Guid g); int GetMute(out bool mute);
 }
 [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IManager {
  int Placeholder0(); int Placeholder1(); int GetSessionEnumerator(out ISessions sessions);
 }
 [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ISessions {
  int GetCount(out int count); int GetSession(int i,out IControl c);
 }
 [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IControl { int GetState(out int state); }
 [Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IControl2 {
  int GetState(out int state); int GetDisplayName(out IntPtr name); int SetDisplayName(IntPtr name,ref Guid g);
  int GetIconPath(out IntPtr p); int SetIconPath(IntPtr p,ref Guid g); int GetGroupingParam(out Guid g);
  int SetGroupingParam(ref Guid g,ref Guid ctx); int RegisterAudioSessionNotification(IntPtr p);
  int UnregisterAudioSessionNotification(IntPtr p); int GetSessionIdentifier(out IntPtr p);
  int GetSessionInstanceIdentifier(out IntPtr p); int GetProcessId(out uint pid);
 }
 public class Snapshot { public string DeviceId; public float Volume; public bool Mute; public uint[] ActiveCapturePids; }
 public static class Audio {
  static void Check(int hr) { if(hr<0) Marshal.ThrowExceptionForHR(hr); }
  static void Release(object o) { if(o!=null && Marshal.IsComObject(o)) Marshal.ReleaseComObject(o); }
  static IDevice Default(IEnum e,int flow) { IDevice d; Check(e.GetDefaultAudioEndpoint(flow,1,out d)); return d; }
  static object Activate(IDevice d,Guid id) { object o; Check(d.Activate(ref id,23,IntPtr.Zero,out o)); return o; }
  public static Snapshot Read() {
   object eo=null,vo=null,mo=null; IDevice render=null,capture=null; ISessions sessions=null;
   try {
    eo=new Enumerator(); var en=(IEnum)eo; render=Default(en,0); string id; Check(render.GetId(out id));
    vo=Activate(render,typeof(IVolume).GUID); var v=(IVolume)vo; float vol; bool mute;
    Check(v.GetMasterVolumeLevelScalar(out vol)); Check(v.GetMute(out mute));
    capture=Default(en,1); mo=Activate(capture,typeof(IManager).GUID); Check(((IManager)mo).GetSessionEnumerator(out sessions));
    int n; Check(sessions.GetCount(out n)); var pids=new List<uint>();
    for(int i=0;i<n;i++){ IControl c=null; try { if(sessions.GetSession(i,out c)!=0 || c==null) continue;
      int state; uint pid; var c2=c as IControl2; if(c2!=null && c.GetState(out state)==0 && state==1 && c2.GetProcessId(out pid)==0 && pid!=0) pids.Add(pid);
     } finally { Release(c); } }
    return new Snapshot {DeviceId=id,Volume=vol,Mute=mute,ActiveCapturePids=pids.ToArray()};
   } finally { Release(sessions); Release(mo); Release(capture); Release(vo); Release(render); Release(eo); }
  }
  public static bool Set(string expectedId,float value) {
   object eo=null,vo=null; IDevice render=null;
   try { eo=new Enumerator(); render=Default((IEnum)eo,0); string id; Check(render.GetId(out id)); if(id!=expectedId) return false;
    vo=Activate(render,typeof(IVolume).GUID); Guid g=Guid.NewGuid(); Check(((IVolume)vo).SetMasterVolumeLevelScalar(value,ref g)); return true;
   } finally { Release(vo); Release(render); Release(eo); }
  }
  public static bool Restore(string expectedId,float expectedCurrent,float target) {
   object eo=null,vo=null; IDevice render=null;
   try { eo=new Enumerator(); render=Default((IEnum)eo,0); string id; Check(render.GetId(out id)); if(id!=expectedId) return false;
    vo=Activate(render,typeof(IVolume).GUID); var v=(IVolume)vo; bool mute; float current;
    Check(v.GetMute(out mute)); Check(v.GetMasterVolumeLevelScalar(out current));
    if(mute || Math.Abs(current-expectedCurrent)>0.006f) return false;
    Guid g=Guid.NewGuid(); Check(v.SetMasterVolumeLevelScalar(target,ref g)); return true;
   } finally { Release(vo); Release(render); Release(eo); }
  }
 }
}