using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OpenD
{
    // Job Object Windows: yt-dlp и все его дочерние процессы (ffmpeg, deno) умирают вместе с задачей
    // или с самим OpenD — «процессы-сироты» больше не держат скачанные файлы занятыми.
    sealed class Job : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        struct BasicLimit
        {
            public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass, SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct IoCounters { public ulong A, B, C, D, E, F; }

        [StructLayout(LayoutKind.Sequential)]
        struct ExtendedLimit
        {
            public BasicLimit Basic;
            public IoCounters Io;
            public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateJobObject(IntPtr attrs, string name);
        [DllImport("kernel32.dll")] static extern bool SetInformationJobObject(IntPtr job, int cls, IntPtr info, int len);
        [DllImport("kernel32.dll")] static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
        [DllImport("kernel32.dll")] static extern bool TerminateJobObject(IntPtr job, uint exitCode);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

        IntPtr handle;

        public Job()
        {
            handle = CreateJobObject(IntPtr.Zero, null);
            if (handle == IntPtr.Zero) return;
            ExtendedLimit info = new ExtendedLimit();
            info.Basic.LimitFlags = 0x2000;                      // KILL_ON_JOB_CLOSE
            int size = Marshal.SizeOf(typeof(ExtendedLimit));
            IntPtr p = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(info, p, false);
                if (!SetInformationJobObject(handle, 9, p, size)) Log.Write("Job: SetInformationJobObject не удалось");
            }
            finally { Marshal.FreeHGlobal(p); }
        }

        public void Add(Process proc)
        {
            if (handle != IntPtr.Zero && !AssignProcessToJobObject(handle, proc.Handle))
                Log.Write("Job: AssignProcessToJobObject не удалось, код " + Marshal.GetLastWin32Error());
        }

        public void KillAll()
        {
            if (handle != IntPtr.Zero) TerminateJobObject(handle, 1);
        }

        public void Dispose()
        {
            if (handle == IntPtr.Zero) return;
            CloseHandle(handle);                                 // KILL_ON_JOB_CLOSE добивает оставшихся
            handle = IntPtr.Zero;
        }
    }
}
