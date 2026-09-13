using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Automation;

namespace LittleTomato {
 public static partial class NoteSmoke {
  static void Press(Button button){button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));}
  static void CheckGroups(Program app,Action<bool,string> check,string folder){
   app.Engine.End();app.Data.Settings.Theme="light";app.ApplyTheme();var w=app.Notes.New();var n=w.Note;app.Notes.Rename(n,"今天的小计划");
   var doc=new NoteDocument{Version=2};foreach(var text in new[]{"整理思路","小项目","完成原型","阅读一章"})doc.Blocks.Add(new NoteBlock{Kind="number",Checklist=true,Indent=text=="完成原型"?1:0,Runs=new List<NoteRun>{new NoteRun{Text=text}}});w.Render(doc);w.Record();w.Flush();
   var ids=w.Capture().Blocks.Select(b=>b.Id).ToArray();check(ids.Distinct().Count()==4&&w.Capture().Blocks.Select(b=>b.Id).SequenceEqual(ids),"editor capture keeps stable unique paragraph IDs");
   w.Editor.CaretPosition=Blocks(w)[0].ContentEnd;w.EditMenu.PlacementTarget=w.Editor;w.EditMenu.IsOpen=true;w.UpdateLayout();check(Item(w.EditMenu,"编号清单转为任务组").IsEnabled&&!Item(w.EditMenu,"专注此条任务").IsEnabled,"focus menu enables conversion and requires association before direct start");w.EditMenu.IsOpen=false;Click(w,"编号清单转为任务组");
   var group=NoteTasks.Group(app.Data,n);check(group!=null&&NoteTasks.Branch(app.Data,group).Count==5&&app.Main.Page=="tasks"&&app.Main.PreviewedTask==group,"conversion opens group overview with all branches");
   check(app.Data.Active==null,"conversion does not start a timer");
   app.Main.UpdateLayout();check(Children(app.Main).OfType<Button>().Any(b=>AutomationProperties.GetName(b)=="开始任务 完成原型"),"nested branch offers an individual timer action");
   Press(NamedButton(app.Main,"折叠任务组 今天的小计划"));app.Main.UpdateLayout();check(!Children(app.Main).OfType<Button>().Any(b=>AutomationProperties.GetName(b)=="开始任务 完成原型"),"group fold hides all child cards");Press(NamedButton(app.Main,"折叠任务组 今天的小计划"));
   var first=NoteTasks.Linked(app.Data,n,ids[0]);first.Minutes=35;int count=app.Data.Tasks.Count;w.ConvertTaskGroup();check(app.Data.Tasks.Count==count&&first.Minutes==35,"repeated editor conversion preserves per-task duration and avoids duplicates");
   w.Activate();w.Editor.Focus();w.Editor.CaretPosition=Blocks(w)[0].ContentEnd;Enter(w);var split=w.Capture();check(split.Blocks.Select(b=>b.Id).Distinct().Count()==5&&split.Blocks[0].Id==ids[0]&&split.Blocks[2].Id==ids[1],"Enter creates a new identity without moving existing paragraph associations");w.UndoEdit();check(w.Capture().Blocks.Select(b=>b.Id).SequenceEqual(ids),"undo paragraph insertion restores linked identities");w.Flush();
   w.Editor.CaretPosition=Blocks(w)[0].ContentEnd;w.Editor.Selection.Text="与计划";w.Flush();check(first.Title=="整理思路与计划"&&first.Minutes==35,"typing in linked note updates task title and preserves timer settings");
   app.Engine.MarkDone(first);check(w.Capture().Blocks[0].Done&&w.Capture().Blocks[0].Checklist,"main task completion refreshes an open note checkbox");w.UndoEdit();w.Flush();check(!first.Done&&!w.Capture().Blocks[0].Done,"note undo reverses external completion in both places");
   w.Editor.CaretPosition=Blocks(w)[2].ContentEnd;w.EditMenu.IsOpen=true;w.UpdateLayout();check(Item(w.EditMenu,"专注此条任务").IsEnabled&&Item(w.EditMenu,"更新编号任务组")!=null,"linked paragraph enables focus and shows update command");w.EditMenu.IsOpen=false;Click(w,"专注此条任务");var nested=NoteTasks.Linked(app.Data,n,ids[2]);
   check(app.Data.Active!=null&&app.Data.Active.TaskId==nested.Id&&app.Data.Active.Running&&!app.Main.IsVisible,"note focus action starts selected branch and collapses main window");
   app.ShowMain();app.Main.PreviewTask(first);check(app.Data.Active.TaskId==nested.Id&&app.Data.Active.Running&&app.Main.PreviewedTask==first,"previewing another branch does not switch active timer");
   var box=(CheckBox)Blocks(w)[2].Inlines.OfType<InlineUIContainer>().First().Child;box.IsChecked=true;box.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));w.Flush();check(nested.Done&&app.Data.Active==null,"checking focused note item ends its timer and marks matching branch complete");
   app.Engine.MarkDone(nested);app.Notes.ShowGroup(n);app.Notes.Rename(n,"今天的小计划 · 专注清单");check(group.Title==n.Title&&w.Title.Contains(n.Title),"renaming note updates group name and note caption");
   app.Main.OpenTaskGroup(group);var search=Children(app.Main).OfType<TextBox>().First(b=>AutomationProperties.GetName(b)=="搜索任务");search.Text="完成原型";app.Main.UpdateLayout();check(Children(app.Main).OfType<Button>().Any(b=>AutomationProperties.GetName(b)=="开始任务 完成原型")&&Children(app.Main).OfType<Button>().Any(b=>AutomationProperties.GetName(b)=="折叠任务组 "+group.Title),"search finds nested task with group context");
   search.Text=group.Title;app.Main.UpdateLayout();check(Children(app.Main).OfType<Button>().Any(b=>AutomationProperties.GetName(b)=="开始任务 阅读一章"),"search by group title includes descendants");
   app.Main.Width=1100;app.Main.Height=820;app.Main.UpdateLayout();Capture(app.Main,Path.Combine(folder,"task-groups-light.png"));Capture(w,Path.Combine(folder,"note-group-light.png"));
   w.EditMenu.IsOpen=true;w.UpdateLayout();Item(w.EditMenu,"番茄专注").IsSubmenuOpen=true;w.UpdateLayout();var popup=(System.Windows.Controls.Primitives.Popup)Item(w.EditMenu,"番茄专注").Template.FindName("PART_Popup",Item(w.EditMenu,"番茄专注"));Capture((FrameworkElement)popup.Child,Path.Combine(folder,"note-focus-menu.png"));w.EditMenu.IsOpen=false;
   app.Data.Settings.Theme="dark";app.ApplyTheme();Capture(app.Main,Path.Combine(folder,"task-groups-dark.png"));
   string body=n.Body;w.Close();w=app.Notes.Open(n);check(w.Capture().Blocks.Select(b=>b.Id).SequenceEqual(ids)&&n.Body==body,"reopening note preserves every linked paragraph ID");w.ConvertTaskGroup();check(app.Data.Tasks.Count==count,"conversion after reopening remains idempotent");
   var restored=app.Store.Load();check(NoteTasks.Group(restored,restored.Notes.First(x=>x.Id==n.Id))!=null&&restored.Tasks.Count==count,"saved data reload keeps task group and links");w.Close();
  }
 }
}
