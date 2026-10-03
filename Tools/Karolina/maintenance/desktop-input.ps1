param([Parameter(Mandatory=$true)][long]$WindowHandle, [string]$Mode='probe', [string]$Text='KAROLINA_INPUT_PROBE')
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName WindowsBase
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class KarolinaInputDriver {
  [StructLayout(LayoutKind.Sequential)] public struct POINT {public int X,Y;}
  [StructLayout(LayoutKind.Sequential)] public struct GUIINFO {public int Size,Flags;public IntPtr Active,Focus,Capture,MenuOwner,MoveSize,Caret;public int Left,Top,Right,Bottom;}
  [StructLayout(LayoutKind.Sequential)] public struct KEYINPUT {public ushort Vk,Scan;public uint Flags,Time;public UIntPtr Extra;}
  [StructLayout(LayoutKind.Explicit)] public struct INPUTUNION {[FieldOffset(0)] public KEYINPUT Key;[FieldOffset(0)] public long Pad1;[FieldOffset(8)] public long Pad2;[FieldOffset(16)] public long Pad3;[FieldOffset(24)] public long Pad4;}
  [StructLayout(LayoutKind.Sequential)] public struct INPUT {public uint Type;public INPUTUNION Data;}
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
  [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT point);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
  [DllImport("user32.dll")] public static extern bool GetGUIThreadInfo(uint thread,ref GUIINFO info);
  [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h,uint flags);
  [DllImport("user32.dll",SetLastError=true)] public static extern uint SendInput(uint count,INPUT[] input,int size);
  public static long FocusRoot() {var info=new GUIINFO();info.Size=Marshal.SizeOf(info);if(!GetGUIThreadInfo(0,ref info))return 0;return GetAncestor(info.Focus,2).ToInt64();}
  public static void Click(int x,int y) {SetCursorPos(x,y);mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);}
  public static void End() {var input=new INPUT[4];ushort[] vk={0x11,0x23,0x23,0x11};for(int i=0;i<4;i++){input[i].Type=1;input[i].Data.Key.Vk=vk[i];input[i].Data.Key.Flags=(uint)(i<2?0:2);}if(SendInput(4,input,Marshal.SizeOf<INPUT>())!=4)throw new InvalidOperationException("Ctrl+End failed");}
  public static void Type(string text) {foreach(char c in text){var input=new INPUT[2];input[0].Type=input[1].Type=1;input[0].Data.Key.Scan=input[1].Data.Key.Scan=c;input[0].Data.Key.Flags=4;input[1].Data.Key.Flags=6;if(SendInput(2,input,Marshal.SizeOf<INPUT>())!=2)throw new InvalidOperationException("SendInput failed "+Marshal.GetLastWin32Error());}}
}
'@
$root=[System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$WindowHandle)
function Find-Element([string]$name,$type=$null) {
    $condition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$name)
    if($type){$condition=[System.Windows.Automation.AndCondition]::new($condition,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,$type))}
    $deadline=[DateTime]::UtcNow.AddSeconds(10)
    do {
        $element=$root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)
        if($element){return $element}
        Start-Sleep -Milliseconds 100
    } while([DateTime]::UtcNow-lt$deadline)
    return $null
}
function Get-Text($element) {
    $pattern=$null
    if($element.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern,[ref]$pattern)){return $pattern.Current.Value}
    if($element.TryGetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern,[ref]$pattern)){return $pattern.DocumentRange.GetText(-1)}
    throw 'Input has no readable value/text pattern'
}
function Type-Into([string]$name,[string]$value) {
    $element=Find-Element $name ([System.Windows.Automation.ControlType]::Edit)
    if(!$element){throw "Cannot find input: $name"}
    [KarolinaInputDriver]::SetForegroundWindow([IntPtr]$WindowHandle)|Out-Null
    $rect=$element.Current.BoundingRectangle
    [KarolinaInputDriver]::Click([int]($rect.Left+$rect.Width/2),[int]($rect.Top+$rect.Height/2))
    Start-Sleep -Milliseconds 150
    $focus=[KarolinaInputDriver]::FocusRoot()
    if($focus-ne$WindowHandle){throw "Keyboard focus is outside Karolina: $focus"}
    [KarolinaInputDriver]::End()
    [KarolinaInputDriver]::Type($value)
    Start-Sleep -Milliseconds 150
    @{name=$name;value=(Get-Text $element);keyboardFocus=$element.Current.HasKeyboardFocus;rect=@($rect.Left,$rect.Top,$rect.Width,$rect.Height)}
}
$cursor=[KarolinaInputDriver+POINT]::new();[KarolinaInputDriver]::GetCursorPos([ref]$cursor)|Out-Null
try {
    if($Mode-eq'probe'){
        $condition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button)
        $buttons=@($root.FindAll([System.Windows.Automation.TreeScope]::Descendants,$condition)|ForEach-Object {$_.Current.Name})
        @{buttons=$buttons}|ConvertTo-Json -Depth 3 -Compress
        $input=Type-Into '给 Codex 的指令' $Text
        @{buttons=$buttons;input=$input}|ConvertTo-Json -Depth 6 -Compress
    } elseif($Mode-eq'chat') {Type-Into '给 Codex 的指令' $Text|ConvertTo-Json -Compress}
    elseif($Mode-eq'git') {
        $button=Find-Element 'Git 变更';if(!$button){throw 'Git navigation missing'}
        $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Milliseconds 250
        Type-Into '提交说明' $Text|ConvertTo-Json -Compress
    } elseif($Mode-eq'search') {Type-Into '搜索标题' $Text|ConvertTo-Json -Compress}
    elseif($Mode-eq'document') {
        $button=Find-Element '需求案' ([System.Windows.Automation.ControlType]::Button)
        $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        $condition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button)
        $deadline=[DateTime]::UtcNow.AddSeconds(10);$document=$null
        do {
            $document=$root.FindAll([System.Windows.Automation.TreeScope]::Descendants,$condition)|Where-Object {$_.Current.Name.StartsWith('真实桌面输入验收')}|Select-Object -First 1
            if(!$document){Start-Sleep -Milliseconds 100}
        } while(!$document-and[DateTime]::UtcNow-lt$deadline)
        if(!$document){throw 'Fixture document missing'}
        $document.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Milliseconds 250
        $button=Find-Element '编辑正文' ([System.Windows.Automation.ControlType]::Button)
        $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        $result=Type-Into '文档正文' $Text
        $save=Find-Element '保存' ([System.Windows.Automation.ControlType]::Button)
        $save.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        $result|ConvertTo-Json -Compress
    }
} finally {[KarolinaInputDriver]::SetCursorPos($cursor.X,$cursor.Y)|Out-Null}
