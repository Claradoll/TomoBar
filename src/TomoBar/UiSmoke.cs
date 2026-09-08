using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
namespace LittleTomato {
 // Integration tests execute inside our own app; never touch real user data or other apps.
 public static class UiSmoke {
  static IEnumerable<DependencyObject> Descendants(DependencyObject root){for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var nested in Descendants(child))yield return nested;}}
  static void Capture(Window w,string path){if(!w.IsVisible)throw new InvalidOperationException("Cannot capture a hidden window: "+path);w.UpdateLayout();var bmp=new RenderTargetBitmap((int)Math.Ceiling(w.ActualWidth),(int)Math.Ceiling(w.ActualHeight),96,96,PixelFormats.Pbgra32);bmp.Render(w);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using(var f=File.Create(path))encoder.Save(f);}
  static void Mouse(TaskbarStrip bar,string name,int clicks){typeof(TaskbarStrip).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(bar,new object[]{new Forms.MouseEventArgs(Forms.MouseButtons.Left,clicks,40,15,0)});}
  [System.Runtime.InteropServices.DllImport("user32.dll")]
  static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
  static void Middle(TaskbarStrip bar,uint message=0){
   if(message==0){Middle(bar,0x207);Middle(bar,0x208);return;}
   SendMessage(bar.Handle,message,new IntPtr(message==0x208?0:0x10),new IntPtr(40|(15<<16)));
  }
  static void CheckMiddleClick(Program app,Action<bool,string> check){
   app.Main.Hide();app.Mini.Hide();app.Engine.End();app.Data.Settings.Locked=true;
   Middle(app.Bar);check(app.Data.Active==null,"middle click while stopped does not start focus");
   app.Engine.Start(app.Engine.EnsureFocusTask());var focus=app.Data.Active;
   Middle(app.Bar);check(Object.ReferenceEquals(focus,app.Data.Active)&&focus.Running,"middle click preserves running focus");
   app.Engine.Pause();Middle(app.Bar);check(Object.ReferenceEquals(focus,app.Data.Active)&&!focus.Running,"middle click preserves paused focus");
   app.Engine.End();int history=app.Data.Sessions.Count,cycle=app.Data.Cycle;double total=app.Engine.Total(app.Data.Selected);
   foreach(string kind in new[]{"short","long"})foreach(bool running in new[]{true,false}){
    app.Data.Active=new ActiveTimer {TaskId=app.Data.Selected,Kind=kind,Duration=kind=="short"?300:900,Running=running,StartUtc=DateTime.UtcNow};app.Engine.Signal();
    check(app.Bar.AccessibleDescription.Contains("中键跳过休息"),kind+" rest exposes middle-click hint");
    Middle(app.Bar);check(app.Data.Active==null,kind+(running?" running":" paused")+" rest skipped by native middle-button messages");
   }
   check(app.Data.Sessions.Count==history&&app.Data.Cycle==cycle&&app.Engine.Total(app.Data.Selected)==total,"skipping rest preserves focus statistics and cycle");
   check(app.Store.Load().Active==null,"middle-click skip persisted immediately");
   check(!app.Main.IsVisible&&!app.Mini.IsVisible&&!app.Bar.AccessibleDescription.Contains("中键跳过休息"),"skip leaves windows hidden and clears rest hint");
   var rest=new ActiveTimer {TaskId=app.Data.Selected,Kind="short",Duration=300,StartUtc=DateTime.UtcNow};app.Data.Active=rest;app.Engine.Signal();
   SendMessage(app.Bar.Handle,0x20A,new IntPtr(120<<16),IntPtr.Zero);check(Object.ReferenceEquals(rest,app.Data.Active),"wheel scrolling does not skip rest");
   Middle(app.Bar,0x207);SendMessage(app.Bar.Handle,0x208,IntPtr.Zero,new IntPtr((app.Bar.Width+20)|(15<<16)));check(Object.ReferenceEquals(rest,app.Data.Active),"middle release outside strip cancels skip");
   Middle(app.Bar,0x207);app.Engine.Start(app.Engine.Current);focus=app.Data.Active;Middle(app.Bar,0x208);check(Object.ReferenceEquals(focus,app.Data.Active)&&focus.Running,"rest-to-focus transition during gesture cannot end focus");
   Middle(app.Bar,0x207);app.Data.Active=rest;app.Engine.Signal();Middle(app.Bar,0x208);check(Object.ReferenceEquals(rest,app.Data.Active),"focus-to-rest transition during gesture cannot skip new rest");
   typeof(TaskbarStrip).GetField("moved",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(app.Bar,true);
   Middle(app.Bar);check(app.Data.Active==null,"middle skip works after a previous position drag");
   Middle(app.Bar,0x209);Middle(app.Bar,0x208);check(app.Data.Active==null,"middle double-click does not start another phase");
   app.Data.Active=rest;app.Engine.Signal();Mouse(app.Bar,"OnMouseDown",1);Mouse(app.Bar,"OnMouseUp",1);Middle(app.Bar);
  }
  public static void Start(Program app,string folder){
   var report=new List<string>();int stage=0;var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(Math.Max(900,Forms.SystemInformation.DoubleClickTime+200))};Action<bool,string> check=(ok,msg)=>{report.Add((ok?"PASS ":"FAIL ")+msg);if(!ok)Environment.ExitCode=1;};
   timer.Tick+=(s,e)=>{try{switch(stage++){
    case 0:
     if(Environment.GetCommandLineArgs().Contains("--background"))check(!app.Main.IsVisible&&!app.Mini.IsVisible,"background launch leaves main and card hidden");app.ShowMain();
     app.Engine.End();app.Engine.Data=new AppData();app.Data.Settings.Notifications=false;app.Data.Settings.Sound=false;app.Data.Settings.Theme="light";
     app.Data.Tasks.Add(new Todo{Title="整理项目设计思路",Minutes=25,Planned=4});app.Data.Tasks.Add(new Todo{Title="阅读 · 留一点时间给自己",Minutes=40,Planned=2});app.Data.Tasks.Add(new Todo{Title="完成今天的学习笔记",Minutes=25,Planned=3});
     for(int i=0;i<7;i++){double seconds=new[]{2400,3600,1500,4500,3000,1800,1500}[i];var start=DateTime.Today.AddDays(i-6).AddHours(9).ToUniversalTime();app.Data.Sessions.Add(new Session{TaskId=app.Data.Tasks[i%3].Id,Title=app.Data.Tasks[i%3].Title,StartUtc=start,EndUtc=start.AddSeconds(seconds),Seconds=seconds,Complete=true,Days=new Dictionary<string,double>{{start.ToLocalTime().ToString("yyyy-MM-dd"),seconds}}});}
     app.ApplyTheme();app.Engine.Start(app.Data.Tasks[0]);app.Data.Active.Elapsed=361;app.Data.Active.Days[DateTime.Today.ToString("yyyy-MM-dd")]=361;app.Main.ShowPage("tasks");app.Main.UpdateLayout();check(app.Main.ActualWidth>=900,"main layout rendered");check(!app.Bar.Embedded&&app.Bar.SurfaceExposed,"taskbar surface is actually exposed to screen hit testing");check(((BitmapSource)app.IconSource).PixelWidth==256,"full resolution 256px app icon loaded");check(Native.IsDesktopClass("Progman")&&Native.IsDesktopClass("WorkerW")&&!Native.IsDesktopClass("ApplicationFrameWindow"),"desktop excluded from fullscreen detection");Capture(app.Main,Path.Combine(folder,"01-main-light.png"));
     Mouse(app.Bar,"OnMouseDown",1);Mouse(app.Bar,"OnMouseUp",1);break;
    case 1:
     check(!app.Data.Active.Running&&!app.Mini.IsVisible,"single click pauses without opening card");app.Main.Hide();Mouse(app.Bar,"OnMouseDown",1);Mouse(app.Bar,"OnMouseUp",1);Mouse(app.Bar,"OnMouseDown",2);Mouse(app.Bar,"OnMouseDoubleClick",2);Mouse(app.Bar,"OnMouseUp",1);break;
    case 2:
     check(!app.Main.IsVisible&&app.Mini.IsVisible&&!app.Data.Active.Running,"double click opens card without toggling paused timer or main");Capture(app.Mini,Path.Combine(folder,"02-mini-light.png"));app.Data.Settings.Embed=false;app.Bar.RefreshPlacement();check(!app.Bar.Embedded&&Native.GetAncestor(app.Bar.Handle,2)==app.Bar.Handle&&(Native.GetWindowLongPtr(app.Bar.Handle,-16).ToInt64()&0x40000000L)==0,"fallback is a detached top-level window");app.Data.Settings.Offset=220;app.Bar.RefreshPlacement();Native.RECT br,tr;Native.GetWindowRect(app.Bar.Handle,out br);Native.GetWindowRect(Native.FindWindow("Shell_TrayWnd",null),out tr);check(Math.Abs((br.Left-tr.Left)-220*Native.GetDpiForWindow(app.Bar.Handle)/96.0)<3,"position setting applies with DPI");using(var b=new System.Drawing.Bitmap(app.Bar.Width,app.Bar.Height)){app.Bar.DrawToBitmap(b,new System.Drawing.Rectangle(0,0,b.Width,b.Height));b.Save(Path.Combine(folder,"03-strip.png"),System.Drawing.Imaging.ImageFormat.Png);}app.Data.Settings.Embed=true;app.Data.Settings.Offset=-1;app.Bar.RefreshPlacement();check(!app.Bar.Embedded&&app.Bar.SurfaceExposed,"legacy embed preference still uses exposed overlay");Native.SetWindowPos(app.Bar.Handle,new IntPtr(-2),0,0,0,0,0x13);app.Bar.RefreshPlacement();check((Native.GetWindowLongPtr(app.Bar.Handle,-20).ToInt64()&8)!=0&&app.Bar.SurfaceExposed,"topmost restored even without moving strip");Mouse(app.Bar,"OnMouseDown",1);Mouse(app.Bar,"OnMouseUp",1);break;
    case 3:
     check(app.Data.Active.Running&&!app.Main.IsVisible&&!app.Mini.IsVisible,"single click resumes and collapses card");Mouse(app.Bar,"OnMouseDown",1);Mouse(app.Bar,"OnMouseUp",1);Mouse(app.Bar,"OnMouseDown",2);Mouse(app.Bar,"OnMouseDoubleClick",2);Mouse(app.Bar,"OnMouseUp",1);
     app.Data.Settings.Theme="dark";app.ApplyTheme();app.Main.ShowPage("tasks");break;
    case 4:
     check(app.Mini.IsVisible&&app.Data.Active.Running,"double click preserves running timer after click interval");app.ShowMain();app.Main.ShowPage("tasks");Capture(app.Main,Path.Combine(folder,"04-main-dark.png"));check(((SolidColorBrush)app.Main.Background).Color.R<60,"dark theme applied to native window");app.Main.ShowPage("stats");Capture(app.Main,Path.Combine(folder,"05-stats-dark.png"));app.Main.ShowPage("settings");Capture(app.Main,Path.Combine(folder,"06-settings-dark.png"));app.Main.ShowPage("tasks");app.Main.EditTask(null);Capture(app.Main,Path.Combine(folder,"07-editor-dark.png"));app.Engine.Pause();app.Main.CloseEditor();app.Main.RefreshTimer();Capture(app.Main,Path.Combine(folder,"08-main-paused.png"));check(Descendants(app.Main).OfType<System.Windows.Controls.Button>().Any(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="继续专注"&&b.Content is System.Windows.Controls.StackPanel),"paused action uses a vector play icon with accessible label");app.Mini.Open();Capture(app.Mini,Path.Combine(folder,"09-mini-paused.png"));app.Mini.Hide();app.Save();var restored=app.Store.Load();check(restored.Active!=null&&!restored.Active.Running,"UI state persisted paused");check(restored.Tasks.Count==3&&restored.Sessions.Count==7,"tasks and session history preserved");app.Data.Settings.Theme="light";app.ApplyTheme();break;
    case 5:
     app.Main.CloseEditor();app.Engine.End();app.Engine.Data=new AppData();app.Data.Settings.Sound=false;app.Data.Settings.Notifications=false;app.ShowMain();Mouse(app.Bar,"OnMouseDown",1);Mouse(app.Bar,"OnMouseUp",1);break;
    case 6:
     check(app.Data.Tasks.Count==1&&app.Engine.Current.Title==Engine.DefaultTaskTitle,"empty list starts default task without name prompt");check(app.Data.Active.Running&&!app.Main.IsVisible&&!app.Mini.IsVisible,"start collapses windows and starts focus");check(app.Bar.Visible&&app.Bar.SurfaceExposed,"taskbar is exposed after quick start hides main");
     check(app.Data.Active.Elapsed>0,"focus continues while main is hidden");check(app.Bar.SurfaceExposed,"taskbar stays exposed after next placement tick");app.ToggleTimer();check(!app.Data.Active.Running,"pause does not restart focus");app.ShowMain();app.ToggleTimer();check(app.Data.Tasks.Count==1&&app.Data.Active.Running&&!app.Main.IsVisible,"resume reuses task and collapses main");
     var other=new Todo{Title="从任务卡片开始",Minutes=40};app.Data.Tasks.Add(other);app.ShowMain();app.StartTask(other);check(app.Data.Active.TaskId==other.Id&&!app.Main.IsVisible,"task card start also collapses main");
     app.Engine.End();app.ShowMain();app.Data.Active=new ActiveTimer{TaskId=other.Id,Kind="short",Duration=300,StartUtc=DateTime.MinValue};app.ToggleTimer();check(app.Data.Active.Running&&app.Main.IsVisible,"starting a break preserves main window");check(app.Data.Active.TimestampRecovered&&app.Data.Active.StartUtc.Kind==DateTimeKind.Utc&&app.Store.Load().Active!=null,"missing break timestamp repaired and saved without an error dialog");break;
    case 7:
     app.Main.EditTask(null);app.Main.UpdateLayout();var inputs=Descendants(app.Main).OfType<System.Windows.Controls.TextBox>();inputs.First(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="任务名称").Text="   ";int previousCount=app.Data.Tasks.Count;Descendants(app.Main).OfType<System.Windows.Controls.Button>().First(b=>String.Equals(b.Content,"保存任务")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));check(app.Data.Tasks.Count==previousCount+1&&app.Data.Tasks.Last().Title==Engine.DefaultTaskTitle,"blank task name saves with default title");break;
    case 8:
     app.Main.CloseEditor();app.Engine.End();app.Main.Hide();Mouse(app.Bar,"OnMouseDown",1);Mouse(app.Bar,"OnMouseUp",1);Mouse(app.Bar,"OnMouseDown",2);Mouse(app.Bar,"OnMouseDoubleClick",2);Mouse(app.Bar,"OnMouseUp",1);break;
    case 9:
     check(app.Data.Active==null&&app.Mini.IsVisible&&!app.Main.IsVisible,"double click while stopped opens card without starting timer");
     app.ShowMain();app.Main.ShowPage("settings");app.Main.UpdateLayout();
     var startup=Descendants(app.Main).OfType<System.Windows.Controls.CheckBox>().First(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="开机自启");check(startup.IsEnabled&&startup.IsChecked==false,"startup setting is visible and defaults off");
     startup.IsChecked=true;startup.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));check(app.Startup.IsRegistered&&app.Startup.UsesCurrentPath,"startup checkbox immediately writes background registration to isolated test key");Capture(app.Main,Path.Combine(folder,"10-startup-settings.png"));
     app.Main.ShowPage("tasks");app.Main.ShowPage("settings");app.Main.UpdateLayout();startup=Descendants(app.Main).OfType<System.Windows.Controls.CheckBox>().First(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="开机自启");check(startup.IsChecked==true,"startup setting reloads registered state");
     startup.IsChecked=false;startup.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));check(!app.Startup.IsRegistered,"startup checkbox removes registration");
     if(app.Startup.KeyPath.StartsWith(@"Software\LittleTomato\Tests\",StringComparison.Ordinal))Microsoft.Win32.Registry.CurrentUser.DeleteSubKey(app.Startup.KeyPath,false);
     app.Main.ShowPage("tasks");app.Main.Height=480;app.Main.Width=760;app.Main.UpdateLayout();
     var focusScroll=Descendants(app.Main).OfType<System.Windows.Controls.ScrollViewer>().First(v=>Descendants(v).OfType<ProgressRing>().Any());check(focusScroll.ScrollableHeight>0,"compact window scrolls full focus card instead of clipping actions");focusScroll.ScrollToBottom();app.Main.UpdateLayout();Capture(app.Main,Path.Combine(folder,"11-compact-window.png"));
     check(Descendants(app.Main).OfType<System.Windows.Controls.Button>().Any(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="我的任务"&&b.Content is System.Windows.Controls.StackPanel),"navigation uses consistent vector icons and accessible labels");
     app.Main.Width=1020;app.Main.Height=750;app.Main.ShowPage("tasks");app.Main.UpdateLayout();Capture(app.Main,Path.Combine(folder,"12-final-light.png"));
     CheckMiddleClick(app,check);break;
    case 10:
     check(app.Data.Active==null&&!app.Main.IsVisible&&!app.Mini.IsVisible,"middle skip cancels pending left click after double-click interval");
     Mouse(app.Bar,"OnMouseDown",1);Mouse(app.Bar,"OnMouseUp",1);break;
    case 11:
     check(app.Data.Active!=null&&app.Data.Active.Kind=="focus"&&app.Data.Active.Running&&!app.Main.IsVisible,"left click starts next focus normally after middle skip");
     File.WriteAllLines(Path.Combine(folder,"ui-results.txt"),report,System.Text.Encoding.UTF8);timer.Stop();app.Exit();break;
   }}catch(Exception ex){Environment.ExitCode=1;report.Add("FAIL "+ex);File.WriteAllLines(Path.Combine(folder,"ui-results.txt"),report);timer.Stop();app.Exit();}};timer.Start();
  }
 }
}
