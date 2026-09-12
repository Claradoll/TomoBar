using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace LittleTomato {
 public static class NoteSmoke {
  static void Capture(FrameworkElement w,string path){w.UpdateLayout();var bmp=new RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(w);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using(var f=File.Create(path))encoder.Save(f);}
  static IEnumerable<DependencyObject> Children(DependencyObject root){for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var c=VisualTreeHelper.GetChild(root,i);yield return c;foreach(var v in Children(c))yield return v;}}
  static void Enter(NoteWindow w){w.Editor.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(w),0,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent});}
  static MenuItem Item(ItemsControl menu,string name){foreach(var item in menu.Items.OfType<MenuItem>()){if(String.Equals(item.Header,name))return item;var nested=Item(item,name);if(nested!=null)return nested;}return null;}
  static void Click(NoteWindow w,string name){Item(w.EditMenu,name).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));}
  static List<Paragraph> Blocks(NoteWindow w){return w.Editor.Document.Blocks.SelectMany(b=>b is Paragraph?new[]{(Paragraph)b}:((System.Windows.Documents.List)b).ListItems.SelectMany(item=>item.Blocks.OfType<Paragraph>())).ToList();}
  static void SetSample(NoteWindow w){var doc=new NoteDocument();doc.Blocks.Add(new NoteBlock{Runs=new List<NoteRun>{new NoteRun{Text="AlphaBeta",Bold=true}}});doc.Blocks.Add(new NoteBlock{Runs=new List<NoteRun>{new NoteRun{Text="Second"}}});w.Render(doc);w.Record();w.Editor.SelectAll();}
  static NoteWindow CheckCombined(Program app,NoteWindow w,Action<bool,string> check,string folder){
   SetSample(w);Click(w,"编号列表");Click(w,"待办勾选");var doc=w.Capture();
   check(doc.Blocks.Count==2&&doc.Blocks.All(b=>b.Kind=="number"&&b.Checklist)&&doc.Blocks[0].Runs[0].Bold,"number then checkbox applies both markers to all selected paragraphs and preserves formatting");
   check(!w.Editor.Selection.IsEmpty&&w.Editor.Selection.Text.Contains("AlphaBeta")&&w.Editor.Selection.Text.Contains("Second"),"paragraph commands retain multi-paragraph selection for successive operations");
   check(w.Editor.Document.Blocks.OfType<System.Windows.Documents.List>().Select(l=>l.StartIndex).SequenceEqual(new[]{1,2})&&Blocks(w).All(p=>p.Inlines.OfType<InlineUIContainer>().Any(i=>i.Child is CheckBox)),"numbered checklist renders both numeric markers and interactive checkboxes");
   w.EditMenu.PlacementTarget=w.Editor;w.EditMenu.IsOpen=true;w.UpdateLayout();
   check(Item(w.EditMenu,"编号列表").IsChecked&&Item(w.EditMenu,"待办勾选").IsChecked,"menu independently reports numbered and checkbox states");
   check(Item(w.EditMenu,"剪切").IsEnabled&&Item(w.EditMenu,"复制").IsEnabled,"selected text enables clipboard actions");w.EditMenu.IsOpen=false;
   Click(w,"待办勾选");check(w.Capture().Blocks.All(b=>b.Kind=="number"&&!b.Checklist),"removing checkboxes preserves numbering on all selected items");
   w.UndoEdit();check(w.Capture().Blocks.All(b=>b.Kind=="number"&&b.Checklist),"undo restores numbered checklist");w.RedoEdit();check(w.Capture().Blocks.All(b=>b.Kind=="number"&&!b.Checklist),"redo removes only checkboxes");
   w.Editor.SelectAll();Click(w,"待办勾选");Click(w,"编号列表");check(w.Capture().Blocks.All(b=>b.Kind=="check"&&b.Checklist),"removing numbering keeps checklist items");
   Click(w,"编号列表");check(w.Capture().Blocks.All(b=>b.Kind=="number"&&b.Checklist),"checkbox then number produces the same combined structure");
   Click(w,"分点列表");check(w.Capture().Blocks.All(b=>b.Kind=="bullet"&&b.Checklist),"switching list marker preserves checkbox state");Click(w,"编号列表");
   var box=(CheckBox)Blocks(w)[0].Inlines.OfType<InlineUIContainer>().First().Child;box.IsChecked=true;box.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
   check(w.Capture().Blocks[0].Done&&!w.Capture().Blocks[0].Runs.Any(r=>r.Strike),"combined checklist completion retains independent original formatting");
   string saved=w.Note.Body;var note=w.Note;w.Close();w=app.Notes.Open(note);check(w.Note.Body==saved&&w.Capture().Blocks[0].Done&&w.Capture().Blocks.All(b=>b.Kind=="number"&&b.Checklist),"combined markers and completion survive save and reopen");
   w.Editor.CaretPosition=Blocks(w)[0].ContentEnd;Enter(w);doc=w.Capture();
   check(doc.Blocks.Count==3&&doc.Blocks[0].Done&&!doc.Blocks[1].Done&&doc.Blocks.All(b=>b.Kind=="number"&&b.Checklist),"Enter after completed numbered task inserts unchecked numbered task");
   check(w.Editor.Document.Blocks.OfType<System.Windows.Documents.List>().Select(l=>l.StartIndex).SequenceEqual(new[]{1,2,3}),"inserting a numbered task renumbers following items");
   w.Editor.Selection.Text="Nested";w.Indent(1);check(w.Capture().Blocks[1].Indent==1&&w.Capture().Blocks[1].Checklist&&w.Editor.Document.Blocks.OfType<System.Windows.Documents.List>().Select(l=>l.StartIndex).SequenceEqual(new[]{1,1,2}),"nested numbered task uses its own counter and retains checkbox");w.Indent(-1);
   check(w.Editor.Document.Blocks.OfType<System.Windows.Documents.List>().Select(l=>l.StartIndex).SequenceEqual(new[]{1,2,3}),"outdent recomputes numbering without dropping checkbox");
   var firstRun=Blocks(w)[0].Inlines.OfType<Span>().First().Inlines.OfType<Run>().First();w.Editor.CaretPosition=firstRun.ContentStart.GetPositionAtOffset(5);Enter(w);doc=w.Capture();
   check(doc.Blocks[0].Runs.Select(r=>r.Text).Aggregate("",(a,b)=>a+b)=="Alpha"&&doc.Blocks[1].Runs.Select(r=>r.Text).Aggregate("",(a,b)=>a+b)=="Beta"&&doc.Blocks[1].Checklist&&!doc.Blocks[1].Done&&doc.Blocks[1].Runs[0].Bold,"Enter inside a formatted numbered task splits text and starts an unchecked task");
   w.Editor.CaretPosition=Blocks(w).Last().ContentEnd;Enter(w);Enter(w);check(w.Capture().Blocks.Last().Kind=="body"&&!w.Capture().Blocks.Last().Checklist,"Enter on empty combined item exits numbering and checklist together");
   w.Editor.Selection.Text="Separator";EditingCommands.EnterParagraphBreak.Execute(null,w.Editor);w.Editor.Selection.Text="Restart";Click(w,"编号列表");check(((System.Windows.Documents.List)w.Editor.Document.Blocks.LastBlock).StartIndex==1,"numbered list after plain paragraph starts at one");
   SetSample(w);var ps=Blocks(w);w.Editor.Selection.Select(ps[0].ContentStart,ps[1].ContentStart);Click(w,"编号列表");check(w.Capture().Blocks[0].Kind=="number"&&w.Capture().Blocks[1].Kind=="body","selection ending at next paragraph start does not format the next paragraph");
   w.Editor.CaretPosition=Blocks(w)[0].ContentEnd;Click(w,"待办勾选");w.Editor.SelectAll();Click(w,"待办勾选");check(w.Capture().Blocks.All(b=>b.Checklist),"mixed selection adds checkboxes to every selected item");Click(w,"待办勾选");check(w.Capture().Blocks.All(b=>!b.Checklist&&b.Kind!="check"),"second mixed-selection toggle removes all checkboxes");
   w.Editor.CaretPosition=Blocks(w)[0].ContentEnd;w.EditMenu.PlacementTarget=w.Editor;w.EditMenu.IsOpen=true;w.UpdateLayout();check(!Item(w.EditMenu,"剪切").IsEnabled&&!Item(w.EditMenu,"复制").IsEnabled,"empty selection disables cut and copy");
   check(!w.EditMenu.Items.OfType<MenuItem>().Any(i=>String.Equals(i.Header,"移到回收站"))&&Item(w.EditMenu,"便签选项")!=null&&Item(w.EditMenu,"编辑历史")!=null,"root menu groups note management and edit history away from formatting");w.EditMenu.IsOpen=false;
   return w;
  }
  public static void Start(Program app,string folder){
   var report=new List<string>();int stage=0;NoteWindow w=null;Note note=null;string saved=null;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(1100)};Action<bool,string> check=(ok,msg)=>{report.Add((ok?"PASS ":"FAIL ")+msg);if(!ok)Environment.ExitCode=1;};
   timer.Tick+=(s,e)=>{try{switch(stage++){
    case 0:
     app.Data.Settings.Sound=false;app.Data.Settings.Notifications=false;app.Data.Settings.Theme="light";app.ApplyTheme();app.Engine.Start(app.Engine.EnsureFocusTask());
     app.ShowMain();app.Main.ShowPage("notes");check(Children(app.Main).OfType<Button>().Any(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="新建便签"),"notes navigation and create action are available");
     w=app.Notes.New();note=w.Note;check(Object.ReferenceEquals(w,app.Notes.Open(note)),"opening same note reuses one editor");w.Editor.AppendText("今天的灵感");w.Editor.SelectAll();w.Command(EditingCommands.ToggleBold);w.Command(EditingCommands.ToggleItalic);w.Decoration(TextDecorationLocation.Strikethrough);w.Highlight();w.Command(EditingCommands.ToggleUnderline);
     var run=w.Capture().Blocks[0].Runs.First(r=>r.Text.Length>0);check(run.Bold&&run.Italic&&run.Strike&&run.Highlight&&run.Underline,"native editor applies all inline formatting");
     saved=note.Body;w.Close();check(app.Store.Load().Notes[0].Body==saved,"closing flushes rich content immediately");w=app.Notes.Open(note);check(w.Capture().Blocks[0].Runs.First(r=>r.Text.Length>0).Strike,"formatting survives closing and reopening");
     w.Editor.SelectAll();w.ClearFormat();w.SetKind("check");var p=(Paragraph)w.Editor.Document.Blocks.FirstBlock;var box=(CheckBox)p.Inlines.OfType<InlineUIContainer>().First().Child;check(w.Editor.IsDocumentEnabled&&box.IsEnabled,"checklist control is enabled for real pointer clicks");box.IsChecked=true;box.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));check(w.Capture().Blocks[0].Done,"checklist responds to checkbox click");check(!w.Capture().Blocks[0].Runs.Any(r=>r.Strike),"completion strikethrough does not overwrite original text formatting");w.UndoEdit();check(!w.Capture().Blocks[0].Done,"undo restores checkbox state");w.RedoEdit();check(w.Capture().Blocks[0].Done,"redo restores checkbox completion");w.Editor.CaretPosition=((Paragraph)w.Editor.Document.Blocks.FirstBlock).ContentEnd;Enter(w);check(w.Capture().Blocks.Count==2&&w.Capture().Blocks[1].Kind=="check"&&!w.Capture().Blocks[1].Done,"Enter continues checklist with unchecked item");Enter(w);check(w.Capture().Blocks.Last().Kind=="body","Enter on empty checklist exits checklist");
     var input=new NoteDocument();input.Blocks.Add(new NoteBlock{Runs=new List<NoteRun>{new NoteRun{Text="-"}}});w.Render(input);w.Record();w.Editor.CaretPosition=((Paragraph)w.Editor.Document.Blocks.FirstBlock).ContentEnd;
     w.Editor.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,new TextComposition(InputManager.Current,w.Editor," ")){RoutedEvent=TextCompositionManager.PreviewTextInputEvent});check(w.Capture().Blocks[0].Kind=="bullet","typed dash-space converts to bullet list");w.Editor.AppendText("第一项");Enter(w);check(w.Capture().Blocks.Count==2&&w.Capture().Blocks[1].Kind=="bullet","Enter continues bullet list");
     w.SetKind("number");w.Editor.AppendText("编号内容");w.Indent(1);check(w.Capture().Blocks.Last().Indent==1&&w.Capture().Blocks.Last().Kind=="number","numbered list supports indentation");
     Click(w,"大标题");check(w.Capture().Blocks.Last().Kind=="h1","context menu applies heading style");Click(w,"正文");check(w.Capture().Blocks.Last().Kind=="body","context menu returns heading to body text");
     check(!Children(w).OfType<ComboBox>().Any()&&!Children(w).OfType<Button>().Any(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="加粗 · Ctrl+B"),"note surface has no formatting toolbar or settings controls");
     w=CheckCombined(app,w,check,folder);
     var preview=new NoteDocument();preview.Blocks.Add(new NoteBlock{Kind="h1",Runs=new List<NoteRun>{new NoteRun{Text="把想法，轻轻记下来。"}}});preview.Blocks.Add(new NoteBlock{Runs=new List<NoteRun>{new NoteRun{Text="记录灵感，也照顾好每一个下一步。"}}});preview.Blocks.Add(new NoteBlock{Kind="h2",Runs=new List<NoteRun>{new NoteRun{Text="今天的小清单"}}});preview.Blocks.Add(new NoteBlock{Kind="number",Checklist=true,Done=true,Runs=new List<NoteRun>{new NoteRun{Text="完成一段专注，让思路清晰起来"}}});preview.Blocks.Add(new NoteBlock{Kind="number",Checklist=true,Runs=new List<NoteRun>{new NoteRun{Text="整理桌面上的灵感与待办"}}});preview.Blocks.Add(new NoteBlock{Kind="bullet",Runs=new List<NoteRun>{new NoteRun{Text="重要的想法",Bold=true},new NoteRun{Text="，用简单的格式留下来。"}}});preview.Blocks.Add(new NoteBlock{Runs=new List<NoteRun>{new NoteRun{Text="慢一点，也在向前。",Highlight=true,Italic=true}}});preview.Blocks.Add(new NoteBlock{Runs=new List<NoteRun>{new NoteRun{Text="TomoBar 项目主页",Link="https://github.com/Claradoll/TomoBar"}}});w.Render(preview);w.Record();break;
    case 1:
     check(app.Store.Load().Notes.First(n=>n.Id==note.Id).Body==note.Body,"debounced autosave stores document without manual save");check(app.Data.Active!=null&&app.Data.Active.Running,"editing notes does not pause focus timer");check(note.DisplayTitle=="把想法，轻轻记下来。","blank title uses first content line");Capture(w,Path.Combine(folder,"note-light.png"));app.Main.ShowPage("notes");Capture(app.Main,Path.Combine(folder,"notes-list.png"));
     app.Data.Settings.Theme="dark";app.ApplyTheme();w.Activate();break;
    case 2:
     Capture(w,Path.Combine(folder,"note-dark.png"));
     Click(w,"置顶便签");check(w.Topmost&&note.Pinned,"context menu pins note");Click(w,"置顶便签");Click(w,"鼠尾草");check(note.Color=="sage","context menu changes paper color");
     w.EditMenu.PlacementTarget=w.Editor;w.EditMenu.IsOpen=true;w.UpdateLayout();check(w.EditMenu.IsOpen,"note right-click editing menu opens");Capture(w.EditMenu,Path.Combine(folder,"note-menu.png"));Item(w.EditMenu,"列表与缩进").IsSubmenuOpen=true;var listPopup=(System.Windows.Controls.Primitives.Popup)Item(w.EditMenu,"列表与缩进").Template.FindName("PART_Popup",Item(w.EditMenu,"列表与缩进"));Capture((FrameworkElement)listPopup.Child,Path.Combine(folder,"note-list-menu.png"));Item(w.EditMenu,"列表与缩进").IsSubmenuOpen=false;Item(w.EditMenu,"文字样式").IsSubmenuOpen=true;check(Item(w.EditMenu,"文字样式").IsSubmenuOpen,"formatting submenu opens");var popup=(System.Windows.Controls.Primitives.Popup)Item(w.EditMenu,"文字样式").Template.FindName("PART_Popup",Item(w.EditMenu,"文字样式"));Capture((FrameworkElement)popup.Child,Path.Combine(folder,"note-format-menu.png"));w.EditMenu.IsOpen=false;
     w.Editor.SelectAll();string beforeFormat=note.Body,beforeText=note.PlainText;w.EditMenu.IsOpen=true;Click(w,"加粗");w.EditMenu.IsOpen=false;check(note.PlainText==beforeText&&w.Capture().Blocks.SelectMany(b=>b.Runs).Where(r=>r.Text.Length>0).All(r=>r.Bold),"context-menu formatting preserves selected text and applies bold");w.UndoEdit();check(note.Body==beforeFormat,"context-menu formatting supports undo");
