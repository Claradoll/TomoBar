using System;
using System.IO;
using Microsoft.Win32;

namespace LittleTomato {
 // Machine-local preference: never import startup registration from a task backup.
 public sealed class StartupRegistration {
  public const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
  public const string ValueName="LittleTomato";
  internal readonly string KeyPath;
  readonly string executable;
  public StartupRegistration(string executablePath):this(executablePath,RunKey){}
  internal StartupRegistration(string executablePath,string keyPath){executable=executablePath;KeyPath=keyPath;}
  public static string CommandFor(string path){
   if(String.IsNullOrWhiteSpace(path)||!Path.IsPathRooted(path)||path.IndexOf('"')>=0||path.IndexOf('\r')>=0||path.IndexOf('\n')>=0)
    throw new ArgumentException("无法使用当前程序路径，请将软件放在固定文件夹后重试。");
   if(path.Length>245)throw new ArgumentException("软件路径过长，请移至较短的文件夹路径后开启自启。");
   string command="\""+Path.GetFullPath(path)+"\" --background";
   if(command.Length>260)throw new ArgumentException("软件路径过长，请移至较短的文件夹路径后开启自启。");
   return command;
  }
  public bool IsRegistered {get {using(var key=Registry.CurrentUser.OpenSubKey(KeyPath))return key!=null&&!String.IsNullOrWhiteSpace(key.GetValue(ValueName,null,RegistryValueOptions.DoNotExpandEnvironmentNames) as string);}}
  public bool UsesCurrentPath {get {using(var key=Registry.CurrentUser.OpenSubKey(KeyPath))return key!=null&&String.Equals(key.GetValue(ValueName) as string,CommandFor(executable),StringComparison.OrdinalIgnoreCase);}}
  public void SetEnabled(bool enabled){
   if(enabled){
    string command=CommandFor(executable);
    if(!File.Exists(executable))throw new IOException("当前程序文件不存在，无法设置开机自启。");
    using(var key=Registry.CurrentUser.CreateSubKey(KeyPath)){
     if(key==null)throw new UnauthorizedAccessException("无法写入当前用户的启动设置。");
     key.SetValue(ValueName,command,RegistryValueKind.String);
     if(!String.Equals(key.GetValue(ValueName) as string,command,StringComparison.Ordinal))throw new IOException("启动设置未能写入，请重试。");
    }
   }else using(var key=Registry.CurrentUser.OpenSubKey(KeyPath,true)){if(key!=null)key.DeleteValue(ValueName,false);}
  }
 }
}
