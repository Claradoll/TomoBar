using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LittleTomato {
 public partial class MainWindow {
  NotesPage notesPage;
  void BuildNotes(){notesPage=new NotesPage(app);content.Children.Add(notesPage);}
  public void RefreshNoteList(){if(Page=="notes"&&notesPage!=null)notesPage.Refresh();}
 }
 public class NotesPage : Grid {
  public bool IsEditingTitle {get{return editingNote!=null;}}
  readonly Program app;TextBox search;StackPanel list;bool trash;Button trashButton;TextBlock count;Note editingNote;TextBox titleEditor;bool committingTitle;
  public NotesPage(Program owner){
   app=owner;RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});RowDefinitions.Add(new RowDefinition());
   var top=UI.Columns(-1,132);var heading=UI.Text("记下来，继续专注。",26,"Ink");heading.FontWeight=FontWeights.SemiBold;var sub=UI.Text("灵感、清单与下一步，都有地方安放。",12,"Muted");sub.Margin=new Thickness(0,9,0,0);top.Children.Add(UI.Stack(heading,sub));var add=UI.Button("新建便签",()=>app.Notes.New(),true);UI.IconLabel(add,"新建便签","plus");add.VerticalAlignment=VerticalAlignment.Center;UI.At(top,add,0,1);top.Margin=new Thickness(0,0,0,22);Children.Add(top);
   var filters=UI.Columns(-1,100);search=UI.Input("搜索便签","");search.ToolTip="搜索标题和正文";search.Height=42;search.TextChanged+=(s,e)=>Refresh();var searchHost=new Grid();searchHost.Children.Add(search);var hint=UI.Text("搜索标题或正文…",12,"Muted");hint.Margin=new Thickness(14,0,14,0);hint.VerticalAlignment=VerticalAlignment.Center;hint.IsHitTestVisible=false;searchHost.Children.Add(hint);search.TextChanged+=(s,e)=>hint.Visibility=search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;filters.Children.Add(searchHost);trashButton=UI.Button("回收站",()=>{trash=!trash;trashButton.Content=trash?"全部便签":"回收站";Refresh();},false);trashButton.Margin=new Thickness(10,0,0,0);UI.At(filters,trashButton,0,1);filters.Margin=new Thickness(0,0,0,16);Grid.SetRow(filters,1);Children.Add(filters);
   list=new StackPanel();var scroll=new ScrollViewer{Content=list,Padding=new Thickness(0,0,8,0),VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetRow(scroll,2);Children.Add(scroll);Refresh();
  }
  void BeginTitleEdit(Note note,Grid host){
   if(editingNote!=null)return;editingNote=note;titleEditor=UI.Input("便签标题 "+note.Id,note.Title);titleEditor.FontSize=17;titleEditor.FontWeight=FontWeights.SemiBold;titleEditor.Padding=new Thickness(4,2,4,2);titleEditor.Margin=new Thickness(-5,0,0,0);titleEditor.ToolTip="留空时使用正文第一行";host.Children.Clear();host.Children.Add(titleEditor);
   var editor=titleEditor;editor.PreviewKeyDown+=(s,e)=>{if(!Object.ReferenceEquals(titleEditor,editor))return;if(e.Key==System.Windows.Input.Key.Enter){e.Handled=true;CommitTitle(false);}else if(e.Key==System.Windows.Input.Key.Escape){e.Handled=true;CommitTitle(true);}};
   editor.LostKeyboardFocus+=(s,e)=>{if(Object.ReferenceEquals(titleEditor,editor))CommitTitle(false);};editor.Focus();editor.SelectAll();
  }
  void CommitTitle(bool cancel){
   if(editingNote==null||committingTitle)return;committingTitle=true;var note=editingNote;var editor=titleEditor;
   try{if(!cancel&&!app.Notes.Rename(note,editor.Text))return;editingNote=null;titleEditor=null;Refresh();}finally{committingTitle=false;}
  }
  public void Refresh(){if(list==null||editingNote!=null)return;list.Children.Clear();string query=search.Text.Trim();var notes=app.Data.Notes.Where(n=>n.Deleted==trash&&(query.Length==0||(n.Title+"\n"+n.PlainText).IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0)).OrderByDescending(n=>n.Pinned).ThenByDescending(n=>n.Updated).ToList();count=UI.Text((trash?"回收站 · ":"全部便签 · ")+notes.Count,11,"Muted");count.Margin=new Thickness(2,0,0,12);list.Children.Add(count);
   if(notes.Count==0){var empty=UI.Stack(UI.Text(trash?"这里没有已删除的便签":query.Length>0?"没有找到相关便签":"给此刻的想法留个位置。",20,"Ink"),UI.Text(trash?"删除的便签可在这里恢复。":query.Length>0?"换个关键词试试。":"点击「新建便签」直接记录，输入会自动保存。",12,"Muted"));foreach(FrameworkElement item in empty.Children)item.Margin=new Thickness(0,10,0,6);list.Children.Add(UI.Card(empty,new Thickness(28)));return;}
   foreach(var item in notes){var note=item;var row=UI.Columns(-1,trash?86:148);var name=UI.Text(note.DisplayTitle,17,"Ink");name.FontWeight=FontWeights.SemiBold;name.TextTrimming=TextTrimming.CharacterEllipsis;var preview=UI.Text(String.IsNullOrWhiteSpace(note.PlainText)?"空白便签 · 点击开始记录":note.PlainText.Replace('\n',' '),12,"Muted");preview.TextTrimming=TextTrimming.CharacterEllipsis;preview.Margin=new Thickness(0,8,0,8);var task=app.Data.Tasks.FirstOrDefault(t=>t.Id==note.TaskId);var date=new DateTime(note.Updated,DateTimeKind.Utc).ToLocalTime().ToString("M月d日 HH:mm");var meta=UI.Text((note.Pinned?"置顶 · ":"")+date+(task==null?"":" · "+task.Title),10,"Muted");meta.TextTrimming=TextTrimming.CharacterEllipsis;
    var open=UI.Button("",()=>app.Notes.Open(note),false);open.Content=UI.Stack(preview,meta);open.HorizontalContentAlignment=HorizontalAlignment.Stretch;open.Background=Brushes.Transparent;open.Padding=new Thickness(0);System.Windows.Automation.AutomationProperties.SetName(open,"打开便签 "+note.DisplayTitle);var titleHost=new Grid{Height=32};var editTitle=UI.Button("",()=>BeginTitleEdit(note,titleHost),false);editTitle.Content=name;editTitle.Padding=new Thickness(0);editTitle.Background=Brushes.Transparent;editTitle.HorizontalContentAlignment=HorizontalAlignment.Stretch;editTitle.ToolTip="单击编辑标题 · Enter 保存，Esc 取消";System.Windows.Automation.AutomationProperties.SetName(editTitle,"编辑便签标题 "+note.Id);titleHost.Children.Add(editTitle);row.Children.Add(UI.Stack(titleHost,open));
    var actions=UI.Row();if(trash)actions.Children.Add(UI.Button("恢复",()=>app.Notes.Restore(note),false));else{actions.Children.Add(UI.Button("打开",()=>app.Notes.Open(note),false));actions.Children.Add(UI.Button("删除",()=>app.Notes.Delete(note),false));}foreach(FrameworkElement b in actions.Children)b.Margin=new Thickness(7,0,0,0);actions.VerticalAlignment=VerticalAlignment.Center;UI.At(row,actions,0,1);
    var card=UI.Card(row,new Thickness(20));card.BorderThickness=new Thickness(3,1,1,1);card.BorderBrush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(note.Color=="sage"?"#98B29B":note.Color=="rose"?"#D4A1A3":"#D9BA77"));card.Margin=new Thickness(0,0,0,12);list.Children.Add(card);
   }
  }
 }
}
