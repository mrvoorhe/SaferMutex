using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace SaferMutex
{

    public interface ISaferMutexMutex : IDisposable
    {
        void WaitOne();
        bool WaitOne(int millisecondsTimeout);
        bool WaitOne(WaitHandle breakHandle);
        bool WaitOne(WaitHandle breakHandle, int millisecondsTimeout);
        bool WaitOne(WaitHandle[] breakHandles, out int index);
        bool WaitOne(WaitHandle[] breakHandles, int millisecondsTimeout, out int index);
        void ReleaseMutex();
    }

    public enum Scope
    {
        CurrentProcess,
        CurrentUser,

        // Note : Probably not going to implement this option anytime soon, so just comment it out for now
        //SystemWide
    }

    public sealed class SaferMutex : ISaferMutexMutex
    {
        private readonly ISaferMutexMutex _implementation;

        public SaferMutex(bool initiallyOwned, string name, Scope scope, out bool owned, out bool createdNew)
        {
            // Easy, all runtime's and platforms will support current process mutex
            if (scope == Scope.CurrentProcess)
                _implementation = new FrameworkMutexBased(initiallyOwned, name, scope, out owned, out createdNew);
            else
            {
                if (PlatformAndRuntimeSupportsGlobalMutex)
                    _implementation = new FrameworkMutexBased(initiallyOwned, name, scope, out owned, out createdNew);
                else
                    _implementation = new FileBased(initiallyOwned, name, scope, out owned, out createdNew);
            }
        }

        public static SaferMutex Create(bool initiallyOwned, string name, Scope scope, out bool owned)
        {
            bool notUsed;
            return Create(initiallyOwned, name, scope, out owned, out notUsed);
        }

        public static SaferMutex Create(bool initiallyOwned, string name, Scope scope, out bool owned, out bool createdNew)
        {
            return new SaferMutex(initiallyOwned, name, scope, out owned, out createdNew);
        }

        private static bool OnWindows
        {
            get
            {
                switch (Environment.OSVersion.Platform)
                {
                    case PlatformID.Win32NT:
                    case PlatformID.Win32S:
                    case PlatformID.Win32Windows:
                    case PlatformID.WinCE:
                    case PlatformID.Xbox:
                        return true;
                    default:
                        return false;
                }
            }
        }

        private static bool PlatformAndRuntimeSupportsGlobalMutex
        {
            get
            {
                // At some point improve this to detect if running on .NET vs mono vs .NET Core as their support for global mutex may differ in the future
                return OnWindows;
            }
        }

        public void Dispose()
        {
            _implementation.Dispose();
        }

        public void WaitOne()
        {
            _implementation.WaitOne();
        }

        public bool WaitOne(int millisecondsTimeout)
        {
            return _implementation.WaitOne(millisecondsTimeout);
        }

        public bool WaitOne(WaitHandle breakHandle)
        {
            return _implementation.WaitOne(breakHandle);
        }

        public bool WaitOne(WaitHandle breakHandle, int millisecondsTimeout)
        {
            return _implementation.WaitOne(breakHandle, millisecondsTimeout);
        }

        public bool WaitOne(WaitHandle[] breakHandles, out int index)
        {
            return _implementation.WaitOne(breakHandles, out index);
        }

        public bool WaitOne(WaitHandle[] breakHandles, int millisecondsTimeout, out int index)
        {
            return _implementation.WaitOne(breakHandles, millisecondsTimeout, out index);
        }

        public void ReleaseMutex()
        {
            _implementation.ReleaseMutex();
        }

        #region Implementation Classes

        internal sealed class FrameworkMutexBased : ISaferMutexMutex
        {
            private readonly Mutex _mutex;
            private bool _disposed;
            private bool _hasLock;

            public FrameworkMutexBased(bool initiallyOwned, string name, Scope scope, out bool owned, out bool createdNew)
            {
                if (name.StartsWith("Global\\"))
                    throw new ArgumentException($"The Global\\ name prefix should not be used.  Use the Scope parameter to control the scope of the mutex");

                switch (scope)
                {
                    case Scope.CurrentProcess:
                        _mutex = new Mutex(initiallyOwned, name, out createdNew);
                        break;
                    case Scope.CurrentUser:
                        _mutex = new Mutex(initiallyOwned, $"Global\\{name}", out createdNew);
                        break;
                    //case Scope.SystemWide:
                    //    _mutex = CreateForMultiUser(initiallyOwned, name, out createdNew);
                    //    break;
                    default:
                        throw new NotImplementedException($"Support for scope {scope} has not been implemented");
                }

                if (!initiallyOwned)
                {
                    owned = false;
                    return;
                }

                if (createdNew)
                {
                    _hasLock = owned = true;
                    return;
                }

                _hasLock = owned = WaitOne(0);
            }

            public void WaitOne()
            {
                _mutex.WaitOne();
                _hasLock = true;
            }

            public bool WaitOne(int millisecondsTimeout)
            {
                _hasLock = _mutex.WaitOne(millisecondsTimeout);
                return _hasLock;
            }

            public bool WaitOne(WaitHandle breakHandle)
            {
                return WaitOne(breakHandle, Timeout.Infinite);
            }

            public bool WaitOne(WaitHandle breakHandle, int millisecondsTimeout)
            {
                int notUsed;
                return WaitOne(new[] { breakHandle }, millisecondsTimeout, out notUsed);
            }

            public bool WaitOne(WaitHandle[] breakHandles, out int index)
            {
                return WaitOne(breakHandles, Timeout.Infinite, out index);
            }

            public bool WaitOne(WaitHandle[] breakHandles, int millisecondsTimeout, out int index)
            {
                using (var wrapperHandle = new ManualResetEvent(false))
                {
                    wrapperHandle.SafeWaitHandle = _mutex.SafeWaitHandle;
                    WaitHandle[] handles = new WaitHandle[breakHandles.Length + 1];
                    handles[0] = wrapperHandle;

                    for (int i = 0; i < breakHandles.Length; i++)
                    {
                        handles[i + 1] = breakHandles[i];
                    }

                    int setIndex = WaitHandle.WaitAny(handles, millisecondsTimeout);

                    // Timeout
                    if (setIndex < 0)
                    {
                        index = setIndex;
                        return false;
                    }

                    // Mutex owned
                    if (setIndex == 0)
                    {
                        index = int.MinValue;
                        return true;
                    }

                    // Otherwise, a break handle was set.
                    // Need to adjust the index by -1 so that the index
                    // returned matches up with the index in the array that was passed in
                    index = setIndex - 1;
                    return false;
                }
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                if (_hasLock)
                    ReleaseMutex();

                if (_mutex != null)
                    _mutex.Dispose();

                _disposed = true;
            }

            public void ReleaseMutex()
            {
                _mutex.ReleaseMutex();
                _hasLock = false;
            }

            private static Mutex CreateForMultiUser(bool initiallyOwned, string name, out bool createdNew)
            {
                // Note multi user is not implemented yet and some of the code isn't in netstd1.6, so just comment it out for now
                //MutexSecurity security = new MutexSecurity();
                //MutexAccessRule rule = new MutexAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), MutexRights.FullControl, AccessControlType.Allow);
                //security.AddAccessRule(rule);
                //return new Mutex(initiallyOwned, $"Global\\{name}", out createdNew, security);
                throw new NotImplementedException();
            }
        }

        internal sealed class FileBased : ISaferMutexMutex
        {
            private const string LockFileExtension = "lock";
            private const int WaitPeriodDuringInfiniteTimeout = 100;

            private const int AttemptsPerTimeout = 10;

            private readonly string _lockFilePath;
            private readonly bool _createdNew;
            private FileStream _lockFileStream;
            private bool _hasLock;

            private bool _disposed;

            public FileBased(bool initiallyOwned, string name, Scope scope, out bool owned, out bool createdNew, string customRootLockDirectory = null)
            {
                if (name.StartsWith("Global\\"))
                    throw new ArgumentException($"The Global\\ name prefix should not be used.  Use the Scope parameter to control the scope of the mutex");

                _lockFilePath = GenerateLockFilePath(name, scope, customRootLockDirectory);

                if (!initiallyOwned)
                {
                    TryTouchLockFile(out createdNew);
                    owned = false;
                    _createdNew = createdNew;
                    return;
                }

                if (!CreateOrOpenLockFileStream(out createdNew))
                {
                    owned = false;
                    _createdNew = createdNew;
                    return;
                }

                owned = TryLock();
                _createdNew = createdNew;
            }

            public void WaitOne()
            {
                WaitOne(null);
            }

            public bool WaitOne(int millisecondsTimeout)
            {
                return WaitOne(null, millisecondsTimeout);
            }

            public bool WaitOne(WaitHandle breakHandle)
            {
                return WaitOne(breakHandle, Timeout.Infinite);
            }

            public bool WaitOne(WaitHandle breakHandle, int millisecondsTimeout)
            {
                int notUsed;
                return WaitOne(breakHandle == null ? null : new [] { breakHandle }, millisecondsTimeout, out notUsed);
            }

            public bool WaitOne(WaitHandle[] breakHandles, out int index)
            {
                return WaitOne(breakHandles, Timeout.Infinite, out index);
            }

            public bool WaitOne(WaitHandle[] breakHandles, int millisecondsTimeout, out int index)
            {
                // If the timeout is 0, then we will just check and return immediately.
                if (millisecondsTimeout == 0)
                {
                    if (TryLock())
                    {
                        index = int.MinValue;
                        return true;
                    }

                    index = -1;
                    return false;
                }

                int sleepTimeBetweenTries = WaitPeriodDuringInfiniteTimeout;
                if (millisecondsTimeout != Timeout.Infinite)
                {
                    // Make sure the sleep time is at least 1 ms. (not 0).
                    sleepTimeBetweenTries = Math.Max(millisecondsTimeout / AttemptsPerTimeout, 1);

                    // Make sure that a long timeout doesn't mean we wont check very often.  The InfiniteTimeout period should
                    // be the longest that any mutex ever waits to check.
                    sleepTimeBetweenTries = Math.Min(sleepTimeBetweenTries, WaitPeriodDuringInfiniteTimeout);
                }

                int elapsedTimeCounter = 0;
                while (true)
                {
                    if (TryLock())
                    {
                        index = int.MinValue;
                        return true;
                    }

                    // Do stuff for timeout break condition
                    if (millisecondsTimeout != Timeout.Infinite)
                    {
                        elapsedTimeCounter += sleepTimeBetweenTries;

                        if (elapsedTimeCounter >= millisecondsTimeout)
                        {
                            index = -1;
                            return false;
                        }
                    }

                    if (breakHandles != null)
                    {
                        // While we sleep, wait on our break handless
                        int breakIndex = WaitHandle.WaitAny(breakHandles, sleepTimeBetweenTries);

                        if (breakIndex != WaitHandle.WaitTimeout)
                        {
                            index = breakIndex;
                            return false;
                        }
                    }
                    else
                    {
                        Thread.Sleep(sleepTimeBetweenTries);
                    }
                }
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                ReleaseMutex();

                if (_lockFileStream != null)
                {
                    _lockFileStream.Dispose();
                    _lockFileStream = null;
                }

                TryDeleteLock();

                this._disposed = true;
            }

            public void ReleaseMutex()
            {
                if (_hasLock == false)
                {
                    return;
                }

                try
                {
                    _lockFileStream.Unlock(0, int.MaxValue);
                    _lockFileStream.Dispose();
                    _lockFileStream = null;
                    _hasLock = false;
                }
                catch (Exception)
                {
                }
            }

            private bool CreateOrOpenLockFileStream(out bool createdNew)
            {
                var lockDirectory = Path.GetDirectoryName(_lockFilePath);
                if (!Directory.Exists(lockDirectory))
                {
                    // Robust support for race free directory creation is not implemented.
                    // And isn't an issue for the time being since we will use a root temp dir that we can expect to exist
                    // ...absent a user manually deleting it
                    throw new DirectoryNotFoundException($"The lock directory must already exist : {lockDirectory}");
                }

                try
                {
                    // TODO by Mike : Probably not a great idea.  Could this be racey?  Might be OK since the stream creation would throw and then createdNew would be set back to false.
                    createdNew = !File.Exists(_lockFilePath);

                    _lockFileStream = new FileStream(_lockFilePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);

                    //if (_scope == Scope.SystemWide)
                    //{
                    //    // Woud need  (666) rw-rw-rw- permissions on the lock file
                    //    throw new NotImplementedException("To support this, would need to chmod 011 on the lock file");
                    //}

                    return true;
                }
                catch (IOException)
                {
                    createdNew = false;
                }
                catch (UnauthorizedAccessException)
                {
                    createdNew = false;
                }

                try
                {
                    _lockFileStream = new FileStream(_lockFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    return true;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                return false;
            }

            private void TryTouchLockFile(out bool createdNew)
            {
                var lockDirectory = Path.GetDirectoryName(_lockFilePath);
                if (!Directory.Exists(lockDirectory))
                {
                    // Robust support for race free directory creation is not implemented.
                    // And isn't an issue for the time being since we will use a root temp dir that we can expect to exist
                    // ...absent a user manually deleting it
                    throw new DirectoryNotFoundException($"The lock directory must already exist : {lockDirectory}");
                }

                try
                {
                    //// TODO by Mike : Probably not a great idea.  Could this be racey?  Might be OK since the stream creation would throw and then createdNew would be set back to false.
                    //if (File.Exists(_lockFilePath))
                    //{
                    //    createdNew = false;
                    //    return;
                    //}

                    //createdNew = !File.Exists(_lockFilePath);

                    using (var tmpStream = new FileStream(_lockFilePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                    {
                        //if (_scope == Scope.SystemWide)
                        //{
                        //    // Woud need  (666) rw-rw-rw- permissions on the lock file
                        //    throw new NotImplementedException("To support this, would need to chmod 011 on the lock file");
                        //}
                    }

                    createdNew = true;
                }
                catch (IOException)
                {
                    createdNew = false;
                }
                catch (UnauthorizedAccessException)
                {
                    createdNew = false;
                }
            }

            private void TryDeleteLock()
            {
                // Only let the original creator do the file clean.  This way we can accurately track "createdNew"
                if (!_createdNew)
                    return;

                try
                {
                    File.Delete(this._lockFilePath);
                }
                catch (IOException)
                {
                    //  Another processes mutex class could have grabbed ownership over the lock file between the time
                    // this instance closed it's write stream and the time this delete call happens
                }
                catch (UnauthorizedAccessException)
                {
                    // It's possible that this mutex is used by two different processes running as different users.  When this happens
                    // we may get an auth exception when deleting the file instead of an IOException
                }
            }

            private bool TryLock()
            {
                if (_hasLock)
                    throw new InvalidOperationException("Cannot obtain mutex lock when this instance already has it.");

                bool createdNew;
                if (_lockFileStream == null && !CreateOrOpenLockFileStream(out createdNew))
                    return false;

                try
                {
                    // TODO : Lock doesn't exist in netstd1.6
                    // TODO : Remove and go back to the old way of just holding open the file
                    _lockFileStream.Lock(0, int.MaxValue);
                    _hasLock = true;
                    return true;
                }
                catch (IOException)
                {
                    return false;
                }
            }

            private static string SanitizeForPathUsage(string name)
            {
                return name.Replace(' ', '_');
            }

            private static string LockDirectoryFor(Scope scope, string customRootLockDirectory)
            {
                if (!string.IsNullOrEmpty(customRootLockDirectory))
                    return customRootLockDirectory;

                switch (scope)
                {
                    case Scope.CurrentProcess:
                    case Scope.CurrentUser:
                        // For now, use the root temp dir in order to avoid directory creation races
                        return Path.GetTempPath();
                    //case Scope.SystemWide:
                    //    throw new NotImplementedException();
                    default:
                        throw new NotImplementedException($"Support for scope {scope} has not been implemented");
                }
            }

            private static string LockFileNameFor(string pathSafeName, Scope scope)
            {
                switch (scope)
                {
                    case Scope.CurrentProcess:
                        return $"BetterMutex_{System.Diagnostics.Process.GetCurrentProcess().Id}_{pathSafeName}.{LockFileExtension}";
                    case Scope.CurrentUser:
                    //case Scope.SystemWide:
                        return $"BetterMutex_{pathSafeName}.{LockFileExtension}";
                    default:
                        throw new NotImplementedException($"Support for scope {scope} has not been implemented");
                }
            }

            private static string GenerateLockFilePath(string name, Scope scope, string customRootLockDirectory)
            {
                var fileName = LockFileNameFor(SanitizeForPathUsage(name), scope);
                return Path.Combine(LockDirectoryFor(scope, customRootLockDirectory), fileName);
            }
        }

        internal sealed class FileBased2 : ISaferMutexMutex
        {
            private const string LockFileExtension = "lock";
            private const int WaitPeriodDuringInfiniteTimeout = 100;

            private const int AttemptsPerTimeout = 10;

            private readonly string _lockFilePath;
            private readonly bool _createdNew;
            private FileStream _lockFileStream;
            private bool _hasLock;

            private bool _disposed;

            public FileBased2(bool initiallyOwned, string name, Scope scope, out bool owned, out bool createdNew, string customRootLockDirectory = null)
            {
                if (name.StartsWith("Global\\"))
                    throw new ArgumentException($"The Global\\ name prefix should not be used.  Use the Scope parameter to control the scope of the mutex");

                _lockFilePath = GenerateLockFilePath(name, scope, customRootLockDirectory);

                if (!initiallyOwned)
                {
                    TryTouchLockFile(out createdNew);
                    owned = false;
                    _createdNew = createdNew;
                    return;
                }

                if (!TryLockNew(out createdNew))
                {
                    owned = false;
                    _createdNew = createdNew;
                    return;
                }

                owned = true;
                _createdNew = createdNew;
            }

            public void WaitOne()
            {
                WaitOne(null);
            }

            public bool WaitOne(int millisecondsTimeout)
            {
                return WaitOne(null, millisecondsTimeout);
            }

            public bool WaitOne(WaitHandle breakHandle)
            {
                return WaitOne(breakHandle, Timeout.Infinite);
            }

            public bool WaitOne(WaitHandle breakHandle, int millisecondsTimeout)
            {
                int notUsed;
                return WaitOne(breakHandle == null ? null : new[] { breakHandle }, millisecondsTimeout, out notUsed);
            }

            public bool WaitOne(WaitHandle[] breakHandles, out int index)
            {
                return WaitOne(breakHandles, Timeout.Infinite, out index);
            }

            public bool WaitOne(WaitHandle[] breakHandles, int millisecondsTimeout, out int index)
            {
                // If the timeout is 0, then we will just check and return immediately.
                if (millisecondsTimeout == 0)
                {
                    if (TryLock())
                    {
                        index = int.MinValue;
                        return true;
                    }

                    index = -1;
                    return false;
                }

                int sleepTimeBetweenTries = WaitPeriodDuringInfiniteTimeout;
                if (millisecondsTimeout != Timeout.Infinite)
                {
                    // Make sure the sleep time is at least 1 ms. (not 0).
                    sleepTimeBetweenTries = Math.Max(millisecondsTimeout / AttemptsPerTimeout, 1);

                    // Make sure that a long timeout doesn't mean we wont check very often.  The InfiniteTimeout period should
                    // be the longest that any mutex ever waits to check.
                    sleepTimeBetweenTries = Math.Min(sleepTimeBetweenTries, WaitPeriodDuringInfiniteTimeout);
                }

                int elapsedTimeCounter = 0;
                while (true)
                {
                    if (TryLock())
                    {
                        index = int.MinValue;
                        return true;
                    }

                    // Do stuff for timeout break condition
                    if (millisecondsTimeout != Timeout.Infinite)
                    {
                        elapsedTimeCounter += sleepTimeBetweenTries;

                        if (elapsedTimeCounter >= millisecondsTimeout)
                        {
                            index = -1;
                            return false;
                        }
                    }

                    if (breakHandles != null)
                    {
                        // While we sleep, wait on our break handless
                        int breakIndex = WaitHandle.WaitAny(breakHandles, sleepTimeBetweenTries);

                        if (breakIndex != WaitHandle.WaitTimeout)
                        {
                            index = breakIndex;
                            return false;
                        }
                    }
                    else
                    {
                        Thread.Sleep(sleepTimeBetweenTries);
                    }
                }
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                ReleaseMutex();

                if (_lockFileStream != null)
                {
                    _lockFileStream.Dispose();
                    _lockFileStream = null;
                }

                TryDeleteLock();

                this._disposed = true;
            }

            public void ReleaseMutex()
            {
                if (_hasLock == false)
                    return;

                try
                {
                    _lockFileStream.Dispose();
                    _lockFileStream = null;
                    _hasLock = false;
                }
                catch (Exception)
                {
                }
            }

            private bool TryLock()
            {
                if (_lockFileStream != null)
                {
                    throw new InvalidOperationException("Already have ownership of the mutex");
                }

                try
                {
                    _lockFileStream = new FileStream(this._lockFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                    _hasLock = true;
                }
                catch (IOException)
                {
                    return false;
                }
                catch (UnauthorizedAccessException)
                {
                    // Can happen instead of IOException if another user is holding the file
                    return false;
                }
                catch (System.Security.SecurityException)
                {
                    return false;
                }

                return true;
            }

            private bool TryLockNew(out bool createdNew)
            {
                var lockDirectory = Path.GetDirectoryName(_lockFilePath);
                if (!Directory.Exists(lockDirectory))
                {
                    // Robust support for race free directory creation is not implemented.
                    // And isn't an issue for the time being since we will use a root temp dir that we can expect to exist
                    // ...absent a user manually deleting it
                    throw new DirectoryNotFoundException($"The lock directory must already exist : {lockDirectory}");
                }

                if (_lockFileStream != null)
                {
                    throw new InvalidOperationException("Already have ownership of the mutex");
                }

                try
                {
                    // TODO by Mike : Probably not a great idea.  Could this be racey?  Might be OK since the stream creation would throw and then createdNew would be set back to false.
                    createdNew = !File.Exists(_lockFilePath);

                    _lockFileStream = new FileStream(this._lockFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                    _hasLock = true;
                }
                catch (IOException)
                {
                    createdNew = false;
                    return false;
                }
                catch (UnauthorizedAccessException)
                {
                    createdNew = false;
                    // Can happen instead of IOException if another user is holding the file
                    return false;
                }
                catch (System.Security.SecurityException)
                {
                    createdNew = false;
                    return false;
                }

                return true;
            }

            private void TryTouchLockFile(out bool createdNew)
            {
                var lockDirectory = Path.GetDirectoryName(_lockFilePath);
                if (!Directory.Exists(lockDirectory))
                {
                    // Robust support for race free directory creation is not implemented.
                    // And isn't an issue for the time being since we will use a root temp dir that we can expect to exist
                    // ...absent a user manually deleting it
                    throw new DirectoryNotFoundException($"The lock directory must already exist : {lockDirectory}");
                }

                try
                {
                    //// TODO by Mike : Probably not a great idea.  Could this be racey?  Might be OK since the stream creation would throw and then createdNew would be set back to false.
                    //if (File.Exists(_lockFilePath))
                    //{
                    //    createdNew = false;
                    //    return;
                    //}

                    //createdNew = !File.Exists(_lockFilePath);

                    using (var tmpStream = new FileStream(_lockFilePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                    {
                        //if (_scope == Scope.SystemWide)
                        //{
                        //    // Woud need  (666) rw-rw-rw- permissions on the lock file
                        //    throw new NotImplementedException("To support this, would need to chmod 011 on the lock file");
                        //}
                    }

                    createdNew = true;
                }
                catch (IOException)
                {
                    createdNew = false;
                }
                catch (UnauthorizedAccessException)
                {
                    createdNew = false;
                }
            }

            private void TryDeleteLock()
            {
                // Only let the original creator do the file clean.  This way we can accurately track "createdNew"
                if (!_createdNew)
                    return;

                try
                {
                    File.Delete(this._lockFilePath);
                }
                catch (IOException)
                {
                    //  Another processes mutex class could have grabbed ownership over the lock file between the time
                    // this instance closed it's write stream and the time this delete call happens
                }
                catch (UnauthorizedAccessException)
                {
                    // It's possible that this mutex is used by two different processes running as different users.  When this happens
                    // we may get an auth exception when deleting the file instead of an IOException
                }
            }

            private static string SanitizeForPathUsage(string name)
            {
                return name.Replace(' ', '_');
            }

            private static string LockDirectoryFor(Scope scope, string customRootLockDirectory)
            {
                if (!string.IsNullOrEmpty(customRootLockDirectory))
                    return customRootLockDirectory;

                switch (scope)
                {
                    case Scope.CurrentProcess:
                    case Scope.CurrentUser:
                        // For now, use the root temp dir in order to avoid directory creation races
                        return Path.GetTempPath();
                    //case Scope.SystemWide:
                    //    throw new NotImplementedException();
                    default:
                        throw new NotImplementedException($"Support for scope {scope} has not been implemented");
                }
            }

            private static string LockFileNameFor(string pathSafeName, Scope scope)
            {
                switch (scope)
                {
                    case Scope.CurrentProcess:
                        return $"BetterMutex_{System.Diagnostics.Process.GetCurrentProcess().Id}_{pathSafeName}.{LockFileExtension}";
                    case Scope.CurrentUser:
                        //case Scope.SystemWide:
                        return $"BetterMutex_{pathSafeName}.{LockFileExtension}";
                    default:
                        throw new NotImplementedException($"Support for scope {scope} has not been implemented");
                }
            }

            private static string GenerateLockFilePath(string name, Scope scope, string customRootLockDirectory)
            {
                var fileName = LockFileNameFor(SanitizeForPathUsage(name), scope);
                return Path.Combine(LockDirectoryFor(scope, customRootLockDirectory), fileName);
            }
        }

        #endregion
    }
}
