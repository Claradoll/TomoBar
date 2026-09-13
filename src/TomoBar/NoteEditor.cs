using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Automation;

namespace LittleTomato {
 public partial class NoteWindow : Window {
  readonly Program app;public readonly Note Note;public readonly RichTextBox Editor;
  public ContextMenu EditMenu;Border tint;DispatcherTimer saveTimer;MenuItem pinItem,taskItems;
  bool loading,dirty;string lastBody;readonly Stack<EditState> undo=new Stack<EditState>(),redo=new Stack<EditState>();
  class EditState {public string Body;public int Offset;}
  public NoteWindow(Program owner,Note note,NoteDocument doc){
   app=owner;Note=note;Title="便签";Style=(Style)Application.Current.FindResource(typeof(Window));UI.Chrome(this);Icon=app.IconSource;
   Width=Math.Min(note.Width,SystemParameters.WorkArea.Width-24);Height=Math.Min(note.Height,SystemParameters.WorkArea.Height-24);MinWidth=280;MinHeight=240;WindowStartupLocation=WindowStartupLocation.CenterScreen;Topmost=note.Pinned;
   var root=new Grid();root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(36)});root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(2)});root.RowDefinitions.Add(new RowDefinition());
   root.Children.Add(BuildNoteHeader());
   tint=new Border{Height=2,VerticalAlignment=VerticalAlignment.Top};Grid.SetRow(tint,1);root.Children.Add(tint);
   Editor=new RichTextBox{BorderThickness=new Thickness(0),Padding=new Thickness(22,14,22,18),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,AcceptsTab=false,IsDocumentEnabled=true,IsUndoEnabled=false,FontSize=15,Background=Brushes.Transparent};Editor.SetResourceReference(Control.ForegroundProperty,"Ink");Editor.SetResourceReference(RichTextBox.CaretBrushProperty,"Ink");AutomationProperties.SetName(Editor,"便签正文");Grid.SetRow(Editor,2);root.Children.Add(Editor);
   DataObject.AddPastingHandler(Editor,(s,e)=>{if(e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText))e.FormatToApply=DataFormats.UnicodeText;else e.CancelCommand();});
   EditMenu=BuildEditMenu();Editor.ContextMenu=EditMenu;root.ContextMenu=EditMenu;
   Content=root;saveTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(650)};saveTimer.Tick+=(s,e)=>Flush();
   Render(doc);lastBody=Store.Encode(Capture());ApplyColor();
   Editor.TextChanged+=(s,e)=>Record();Editor.PreviewKeyDown+=Keys;
   Editor.PreviewTextInput+=(s,e)=>{if(e.Text==" "){var p=Editor.CaretPosition.Paragraph;if(p==null)return;string prefix=TextBefore(p,Editor.CaretPosition);string kind=prefix=="-"||prefix=="*"?"bullet":prefix=="1."?"number":prefix=="[]"||prefix=="[ ]"?"check":null;if(kind!=null&&Plain(p)==prefix){loading=true;new TextRange(AtCharacter(p,0),Editor.CaretPosition).Text="";loading=false;SetKind(kind);e.Handled=true;}else Dispatcher.BeginInvoke(new Action(AutoLink),DispatcherPriority.Background);}};
   Editor.PreviewMouseLeftButtonDown+=(s,e)=>{if(Keyboard.Modifiers!=ModifierKeys.Control)return;var pos=Editor.GetPositionFromPoint(e.GetPosition(Editor),true);var p=pos==null?null:pos.Paragraph;if(p==null)return;for(DependencyObject parent=pos.Parent;parent!=null;parent=LogicalTreeHelper.GetParent(parent)){var target=parent as Hyperlink;if(target!=null&&target.NavigateUri!=null){OpenLink(target.NavigateUri.AbsoluteUri);e.Handled=true;return;}}string text=Plain(p);int index=CharacterOffset(p,pos);foreach(Match match in Regex.Matches(text,@"https?://[^\s<>]+")){if(index>=match.Index&&index<=match.Index+match.Length){OpenLink(match.Value.TrimEnd('.',',','。','，',')','）'));e.Handled=true;break;}}};
   CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo,(s,e)=>UndoEdit(),(s,e)=>{e.CanExecute=true;e.Handled=true;}));CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo,(s,e)=>RedoEdit(),(s,e)=>{e.CanExecute=true;e.Handled=true;}));
   Closing+=(s,e)=>{if(!Flush())e.Cancel=true;};Closed+=(s,e)=>saveTimer.Stop();Loaded+=(s,e)=>{Editor.Focus();};SizeChanged+=(s,e)=>{if(IsLoaded&&!resizingFold&&!IsFolded&&WindowState==WindowState.Normal){Note.Width=ActualWidth;Note.Height=ActualHeight;Schedule();}};
  }
  static MenuItem Menu(ItemsControl owner,string text,Action action){var item=new MenuItem{Header=text,Style=(Style)Application.Current.FindResource("NoteMenuItem")};if(action!=null)item.Click+=(s,e)=>action();owner.Items.Add(item);return item;}
  static Separator MenuSeparator(){return new Separator{Style=(Style)Application.Current.FindResource("NoteMenuSeparator")};}
  ContextMenu BuildEditMenu(){
   var menu=new ContextMenu{Style=(Style)Application.Current.FindResource("NoteContextMenu")};
   var cut=Menu(menu,"剪切",()=>{Editor.Focus();Editor.Cut();});cut.InputGestureText="Ctrl+X";
   var copy=Menu(menu,"复制",()=>Editor.Copy());copy.InputGestureText="Ctrl+C";
   var paste=Menu(menu,"粘贴纯文本",()=>{Editor.Focus();Editor.Paste();});paste.InputGestureText="Ctrl+V";
   Menu(menu,"全选",()=>{Editor.Focus();Editor.SelectAll();}).InputGestureText="Ctrl+A";menu.Items.Add(MenuSeparator());
   var text=Menu(menu,"文字样式",null);
   var bold=Menu(text,"加粗",()=>Command(EditingCommands.ToggleBold));bold.IsCheckable=true;bold.InputGestureText="Ctrl+B";
   var italic=Menu(text,"斜体",()=>Command(EditingCommands.ToggleItalic));italic.IsCheckable=true;italic.InputGestureText="Ctrl+I";
   var underline=Menu(text,"下划线",()=>Command(EditingCommands.ToggleUnderline));underline.IsCheckable=true;underline.InputGestureText="Ctrl+U";
   var strike=Menu(text,"删除线",()=>Decoration(TextDecorationLocation.Strikethrough));strike.IsCheckable=true;
   var highlight=Menu(text,"高亮",Highlight);highlight.IsCheckable=true;text.Items.Add(MenuSeparator());Menu(text,"清除文字格式",ClearFormat);
   var paragraphs=Menu(menu,"段落样式",null);var kinds=new Dictionary<string,MenuItem>();
   string[] names={"正文","大标题","小标题"},keys={"body","h1","h2"};
   for(int i=0;i<keys.Length;i++){string kind=keys[i];var item=Menu(paragraphs,names[i],()=>SetKind(kind));item.IsCheckable=true;kinds[kind]=item;}
   var lists=Menu(menu,"列表与缩进",null);
   var bullet=Menu(lists,"分点列表",()=>SetKind("bullet"));bullet.IsCheckable=true;kinds["bullet"]=bullet;
   var number=Menu(lists,"编号列表",()=>SetKind("number"));number.IsCheckable=true;kinds["number"]=number;
   lists.Items.Add(MenuSeparator());Menu(lists,"增加缩进",()=>Indent(1)).InputGestureText="Tab";Menu(lists,"减少缩进",()=>Indent(-1)).InputGestureText="Shift+Tab";
   var checklist=Menu(menu,"待办勾选",ToggleChecklist);checklist.IsCheckable=true;checklist.InputGestureText="Ctrl+1";checklist.ToolTip="可与编号或分点列表同时使用";
   Menu(menu,"插入链接",InsertLink);menu.Items.Add(MenuSeparator());
   var history=Menu(menu,"编辑历史",null);var undoItem=Menu(history,"撤销",UndoEdit);undoItem.InputGestureText="Ctrl+Z";var redoItem=Menu(history,"重做",RedoEdit);redoItem.InputGestureText="Ctrl+Y";
   var options=Menu(menu,"便签选项",null);
   pinItem=Menu(options,"置顶便签",TogglePin);pinItem.IsCheckable=true;
   var colors=Menu(options,"便签颜色",null);var colorItems=new Dictionary<string,MenuItem>();string[] colorKeys={"paper","sage","rose"},colorNames={"奶油纸","鼠尾草","浅玫瑰"};
   for(int i=0;i<colorKeys.Length;i++){string color=colorKeys[i];var item=Menu(colors,colorNames[i],()=>{Note.Color=color;ApplyColor();Schedule();});item.IsCheckable=true;colorItems[color]=item;}
   options.Items.Add(MenuSeparator());taskItems=Menu(options,"关联任务",null);
   var taskAction=Menu(options,"转为任务",()=>{if(Flush())app.Notes.ToTask(Note);});options.Items.Add(MenuSeparator());Menu(options,"移到回收站",()=>app.Notes.Delete(Note));
   menu.Opened+=(s,e)=>{
    cut.IsEnabled=copy.IsEnabled=!Editor.Selection.IsEmpty;try{paste.IsEnabled=Clipboard.ContainsText();}catch(System.Runtime.InteropServices.ExternalException){paste.IsEnabled=false;}
    undoItem.IsEnabled=undo.Count>0;redoItem.IsEnabled=redo.Count>0;pinItem.IsChecked=Note.Pinned;
    foreach(var pair in colorItems)pair.Value.IsChecked=Note.Color==pair.Key;
    var doc=Capture();var selected=SelectedBlocks().Select(i=>doc.Blocks[i]).ToList();
    foreach(var pair in kinds)pair.Value.IsChecked=selected.All(block=>(block.Kind=="check"?"body":block.Kind)==pair.Key);
    checklist.IsChecked=selected.All(HasChecklist);
    bold.IsChecked=Object.Equals(Editor.Selection.GetPropertyValue(TextElement.FontWeightProperty),FontWeights.Bold);italic.IsChecked=Object.Equals(Editor.Selection.GetPropertyValue(TextElement.FontStyleProperty),FontStyles.Italic);
    var dec=Editor.Selection.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection;underline.IsChecked=dec!=null&&dec.Any(d=>d.Location==TextDecorationLocation.Underline);strike.IsChecked=dec!=null&&dec.Any(d=>d.Location==TextDecorationLocation.Strikethrough);
    var brush=Editor.Selection.GetPropertyValue(TextElement.BackgroundProperty) as SolidColorBrush;highlight.IsChecked=brush!=null&&brush.Color.A!=0;
    taskAction.Header=app.Data.Tasks.Any(t=>t.Id==Note.TaskId)?"查看关联任务":"转为任务";
    taskItems.Items.Clear();var independent=Menu(taskItems,"独立便签",()=>{Note.TaskId=null;Schedule();});independent.IsCheckable=true;independent.IsChecked=Note.TaskId==null;
    foreach(var task in app.Data.Tasks.Where(t=>!t.Archived)){var t=task;var item=Menu(taskItems,t.Title,()=>{Note.TaskId=t.Id;Schedule();});item.IsCheckable=true;item.IsChecked=Note.TaskId==t.Id;}
   };
   return menu;
  }
  void ApplyColor(){bool dark=app.Data.Settings.Theme=="dark"||app.Data.Settings.Theme=="system"&&!Native.AppsLight;string color=Note.Color=="sage"?(dark?"#242D26":"#F1F6EC"):Note.Color=="rose"?(dark?"#302527":"#FFF2EF"):(dark?"#2C291F":"#FFFAEA");Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));if(tint!=null)tint.Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(Note.Color=="sage"?"#98B29B":Note.Color=="rose"?"#D4A1A3":"#D9BA77"));}
  void Schedule(){if(loading||saveTimer==null)return;dirty=true;Note.Updated=DateTime.UtcNow.Ticks;RefreshHeader();saveTimer.Stop();saveTimer.Start();}
  public bool Flush(){if(saveTimer!=null)saveTimer.Stop();if(!dirty)return true;if(!app.Save())return false;dirty=false;app.Notes.Changed();return true;}
  int Offset(){return Editor.Document.ContentStart.GetOffsetToPosition(Editor.CaretPosition);}
  public void Record(){if(loading)return;var doc=Capture();string body=Store.Encode(doc);if(body==lastBody)return;if(lastBody!=null){undo.Push(new EditState{Body=lastBody,Offset=Offset()});if(undo.Count>200){var keep=undo.Take(150).Reverse().ToArray();undo.Clear();foreach(var state in keep)undo.Push(state);}redo.Clear();}lastBody=body;Note.Body=body;Note.PlainText=doc.PlainText;Schedule();}
  public void UndoEdit(){History(undo,redo);}
  public void RedoEdit(){History(redo,undo);}
  void History(Stack<EditState> from,Stack<EditState> to){if(from.Count==0)return;to.Push(new EditState{Body=lastBody,Offset=Offset()});var state=from.Pop();var doc=NoteCodec.Decode(state.Body);Render(doc);lastBody=state.Body;Note.Body=state.Body;Note.PlainText=doc.PlainText;int max=Editor.Document.ContentStart.GetOffsetToPosition(Editor.Document.ContentEnd);Editor.CaretPosition=Editor.Document.ContentStart.GetPositionAtOffset(Math.Min(max,Math.Max(0,state.Offset)))??Editor.Document.ContentEnd;Editor.Focus();Schedule();}
  static string Plain(Paragraph p){var runs=new List<NoteRun>();ReadInlines(p.Inlines,runs,null);return String.Concat(runs.Select(r=>r.Text));}
  // WPF TextRange.Text includes generated list numbers; offsets must count only editable content.
  static int CharacterOffset(Paragraph p,TextPointer end){int count=0;var pos=p.ContentStart;while(pos!=null&&pos.CompareTo(end)<0&&pos.CompareTo(p.ContentEnd)<0){if(pos.GetPointerContext(LogicalDirection.Forward)==TextPointerContext.Text){string text=pos.GetTextInRun(LogicalDirection.Forward);int length=Math.Min(text.Length,pos.GetOffsetToPosition(end));count+=length;pos=pos.GetPositionAtOffset(length);}else{if(pos.GetPointerContext(LogicalDirection.Forward)==TextPointerContext.ElementStart&&pos.GetAdjacentElement(LogicalDirection.Forward) is LineBreak)count++;pos=pos.GetNextContextPosition(LogicalDirection.Forward);}}return count;}
  static string TextBefore(Paragraph p,TextPointer end){string text=Plain(p);return text.Substring(0,Math.Min(text.Length,CharacterOffset(p,end)));}
  static IEnumerable<Paragraph> Paragraphs(BlockCollection blocks){foreach(var b in blocks){var p=b as Paragraph;if(p!=null)yield return p;var list=b as System.Windows.Documents.List;if(list!=null)foreach(var item in list.ListItems)foreach(var child in Paragraphs(item.Blocks))yield return child;}}
  public NoteDocument Capture(){var doc=new NoteDocument();foreach(var p in Paragraphs(Editor.Document.Blocks)){var box=p.Inlines.OfType<InlineUIContainer>().Select(i=>i.Child).OfType<CheckBox>().FirstOrDefault();var list=p.Parent is ListItem?((ListItem)p.Parent).Parent as System.Windows.Documents.List:null;var block=new NoteBlock{Kind=list!=null?(list.MarkerStyle==TextMarkerStyle.Decimal?"number":"bullet"):(p.Tag as string??"body"),Checklist=box!=null,Done=box!=null&&box.IsChecked==true,Indent=(int)Math.Round((list==null?p.Margin.Left:list.Margin.Left)/22)};ReadInlines(p.Inlines,block.Runs,null);if(block.Kind=="check"&&box==null)block.Kind="body";if(box!=null&&block.Kind=="body")block.Kind="check";if(box!=null&&block.Kind!="check")doc.Version=2;doc.Blocks.Add(block);}return doc;}
  static bool HasDecoration(Inline inline,TextDecorationLocation location){for(TextElement el=inline;el is Inline;el=el.Parent as TextElement){if((el.Tag as string)=="completion")continue;var decorations=el.ReadLocalValue(Inline.TextDecorationsProperty) as TextDecorationCollection;if(decorations!=null&&decorations.Any(d=>d.Location==location))return true;}return false;}
  static void ReadInlines(InlineCollection inlines,List<NoteRun> output,string link){foreach(var i in inlines){var run=i as Run;var hyperlink=i as Hyperlink;var span=i as Span;if(run!=null)output.Add(new NoteRun{Text=run.Text,Bold=run.FontWeight==FontWeights.Bold||run.FontWeight==FontWeights.SemiBold,Italic=run.FontStyle==FontStyles.Italic,Underline=HasDecoration(run,TextDecorationLocation.Underline),Strike=HasDecoration(run,TextDecorationLocation.Strikethrough),Highlight=run.Background is SolidColorBrush&&((SolidColorBrush)run.Background).Color.A!=0,Link=link});else if(i is LineBreak)output.Add(new NoteRun{Text="\n"});else if(span!=null)ReadInlines(span.Inlines,output,hyperlink!=null&&hyperlink.NavigateUri!=null?hyperlink.NavigateUri.AbsoluteUri:link);}}
  public void Render(NoteDocument doc){loading=true;try{var flow=new FlowDocument{FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI"),FontSize=15,PagePadding=new Thickness(0),LineHeight=25};flow.SetResourceReference(TextElement.ForegroundProperty,"Ink");int[] counters=new int[7];bool inNumbers=false;foreach(var block in doc.Blocks){var p=new Paragraph{Tag=block.Kind,Margin=new Thickness(block.Indent*22,0,0,9),FontSize=block.Kind=="h1"?24:block.Kind=="h2"?19:15};if(block.Kind=="h1"||block.Kind=="h2"){p.FontWeight=FontWeights.SemiBold;p.Margin=new Thickness(block.Indent*22,9,0,10);}InlineCollection target=p.Inlines;
    if(HasChecklist(block)){var box=new CheckBox{IsChecked=block.Done,Margin=new Thickness(0,0,7,0),Focusable=false,VerticalAlignment=VerticalAlignment.Center,ToolTip="标记完成"};AutomationProperties.SetName(box,"便签待办勾选");p.Inlines.Add(new InlineUIContainer(box){BaselineAlignment=BaselineAlignment.Center});var completion=new Span{Tag="completion"};if(block.Done){completion.SetResourceReference(TextElement.ForegroundProperty,"Muted");completion.TextDecorations=TextDecorations.Strikethrough;}p.Inlines.Add(completion);target=completion.Inlines;box.Click+=(s,e)=>{loading=true;if(box.IsChecked==true){completion.SetResourceReference(TextElement.ForegroundProperty,"Muted");completion.TextDecorations=TextDecorations.Strikethrough;}else{completion.ClearValue(TextElement.ForegroundProperty);completion.TextDecorations=null;}loading=false;Record();};}
    foreach(var r in block.Runs){var run=new Run(r.Text){FontWeight=r.Bold?FontWeights.Bold:FontWeights.Normal,FontStyle=r.Italic?FontStyles.Italic:FontStyles.Normal};if(block.Kind=="h1"||block.Kind=="h2")run.FontWeight=FontWeights.SemiBold;var decorations=new TextDecorationCollection();if(r.Underline)decorations.Add(TextDecorations.Underline);if(r.Strike)decorations.Add(TextDecorations.Strikethrough);run.TextDecorations=decorations;if(r.Highlight)run.Background=new SolidColorBrush(Color.FromArgb(95,224,173,45));if(NoteCodec.SafeLink(r.Link)){var h=new Hyperlink(run){NavigateUri=new Uri(r.Link),ToolTip="Ctrl+单击打开链接"};h.SetResourceReference(TextElement.ForegroundProperty,"Accent");h.RequestNavigate+=(s,e)=>{if(Keyboard.Modifiers==ModifierKeys.Control)OpenLink(e.Uri.AbsoluteUri);e.Handled=true;};target.Add(h);}else target.Add(run);}
    if(block.Runs.Count==0)target.Add(new Run(""));
    if(block.Kind=="bullet"||block.Kind=="number"){int level=Math.Max(0,Math.Min(6,block.Indent));int number=1;if(block.Kind=="number"){if(!inNumbers)Array.Clear(counters,0,counters.Length);counters[level]++;for(int deeper=level+1;deeper<counters.Length;deeper++)counters[deeper]=0;number=counters[level];inNumbers=true;}else inNumbers=false;var list=new System.Windows.Documents.List{MarkerStyle=block.Kind=="number"?TextMarkerStyle.Decimal:TextMarkerStyle.Disc,StartIndex=Math.Max(1,number),Margin=new Thickness(block.Indent*22,0,0,0),Padding=new Thickness(24,0,0,0)};p.Margin=new Thickness(0,0,0,9);list.ListItems.Add(new ListItem(p));flow.Blocks.Add(list);}else{inNumbers=false;flow.Blocks.Add(p);}}
    if(flow.Blocks.Count==0)flow.Blocks.Add(new Paragraph());Editor.Document=flow;
   }finally{loading=false;}}
  static bool HasChecklist(NoteBlock block){return block.Checklist||block.Kind=="check";}
  List<int> SelectedBlocks(){
   var ps=Paragraphs(Editor.Document.Blocks).ToList();
   int a=ps.FindIndex(p=>Editor.Selection.Start.CompareTo(p.ContentEnd)<=0);if(a<0)a=ps.Count-1;
   int b=Editor.Selection.IsEmpty?a:ps.FindLastIndex(p=>Editor.Selection.End.CompareTo(p.ContentStart)>0&&(Editor.Selection.End.CompareTo(p.ContentEnd)>0||CharacterOffset(p,Editor.Selection.End)>0));b=Math.Max(a,b);
   return Enumerable.Range(a,b-a+1).ToList();
  }
  void ApplyBlocks(Action<NoteBlock> change){
   var indices=SelectedBlocks();var ps=Paragraphs(Editor.Document.Blocks).ToList();bool empty=Editor.Selection.IsEmpty;
   int first=indices.First(),last=indices.Last();int startChars=Editor.Selection.Start.CompareTo(ps[first].ContentStart)<=0?0:CharacterOffset(ps[first],Editor.Selection.Start);
   int endChars=Editor.Selection.End.CompareTo(ps[last].ContentEnd)>0?Plain(ps[last]).Length:CharacterOffset(ps[last],Editor.Selection.End);
   var doc=Capture();foreach(int i in indices)change(doc.Blocks[i]);Render(doc);ps=Paragraphs(Editor.Document.Blocks).ToList();
   var start=AtCharacter(ps[first],startChars);var end=AtCharacter(ps[last],endChars);if(empty)Editor.CaretPosition=start;else Editor.Selection.Select(start,end);Editor.Focus();Record();
  }
  public void ToggleChecklist(){
   var doc=Capture();bool enable=!SelectedBlocks().All(i=>HasChecklist(doc.Blocks[i]));
   ApplyBlocks(block=>{block.Checklist=enable;if(block.Kind=="body"&&enable)block.Kind="check";else if(block.Kind=="check"&&!enable)block.Kind="body";if(!enable)block.Done=false;});
  }
  public void SetKind(string kind){
   if(kind=="check"){ToggleChecklist();return;}var doc=Capture();bool toggle=kind=="bullet"||kind=="number";string target=toggle&&SelectedBlocks().All(i=>doc.Blocks[i].Kind==kind)?"body":kind;
   ApplyBlocks(block=>{bool check=HasChecklist(block);block.Kind=target=="body"&&check?"check":target;block.Checklist=check;});
  }
  public void Indent(int direction){ApplyBlocks(block=>block.Indent=Math.Max(0,Math.Min(6,block.Indent+direction)));}
  public void RefreshTheme(){ApplyColor();Editor.Document.Foreground=UI.Brush("Ink");foreach(var p in Paragraphs(Editor.Document.Blocks))RefreshInlines(p.Inlines);}
  void RefreshInlines(InlineCollection collection){foreach(var inline in collection){var h=inline as Hyperlink;if(h!=null)h.Foreground=UI.Brush("Accent");var span=inline as Span;if(span!=null){if((span.Tag as string)=="completion"&&span.TextDecorations!=null&&span.TextDecorations.Any())span.Foreground=UI.Brush("Muted");RefreshInlines(span.Inlines);}}}
  public void Command(RoutedUICommand command){Editor.Focus();command.Execute(null,Editor);Record();}
  public void Decoration(TextDecorationLocation location){Editor.Focus();var current=Editor.Selection.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection;var value=current==null?new TextDecorationCollection():current.Clone();bool had=value.Any(d=>d.Location==location);foreach(var d in value.Where(d=>d.Location==location).ToList())value.Remove(d);if(!had)value.Add(location==TextDecorationLocation.Strikethrough?TextDecorations.Strikethrough:TextDecorations.Underline);Editor.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty,value);Record();}
  public void Highlight(){Editor.Focus();var brush=Editor.Selection.GetPropertyValue(TextElement.BackgroundProperty) as SolidColorBrush;Editor.Selection.ApplyPropertyValue(TextElement.BackgroundProperty,brush!=null&&brush.Color.A!=0?Brushes.Transparent:new SolidColorBrush(Color.FromArgb(95,224,173,45)));Record();}
  public void ClearFormat(){Editor.Focus();Editor.Selection.ApplyPropertyValue(TextElement.FontWeightProperty,FontWeights.Normal);Editor.Selection.ApplyPropertyValue(TextElement.FontStyleProperty,FontStyles.Normal);Editor.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty,new TextDecorationCollection());Editor.Selection.ApplyPropertyValue(TextElement.BackgroundProperty,Brushes.Transparent);Record();}
  void Keys(object sender,KeyEventArgs e){
   if(Keyboard.Modifiers==ModifierKeys.Control&&(e.Key==Key.Z||e.Key==Key.Y)){if(e.Key==Key.Z)UndoEdit();else RedoEdit();e.Handled=true;return;}
   if(Keyboard.Modifiers==ModifierKeys.Control&&(e.Key==Key.D1||e.Key==Key.NumPad1)){ToggleChecklist();e.Handled=true;return;}
   if(e.Key==Key.Tab){Indent((Keyboard.Modifiers&ModifierKeys.Shift)!=0?-1:1);e.Handled=true;return;}
   if(e.Key!=Key.Enter||Keyboard.Modifiers!=ModifierKeys.None)return;var p=Editor.CaretPosition.Paragraph;if(p==null)return;
   var ps=Paragraphs(Editor.Document.Blocks).ToList();var b=Capture().Blocks[ps.IndexOf(p)];if(b.Kind=="body"&&!HasChecklist(b))return;e.Handled=true;
   if(String.IsNullOrWhiteSpace(Plain(p))&&Editor.Selection.IsEmpty){ApplyBlocks(block=>{block.Kind="body";block.Checklist=false;block.Done=false;});return;}
   loading=true;try{EditingCommands.EnterParagraphBreak.Execute(null,Editor);}finally{loading=false;}
   ps=Paragraphs(Editor.Document.Blocks).ToList();int next=ps.IndexOf(Editor.CaretPosition.Paragraph);if(next<0){Record();return;}
   var doc=Capture();var added=doc.Blocks[next];added.Kind=b.Kind=="h1"||b.Kind=="h2"?"body":b.Kind;added.Checklist=HasChecklist(b);if(added.Kind=="body"&&added.Checklist)added.Kind="check";added.Done=false;added.Indent=b.Indent;
   if(next>0){var previous=doc.Blocks[next-1];previous.Kind=b.Kind;previous.Checklist=HasChecklist(b);previous.Done=b.Done;previous.Indent=b.Indent;}
   Render(doc);Editor.CaretPosition=AtCharacter(Paragraphs(Editor.Document.Blocks).ElementAt(next),0);Editor.Focus();Record();
  }
  static TextPointer AtCharacter(Paragraph p,int count){var pos=p.ContentStart;while(pos!=null&&pos.CompareTo(p.ContentEnd)<0){if(pos.GetPointerContext(LogicalDirection.Forward)==TextPointerContext.Text){string text=pos.GetTextInRun(LogicalDirection.Forward);if(count<=text.Length)return pos.GetPositionAtOffset(count);count-=text.Length;pos=pos.GetPositionAtOffset(text.Length);}else {if(pos.GetPointerContext(LogicalDirection.Forward)==TextPointerContext.ElementStart&&pos.GetAdjacentElement(LogicalDirection.Forward) is LineBreak){if(count==0)return pos;count--;}pos=pos.GetNextContextPosition(LogicalDirection.Forward);}}return p.ContentEnd;}
  void AutoLink(){var p=Editor.CaretPosition.Paragraph;if(p==null)return;string before=TextBefore(p,Editor.CaretPosition);var match=Regex.Match(before,@"https?://[^\s<>]+(?=\s$)");if(!match.Success||!NoteCodec.SafeLink(match.Value))return;var start=AtCharacter(p,match.Index);var end=AtCharacter(p,match.Index+match.Length);if(start.Parent is Hyperlink||start.Parent is Run&&((Run)start.Parent).Parent is Hyperlink)return;loading=true;var h=new Hyperlink(start,end){NavigateUri=new Uri(match.Value),ToolTip="Ctrl+单击打开链接"};h.SetResourceReference(TextElement.ForegroundProperty,"Accent");h.RequestNavigate+=(s,e)=>{if(Keyboard.Modifiers==ModifierKeys.Control)OpenLink(e.Uri.AbsoluteUri);e.Handled=true;};loading=false;Record();}
  void InsertLink(){var dialog=UI.Dialog(this,"插入链接",380,205);var field=UI.Input("网址","https://");var panel=UI.Stack(UI.Label("网址 · 支持 https:// 或 http://"),field);panel.Margin=new Thickness(20);panel.Children.Add(UI.Button("插入",()=>{if(!NoteCodec.SafeLink(field.Text)){UI.Notice(dialog,"请输入完整的 http 或 https 网址。");return;}dialog.DialogResult=true;},true));dialog.Content=panel;if(dialog.ShowDialog()!=true)return;Editor.Focus();loading=true;if(Editor.Selection.IsEmpty){var start=Editor.CaretPosition;var run=new Run(field.Text,start);var link=new Hyperlink(run.ElementStart,run.ElementEnd){NavigateUri=new Uri(field.Text)};link.SetResourceReference(TextElement.ForegroundProperty,"Accent");Editor.CaretPosition=link.ElementEnd;}else{var link=new Hyperlink(Editor.Selection.Start,Editor.Selection.End){NavigateUri=new Uri(field.Text)};link.SetResourceReference(TextElement.ForegroundProperty,"Accent");}loading=false;Record();}
  void OpenLink(string link){if(!NoteCodec.SafeLink(link))return;try{System.Diagnostics.Process.Start(link);}catch(Exception ex){UI.Notice(this,"无法打开链接："+ex.Message);}}
 }
}
