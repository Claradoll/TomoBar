using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms=System.Windows.Forms;

[assembly:AssemblyTitle("小番茄")]
[assembly:AssemblyDescription("轻量的 Windows 任务与番茄钟")]
[assembly:AssemblyProduct("小番茄")]
[assembly:AssemblyVersion("1.5.1.0")]
[assembly:AssemblyFileVersion("1.5.1.0")]
namespace LittleTomato {
 public static class Launcher { [STAThread] public static int Main(string[] args) {System.Globalization.CultureInfo.DefaultThreadCurrentCulture=System.Globalization.CultureInfo.GetCultureInfo("zh-CN");System.Globalization.CultureInfo.DefaultThreadCurrentUICulture=System.Globalization.CultureInfo.GetCultureInfo("zh-CN");return Program.Start(args);} }
 public class Program {
  public AppData Data {get{return Engine.Data;}}
  public NoteManager Notes;public StartupRegistration Startup;public Store Store; public Engine Engine;public MainWindow Main;public MiniWindow Mini;public TaskbarStrip Bar;public bool Quitting;public ImageSource IconSource;
  Application application;DispatcherTimer timer;Forms.NotifyIcon tray;Forms.Timer trayClickTimer;Mutex mutex;string instanceTag;int ticks;bool savingError,saveInProgress,shownHint;DateTime savedAt=DateTime.UtcNow;string folder;bool test,diagnostics;IntPtr hwnd;
  public static int Start(string[] args) {
   try {
    if(args.Contains("--self-test"))return SelfTests.Run(args.Length>1?args[1]:"test-results.txt");
    if(args.Contains("--make-icon")){MakeIcon(args[1]);return 0;}
    var program=new Program();program.Run(args);return Environment.ExitCode;
   }catch(Exception ex){try{File.AppendAllText(Path.Combine(Path.GetTempPath(),"LittleTomato-error.log"),DateTime.Now+"\n"+ex+"\n");}catch{}MessageBox.Show("小番茄未能启动：\n"+ex.Message+"\n\n详细日志："+Path.Combine(Path.GetTempPath(),"LittleTomato-error.log"),"小番茄",MessageBoxButton.OK,MessageBoxImage.Error);return 1;}
  }
  void Run(string[] args) {
   diagnostics=args.Contains("--diagnostics");
   int di=Array.IndexOf(args,"--data-dir");test=di>=0;folder=di>=0&&args.Length>di+1?Path.GetFullPath(args[di+1]):DefaultDataDirectory();
   Startup=new StartupRegistration(Assembly.GetExecutingAssembly().Location,test?@"Software\LittleTomato\Tests\"+StableHash(folder):StartupRegistration.RunKey);
   instanceTag="TomoBar.Instance."+StableHash(folder);
   bool first;string key="Local\\LittleTomato-"+StableHash(folder);mutex=new Mutex(true,key,out first);
   if(!first){IntPtr previous=Native.FindInstance(instanceTag);if(previous==IntPtr.Zero&&!test)previous=Native.FindWindow(null,"小番茄 · 专注每一刻");if(previous!=IntPtr.Zero&&!args.Contains("--background"))Native.PostMessage(previous,args.Contains("--exit")?0x8003u:0x8001u,IntPtr.Zero,IntPtr.Zero);mutex.Dispose();return;}
   if(args.Contains("--exit")){mutex.ReleaseMutex();mutex.Dispose();return;}
   Store=new Store(folder);Engine=new Engine(Store.Load(),new TimerClock());
   application=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Theme.xaml"))application.Resources=(ResourceDictionary)XamlReader.Load(stream);
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("tomato.ico")){if(stream!=null){var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);var icon=decoder.Frames.OrderByDescending(f=>f.PixelWidth).First();icon.Freeze();IconSource=icon;}}
   Notes=new NoteManager(this);ApplyTheme();Main=new MainWindow(this);if(test)Main.Title="小番茄 · 测试 · "+StableHash(folder).Substring(0,6);application.MainWindow=Main;Mini=new MiniWindow(this);
   Main.SourceInitialized+=(s,e)=>{hwnd=new WindowInteropHelper(Main).Handle;Native.SetProp(hwnd,instanceTag,new IntPtr(1));HwndSource.FromHwnd(hwnd).AddHook(Hook);Native.RegisterHotKey(hwnd,1,0x4003,0x50);Native.RegisterHotKey(hwnd,2,0x4003,0x4F);RegisterNoteShortcut();};
   if(args.Contains("--background"))new WindowInteropHelper(Main).EnsureHandle();else Main.Show();
   Bar=new TaskbarStrip(this);Bar.RefreshPlacement();
   tray=new Forms.NotifyIcon {Visible=true,Text="小番茄 · 选择任务开始专注"};using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("tomato.ico")){tray.Icon=stream==null?System.Drawing.SystemIcons.Application:new System.Drawing.Icon(stream);}
   trayClickTimer=new Forms.Timer {Interval=Forms.SystemInformation.DoubleClickTime};trayClickTimer.Tick+=(s,e)=>{trayClickTimer.Stop();ToggleMini();};
   var menu=new Forms.ContextMenuStrip();menu.Opening+=(s,e)=>trayClickTimer.Stop();menu.Items.Add("打开小番茄",null,(s,e)=>ShowMain());menu.Items.Add("暂停 / 继续",null,(s,e)=>ToggleTimer());menu.Items.Add("新建便签",null,(s,e)=>Notes.New());menu.Items.Add("设置",null,(s,e)=>{ShowMain();Main.ShowPage("settings");});menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add("退出",null,(s,e)=>Exit());tray.ContextMenuStrip=menu;tray.MouseClick+=(s,e)=>{if(e.Button==Forms.MouseButtons.Left){trayClickTimer.Stop();trayClickTimer.Start();}};tray.MouseDoubleClick+=(s,e)=>{if(e.Button==Forms.MouseButtons.Left){trayClickTimer.Stop();ShowMain();}};tray.BalloonTipClicked+=(s,e)=>ToggleMini();
   Engine.Changed+=OnChanged;Engine.Finished+=Notify;
   SystemEvents.PowerModeChanged+=PowerChanged;SystemEvents.SessionSwitch+=SessionChanged;SystemEvents.UserPreferenceChanged+=ThemeChanged;SystemEvents.DisplaySettingsChanged+=DisplayChanged;
   application.SessionEnding+=(s,e)=>{Engine.Pause();Save();};application.DispatcherUnhandledException+=(s,e)=>{Log(e.Exception);if(!savingError){savingError=true;MessageBox.Show(Main,"操作未能完成，已有数据仍保留。\n"+e.Exception.Message,"小番茄");savingError=false;}e.Handled=true;};
   timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(250)};timer.Tick+=(s,e)=>{Engine.Tick();if(++ticks%4==0){if(Main.IsVisible)Main.RefreshTimer();if(Mini.IsVisible)Mini.Refresh();if(Bar.IsDisposed)Bar=new TaskbarStrip(this);Bar.RefreshPlacement();if(diagnostics)WriteDiagnostics();string text="小番茄 · "+Engine.DisplayTime+" · "+Engine.Phase;if(text.Length<64)tray.Text=text;}if((DateTime.UtcNow-savedAt).TotalSeconds>=5 && Data.Active!=null&&Data.Active.Running){Save();}};timer.Start();
   if(Store.LoadNotice!=null)Notify(Store.LoadNotice);else if(Data.Active!=null)Notify("已找回上次计时，当前暂停，点击继续即可。");
   if(args.Contains("--diagnostics")){var diag=new DispatcherTimer {Interval=TimeSpan.FromSeconds(3)};diag.Tick+=(s,e)=>{diag.Stop();WriteDiagnostics();};diag.Start();}
   if(test && args.Contains("--notes-smoke"))NoteSmoke.Start(this,folder);
   if(test && args.Contains("--ui-smoke"))UiSmoke.Start(this,folder);
   application.Run();
   SystemEvents.PowerModeChanged-=PowerChanged;SystemEvents.SessionSwitch-=SessionChanged;SystemEvents.UserPreferenceChanged-=ThemeChanged;SystemEvents.DisplaySettingsChanged-=DisplayChanged;
   if(mutex!=null){mutex.ReleaseMutex();mutex.Dispose();}
  }
  static string StableHash(string text) {using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToLowerInvariant()))).Replace("-","").Substring(0,20);}
  static string DefaultDataDirectory() {
   string local=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data");try{Directory.CreateDirectory(local);string probe=Path.Combine(local,".write-test");File.WriteAllText(probe,"");File.Delete(probe);return local;}catch{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LittleTomato");}
  }
  IntPtr Hook(IntPtr h,int msg,IntPtr wp,IntPtr lp,ref bool handled) {if(msg==0x312){if(wp.ToInt32()==1)ToggleTimer();else if(wp.ToInt32()==3)Notes.New();else ShowMain();handled=true;}else if(msg==0x8001){ShowMain();handled=true;}else if(msg==0x8003){Exit();handled=true;}return IntPtr.Zero;}
  public bool NoteShortcutAvailable;
  public void RegisterNoteShortcut(){if(hwnd==IntPtr.Zero)return;Native.UnregisterHotKey(hwnd,3);NoteShortcutAvailable=Data.Settings.NoteShortcut=="off"||Native.RegisterHotKey(hwnd,3,0x4003,Data.Settings.NoteShortcut=="M"?0x4Du:0x4Eu);}
  void Dispatch(Action a){if(!Quitting)application.Dispatcher.BeginInvoke(a);}
  void PowerChanged(object s,PowerModeChangedEventArgs e){if(e.Mode==PowerModes.Suspend)Dispatch(()=>Engine.Pause());else if(e.Mode==PowerModes.Resume)Dispatch(()=>{Engine.Pause();Bar.RefreshPlacement();});}
  void SessionChanged(object s,SessionSwitchEventArgs e){if(e.Reason==SessionSwitchReason.SessionLock&&Data.Settings.PauseOnLock)Dispatch(()=>Engine.Pause());}
  void ThemeChanged(object s,UserPreferenceChangedEventArgs e){Dispatch(()=>{ApplyTheme();if(Bar!=null)Bar.Invalidate();});}
  void DisplayChanged(object s,EventArgs e){Dispatch(()=>Bar.RefreshPlacement());}
  void OnChanged(){Save();if(Main!=null)Main.ModelChanged();if(Mini!=null)Mini.Refresh();if(Bar!=null)Bar.RefreshPlacement();}
  public static string SaveFailureMessage(Exception ex){if(ex is System.Runtime.Serialization.SerializationException)return "程序无法转换部分数据，尚未写入本次更改。现有文件未被覆盖。\n"+ex.Message;if(ex is IOException||ex is UnauthorizedAccessException)return "暂时无法写入数据文件，请检查目录权限或磁盘空间。\n"+ex.Message;return "程序保存数据时发生错误，尚未写入本次更改。\n"+ex.Message;}
  public bool Save(){if(saveInProgress)return false;saveInProgress=true;try{Store.Save(Data);savedAt=DateTime.UtcNow;savingError=false;return true;}catch(Exception ex){savedAt=DateTime.UtcNow;if(!savingError){savingError=true;Log(ex);MessageBox.Show(Main,SaveFailureMessage(ex),"保存失败",MessageBoxButton.OK,MessageBoxImage.Warning);}return false;}finally{saveInProgress=false;}}
  void Log(Exception ex){try{File.AppendAllText(Path.Combine(folder,"error.log"),DateTime.Now+"\n"+ex+"\n");}catch{}}
  public void ShowMain(){if(Mini!=null)Mini.Hide();Main.Show();if(Main.WindowState==WindowState.Minimized)Main.WindowState=WindowState.Normal;Main.Activate();Native.SetForegroundWindow(new WindowInteropHelper(Main).Handle);Main.ModelChanged();}
  public void ToggleMini(){if(Mini.IsVisible)Mini.Hide();else Mini.Open();}
  public void ToggleTimer(){if(Main!=null)Main.ClearTaskPreview();bool wasRunning=Data.Active!=null&&Data.Active.Running;if(Data.Active==null)Engine.EnsureFocusTask();Engine.Toggle();if(!wasRunning)CollapseForFocus();}
  public void StartTask(Todo task){if(Main!=null)Main.PreviewTask(task);Engine.Start(task);CollapseForFocus();}
  void CollapseForFocus(){if(Data.Active==null||!Data.Active.Running||Data.Active.Kind!="focus")return;Main.CloseEditor();Mini.Hide();Main.Hide();if(Bar!=null)Bar.RefreshPlacement();}
  public void EndTimer(){if(Data.Active==null)return;Engine.End();}
  public void ShowTrayHint(){if(shownHint||tray==null)return;shownHint=true;tray.ShowBalloonTip(2500,"小番茄仍在运行","从任务栏计时条或托盘图标随时回来。",Forms.ToolTipIcon.None);}
  public void Notify(string text){if(Data.Settings.Sound)System.Media.SystemSounds.Asterisk.Play();if(Data.Settings.Notifications&&tray!=null)tray.ShowBalloonTip(6000,"小番茄",text,Forms.ToolTipIcon.None);}
  public void Exit(){if(Quitting)return;if(Notes!=null&&!Notes.Flush())return;Engine.Pause();if(!Save()){ShowMain();return;}Quitting=true;if(Notes!=null)Notes.CloseAll();if(timer!=null)timer.Stop();Native.UnregisterHotKey(hwnd,1);Native.UnregisterHotKey(hwnd,2);Native.UnregisterHotKey(hwnd,3);if(Bar!=null){Bar.Detach();Bar.Close();Bar.Dispose();}if(trayClickTimer!=null)trayClickTimer.Dispose();if(tray!=null){tray.Visible=false;tray.Dispose();}if(Mini!=null)Mini.Close();Native.RemoveProp(hwnd,instanceTag);Main.Close();application.Shutdown();}
  public void ApplyTheme(){bool dark=Data.Settings.Theme=="dark" || Data.Settings.Theme=="system"&&!Native.AppsLight;string[] keys={"Bg","Card","Ink","Muted","Line","Soft","Accent","Tint","Green","OnAccent"};string[] light={"#F6F5F2","#FFFFFF","#272B29","#69716A","#E7E8E2","#F0F1EC","#C45146","#FBEDE9","#63866A","#FFFFFF"};string[] night={"#1B1E1D","#242826","#F0F1EB","#A1A99F","#353C36","#303631","#E78778","#3E302C","#91B594","#1B1E1D"};for(int i=0;i<keys.Length;i++)application.Resources[keys[i]]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark?night[i]:light[i]));if(Notes!=null)Notes.RefreshTheme();if(Main!=null){int value=dark?1:0;Native.DwmSetWindowAttribute(new WindowInteropHelper(Main).Handle,20,ref value,4);Main.RefreshTimer();}}
  public void Backup(Window owner){Engine.Tick();if(!Save())return;var d=new SaveFileDialog {Title="备份小番茄数据",Filter="小番茄备份 (*.json)|*.json",FileName="小番茄备份-"+DateTime.Now.ToString("yyyyMMdd-HHmm")+".json"};if(d.ShowDialog(owner)==true){File.WriteAllText(d.FileName,Store.Encode(Data),new UTF8Encoding(false));UI.Notice(owner,"备份已保存。任务、便签、设置与专注记录都包含在内。");}}
  public void Restore(Window owner){var d=new OpenFileDialog {Title="选择小番茄备份",Filter="小番茄备份 (*.json)|*.json"};if(d.ShowDialog(owner)!=true)return;AppData imported;try{var fi=new FileInfo(d.FileName);if(fi.Length>50*1024*1024)throw new IOException("备份文件超过 50 MB。");imported=Store.Normalize(Store.Decode<AppData>(File.ReadAllText(d.FileName,Encoding.UTF8)));}catch(Exception ex){UI.Notice(owner,"无法读取这个备份："+ex.Message);return;}if(MessageBox.Show(owner,"恢复后将替换当前任务、便签、记录和设置。恢复前会自动保留一份当前数据备份。","恢复备份",MessageBoxButton.OKCancel,MessageBoxImage.Question)!=MessageBoxResult.OK)return;if(Notes!=null&&!Notes.CloseAll())return;Engine.Pause();File.WriteAllText(Path.Combine(Store.DirectoryPath,"before-restore-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".json"),Store.Encode(Data),Encoding.UTF8);Engine.Data=imported;RegisterNoteShortcut();ApplyTheme();Engine.Signal();Main.ShowPage("settings");UI.Notice(owner,"备份已恢复。未结束的计时以暂停状态恢复。");}
  public void ExportCsv(Window owner){var d=new SaveFileDialog {Title="导出专注记录",Filter="CSV 表格 (*.csv)|*.csv",FileName="小番茄专注记录-"+DateTime.Now.ToString("yyyyMMdd")+".csv"};if(d.ShowDialog(owner)!=true)return;var sb=new StringBuilder("任务,开始时间,结束时间,实际专注秒数,实际专注分钟,完整番茄\r\n");foreach(var s in Data.Sessions.OrderBy(r=>r.StartUtc))sb.Append(Csv(s.Title)).Append(',').Append(Csv(s.StartUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"))).Append(',').Append(Csv(s.EndUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"))).Append(',').Append(Math.Round(s.Seconds,1).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(Math.Round(s.Seconds/60,2).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(s.Complete?"是":"否").Append("\r\n");File.WriteAllText(d.FileName,sb.ToString(),new UTF8Encoding(true));UI.Notice(owner,"记录已导出，可用 Excel 打开。正在进行的计时会在结束后进入记录。");}
  static string Csv(string value){value=value??"";if(value.Length>0&&"=+-@\t\r".IndexOf(value[0])>=0)value="'"+value;return "\""+value.Replace("\"","\"\"")+"\"";}
  void WriteDiagnostics(){try{var process=Process.GetCurrentProcess();Native.RECT r;Native.GetWindowRect(Bar.Handle,out r);File.WriteAllText(Path.Combine(folder,"diagnostics.txt"),"Mode="+Bar.Mode+"\nEmbedded="+Bar.Embedded+"\nVisible="+Bar.Visible+"\nSurfaceExposed="+Bar.SurfaceExposed+"\nIconPixels="+((BitmapSource)IconSource).PixelWidth+"\nMainVisible="+Main.IsVisible+"\nRemaining="+Engine.Remaining+"\nForegroundClass="+Native.WindowClass(Native.GetForegroundWindow())+"\nBar="+r.Left+","+r.Top+","+r.Width+","+r.Height+"\nPrivateMB="+(process.PrivateMemorySize64/1048576)+"\nWorkingSetMB="+(process.WorkingSet64/1048576)+"\nFramework="+Environment.Version+"\nDpi="+Native.GetDpiForWindow(Bar.Handle));}catch(Exception ex){Log(ex);}}
  static void MakeIcon(string path){int[] sizes={16,24,32,48,64,128,256};var blobs=new System.Collections.Generic.List<byte[]>();foreach(int size in sizes){using(var bmp=new System.Drawing.Bitmap(size,size)){using(var g=System.Drawing.Graphics.FromImage(bmp)){g.Clear(System.Drawing.Color.Transparent);TomatoArt.Draw(g,0,0,size,false);}using(var ms=new MemoryStream()){bmp.Save(ms,System.Drawing.Imaging.ImageFormat.Png);blobs.Add(ms.ToArray());}}}using(var fs=File.Create(path))using(var w=new BinaryWriter(fs)){w.Write((short)0);w.Write((short)1);w.Write((short)sizes.Length);int offset=6+16*sizes.Length;for(int i=0;i<sizes.Length;i++){w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)0);w.Write((byte)0);w.Write((short)1);w.Write((short)32);w.Write(blobs[i].Length);w.Write(offset);offset+=blobs[i].Length;}foreach(var blob in blobs)w.Write(blob);}}
 }
}
