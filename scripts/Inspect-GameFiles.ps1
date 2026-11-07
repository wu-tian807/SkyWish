# Read-only diagnostics: report disk files currently open by the game, no window control.
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
public static class GameOpenFiles {
  [DllImport("ntdll.dll")] static extern int NtQuerySystemInformation(int c, IntPtr b, int n, out int required);
  [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
  [DllImport("kernel32.dll")] static extern bool DuplicateHandle(IntPtr source, IntPtr handle, IntPtr target, out IntPtr copy, uint access, bool inherit, uint options);
  [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
  [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
  [DllImport("kernel32.dll")] static extern uint GetFileType(IntPtr h);
  [DllImport("kernel32.dll")] static extern bool GetFileInformationByHandle(IntPtr h, out FileInfo info);
  [StructLayout(LayoutKind.Sequential, Pack=4)] struct FileInfo { public uint attr; public long created,accessed,written; public uint volume,sizeHigh,sizeLow,links,indexHigh,indexLow; }
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] static extern uint GetFinalPathNameByHandle(IntPtr h, StringBuilder b, uint n, uint flags);
  public static string[] Read(int pid) {
    var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    IntPtr source = OpenProcess(0x40, false, pid);
    if(source == IntPtr.Zero) throw new Exception("Cannot open game for read-only handle inspection");
    IntPtr buffer = IntPtr.Zero;
    try {
      int size = 1024*1024, required, status;
      do {
        if(buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
        buffer = Marshal.AllocHGlobal(size);
        status = NtQuerySystemInformation(64, buffer, size, out required);
        if(status == unchecked((int)0xC0000004)) size = Math.Max(size*2, required+65536);
        else break;
      } while(size < 256*1024*1024);
      if(status != 0) throw new Exception("Handle inventory status " + status);
      long count = Marshal.ReadIntPtr(buffer).ToInt64();
      for(long i=0;i<count;i++) {
        IntPtr item = new IntPtr(buffer.ToInt64()+16+i*40);
        if(Marshal.ReadIntPtr(item,8).ToInt64()!=pid) continue;
        IntPtr copy;
        if(!DuplicateHandle(source, Marshal.ReadIntPtr(item,16), GetCurrentProcess(), out copy, 0, false, 2)) continue;
        try {
          if(GetFileType(copy)!=1) continue;
          var path = new StringBuilder(32768);
          uint n = GetFinalPathNameByHandle(copy,path,(uint)path.Capacity,0);
          if(n>0 && n<path.Capacity) {
            FileInfo info;
            string detail = GetFileInformationByHandle(copy,out info) ? " [id="+info.indexHigh.ToString("x8")+info.indexLow.ToString("x8")+"; size="+(((long)info.sizeHigh<<32)+info.sizeLow)+"]" : "";
            result.Add(path.ToString()+detail);
          }
        } finally { CloseHandle(copy); }
      }
      var output = new string[result.Count]; result.CopyTo(output); Array.Sort(output); return output;
    } finally { if(buffer!=IntPtr.Zero) Marshal.FreeHGlobal(buffer); CloseHandle(source); }
  }
}
'@
$game = Get-Process Cities2 -ErrorAction Stop
if (@($game).Count -ne 1) { throw 'Expected exactly one running game' }
[GameOpenFiles]::Read($game.Id) | Where-Object { $_ -match 'LocalLow|\.log$|CityDirector' }
