using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Shell;
using System.Windows.Automation;

namespace LittleTomato {
 public static class NoteDocking {
  [StructLayout(LayoutKind.Sequential)] struct MonitorInfo {public int Size;public Native.RECT Monitor,Work;public uint Flags;}
  [DllImport("user32.dll")] static extern IntPtr MonitorFromRect(ref Native.RECT rect,uint flags);
  [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
  public static Native.RECT WorkArea(Native.RECT rect){var info=new MonitorInfo{Size=Marshal.SizeOf(typeof(MonitorInfo))};return GetMonitorInfo(MonitorFromRect(ref rect,2),ref info)?info.Work:rect;}
  public static Native.RECT Snap(Native.RECT rect,Native.RECT work,int distance){
   int dx=0,dy=0;int left=work.Left-rect.Left,right=work.Right-rect.Right,top=work.Top-rect.Top,bottom=work.Bottom-rect.Bottom;
   if(Math.Abs(left)<=distance||Math.Abs(right)<=distance)dx=Math.Abs(left)<=Math.Abs(right)?left:right;
   if(Math.Abs(top)<=distance||Math.Abs(bottom)<=distance)dy=Math.Abs(top)<=Math.Abs(bottom)?top:bottom;
   rect.Left+=dx;rect.Right+=dx;rect.Top+=dy;rect.Bottom+=dy;return rect;
  }
  public static Native.RECT Constrain(Native.RECT rect,Native.RECT work){
   int width=Math.Min(rect.Width,work.Width),height=Math.Min(rect.Height,work.Height);
   int x=Math.Max(work.Left,Math.Min(rect.Left,work.Right-width)),y=Math.Max(work.Top,Math.Min(rect.Top,work.Bottom-height));
   return new Native.RECT{Left=x,Top=y,Right=x+width,Bottom=y+height};
  }
  public static Native.RECT AfterResize(Native.RECT before,Native.RECT after,Native.RECT work){
   if(before.Bottom>=work.Bottom-2){int height=after.Height;after.Bottom=work.Bottom;after.Top=after.Bottom-height;}
   if(before.Right>=work.Right-2){int width=after.Width;after.Right=work.Right;after.Left=after.Right-width;}
   return Constrain(after,work);
  }
 }
 public partial class NoteWindow {
  TextBlock headerTitle;Button headerPin;bool resizingFold;double expandedHeight;bool inMoveSize,noteMoved,noteResized;
  public bool IsFolded {get;private set;}
  Grid BuildNoteHeader(){
   var header=UI.Columns(-1,34,34,34);header.Height=36;header.Margin=new Thickness(14,0,4,0);
   headerTitle=UI.Text(Note.DisplayTitle,12,"Ink");headerTitle.FontWeight=FontWeights.Medium;headerTitle.TextTrimming=TextTrimming.CharacterEllipsis;headerTitle.VerticalAlignment=VerticalAlignment.Center;headerTitle.Margin=new Thickness(0,0,8,0);header.Children.Add(headerTitle);
   headerPin=UI.Button("",TogglePin,false);var min=UI.Button("",()=>WindowState=WindowState.Minimized,false);var close=UI.Button("",()=>Close(),false);
   UI.IconLabel(headerPin,"","pin");UI.IconLabel(min,"","minimize");UI.IconLabel(close,"","close");
   AutomationProperties.SetName(min,"最小化便签");AutomationProperties.SetName(close,"关闭便签");min.ToolTip="最小化便签";close.ToolTip="关闭便签，自动保存内容";
   foreach(var button in new[]{headerPin,min,close}){button.Padding=new Thickness(0);button.Background=Brushes.Transparent;var icon=(FrameworkElement)((StackPanel)button.Content).Children[0];icon.Width=13;icon.Height=13;WindowChrome.SetIsHitTestVisibleInChrome(button,true);}
   UI.At(header,headerPin,0,1);UI.At(header,min,0,2);UI.At(header,close,0,3);RefreshHeader();
   WindowChrome.GetWindowChrome(this).CaptionHeight=36;
   SourceInitialized+=(s,e)=>HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(NoteWindowMessage);
   return header;
  }
  public void RefreshHeader(){
   Title="便签 · "+Note.DisplayTitle;if(headerTitle!=null){headerTitle.Text=Note.DisplayTitle;headerTitle.ToolTip=Note.DisplayTitle;}
   if(headerPin!=null){headerPin.SetResourceReference(Control.ForegroundProperty,Note.Pinned?"Accent":"Muted");headerPin.SetResourceReference(Control.BackgroundProperty,Note.Pinned?"Tint":"TransparentNoteButton");headerPin.ToolTip=Note.Pinned?"取消置顶":"置顶便签 · 始终在其他窗口上方";AutomationProperties.SetName(headerPin,Note.Pinned?"取消置顶便签":"置顶便签");}
  }
  public void TogglePin(){Note.Pinned=!Note.Pinned;Topmost=Note.Pinned;if(pinItem!=null)pinItem.IsChecked=Note.Pinned;RefreshHeader();Schedule();}
  public void ToggleFold(){
   if(WindowState==WindowState.Minimized)return;
   var handle=new WindowInteropHelper(this).Handle;var before=new Native.RECT();bool positioned=handle!=IntPtr.Zero&&Native.GetWindowRect(handle,out before);var work=positioned?NoteDocking.WorkArea(before):before;
   resizingFold=true;
   try{if(!IsFolded){expandedHeight=ActualHeight>38?ActualHeight:Note.Height;IsFolded=true;Editor.Visibility=Visibility.Collapsed;MinHeight=38;ResizeMode=ResizeMode.CanMinimize;Height=38;}else{IsFolded=false;ResizeMode=ResizeMode.CanResize;MinHeight=240;Height=Math.Max(240,expandedHeight);Editor.Visibility=Visibility.Visible;}}
   finally{resizingFold=false;}
   UpdateLayout();Native.RECT after;if(positioned&&Native.GetWindowRect(handle,out after))ApplyPlacement(handle,NoteDocking.AfterResize(before,after,work));
  }
  static void ApplyPlacement(IntPtr handle,Native.RECT target){Native.RECT current;if(Native.GetWindowRect(handle,out current)&&(current.Left!=target.Left||current.Top!=target.Top||current.Width!=target.Width||current.Height!=target.Height))Native.SetWindowPos(handle,IntPtr.Zero,target.Left,target.Top,target.Width,target.Height,0x4|0x10);}
  IntPtr NoteWindowMessage(IntPtr handle,int message,IntPtr wp,IntPtr lp,ref bool handled){
   if(message==0xA3&&wp.ToInt32()==2){ToggleFold();handled=true;return IntPtr.Zero;}
   // Avoid maximizing via the system menu or Windows titlebar gestures.
   if(message==0x112&&(wp.ToInt64()&0xFFF0)==0xF030){handled=true;return IntPtr.Zero;}
   if(message==0x231){inMoveSize=true;noteMoved=false;noteResized=false;}
   // Do not rewrite WM_MOVING: Windows can feed the adjusted rectangle into the
   // next small movement, repeatedly cancelling the user's drag away from an edge.
   if(message==0x216&&inMoveSize)noteMoved=true;
   if(message==0x214&&inMoveSize)noteResized=true;
   if(message==0x232){bool moved=inMoveSize&&noteMoved,adjust=inMoveSize&&(noteMoved||noteResized);inMoveSize=false;noteMoved=false;noteResized=false;if(adjust){Native.RECT rect;if(Native.GetWindowRect(handle,out rect)){var area=NoteDocking.WorkArea(rect);var target=moved?NoteDocking.Snap(rect,area,(int)Math.Round(12*Math.Max(1,Native.GetDpiForWindow(handle)/96.0))):rect;ApplyPlacement(handle,NoteDocking.Constrain(target,area));}}}
   return IntPtr.Zero;
  }
 }
}
