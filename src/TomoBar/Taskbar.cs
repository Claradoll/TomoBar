using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Forms=System.Windows.Forms;

namespace LittleTomato {
 public static class Native {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; public int Width {get{return Right-Left;}} public int Height {get{return Bottom-Top;}} }
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string cls,string title);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd,out RECT rect);
  [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  delegate bool EnumWindowProc(IntPtr hwnd,IntPtr parameter);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowProc callback,IntPtr parameter);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern bool SetProp(IntPtr hwnd,string name,IntPtr data);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetProp(IntPtr hwnd,string name);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr RemoveProp(IntPtr hwnd,string name);
  public static IntPtr FindInstance(string tag){IntPtr found=IntPtr.Zero;EnumWindows((h,p)=>{if(GetProp(h,tag)==new IntPtr(1)){found=h;return false;}return true;},IntPtr.Zero);return found;}
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,System.Text.StringBuilder name,int length);
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(Point point);
  public static string WindowClass(IntPtr h){var b=new System.Text.StringBuilder(256);GetClassName(h,b,b.Capacity);return b.ToString();}
  public static bool IsDesktopClass(string name){return name=="Progman"||name=="WorkerW"||name=="Shell_TrayWnd"||name=="Shell_SecondaryTrayWnd";}
  [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hwnd,uint flags);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int cx,int cy,uint flags);
  [DllImport("user32.dll",SetLastError=true)] public static extern IntPtr SetParent(IntPtr child,IntPtr parent);
  [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr child);
  [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr h,int n);
  [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr h,int n,IntPtr value);
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetWindowDpiAwarenessContext(IntPtr h);
  [DllImport("user32.dll")] public static extern bool AreDpiAwarenessContextsEqual(IntPtr a,IntPtr b);
  [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h,int id,uint mod,uint key);
  [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h,uint msg,IntPtr w,IntPtr l);
  [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h,int attr,ref int value,int size);
  public static bool RegistryFlag(string path,string name,bool fallback) { try { using(var k=Registry.CurrentUser.OpenSubKey(path)) {var v=k==null?null:k.GetValue(name);return v==null?fallback:Convert.ToInt32(v)!=0;} }catch{return fallback;} }
  public static bool SystemLight {get{return RegistryFlag(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","SystemUsesLightTheme",true);}}
  public static bool AppsLight {get{return RegistryFlag(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",true);}}
 }
 public static class TomatoArt {
  public static GraphicsPath Round(RectangleF r,float radius) {var p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
  public static void Draw(Graphics g,float x,float y,float size,bool rest) {
   Draw(g,x,y,size,rest?Color.FromArgb(116,158,123):Color.FromArgb(222,102,87),Color.FromArgb(85,127,91));
  }
  public static void Draw(Graphics g,float x,float y,float size,Color body,Color leaf) {
   var save=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(size/32,size/32);g.SmoothingMode=SmoothingMode.AntiAlias;
   using(var b=new SolidBrush(body)) {g.FillEllipse(b,3,9,26,21);g.FillEllipse(b,6,7,14,15);}
   using(var b=new SolidBrush(leaf)) {PointF[] leaves={new PointF(16,12),new PointF(7,6),new PointF(14,7),new PointF(16,1),new PointF(19,7),new PointF(26,5),new PointF(22,12),new PointF(17,10)};g.FillPolygon(b,leaves);}
   using(var p=new Pen(Color.FromArgb(115,255,255,255),2))g.DrawArc(p,7,12,13,13,130,55);
   g.Restore(save);
  }
 }
 public static class StripArt {
  static Color Hex(string hex){return ColorTranslator.FromHtml(hex);}
  static float TimeWidth(string text,float scale){float width=0;foreach(char c in text)width+=(c==':'?5.6f:9.6f)*scale;return width;}
  static void DrawTime(Graphics g,string text,RectangleF bounds,float scale,Color ink) {
   // Fixed-width digit cells prevent any horizontal movement as the seconds change.
   float digit=9.6f*scale,colon=5.6f*scale;
   // Anchor the number group beside the tomato, keeping the icon gap constant.
   float x=bounds.Left;
   using(var font=new Font("Segoe UI Semibold",17*scale,FontStyle.Regular,GraphicsUnit.Pixel))
   using(var brush=new SolidBrush(ink))using(var soft=new SolidBrush(Color.FromArgb(165,ink)))
   using(var format=new StringFormat(StringFormat.GenericTypographic){Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.NoWrap}){
    foreach(char c in text){float cell=c==':'?colon:digit;
     if(c==':'){float d=1.7f*scale,cx=x+cell/2-d/2,cy=bounds.Top+bounds.Height/2;g.FillEllipse(soft,cx,cy-3.3f*scale-d/2,d,d);g.FillEllipse(soft,cx,cy+3.3f*scale-d/2,d,d);}
     else g.DrawString(c.ToString(),font,brush,new RectangleF(x,bounds.Top-.4f*scale,cell,bounds.Height),format);
     x+=cell;
    }
   }
  }
  public static void Draw(Graphics g,Size size,float scale,string text,bool active,bool running,bool light,bool hover) {
   int state=!active?0:running?1:2;
   Color accent=Hex(state==1?(light?"#35805A":"#77BF94"):state==2?(light?"#B97828":"#DEA454"):"#242827");
   Color background=Hex(light?"#F4F5F3":"#292D2B");
   Color border=Hex(light?(state==1?"#A3C4AD":state==2?"#D6B889":"#AAAFAC"):(state==1?"#55745F":state==2?"#8C714A":"#737B75"));
   Color ink=Hex(light?"#303A34":"#E6ECE7");
   g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(background);
   float inset=.75f*scale;
   using(var shape=TomatoArt.Round(new RectangleF(inset,inset,size.Width-2*inset,size.Height-2*inset),9*scale-inset))
   using(var pen=new Pen(hover?(state==0?border:accent):border,(hover?1.6f:1.3f)*scale))g.DrawPath(pen,shape);
   // Center the complete visual group, with balanced gaps on both sides of the time.
   float textWidth=text=="开始"?28*scale:TimeWidth(text,scale);
   float groupWidth=(18+8+10+5)*scale+textWidth;
   float groupLeft=(size.Width-groupWidth)/2;
   float textLeft=groupLeft+26*scale;
   TomatoArt.Draw(g,groupLeft-2*scale,5*scale,22*scale,false);
   g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
   if(text!="开始")DrawTime(g,text,new RectangleF(textLeft,0,textWidth,size.Height),scale,ink);
   else using(var font=new Font("Microsoft YaHei UI",13*scale,FontStyle.Regular,GraphicsUnit.Pixel))
   using(var brush=new SolidBrush(ink))using(var format=new StringFormat(StringFormat.GenericTypographic){Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.NoWrap})
    g.DrawString(text,font,brush,new RectangleF(textLeft,0,textWidth,size.Height),format);
   var dot=new RectangleF(textLeft+textWidth+10*scale,size.Height/2f-2.5f*scale,5*scale,5*scale);
   using(var b=new SolidBrush(accent))g.FillEllipse(b,dot);
   // The black stopped dot keeps a fine light rim on a dark taskbar.
   if(state==0&&!light)using(var p=new Pen(Hex("#A6AFA8"),.8f*scale))g.DrawEllipse(p,dot);
  }
 }
 public class TaskbarStrip : Forms.Form {
  Program app; IntPtr taskbar; bool embedded; bool dragging; bool moved,suppressClick; Point down,origin; Forms.Timer clickTimer; Forms.ToolTip tip=new Forms.ToolTip(); string lastText="",lastTip=""; bool lastRest,lastRunning; float scale=1; int regionWidth,regionHeight;
  public string Mode {get{return embedded?"任务栏内嵌":"贴合任务栏";}}
  public bool Embedded {get{return embedded;}}
  bool hovered;
  public bool SurfaceExposed {get {if(!Visible)return false;Native.RECT r;Native.GetWindowRect(Handle,out r);return Native.WindowFromPoint(new Point(r.Left+r.Width/2,r.Top+r.Height/2))==Handle;}}
  protected override bool ShowWithoutActivation {get{return true;}}
  protected override Forms.CreateParams CreateParams {get {var p=base.CreateParams;p.ExStyle|=0x08000000|0x80;return p;}}
  public TaskbarStrip(Program owner) {
   app=owner;Text="小番茄 · 任务栏计时条";FormBorderStyle=Forms.FormBorderStyle.None;ShowInTaskbar=false;StartPosition=Forms.FormStartPosition.Manual;TopMost=true;DoubleBuffered=true;Width=112;Height=32;
   SetStyle(Forms.ControlStyles.OptimizedDoubleBuffer|Forms.ControlStyles.AllPaintingInWmPaint|Forms.ControlStyles.UserPaint,true);
   // Wait for the system double-click interval so opening the card never toggles the timer.
   clickTimer=new Forms.Timer {Interval=Forms.SystemInformation.DoubleClickTime};clickTimer.Tick+=(s,e)=>{clickTimer.Stop();app.ToggleTimer();};
   var menu=new Forms.ContextMenuStrip();menu.Items.Add("打开主界面",null,(s,e)=>app.ShowMain());menu.Items.Add("暂停 / 继续",null,(s,e)=>app.ToggleTimer());menu.Items.Add(new Forms.ToolStripSeparator());
   var locked=new Forms.ToolStripMenuItem("锁定位置");locked.Click+=(s,e)=>{app.Data.Settings.Locked=!app.Data.Settings.Locked;app.Save();};menu.Items.Add(locked);
   menu.Items.Add("恢复默认位置",null,(s,e)=>{app.Data.Settings.Offset=-1;app.Save();RefreshPlacement();});
   menu.Items.Add("设置",null,(s,e)=>{app.ShowMain();app.Main.ShowPage("settings");});menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add("退出小番茄",null,(s,e)=>app.Exit());
   menu.Opening+=(s,e)=>{clickTimer.Stop();locked.Checked=app.Data.Settings.Locked;};ContextMenuStrip=menu;
  }
  protected override void WndProc(ref Forms.Message m) { if(m.Msg==0x21){m.Result=new IntPtr(3);return;}base.WndProc(ref m); }
  protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hovered=true;Invalidate();}
  protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hovered=false;Invalidate();}
  protected override void OnMouseDown(Forms.MouseEventArgs e) {base.OnMouseDown(e);if(e.Button!=Forms.MouseButtons.Left)return;down=Forms.Cursor.Position;Native.RECT r;Native.GetWindowRect(Handle,out r);origin=new Point(r.Left,r.Top);dragging=!app.Data.Settings.Locked;moved=false;if(dragging)Capture=true;}
  protected override void OnMouseMove(Forms.MouseEventArgs e) {base.OnMouseMove(e);if(!dragging || e.Button!=Forms.MouseButtons.Left)return;int dx=Forms.Cursor.Position.X-down.X;if(Math.Abs(dx)>4)moved=true;if(moved) {clickTimer.Stop();Native.RECT r;if(Native.GetWindowRect(taskbar,out r))app.Data.Settings.Offset=Math.Max(0,(int)((origin.X+dx-r.Left)/scale));RefreshPlacement();}}
  protected override void OnMouseUp(Forms.MouseEventArgs e) {base.OnMouseUp(e);dragging=false;Capture=false;if(suppressClick){suppressClick=false;return;}if(moved){app.Save();return;}if(e.Button==Forms.MouseButtons.Left && e.Clicks<2){clickTimer.Stop();clickTimer.Start();}}
  protected override void OnMouseDoubleClick(Forms.MouseEventArgs e) {base.OnMouseDoubleClick(e);if(e.Button==Forms.MouseButtons.Left){suppressClick=true;clickTimer.Stop();app.Mini.Open();}}
  public void Detach() {if(!IsHandleCreated)return;if(embedded){Native.SetParent(Handle,IntPtr.Zero);var st=Native.GetWindowLongPtr(Handle,-16).ToInt64();Native.SetWindowLongPtr(Handle,-16,new IntPtr((st&~0x40000000L)|0x80000000L));embedded=false;} }
  public void RefreshPlacement() {
   if(IsDisposed)return;
   IntPtr found=Native.FindWindow("Shell_TrayWnd",null);
   if(found==IntPtr.Zero || !app.Data.Settings.ShowBar) {if(Visible)Hide();return;}
   if(found!=taskbar) {Detach();taskbar=found;}
   Native.RECT tr;if(!Native.GetWindowRect(taskbar,out tr))return;
   var screen=Forms.Screen.FromHandle(taskbar);bool horizontal=tr.Width>tr.Height;
   float newScale=Math.Max(1,Native.GetDpiForWindow(taskbar)/96f);scale=newScale;
   int w=(int)((app.Engine.Remaining>=6000?128:112)*scale),h=(int)(32*scale);
   bool centered=Native.RegistryFlag(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced","TaskbarAl",true);
   bool widgets=Native.RegistryFlag(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced","TaskbarDa",true);
   // Windows 11's composed taskbar can cover a successfully parented HWND.
   // Keep a separate no-activate topmost surface; never parent into Explorer.
   if(embedded)Detach();
   if(!IsHandleCreated)CreateControl();
   int offset=app.Data.Settings.Offset<0 ? (widgets?184:12):app.Data.Settings.Offset;
   int x=tr.Left+Math.Min(Math.Max((int)(offset*scale),8),Math.Max(8,tr.Width-w-8));
   int y=tr.Top+(tr.Height-h)/2;
   // On a left-aligned or vertical taskbar the safe fallback lives just beside the bar.
   if(!horizontal) {x=tr.Left<=screen.Bounds.Left?tr.Right+8:tr.Left-w-8;y=screen.WorkingArea.Bottom-h-10;}
   else if(!centered) {x=screen.WorkingArea.Left+12;y=tr.Top>screen.Bounds.Top+screen.Bounds.Height/2?tr.Top-h-8:tr.Bottom+8;}
   Native.RECT fg;IntPtr front=Native.GetForegroundWindow();uint pid;Native.GetWindowThreadProcessId(front,out pid);
   bool fullscreen=pid!=(uint)System.Diagnostics.Process.GetCurrentProcess().Id && !Native.IsDesktopClass(Native.WindowClass(front)) && Native.GetWindowRect(front,out fg) && fg.Left<=screen.Bounds.Left && fg.Top<=screen.Bounds.Top && fg.Right>=screen.Bounds.Right && fg.Bottom>=screen.Bounds.Bottom;
   var visibleBar=Rectangle.Intersect(new Rectangle(tr.Left,tr.Top,tr.Width,tr.Height),screen.Bounds);
   bool hide=fullscreen || !Native.IsWindowVisible(taskbar) || (horizontal?visibleBar.Height<12:visibleBar.Width<12);
   if(hide) {if(Visible)Hide();return;}
   if(!Visible)Show();
   // Reassert z-order even without a geometry change (Explorer can raise itself).
   Native.SetWindowPos(Handle,new IntPtr(-1),x,y,w,h,0x10|0x40);
   if(regionWidth!=w||regionHeight!=h){using(var path=TomatoArt.Round(new RectangleF(0,0,w,h),9*scale)) {var oldRegion=Region;Region=new Region(path);if(oldRegion!=null)oldRegion.Dispose();}regionWidth=w;regionHeight=h;}
   string txt=app.Engine.Data.Active==null?"开始":app.Engine.DisplayTime;bool rest=app.Data.Active!=null&&app.Data.Active.Kind!="focus",running=app.Data.Active!=null&&app.Data.Active.Running;
   if(txt!=lastText || rest!=lastRest || running!=lastRunning){lastText=txt;lastRest=rest;lastRunning=running;Invalidate();}
   string title=app.Engine.Current==null?"选择一项任务，开始专注":app.Engine.Current.Title;
   string status=app.Data.Active==null?"已停止 · 点击开始":running?"进行中 · "+app.Engine.Phase:"已暂停 · "+app.Engine.Phase;
   string nextTip=title+"\n"+status+"\n单击"+(app.Data.Active==null?"开始":running?"暂停":"继续")+" · 双击打开小卡片";if(nextTip!=lastTip){tip.SetToolTip(this,nextTip);lastTip=nextTip;AccessibleName="小番茄计时条";}AccessibleDescription=status+" · "+txt;
  }
  protected override void OnPaint(Forms.PaintEventArgs e) {
   StripArt.Draw(e.Graphics,ClientSize,scale,lastText,app.Data.Active!=null,lastRunning,Native.SystemLight,hovered);
  }
  protected override void Dispose(bool disposing) {if(disposing){clickTimer.Dispose();tip.Dispose();}base.Dispose(disposing);}
 }
}
