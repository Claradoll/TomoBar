using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace LittleTomato {
 [DataContract] public class Todo {
  [DataMember] public string Id = Guid.NewGuid().ToString("N");
  [DataMember] public string Title = "";
  [DataMember] public int Minutes = 25;
  [DataMember] public int Planned = 4;
  [DataMember] public bool Done;
  [DataMember] public bool Archived;
 }
 [DataContract] public class Session {
  [DataMember] public string Id = Guid.NewGuid().ToString("N");
  [DataMember] public string TaskId;
  [DataMember] public string Title;
  [DataMember] public DateTime StartUtc = DateTime.UtcNow;
  [DataMember] public DateTime EndUtc = DateTime.UtcNow;
  [DataMember(EmitDefaultValue=false)] public bool TimestampRecovered;
  [DataMember] public double Seconds;
  [DataMember] public bool Complete;
  // A session's actual activity is split by local calendar day, including pauses and midnight.
  [DataMember] public Dictionary<string,double> Days = new Dictionary<string,double>();
 }
 [DataContract] public class Preferences {
  [DataMember] public int Focus = 25;
  [DataMember] public int ShortBreak = 5;
  [DataMember] public int LongBreak = 15;
  [DataMember] public int Rounds = 4;
  [DataMember] public string Theme = "system";
  [DataMember] public bool Sound = true;
  [DataMember] public bool Notifications = true;
  [DataMember] public bool PauseOnLock = true;
  [DataMember] public bool ShowBar = true;
  [DataMember] public bool Locked = true;
  [DataMember] public bool Embed = true;
  [DataMember] public int Offset = -1;
 }
 [DataContract] public class ActiveTimer {
  [DataMember] public string TaskId;
  [DataMember] public string Kind = "focus";
  [DataMember] public int Duration;
  [DataMember] public double Elapsed;
  [DataMember] public bool Running;
  [DataMember] public DateTime StartUtc = DateTime.UtcNow;
  [DataMember(EmitDefaultValue=false)] public bool TimestampRecovered;
  [DataMember] public Dictionary<string,double> Days = new Dictionary<string,double>();
 }
 [DataContract] public class AppData {
  [DataMember] public int Version = 1;
  [DataMember] public List<Todo> Tasks = new List<Todo>();
  [DataMember] public List<Session> Sessions = new List<Session>();
  [DataMember] public Preferences Settings = new Preferences();
  [DataMember] public ActiveTimer Active;
  [DataMember] public string Selected;
  [DataMember] public int Cycle;
 }
 public class Store {
  public string DirectoryPath, FilePath, LoadNotice;
  public Store(string folder) { DirectoryPath = folder; FilePath = Path.Combine(folder,"data.json"); Directory.CreateDirectory(folder); }
  public static T Decode<T>(string json) { using(var ms = new MemoryStream(Encoding.UTF8.GetBytes(json))) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(ms); }
  public static string Encode<T>(T value) { var data=value as AppData;if(data!=null)RepairTimestamps(data,DateTime.UtcNow);using(var ms = new MemoryStream()) { new DataContractJsonSerializer(typeof(T)).WriteObject(ms,value); return Encoding.UTF8.GetString(ms.ToArray()); } }
  static bool HasTimestamp(DateTime value){return value.Ticks>=TimeSpan.TicksPerDay&&value.Ticks<=DateTime.MaxValue.Ticks-TimeSpan.TicksPerDay;}
  static DateTime AsUtc(DateTime value){return value.Kind==DateTimeKind.Unspecified?DateTime.SpecifyKind(value,DateTimeKind.Utc):value.ToUniversalTime();}
  static DateTime EstimateStart(DateTime end,double seconds){double duration=Double.IsNaN(seconds)||Double.IsInfinity(seconds)?0:Math.Max(0,Math.Min(seconds,366*86400));long ticks=Math.Min(Math.Max(0,end.Ticks-TimeSpan.TicksPerDay),(long)(duration*TimeSpan.TicksPerSecond));return end.AddTicks(-ticks);}
  // Date fields are UTC by schema. Missing/boundary values must never be sent to the JSON serializer.
  // Recovered dates are estimates, explicitly marked; elapsed time, task IDs and history remain intact.
  public static int RepairTimestamps(AppData data,DateTime referenceUtc){
   int repaired=0;referenceUtc=HasTimestamp(referenceUtc)?AsUtc(referenceUtc):DateTime.UtcNow;
   if(data.Sessions!=null)foreach(var s in data.Sessions){if(s==null)continue;bool recovered=false;if(!HasTimestamp(s.EndUtc)){s.EndUtc=referenceUtc;recovered=true;}else s.EndUtc=AsUtc(s.EndUtc);if(!HasTimestamp(s.StartUtc)){s.StartUtc=EstimateStart(s.EndUtc,s.Seconds);recovered=true;}else s.StartUtc=AsUtc(s.StartUtc);if(recovered){s.TimestampRecovered=true;repaired++;}}
   if(data.Active!=null){var a=data.Active;if(!HasTimestamp(a.StartUtc)){a.StartUtc=EstimateStart(referenceUtc,a.Elapsed);a.TimestampRecovered=true;repaired++;}else a.StartUtc=AsUtc(a.StartUtc);}
   return repaired;
  }
  AppData LoadDocument(string path){var data=Decode<AppData>(File.ReadAllText(path,Encoding.UTF8));if(data==null)throw new InvalidDataException("数据为空");int repaired=RepairTimestamps(data,File.GetLastWriteTimeUtc(path));data=Normalize(data);if(repaired>0)LoadNotice="已修复缺失的时间字段，专注时长已保留；恢复的日期为估算值。";return data;}
  public AppData Load() {
   if(!File.Exists(FilePath)) return new AppData();
   try { return LoadDocument(FilePath); }
   catch {
    try { File.Copy(FilePath,FilePath+".damaged-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"),true); } catch {}
    try { var d = LoadDocument(FilePath+".bak"); LoadNotice="已从本地备份恢复数据。"; return d; }
    catch { throw new IOException("数据文件与备份均无法读取。原文件已保留，请通过数据目录检查恢复。"); }
   }
  }
  public static AppData Normalize(AppData d) {
   if(d==null || d.Version!=1 || d.Tasks==null || d.Sessions==null || d.Settings==null) throw new InvalidDataException("不支持的数据格式");
   var p=d.Settings;
   p.Focus=Math.Max(1,Math.Min(180,p.Focus)); p.ShortBreak=Math.Max(1,Math.Min(60,p.ShortBreak)); p.LongBreak=Math.Max(1,Math.Min(120,p.LongBreak)); p.Rounds=Math.Max(1,Math.Min(12,p.Rounds));
   p.Offset=Math.Max(-1,p.Offset);
   if(p.Theme!="light" && p.Theme!="dark") p.Theme="system";
   var ids=new HashSet<string>();
   foreach(var t in d.Tasks) { if(t==null || String.IsNullOrEmpty(t.Id) || !ids.Add(t.Id) || String.IsNullOrWhiteSpace(t.Title)) throw new InvalidDataException("任务数据无效"); t.Minutes=Math.Max(1,Math.Min(180,t.Minutes)); t.Planned=Math.Max(1,Math.Min(99,t.Planned)); }
   foreach(var s in d.Sessions) { if(s==null || Double.IsNaN(s.Seconds) || Double.IsInfinity(s.Seconds) || s.Seconds<0) throw new InvalidDataException("记录无效"); if(s.Days==null) s.Days=new Dictionary<string,double>(); }
   if(d.Active!=null) { var a=d.Active; if(!ids.Contains(a.TaskId) || a.Duration<1 || a.Duration>10800 || a.Elapsed<0 || Double.IsNaN(a.Elapsed) || Double.IsInfinity(a.Elapsed) || (a.Kind!="focus" && a.Kind!="short" && a.Kind!="long")) d.Active=null; else { a.Elapsed=Math.Min(a.Elapsed,a.Duration); a.Running=false; if(a.Days==null) a.Days=new Dictionary<string,double>(); } }
   RepairTimestamps(d,DateTime.UtcNow);return d;
  }
  public void Save(AppData data) {
   var bytes=Encoding.UTF8.GetBytes(Encode(data));
   var tmp=FilePath+".tmp";
   using(var fs=new FileStream(tmp,FileMode.Create,FileAccess.Write,FileShare.None)) { fs.Write(bytes,0,bytes.Length); fs.Flush(true); }
   if(File.Exists(FilePath)) File.Replace(tmp,FilePath,FilePath+".bak",true); else File.Move(tmp,FilePath);
  }
 }
 public interface ITimerClock { double Seconds {get;} DateTime UtcNow {get;} }
 public class TimerClock : ITimerClock {
  [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern bool QueryUnbiasedInterruptTime(out ulong value);
  public double Seconds { get { ulong value;if(!QueryUnbiasedInterruptTime(out value))throw new InvalidOperationException("无法读取系统专注时钟。");return value/10000000.0; } }
  public DateTime UtcNow { get { return DateTime.UtcNow; } }
 }
 public class Engine {
  public const string DefaultTaskTitle = "自由专注";
  public AppData Data;
  ITimerClock clock;
  double last;
  public event Action Changed;
  public event Action<string> Finished;
  public Engine(AppData data,ITimerClock source) { Data=data;clock=source;Store.RepairTimestamps(Data,clock.UtcNow);last=clock.Seconds; if(Data.Active!=null) Data.Active.Running=false; }
  public Todo Selected { get { return Data.Tasks.FirstOrDefault(t=>t.Id==Data.Selected && !t.Archived && !t.Done); } }
  public Todo Current { get { return Data.Active==null ? Selected : Data.Tasks.FirstOrDefault(t=>t.Id==Data.Active.TaskId); } }
  public int Remaining { get { return Data.Active==null ? (Selected==null ? Data.Settings.Focus:Selected.Minutes)*60 : (int)Math.Ceiling(Math.Max(0,Data.Active.Duration-Data.Active.Elapsed)); } }
  public string DisplayTime { get { int s=Remaining;return String.Format("{0:00}:{1:00}",s/60,s%60); } }
  public string Phase { get { return Data.Active==null ? "准备专注" : Data.Active.Kind=="focus" ? "专注时间" : Data.Active.Kind=="long" ? "长休息" : "短休息"; } }
  public void Signal() { if(Changed!=null)Changed(); }
  public void Select(Todo t) { if(Data.Active!=null && Data.Active.TaskId!=t.Id) End(); Data.Selected=t.Id; Signal(); }
  public void Start(Todo t) {
   if(t==null || t.Done || t.Archived)return;
   if(Data.Active!=null && Data.Active.TaskId==t.Id && Data.Active.Kind=="focus") { Resume();return; }
   if(Data.Active!=null)End();
   Data.Selected=t.Id;Data.Active=new ActiveTimer {TaskId=t.Id,Duration=t.Minutes*60,StartUtc=clock.UtcNow,Running=true};last=clock.Seconds;Signal();
  }
  public Todo EnsureFocusTask() {
   var task=Selected??Data.Tasks.FirstOrDefault(t=>!t.Done&&!t.Archived);
   if(task==null){task=new Todo {Title=DefaultTaskTitle,Minutes=Data.Settings.Focus};Data.Tasks.Add(task);}
   Data.Selected=task.Id;return task;
  }
  public void Resume() { if(Data.Active==null) { Start(Selected);return; } if(!Data.Active.Running) { Data.Active.Running=true;last=clock.Seconds;Signal(); } }
  public void Pause() { Tick(); if(Data.Active!=null)Data.Active.Running=false;Signal(); }
  public void Toggle() { if(Data.Active!=null && Data.Active.Running)Pause();else Resume(); }
  public void Tick() {
   double now=clock.Seconds, delta=Math.Max(0,now-last);last=now;
   var a=Data.Active;if(a==null || !a.Running)return;
   // A long message-loop gap may be suspend/resume: never count unobserved offline hours.
   if(delta>30) { a.Running=false;Signal();return; }
   double add=Math.Min(delta,Math.Max(0,a.Duration-a.Elapsed));a.Elapsed+=add;
   if(a.Kind=="focus" && add>0) SplitDays(a.Days,clock.UtcNow,add);
   if(a.Elapsed>=a.Duration)Complete();
  }
  public static void SplitDays(Dictionary<string,double> days,DateTime endUtc,double seconds) {
   DateTime cursor=endUtc.AddTicks(-(long)Math.Round(seconds*TimeSpan.TicksPerSecond));double left=seconds;
   while(cursor<endUtc) { DateTime local=cursor.ToLocalTime();DateTime next=local.Date.AddDays(1).ToUniversalTime(); DateTime end=next<endUtc ? next:endUtc; if(end<=cursor)end=endUtc; string day=local.ToString("yyyy-MM-dd");double value;days.TryGetValue(day,out value);double add=end==endUtc?left:Math.Min(left,(end-cursor).TotalSeconds);days[day]=value+add;left-=add;cursor=end; }
  }
  void Record(bool complete) {
   var a=Data.Active;if(a==null || a.Kind!="focus" || a.Elapsed<=0)return;
   var t=Current;Data.Sessions.Add(new Session {TaskId=a.TaskId,Title=t==null?"已归档任务":t.Title,StartUtc=a.StartUtc,EndUtc=clock.UtcNow,Seconds=a.Elapsed,Complete=complete,TimestampRecovered=a.TimestampRecovered,Days=new Dictionary<string,double>(a.Days)});
  }
  void Complete() {
   var a=Data.Active;string message;
   if(a.Kind=="focus") { Record(true);Data.Cycle++;bool longer=Data.Cycle%Data.Settings.Rounds==0;Data.Active=new ActiveTimer {TaskId=a.TaskId,Kind=longer?"long":"short",Duration=(longer?Data.Settings.LongBreak:Data.Settings.ShortBreak)*60,StartUtc=clock.UtcNow};message="完成一个番茄，休息一下吧。"; }
   else { Data.Active=null;message="休息结束，准备好再开始下一轮。"; }
   Signal();if(Finished!=null)Finished(message);
  }
  public void End() { Tick();Record(false);Data.Active=null;Signal(); }
  public void MarkDone(Todo t) { if(Data.Active!=null && Data.Active.TaskId==t.Id)End(); t.Done=!t.Done; if(t.Done && Data.Selected==t.Id)Data.Selected=null;Signal(); }
  public double Total(string taskId) { double s=Data.Sessions.Where(r=>r.TaskId==taskId).Sum(r=>r.Seconds);var a=Data.Active;return s+(a!=null && a.Kind=="focus" && a.TaskId==taskId?a.Elapsed:0); }
  public int Count(string taskId) { return Data.Sessions.Count(r=>r.TaskId==taskId && r.Complete); }
  public double DayTotal(DateTime day) { string key=day.ToString("yyyy-MM-dd");double total=0,v;foreach(var s in Data.Sessions) { if(s.Days.Count==0) { if(s.StartUtc.ToLocalTime().Date==day.Date)total+=s.Seconds; } else if(s.Days.TryGetValue(key,out v))total+=v; }if(Data.Active!=null && Data.Active.Kind=="focus" && Data.Active.Days.TryGetValue(key,out v))total+=v;return total; }
  public static string Duration(double seconds) { if(seconds<60)return ((int)seconds)+" 秒";if(seconds<3600)return ((int)(seconds/60))+" 分钟";return ((int)(seconds/3600))+" 小时 "+((int)(seconds%3600/60))+" 分钟"; }
 }
}
