using D2RAssist.Types;
using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace D2RAssist.Helpers
{
    public class ProcessWrapper
    {
        public Offsets Offsets;
        public IntPtr ProcessAddress;
        public IntPtr MainWindowHandle;
        private Process _process;
        IntPtr? _processHandle;

        public ProcessWrapper()
        {
            _process = Process.GetProcessesByName("D2R")[0];
            _processHandle =
                WindowsExternal.OpenProcess((uint)WindowsExternal.ProcessAccessFlags.VirtualMemoryRead, false, _process.Id);
            ProcessAddress = _process.MainModule.BaseAddress;
            MainWindowHandle = _process.MainWindowHandle;
            IntPtr processHandle = _processHandle.Value;
            Offsets = new Offsets(_process.MainModule, ref processHandle);
        }

        ~ProcessWrapper()
        {
            try
            {
                Close();
            }
            catch (Exception)
            {
                return;
            }
        }

        public void Close()
        {
            if (_processHandle == null)
            {
                return;
            }
            
            WindowsExternal.CloseHandle(_processHandle.Value); 
            _processHandle = null;
        }

        public bool ReadMemory(IntPtr address, [Out] byte[] buffer, int dwSize, out IntPtr bytesRead)
        {
            bytesRead = default;
            return _processHandle.HasValue &&
                   WindowsExternal.ReadProcessMemory(_processHandle.Value, address, buffer, dwSize, out bytesRead);
        }
    }
    
}