w.Width=280;w.Height=240;w.UpdateLayout();check(w.Editor.ActualHeight>=40,"compact note keeps editor and actions accessible");Capture(w,Path.Combine(folder,"note-compact.png"));w.Width=380;w.Height=440;
     int tasks=app.Data.Tasks.Count;app.Notes.ToTask(note);app.Notes.ToTask(note);check(app.Data.Tasks.Count==tasks+1&&app.Data.Tasks.Any(t=>t.Id==note.TaskId),"convert to task links note and avoids duplicate tasks");
     app.Notes.Delete(note);check(note.Deleted&&!w.IsVisible,"delete moves note into recoverable trash and closes editor");app.Notes.Restore(note);check(!note.Deleted&&app.Store.Load().Notes.Any(n=>n.Id==note.Id&&!n.Deleted),"restore returns note with all content");
     app.Main.ShowPage("notes");var search=Children(app.Main).OfType<TextBox>().First(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="搜索便签");search.Text="不存在的测试词";app.Main.UpdateLayout();check(!Children(app.Main).OfType<Button>().Any(b=>(System.Windows.Automation.AutomationProperties.GetName(b)??"").StartsWith("打开便签 ")),"note search filters unmatched content");search.Text="灵感";app.Main.UpdateLayout();check(Children(app.Main).OfType<Button>().Any(b=>(System.Windows.Automation.AutomationProperties.GetName(b)??"").StartsWith("打开便签 ")),"note search finds body text");
     var restored=Store.Normalize(Store.Decode<AppData>(Store.Encode(app.Data)));check(restored.Notes[0].Body==note.Body&&NoteCodec.Decode(restored.Notes[0].Body).Blocks.Any(b=>b.Done),"backup round trip preserves rich text and checklist");
     w=app.Notes.Open(note);w.Editor.AppendText("\n退出前的最后一笔");check(app.Notes.Flush()&&app.Store.Load().Notes[0].PlainText.Contains("退出前的最后一笔"),"exit flush preserves final unsaved keystrokes");w.Close();
     File.WriteAllLines(Path.Combine(folder,"notes-results.txt"),report,System.Text.Encoding.UTF8);timer.Stop();app.Exit();break;
   }}catch(Exception ex){Environment.ExitCode=1;report.Add("FAIL "+ex);File.WriteAllLines(Path.Combine(folder,"notes-results.txt"),report);timer.Stop();app.Exit();}};timer.Start();
  }
 }
}
