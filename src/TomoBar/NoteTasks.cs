using System;
using System.Collections.Generic;
using System.Linq;

namespace LittleTomato {
 // Stable paragraph IDs keep time records attached when text or indentation changes.
 public static class NoteTasks {
  public static string Text(NoteBlock b){return String.Concat(b.Runs.Select(r=>r.Text)).Trim();}
  public static Todo Group(AppData d,Note n){return d.Tasks.FirstOrDefault(t=>t.Id==n.GroupId&&t.IsGroup);}
  public static Todo Linked(AppData d,Note n,string blockId){return d.Tasks.FirstOrDefault(t=>t.SourceNoteId==n.Id&&t.SourceBlockId==blockId);}
  public static List<Todo> Branch(AppData d,Todo root){var result=new List<Todo>();var seen=new HashSet<string>();Walk(d,root,result,seen);return result;}
  static void Walk(AppData d,Todo root,List<Todo> result,HashSet<string> seen){if(root==null||!seen.Add(root.Id))return;result.Add(root);foreach(var child in d.Tasks.Where(t=>t.ParentId==root.Id).OrderBy(t=>t.NoteOrder))Walk(d,child,result,seen);}
  public static Todo Next(AppData d,Todo task){if(task==null||!task.IsGroup)return task;return Branch(d,task).FirstOrDefault(t=>!t.IsGroup&&!t.Done&&!t.Archived);}
  public static double Total(Engine e,Todo task){return Branch(e.Data,task).Sum(t=>e.Total(t.Id));}
  public static int Count(Engine e,Todo task){return Branch(e.Data,task).Sum(t=>e.Count(t.Id));}
  public static Todo Convert(AppData data,Note note,NoteDocument doc){
   if(!doc.Blocks.Any(b=>b.Kind=="number"&&!String.IsNullOrWhiteSpace(Text(b))))return null;
   var group=Group(data,note);
   if(group==null){group=data.Tasks.FirstOrDefault(t=>t.Id==note.TaskId&&t.ParentId==null&&!t.IsGroup&&t.SourceNoteId==null);if(group==null){group=new Todo{Minutes=data.Settings.Focus,Planned=data.Settings.Rounds};data.Tasks.Add(group);}group.IsGroup=true;group.SourceNoteId=note.Id;note.GroupId=group.Id;note.TaskId=group.Id;}
   group.Title=note.DisplayTitle;group.Done=false;group.Archived=false;
   var parents=new List<Tuple<int,Todo>>();int order=0;
   foreach(var b in doc.Blocks){
    if(b.Kind!="number"){if(b.Indent==0)parents.Clear();continue;}if(String.IsNullOrWhiteSpace(Text(b)))continue;
    while(parents.Count>0&&parents.Last().Item1>=b.Indent)parents.RemoveAt(parents.Count-1);
    var task=Linked(data,note,b.Id);if(task==null){task=new Todo{Title=Text(b),Minutes=data.Settings.Focus,Planned=1,SourceNoteId=note.Id,SourceBlockId=b.Id};data.Tasks.Add(task);}
    task.Title=Text(b);task.ParentId=parents.Count==0?group.Id:parents.Last().Item2.Id;task.NoteOrder=order++;
    // Completing a pomodoro is separate from completing the task itself.
    if(b.Checklist||b.Kind=="check")task.Done=b.Done;
    parents.Add(Tuple.Create(b.Indent,task));
   }
   // Removed lines retain their task and history under the group. Reappearing IDs reconnect on undo.
   note.Body=Store.Encode(doc);note.PlainText=doc.PlainText;return group;
  }
  public static bool SyncExisting(Engine engine,Note note,NoteDocument doc){
   var group=Group(engine.Data,note);if(group==null)return false;bool changed=false;
   if(group.Title!=note.DisplayTitle){group.Title=note.DisplayTitle;changed=true;}
   foreach(var b in doc.Blocks){var task=Linked(engine.Data,note,b.Id);if(task==null)continue;string title=Text(b);if(title.Length>0&&task.Title!=title){task.Title=title;changed=true;}
    if((b.Checklist||b.Kind=="check")&&task.Done!=b.Done){if(b.Done&&engine.Data.Active!=null&&engine.Data.Active.TaskId==task.Id)engine.End();task.Done=b.Done;if(task.Done&&engine.Data.Selected==task.Id)engine.Data.Selected=null;changed=true;}
   }return changed;
  }
  public static void WriteCompletion(AppData data,Todo task){
   var note=data.Notes.FirstOrDefault(n=>n.Id==task.SourceNoteId&&!n.Deleted);if(note==null||String.IsNullOrEmpty(task.SourceBlockId))return;
   NoteDocument doc;try{doc=NoteCodec.Decode(note.Body);}catch{return;}var b=doc.Blocks.FirstOrDefault(p=>p.Id==task.SourceBlockId);if(b==null)return;
   b.Checklist=true;b.Done=task.Done;if(b.Kind=="body")b.Kind="check";if(b.Kind!="check")doc.Version=2;note.Body=Store.Encode(doc);note.PlainText=doc.PlainText;note.Updated=DateTime.UtcNow.Ticks;
  }
 }
}
