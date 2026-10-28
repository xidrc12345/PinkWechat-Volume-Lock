using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Threading;
using System.Windows.Forms;
using VolumeGuard;

static class Entry {
 [STAThread] static void Main(string[] args) {
  bool created; using (var mutex = new Mutex(true, "Local\\PinkCatVolumeGuard", out created)) {
   if (!created) { MessageBox.Show("PinkWechat 已经运行。", "PinkWechat"); return; }
   Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MainForm(args.Contains("--autostart")));
  }
 }
}
class MainForm : Form {
 readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer(); readonly Image cat; readonly NotifyIcon tray; readonly bool autoLaunch; bool autoProtect; bool startupEnabled; bool menuOpen; bool quitting;
 Snapshot last; float? baseline, lastNon22; string device; DateTime lastNon22At, holdUntil, pidAt; uint[] pids = new uint[0];
 bool enabled, active, dragging; int slider = 50; string error; DateTime logAt;
 readonly Rectangle track = new Rectangle(32, 103, 226, 6);
 readonly Rectangle toggle = new Rectangle(62, 140, 166, 38);
 static readonly Color Ink=Color.FromArgb(83,53,75), Pink=Color.FromArgb(236,105,157), Muted=Color.FromArgb(183,153,173);
 public MainForm(bool isAutoLaunch) {
  autoLaunch=isAutoLaunch; autoProtect=isAutoLaunch;
  Text="PinkWechat-音量锁定"; ClientSize=new Size(290,195); FormBorderStyle=FormBorderStyle.None; MaximizeBox=false;
  StartPosition=FormStartPosition.CenterScreen; BackColor=Color.FromArgb(255,248,251); DoubleBuffered=true;
  Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
  using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("pinkcat.png")) { if(stream==null) throw new Exception("Missing avatar"); using(var im=Image.FromStream(stream)) cat=new Bitmap(im); }
  InitializeStartup();
  var menu=new ContextMenuStrip(); menu.Items.Add("显示",null,delegate { ShowWindow(); }); menu.Items.Add("退出",null,delegate { quitting=true; Close(); });
  tray=new NotifyIcon(); tray.Icon=Icon; tray.Text="PinkWechat-音量锁定"; tray.ContextMenuStrip=menu; tray.Visible=true; tray.DoubleClick+=delegate { ShowWindow(); };
  if(autoLaunch) { ShowInTaskbar=false; WindowState=FormWindowState.Minimized; Shown+=delegate { Hide(); }; }
  timer.Interval=100; timer.Tick += Tick; timer.Start();
 }
 static GraphicsPath Round(RectangleF r,float radius) { var p=new GraphicsPath(); float d=radius*2; p.AddArc(r.X,r.Y,d,d,180,90); p.AddArc(r.Right-d,r.Y,d,d,270,90); p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); p.AddArc(r.X,r.Bottom-d,d,d,90,90); p.CloseFigure(); return p; }
 static void Fill(Graphics g,Color c,RectangleF r,float radius) { using(var p=Round(r,radius)) using(var b=new SolidBrush(c)) g.FillPath(b,p); }
 static void TextAt(Graphics g,string text,Font f,Color c,RectangleF rect) { using(var b=new SolidBrush(c)) using(var sf=new StringFormat { Alignment=StringAlignment.Center, LineAlignment=StringAlignment.Center }) g.DrawString(text,f,b,rect,sf); }
 [DllImport("user32.dll")] static extern bool ReleaseCapture();
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,int msg,IntPtr w,IntPtr l);
 protected override void OnPaint(PaintEventArgs e) {
  var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias; g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
  Fill(g,Color.FromArgb(255,251,253),new RectangleF(0,0,290,ClientSize.Height),12);
  using(var b=new SolidBrush(Color.FromArgb(246,112,119))) g.FillEllipse(b,14,14,11,11);
  using(var b=new SolidBrush(Color.FromArgb(247,197,91))) g.FillEllipse(b,32,14,11,11);
  using(var b=new SolidBrush(Color.FromArgb(126,205,157))) g.FillEllipse(b,50,14,11,11);
  using(var f=new Font("Microsoft YaHei UI",9,FontStyle.Bold)) TextAt(g,"PinkWechat-音量锁定",f,Muted,new RectangleF(72,8,180,25));
  using(var pen=new Pen(Muted,1.7f)) { pen.StartCap=LineCap.Round; pen.EndCap=LineCap.Round; for(int i=0;i<3;i++) g.DrawLine(pen,263,15+i*5,276,15+i*5); }
  using(var f=new Font("Microsoft YaHei UI",20,FontStyle.Bold)) TextAt(g,slider+"%",f,Ink,new RectangleF(40,50,210,38));
  Fill(g,Color.FromArgb(248,225,234),track,3);
  if(slider>0) Fill(g,Pink,new RectangleF(track.X,track.Y,Math.Max(6,track.Width*slider/100f),track.Height),3);
  float x=track.X+track.Width*slider/100f; using(var b=new SolidBrush(Pink)) g.FillEllipse(b,x-8,track.Y-5,16,16); using(var pen=new Pen(Color.White,2)) g.DrawEllipse(pen,x-8,track.Y-5,16,16);
  Fill(g,enabled ? Color.FromArgb(247,225,235) : pids.Length==0 ? Color.FromArgb(220,207,214) : Pink,toggle,14);
  using(var f=new Font("Microsoft YaHei UI",10,FontStyle.Bold)) TextAt(g,enabled ? "停止保护" : pids.Length==0 ? "未检测到微信" : "开启保护",f,enabled ? Ink : Color.White,toggle);
  using(var b=new SolidBrush(error!=null ? Color.OrangeRed : enabled ? Color.FromArgb(91,194,150) : Muted)) g.FillEllipse(b,142,184,6,6);
  if(menuOpen) {
   Fill(g,Color.FromArgb(238,223,231),new RectangleF(145,36,132,45),10);
   Fill(g,Color.White,new RectangleF(143,34,132,45),10);
   Fill(g,startupEnabled ? Pink : Color.FromArgb(230,220,225),new RectangleF(153,49,28,15),7);
   using(var b=new SolidBrush(Color.White)) g.FillEllipse(b,startupEnabled ? 168 : 155,51,11,11);
   using(var f=new Font("Microsoft YaHei UI",8)) TextAt(g,"开机自启",f,Ink,new RectangleF(184,44,78,25));
  }
 }
 int At(int x) { return Math.Max(0,Math.Min(100,(int)Math.Round((x-track.X)*100.0/track.Width))); }
 void SetSlider(int x) {
  if(last==null) return; int value=At(x);
  try { if(Audio.Set(last.DeviceId,value/100f)) { slider=value; baseline=value/100f; device=last.DeviceId; error=null; Invalidate(); } }
  catch(Exception ex) { Fail(ex); }
 }
 protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if(e.Button!=MouseButtons.Left)return;
  if(e.Y<34) { if(e.X>=252) { menuOpen=!menuOpen; Invalidate(); return; } if(e.X<29) { Close(); return; } if(e.X<47) { WindowState=FormWindowState.Minimized; return; } if(e.X<65) return; ReleaseCapture(); SendMessage(Handle,0xA1,(IntPtr)2,IntPtr.Zero); return; }
  if(menuOpen && e.X>=143 && e.X<=275 && e.Y>=34 && e.Y<=79) { SetStartup(!startupEnabled); Invalidate(); return; }
  if(menuOpen) { menuOpen=false; Invalidate(); }
  if(e.Y>=88 && e.Y<=124 && e.X>=track.X-10 && e.X<=track.Right+10) { dragging=true; SetSlider(e.X); }
  else if(toggle.Contains(e.Location)) {
   if(!enabled && pids.Length==0) { Log("No WeChat process; protection not started"); return; }
   if(enabled && autoLaunch) autoProtect=false; enabled=!enabled; active=false; holdUntil=DateTime.MinValue; baseline=last!=null && !last.Mute ? (float?)last.Volume : null; device=last==null ? null : last.DeviceId; lastNon22=null; Log(enabled ? "Protection on" : "Protection off"); Invalidate();
  }
 }
 protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if(dragging) SetSlider(e.X); Cursor=(toggle.Contains(e.Location) || (menuOpen && e.Y>=34 && e.Y<=79 && e.X>=143 && e.X<=275) || (e.Y<34 && e.X>=252) || (e.Y>=88 && e.Y<=124 && e.X>=track.X-10 && e.X<=track.Right+10) || (e.Y<30 && e.X<65)) ? Cursors.Hand : Cursors.Default; }
 protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); dragging=false; }
 protected override void OnFormClosing(FormClosingEventArgs e) {
  if(!quitting && e.CloseReason==CloseReason.UserClosing) { e.Cancel=true; Hide(); ShowInTaskbar=false; return; }
  base.OnFormClosing(e);
 }
 const string PreferenceKey=@"Software\PinkWechatVolume";
 void InitializeStartup() {
  try {
   using(var prefs=Registry.CurrentUser.CreateSubKey(PreferenceKey)) {
    object value=prefs.GetValue("StartupEnabled",null);
    bool desired=value==null || Convert.ToInt32(value)!=0;
    using(var run=Registry.CurrentUser.CreateSubKey(RunKey)) {
     if(desired) run.SetValue(RunName,StartupCommand(),RegistryValueKind.String);
     else run.DeleteValue(RunName,false);
    }
    prefs.SetValue("StartupEnabled",desired ? 1 : 0,RegistryValueKind.DWord);
    startupEnabled=ReadStartup();
   }
  } catch(Exception ex) { startupEnabled=ReadStartup(); MessageBox.Show("开机自启设置失败："+ex.Message,"PinkWechat"); }
 }
 const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
 const string RunName="PinkWechatVolume";
 bool ReadStartup() { try { using(var key=Registry.CurrentUser.OpenSubKey(RunKey,false)) { string value=key==null ? null : key.GetValue(RunName) as string; return string.Equals(value,StartupCommand(),StringComparison.OrdinalIgnoreCase); } } catch { return false; } }
 string StartupCommand() { return "\""+Application.ExecutablePath+"\" --autostart"; }
 void SetStartup(bool on) { try { using(var key=Registry.CurrentUser.CreateSubKey(RunKey)) { if(on) key.SetValue(RunName,StartupCommand(),RegistryValueKind.String); else key.DeleteValue(RunName,false); } using(var prefs=Registry.CurrentUser.CreateSubKey(PreferenceKey)) prefs.SetValue("StartupEnabled",on ? 1 : 0,RegistryValueKind.DWord); startupEnabled=ReadStartup(); Log("Startup "+(startupEnabled?"on":"off")); } catch(Exception ex) { MessageBox.Show("开机自启设置失败："+ex.Message,"PinkWechat"); } }
 void ShowWindow() { ShowInTaskbar=true; Show(); WindowState=FormWindowState.Normal; Activate(); }
 void Log(string message) { try { string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PinkCatVolume"); Directory.CreateDirectory(dir); string path=Path.Combine(dir,"guard.log"); if(File.Exists(path)&&new FileInfo(path).Length>1048576) File.Delete(path); File.AppendAllText(path,DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ")+" "+message+Environment.NewLine); } catch {} }
 void Fail(Exception ex) { if((DateTime.Now-logAt).TotalSeconds>5) { Log("ERROR "+ex.Message); logAt=DateTime.Now; } error=ex.Message; baseline=null; device=null; lastNon22=null; active=false; holdUntil=DateTime.MinValue; Invalidate(); }
 void Tick(object sender,EventArgs e) {
  try {
   var now=DateTime.Now; var s=Audio.Read(); last=s; error=null;
   if(device!=null && device!=s.DeviceId) { baseline=null; device=null; lastNon22=null; active=false; holdUntil=DateTime.MinValue; Log("Output changed; baseline cleared"); }
   if(!dragging) { int next=(int)Math.Round(s.Volume*100); if(next!=slider){ slider=next; Invalidate(); } }
   if((now-pidAt).TotalMilliseconds>=500) { var list=Process.GetProcesses(); try { pids=list.Where(p => { try { var n=p.ProcessName; return n.Equals("Weixin",StringComparison.OrdinalIgnoreCase)||n.Equals("WeChat",StringComparison.OrdinalIgnoreCase)||n.StartsWith("WeChatApp",StringComparison.OrdinalIgnoreCase); } catch { return false; } }).Select(p=>(uint)p.Id).ToArray(); } finally { foreach(var p in list)p.Dispose(); } pidAt=now; Invalidate(); }
   if(autoProtect && !enabled && pids.Length>0 && !s.Mute) { enabled=true; baseline=s.Volume; device=s.DeviceId; Log("WeChat detected; automatic protection on"); Invalidate(); }
   if(!enabled) { baseline=!s.Mute ? (float?)s.Volume : null; device=s.DeviceId; return; }
   if(pids.Length==0) { enabled=false; baseline=null; active=false; holdUntil=DateTime.MinValue; Log("WeChat not found; protection stopped"); Invalidate(); return; }
   bool capture=s.ActiveCapturePids.Any(id=>pids.Contains(id)); bool is22=Math.Abs(s.Volume-0.22f)<=0.006f;
   if(capture&&!active) { if(is22 && lastNon22.HasValue && (now-lastNon22At).TotalSeconds<=2) baseline=lastNon22; active=true; holdUntil=DateTime.MinValue; Log("Capture start baseline="+(baseline.HasValue?(baseline.Value*100).ToString("F0"):"unknown")); }
   else if(!capture&&active) { active=false; holdUntil=now.AddSeconds(2); Log("Capture end"); }
   if(capture || now<holdUntil) {
    if(!s.Mute && baseline.HasValue && s.DeviceId==device && Math.Abs(s.Volume-baseline.Value)>0.006f && Audio.Restore(device,s.Volume,baseline.Value)) Log("Corrected "+(s.Volume*100).ToString("F0")+" -> "+(baseline.Value*100).ToString("F0"));
   } else if(!s.Mute) { if(!is22){lastNon22=s.Volume; lastNon22At=now;} baseline=s.Volume; device=s.DeviceId; }
  } catch(Exception ex) { Fail(ex); }
 }
 protected override void Dispose(bool disposing) { if(disposing) { timer.Stop(); timer.Dispose(); if(tray!=null) { tray.Visible=false; tray.Dispose(); } cat.Dispose(); } base.Dispose(disposing); }
}


