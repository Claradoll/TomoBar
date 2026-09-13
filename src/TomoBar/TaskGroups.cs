using System;
using System.Collections.Generic;
using System.Linq;

namespace LittleTomato {
 public partial class MainWindow {
  readonly HashSet<string> collapsedGroups=new HashSet<string>();
  public void OpenTaskGroup(Todo group){filter="todo";search="";collapsedGroups.Remove(group.Id);ShowPage("tasks");PreviewTask(group);System.Windows.Controls.Border card;if(taskCards.TryGetValue(group.Id,out card))card.BringIntoView();}
  void OpenSourceNote(Todo task){var note=app.Data.Notes.FirstOrDefault(n=>n.Id==task.SourceNoteId&&!n.Deleted);if(note!=null)app.Notes.Open(note);}
  int TaskDepth(Todo task){int depth=0;var seen=new HashSet<string>();while(task.ParentId!=null&&seen.Add(task.Id)){task=app.Data.Tasks.FirstOrDefault(t=>t.Id==task.ParentId);if(task==null)break;depth++;}return Math.Min(7,depth);}
  string TaskTotalLabel(Todo task){string own="已投入 "+Engine.Duration(app.Engine.Total(task.Id));return task.IsGroup?"合计投入 "+Engine.Duration(NoteTasks.Total(app.Engine,task)):app.Data.Tasks.Any(t=>t.ParentId==task.Id)?own+"  ·  含分支 "+Engine.Duration(NoteTasks.Total(app.Engine,task)):own;}
  string GroupProgress(Todo task){var items=NoteTasks.Branch(app.Data,task).Where(t=>!t.IsGroup&&!t.Archived).ToList();return items.Count(t=>t.Done)+" / "+items.Count+" 项完成  ·  "+NoteTasks.Count(app.Engine,task)+" 个番茄";}
  double GroupCompletion(Todo task){var items=NoteTasks.Branch(app.Data,task).Where(t=>!t.IsGroup&&!t.Archived).ToList();return items.Count==0?0:items.Count(t=>t.Done)/(double)items.Count;}
  List<Todo> VisibleTaskTree(){
   var all=app.Data.Tasks;var included=new HashSet<string>();
   foreach(var task in all){bool state=filter=="archive"?task.Archived:!task.Archived&&task.Done==(filter=="done");if(!state||task.IsGroup)continue;
    bool matches=String.IsNullOrEmpty(search)||task.Title.IndexOf(search,StringComparison.CurrentCultureIgnoreCase)>=0;
    var parents=new List<Todo>();var parent=task;var seen=new HashSet<string>();while(parent.ParentId!=null&&seen.Add(parent.Id)){parent=all.FirstOrDefault(t=>t.Id==parent.ParentId);if(parent==null)break;parents.Add(parent);if(parent.Title.IndexOf(search,StringComparison.CurrentCultureIgnoreCase)>=0)matches=true;}
    if(!matches)continue;included.Add(task.Id);foreach(var p in parents)included.Add(p.Id);
   }
   foreach(var group in all.Where(t=>t.IsGroup&&!t.Archived))if(filter=="todo"&&(String.IsNullOrEmpty(search)||group.Title.IndexOf(search,StringComparison.CurrentCultureIgnoreCase)>=0))included.Add(group.Id);
   var result=new List<Todo>();var visited=new HashSet<string>();foreach(var root in all.Where(t=>t.ParentId==null||!all.Any(p=>p.Id==t.ParentId)))AppendTasks(root,included,visited,result);foreach(var task in all)if(included.Contains(task.Id)&&!visited.Contains(task.Id))AppendTasks(task,included,visited,result);return result;
  }
  void AppendTasks(Todo task,HashSet<string> included,HashSet<string> visited,List<Todo> result){
   if(!visited.Add(task.Id))return;if(included.Contains(task.Id))result.Add(task);
   bool folded=String.IsNullOrEmpty(search)&&filter=="todo"&&collapsedGroups.Contains(task.Id);
   foreach(var child in app.Data.Tasks.Where(t=>t.ParentId==task.Id).OrderBy(t=>t.NoteOrder)){if(folded){foreach(var hidden in NoteTasks.Branch(app.Data,child))visited.Add(hidden.Id);}else AppendTasks(child,included,visited,result);}
  }
 }
}
