using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.IO;
using System.Windows;

namespace LittleTomato {
 [DataContract] public class Note {
  [DataMember] public string Id=Guid.NewGuid().ToString("N");
  [DataMember] public string Title="";
  [DataMember] public string PlainText="";
  [DataMember] public string Body="";
  [DataMember] public string TaskId;
  [DataMember] public string Color="paper";
  [DataMember] public bool Pinned,Deleted;
  [DataMember] public long Updated=DateTime.UtcNow.Ticks;
  [DataMember] public double Width=380,Height=440;
  public string DisplayTitle {get {var value=String.IsNullOrWhiteSpace(Title)?(PlainText??"").Split('\n').FirstOrDefault(s=>!String.IsNullOrWhiteSpace(s)):Title;return String.IsNullOrWhiteSpace(value)?"未命名便签":value.Trim().Substring(0,Math.Min(value.Trim().Length,80));}}
 }
 [DataContract] public class NoteRun {
  [DataMember] public string Text="";
  [DataMember] public bool Bold,Italic,Underline,Strike,Highlight;
  [DataMember] public string Link;
 }
 [DataContract] public class NoteBlock {
  [DataMember] public string Kind="body";
  [DataMember] public bool Done;
  [DataMember] public int Indent;
  [DataMember] public List<NoteRun> Runs=new List<NoteRun>();
 }
 [DataContract] public class NoteDocument {
  [DataMember] public int Version=1;
  [DataMember] public List<NoteBlock> Blocks=new List<NoteBlock>();
  public string PlainText {get{return String.Join("\n",Blocks.Select(b=>String.Concat(b.Runs.Select(r=>r.Text))));}}
 }
 public static class NoteCodec {
  public static bool SafeLink(string value){Uri uri;return Uri.TryCreate(value,UriKind.Absolute,out uri)&&(uri.Scheme=="http"||uri.Scheme=="https");}
  public static NoteDocument Decode(string text){
   var doc=String.IsNullOrEmpty(text)?new NoteDocument():Store.Decode<NoteDocument>(text);
   if(doc==null||doc.Version!=1||doc.Blocks==null||doc.Blocks.Count>10000)throw new InvalidDataException("便签内容格式无法识别，原始内容已保留。");
   foreach(var b in doc.Blocks){if(b==null||b.Runs==null||!new[]{"body","h1","h2","bullet","number","check"}.Contains(b.Kind))throw new InvalidDataException("便签段落格式无效，原始内容已保留。");b.Indent=Math.Max(0,Math.Min(6,b.Indent));foreach(var r in b.Runs){if(r==null)throw new InvalidDataException("便签文字格式无效。");r.Text=r.Text??"";if(!SafeLink(r.Link))r.Link=null;}}
   if(doc.Blocks.Count==0)doc.Blocks.Add(new NoteBlock());return doc;
  }
  public static void Normalize(AppData data){
   if(data.Notes==null)data.Notes=new List<Note>();var ids=new HashSet<string>();
   foreach(var n in data.Notes){if(n==null||String.IsNullOrEmpty(n.Id)||!ids.Add(n.Id))throw new InvalidDataException("便签标识无效。");n.Title=n.Title??"";n.PlainText=n.PlainText??"";n.Body=n.Body??"";if(!new[]{"paper","sage","rose"}.Contains(n.Color))n.Color="paper";if(n.Updated<TimeSpan.TicksPerDay||n.Updated>DateTime.MaxValue.Ticks-TimeSpan.TicksPerDay)n.Updated=DateTime.UtcNow.Ticks;n.Width=Double.IsNaN(n.Width)||Double.IsInfinity(n.Width)?380:Math.Max(280,Math.Min(1400,n.Width));n.Height=Double.IsNaN(n.Height)||Double.IsInfinity(n.Height)?440:Math.Max(240,Math.Min(1200,n.Height));}
  }
 }
 public class NoteManager {
  readonly Program app;readonly Dictionary<string,NoteWindow> windows=new Dictionary<string,NoteWindow>();
  public NoteManager(Program owner){app=owner;}
  public NoteWindow New(){var n=new Note();app.Data.Notes.Add(n);if(!app.Save()){app.Data.Notes.Remove(n);return null;}Changed();return Open(n);}
  public NoteWindow Open(Note n){
   NoteWindow window;if(windows.TryGetValue(n.Id,out window)){window.Show();window.WindowState=WindowState.Normal;window.Activate();return window;}
   try{var doc=NoteCodec.Decode(n.Body);window=new NoteWindow(app,n,doc);windows[n.Id]=window;window.Closed+=(s,e)=>{windows.Remove(n.Id);Changed();};window.Show();window.Activate();return window;}
   catch(Exception ex){UI.Notice(app.Main,"便签未能打开，原始内容未被覆盖。\n"+ex.Message);return null;}
  }
  public void RefreshTheme(){foreach(var w in windows.Values)w.RefreshTheme();}
  public void Changed(){if(app.Main!=null)app.Main.RefreshNoteList();}
  public bool Flush(){foreach(var w in windows.Values.ToArray())if(!w.Flush())return false;return true;}
  public bool CloseAll(){if(!Flush())return false;foreach(var w in windows.Values.ToArray())w.Close();return windows.Count==0;}
  public void Delete(Note note){NoteWindow w;if(windows.TryGetValue(note.Id,out w)){w.Close();if(windows.ContainsKey(note.Id))return;}note.Deleted=true;if(!app.Save()){note.Deleted=false;return;}Changed();}
  public void Restore(Note note){note.Deleted=false;if(!app.Save())note.Deleted=true;Changed();}
  public void ToTask(Note note){
   var existing=app.Data.Tasks.FirstOrDefault(t=>t.Id==note.TaskId);
   if(existing==null){existing=new Todo{Title=note.DisplayTitle,Minutes=app.Data.Settings.Focus,Planned=app.Data.Settings.Rounds};app.Data.Tasks.Add(existing);note.TaskId=existing.Id;}
   app.Engine.Signal();app.ShowMain();app.Main.ShowPage("tasks");Changed();
  }
 }
}
