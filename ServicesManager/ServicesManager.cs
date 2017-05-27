using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

// This project must target the .NET 4 framework Client Profile for
// compatibility with choco (Posh 2 and Windows 7).
// To learn the differences between the full framework and the Client Profile:
// http://stackoverflow.com/a/2786831/3514658

class ServicesManager {
    
    static void Main(string[] args) {
        // Use the service name and *NOT* the display name.
        ServiceProperties props = GetServiceProperties("Dnscache");
        PrintServiceProperties(props);
        SetServiceProperties("OpenVPNService", ServiceControllerStatus.Running, ServiceStartMode.AutomaticDelayed);
    }

    static public ServiceProperties GetServiceProperties(string serviceName) {

        UInt32 dwBytesNeeded;
        string errMsg;
        bool success;

        IntPtr databaseHandle = OpenSCManager(
            null,
            null,
            NativeConstants.ServiceControlManager.SC_MANAGER_ALL_ACCESS);
        // An error might happen here if we are not running as administrator and the service
        // database is locked.
        if (databaseHandle == IntPtr.Zero) {
            throw new ExternalException("Unable to OpenSCManager. Not enough rights.");
        }

        IntPtr serviceHandle = OpenService(
            databaseHandle,
            serviceName,
            NativeConstants.Service.SERVICE_ALL_ACCESS);
        if (serviceHandle == IntPtr.Zero) {
            Cleanup(databaseHandle, IntPtr.Zero, out errMsg);
            throw new ExternalException("Unable to OpenService '" + serviceName + "':" + errMsg);
        }

        // Take basic info. Everything is taken into account except the
        // delayed autostart.
        //
        // The 'ref' keyword tells the compiler that the object is initialized
        // before entering the function, while 'out' tells the compiler that the
        // object will be initialized inside the function.
        // src.: http://stackoverflow.com/a/388467/3514658

        // Determine the buffer size needed (dwBytesNeeded).
        success = QueryServiceConfig(serviceHandle, IntPtr.Zero, 0, out dwBytesNeeded);
        if (!success && Marshal.GetLastWin32Error() != NativeConstants.SystemErrorCode.ERROR_INSUFFICIENT_BUFFER) {
                Cleanup(databaseHandle, serviceHandle, out errMsg);
                throw new ExternalException("Unable to get service config for '" + serviceName + "': " + errMsg);
        }

        // Get the main info of the service. See this struct for more info:
        // src.: https://msdn.microsoft.com/en-us/library/windows/desktop/ms684950(v=vs.85).aspx
        IntPtr ptr = Marshal.AllocHGlobal((int)dwBytesNeeded);
        success = QueryServiceConfig(serviceHandle, ptr, dwBytesNeeded, out dwBytesNeeded);
        if (!success) {
            Marshal.FreeHGlobal(ptr);
            Cleanup(databaseHandle, serviceHandle, out errMsg);
            throw new ExternalException("Unable to get service config for '" + serviceName + "': " + errMsg);
        }
        // Copy memory to serviceConfig.
        QUERY_SERVICE_CONFIG serviceConfig = new QUERY_SERVICE_CONFIG();
        Marshal.PtrToStructure(ptr, serviceConfig);
        Marshal.FreeHGlobal(ptr);

        ServiceProperties props = new ServiceProperties();
        props.ServiceConfig.QUERY_SERVICE_CONFIG = serviceConfig;

        // Get all possible infos
        List<uint> infoLevels = new List<uint>();
        infoLevels.Add(NativeConstants.Service.SERVICE_CONFIG_DELAYED_AUTO_START_INFO);
        infoLevels.Add(NativeConstants.Service.SERVICE_CONFIG_DESCRIPTION);
        infoLevels.Add(NativeConstants.Service.SERVICE_CONFIG_FAILURE_ACTIONS);
        infoLevels.Add(NativeConstants.Service.SERVICE_CONFIG_FAILURE_ACTIONS_FLAG);
        infoLevels.Add(NativeConstants.Service.SERVICE_CONFIG_PREFERRED_NODE);
        infoLevels.Add(NativeConstants.Service.SERVICE_CONFIG_PRESHUTDOWN_INFO);
        infoLevels.Add(NativeConstants.Service.SERVICE_CONFIG_REQUIRED_PRIVILEGES_INFO);
        infoLevels.Add(NativeConstants.Service.SERVICE_CONFIG_SERVICE_SID_INFO);
        infoLevels.Add(NativeConstants.Service.SERVICE_CONFIG_TRIGGER_INFO);
        infoLevels.Add(NativeConstants.Service.SERVICE_CONFIG_LAUNCH_PROTECTED);
        for (int i = 0; i < infoLevels.Count; i++) {

            // Determine the buffer size needed (dwBytesNeeded).
            success = QueryServiceConfig2(
                serviceHandle,
                infoLevels[i],
                IntPtr.Zero,
                0,
                out dwBytesNeeded);
            if (!success && Marshal.GetLastWin32Error() != NativeConstants.SystemErrorCode.ERROR_INSUFFICIENT_BUFFER) {
                continue;
            }

            // Get the info if the service is set in delayed mode or not.
            ptr = Marshal.AllocHGlobal((int)dwBytesNeeded);
            success = QueryServiceConfig2(
                    serviceHandle,
                    infoLevels[i],
                    ptr,
                    dwBytesNeeded,
                    out dwBytesNeeded);
            if (!success) {
                Marshal.FreeHGlobal(ptr);
                Cleanup(databaseHandle, serviceHandle, out errMsg);
                throw new ExternalException("Unable to get service delayed auto start property for '" + serviceName + "': " + errMsg);
            }

            // While we could use introspection to be able to take on the fly
            // the appropriate class to instanciate, we should avoid this as
            // instrospection is a costly process.
            switch (infoLevels[i]) {
                case NativeConstants.Service.SERVICE_CONFIG_DELAYED_AUTO_START_INFO:
                    Marshal.PtrToStructure(ptr, props.ServiceConfig2.SERVICE_DELAYED_AUTO_START_INFO);
                    break;

                case NativeConstants.Service.SERVICE_CONFIG_DESCRIPTION:
                    Marshal.PtrToStructure(ptr, props.ServiceConfig2.SERVICE_DESCRIPTION);
                    break;

                case NativeConstants.Service.SERVICE_CONFIG_FAILURE_ACTIONS:
                    Marshal.PtrToStructure(ptr, props.ServiceConfig2.SERVICE_FAILURE_ACTIONS);
                    break;

                case NativeConstants.Service.SERVICE_CONFIG_FAILURE_ACTIONS_FLAG:
                    Marshal.PtrToStructure(ptr, props.ServiceConfig2.SERVICE_FAILURE_ACTIONS_FLAG);
                    break;

                case NativeConstants.Service.SERVICE_CONFIG_PREFERRED_NODE:
                    Marshal.PtrToStructure(ptr, props.ServiceConfig2.SERVICE_PREFERRED_NODE_INFO);
                    break;

                case NativeConstants.Service.SERVICE_CONFIG_PRESHUTDOWN_INFO:
                    Marshal.PtrToStructure(ptr, props.ServiceConfig2.SERVICE_PRESHUTDOWN_INFO);
                    break;

                case NativeConstants.Service.SERVICE_CONFIG_REQUIRED_PRIVILEGES_INFO:
                    Marshal.PtrToStructure(ptr, props.ServiceConfig2.SERVICE_REQUIRED_PRIVILEGES_INFO);
                    break;

                case NativeConstants.Service.SERVICE_CONFIG_SERVICE_SID_INFO:
                    Marshal.PtrToStructure(ptr, props.ServiceConfig2.SERVICE_SID_INFO);
                    break;

                case NativeConstants.Service.SERVICE_CONFIG_TRIGGER_INFO:
                    Marshal.PtrToStructure(ptr, props.ServiceConfig2.SERVICE_TRIGGER_INFO);
                    break;

                case NativeConstants.Service.SERVICE_CONFIG_LAUNCH_PROTECTED:
                    Marshal.PtrToStructure(ptr, props.ServiceConfig2.SERVICE_LAUNCH_PROTECTED_INFO);
                    break;
            }
            Marshal.FreeHGlobal(ptr);
        }

        SERVICE_STATUS serviceStatus = new SERVICE_STATUS();
        success = QueryServiceStatus(
                serviceHandle,
                out serviceStatus);
        if (!success) {
            Cleanup(databaseHandle, serviceHandle, out errMsg);
            throw new ExternalException("Unable to get service status for '" + serviceName + "': " + errMsg);
        }
        props.ServiceStatus.SERVICE_STATUS = serviceStatus;

        props.Name = serviceName;
        props.DisplayName = serviceConfig.lpDisplayName;

        switch (serviceConfig.dwStartType) {
            case NativeConstants.Service.SERVICE_AUTO_START:
                if (props.ServiceConfig2.SERVICE_DELAYED_AUTO_START_INFO.fDelayedAutostart) {
                    props.StartMode = ServiceStartMode.AutomaticDelayed;
                }
                else {
                    props.StartMode = ServiceStartMode.Automatic;
                }
                break;
            case NativeConstants.Service.SERVICE_BOOT_START:
                props.StartMode = ServiceStartMode.Boot;
                break;
            case NativeConstants.Service.SERVICE_DISABLED:
                props.StartMode = ServiceStartMode.Disabled;
                break;
            case NativeConstants.Service.SERVICE_DEMAND_START:
                props.StartMode = ServiceStartMode.Manual;
                break;
            case NativeConstants.Service.SERVICE_SYSTEM_START:
                props.StartMode = ServiceStartMode.System;
                break;
            default:
                CloseServiceHandle(databaseHandle);
                CloseServiceHandle(serviceHandle);
                throw new ExternalException("The service '" + serviceName + "' has an invalid start type");
        }

        switch (serviceStatus.dwCurrentState) {
            case NativeConstants.Service.SERVICE_CONTINUE_PENDING:
                props.Status = ServiceControllerStatus.ContinuePending;
                break;
            case NativeConstants.Service.SERVICE_PAUSE_PENDING:
                props.Status = ServiceControllerStatus.PausePending;
                break;
            case NativeConstants.Service.SERVICE_PAUSED:
                props.Status = ServiceControllerStatus.Paused;
                break;
            case NativeConstants.Service.SERVICE_RUNNING:
                props.Status = ServiceControllerStatus.Running;
                break;
            case NativeConstants.Service.SERVICE_START_PENDING:
                props.Status = ServiceControllerStatus.StartPending;
                break;
            case NativeConstants.Service.SERVICE_STOP_PENDING:
                props.Status = ServiceControllerStatus.StopPending;
                break;
            case NativeConstants.Service.SERVICE_STOPPED:
                props.Status = ServiceControllerStatus.Stopped;
                break;
            default:
                CloseServiceHandle(databaseHandle);
                CloseServiceHandle(serviceHandle);
                throw new ExternalException("The service '" + serviceName + "' has an invalid status");
        }

        CloseServiceHandle(databaseHandle);
        CloseServiceHandle(serviceHandle);

        return props;
    }

    static public void SetServiceProperties(
        string serviceName,
        ServiceControllerStatus status,
        ServiceStartMode startMode) {

        ServiceProperties props = GetServiceProperties(serviceName);

        string errMsg;

        IntPtr databaseHandle = OpenSCManager(
            null,
            null,
            NativeConstants.ServiceControlManager.SC_MANAGER_ALL_ACCESS);
        // An error might happen here if we are not running as administrator and the service
        // database is locked.
        if (databaseHandle == IntPtr.Zero) {
            throw new ExternalException("Unable to OpenSCManager. Not enough rights.");
        }

        IntPtr serviceHandle = OpenService(
            databaseHandle,
            serviceName,
            NativeConstants.Service.SERVICE_ALL_ACCESS);
        if (serviceHandle == IntPtr.Zero) {
            Cleanup(databaseHandle, IntPtr.Zero, out errMsg);
            throw new ExternalException("Unable to OpenService '" + serviceName + "':" + errMsg);
        }

        // Change service startType config
        Int32 dwStartType;
        SERVICE_DELAYED_AUTO_START_INFO DelayedInfo =
            new SERVICE_DELAYED_AUTO_START_INFO();
        DelayedInfo.fDelayedAutostart = false;
        switch (startMode) {
            case ServiceStartMode.AutomaticDelayed:
                dwStartType = NativeConstants.Service.SERVICE_AUTO_START;
                DelayedInfo.fDelayedAutostart = true;
                break;
            case ServiceStartMode.Automatic:
                dwStartType = NativeConstants.Service.SERVICE_AUTO_START;
                break;
            case ServiceStartMode.Boot:
                dwStartType = NativeConstants.Service.SERVICE_BOOT_START;
                break;
            case ServiceStartMode.Disabled:
                dwStartType = NativeConstants.Service.SERVICE_DISABLED;
                break;
            case ServiceStartMode.Manual:
                dwStartType = NativeConstants.Service.SERVICE_DEMAND_START;
                break;
            case ServiceStartMode.System:
                dwStartType = NativeConstants.Service.SERVICE_SYSTEM_START;
                break;
            default:
                CloseServiceHandle(databaseHandle);
                CloseServiceHandle(serviceHandle);
                throw new ExternalException("The service '" + serviceName + "' has an invalid start type");
        }

        if (!ChangeServiceConfig(
                // handle of service
                serviceHandle,
                // service type: no change
                NativeConstants.Service.SERVICE_NO_CHANGE,
                // service start type
                dwStartType,
                // error control: no change
                NativeConstants.Service.SERVICE_NO_CHANGE,
                // binary path: no change
                IntPtr.Zero,
                // load order group: no change
                IntPtr.Zero,
                // tag ID: no change
                IntPtr.Zero,
                // dependencies: no change
                IntPtr.Zero,
                // account name: no change
                IntPtr.Zero,
                // password: no change
                IntPtr.Zero,
                // display name: no change
                IntPtr.Zero)) {
            Cleanup(databaseHandle, serviceHandle, out errMsg);
            throw new ExternalException("Unable to change configuration for service '" + serviceName + "':" + errMsg);
        }

        IntPtr pDelayedInfo = Marshal.AllocHGlobal(Marshal.SizeOf(DelayedInfo));
        Marshal.StructureToPtr(DelayedInfo, pDelayedInfo, true);

        if (!ChangeServiceConfig2(
                serviceHandle,
                NativeConstants.Service.SERVICE_CONFIG_DELAYED_AUTO_START_INFO,
                pDelayedInfo)) {
            Cleanup(databaseHandle, serviceHandle, out errMsg);
            Marshal.FreeHGlobal(pDelayedInfo);
            pDelayedInfo = IntPtr.Zero;
            throw new ExternalException("Unable to change configuration for service '" + serviceName + "':" + errMsg);
        }

        Marshal.FreeHGlobal(pDelayedInfo);
        pDelayedInfo = IntPtr.Zero;

        // If the user wants to start the service
        if (status == ServiceControllerStatus.Running ||
            status == ServiceControllerStatus.StartPending) {

            // Try to start the service, only if it is not already started
            if (props.Status == ServiceControllerStatus.Stopped ||
                props.Status == ServiceControllerStatus.StopPending) {

            }
        }
    }

    static private void PrintServiceProperties(ServiceProperties props) {
        
        Console.WriteLine("DisplayName: '" + props.DisplayName + "'");
        Console.WriteLine("StartMode: '" + props.StartMode + "'");
        Console.WriteLine("Status: '" + props.Status + "'");
        Console.WriteLine("lpDisplayName: '" + props.ServiceConfig.QUERY_SERVICE_CONFIG.lpDisplayName + "'");
        Console.WriteLine("lpDisplayName: '" + props.ServiceConfig.QUERY_SERVICE_CONFIG.lpDisplayName + "'");
        Console.WriteLine("lpDescription: '" + props.ServiceConfig2.SERVICE_DESCRIPTION.lpDescription + "'");
        Console.WriteLine("dwServiceSidType: '" + props.ServiceConfig2.SERVICE_SID_INFO.dwServiceSidType + "'");
        Console.ReadLine();
    }

    static private void Cleanup(
        IntPtr databaseHandle,
        IntPtr serviceHandle,
        out string errMsg) {

        int errCode = Marshal.GetLastWin32Error();
        if (serviceHandle != IntPtr.Zero) {
            CloseServiceHandle(serviceHandle);
        }

        if (databaseHandle != IntPtr.Zero) {
            CloseServiceHandle(databaseHandle);
        }
        // We can't get the capitalized error constant programatically. This
        // piss of code is thus needed. Otherwise we need to load a file
        // containing all Win32 error constants.
        // src.: https://stackoverflow.com/a/30204142/3514658
        // src.: http://pinvoke.net/default.aspx/Constants/WINERROR.html
        switch (errCode) {
            case NativeConstants.SystemErrorCode.ERROR_ACCESS_DENIED:
                errMsg = "ERROR_ACCESS_DENIED";
                break;
            case NativeConstants.SystemErrorCode.ERROR_INSUFFICIENT_BUFFER:
                errMsg = "ERROR_INSUFFICIENT_BUFFER";
                break;
            case NativeConstants.SystemErrorCode.ERROR_INVALID_HANDLE:
                errMsg = "ERROR_INVALID_HANDLE";
                break;
            case NativeConstants.SystemErrorCode.ERROR_INVALID_NAME:
                errMsg = "ERROR_INVALID_NAME";
                break;
            case NativeConstants.SystemErrorCode.ERROR_SERVICE_DOES_NOT_EXIST:
                errMsg = "ERROR_SERVICE_DOES_NOT_EXIST";
                break;
            case NativeConstants.SystemErrorCode.ERROR_CIRCULAR_DEPENDENCY:
                errMsg = "ERROR_CIRCULAR_DEPENDENCY";
                break;
            case NativeConstants.SystemErrorCode.ERROR_DUPLICATE_SERVICE_NAME:
                errMsg = "ERROR_DUPLICATE_SERVICE_NAME";
                break;
            case NativeConstants.SystemErrorCode.ERROR_INVALID_PARAMETER:
                errMsg = "ERROR_INVALID_PARAMETER";
                break;
            case NativeConstants.SystemErrorCode.ERROR_INVALID_SERVICE_ACCOUNT:
                errMsg = "ERROR_INVALID_SERVICE_ACCOUNT";
                break;
            case NativeConstants.SystemErrorCode.ERROR_SERVICE_MARKED_FOR_DELETE:
                errMsg = "ERROR_SERVICE_MARKED_FOR_DELETE";
                break;
            default:
                errMsg = errCode.ToString();
                break;
        }
    }

   public class ServiceProperties {
        public ServiceConfig ServiceConfig = new ServiceConfig();
        public ServiceConfig2 ServiceConfig2 = new ServiceConfig2();
        public ServiceStatus ServiceStatus = new ServiceStatus();

        public String Name;
        public String DisplayName;
        public ServiceStartMode StartMode;
        public ServiceControllerStatus Status;
    }

    public class ServiceConfig {
        public QUERY_SERVICE_CONFIG QUERY_SERVICE_CONFIG = new QUERY_SERVICE_CONFIG();
    }

    public class ServiceConfig2 {
        public SERVICE_DELAYED_AUTO_START_INFO SERVICE_DELAYED_AUTO_START_INFO = new SERVICE_DELAYED_AUTO_START_INFO();
        public SERVICE_DESCRIPTION SERVICE_DESCRIPTION = new SERVICE_DESCRIPTION();
        public SERVICE_FAILURE_ACTIONS SERVICE_FAILURE_ACTIONS = new SERVICE_FAILURE_ACTIONS();
        public SERVICE_FAILURE_ACTIONS_FLAG SERVICE_FAILURE_ACTIONS_FLAG = new SERVICE_FAILURE_ACTIONS_FLAG();
        public SERVICE_PREFERRED_NODE_INFO SERVICE_PREFERRED_NODE_INFO = new SERVICE_PREFERRED_NODE_INFO();
        public SERVICE_PRESHUTDOWN_INFO SERVICE_PRESHUTDOWN_INFO = new SERVICE_PRESHUTDOWN_INFO();
        public SERVICE_REQUIRED_PRIVILEGES_INFO SERVICE_REQUIRED_PRIVILEGES_INFO = new SERVICE_REQUIRED_PRIVILEGES_INFO();
        public SERVICE_SID_INFO SERVICE_SID_INFO = new SERVICE_SID_INFO();
        public SERVICE_TRIGGER_INFO SERVICE_TRIGGER_INFO = new SERVICE_TRIGGER_INFO();
        public SERVICE_LAUNCH_PROTECTED_INFO SERVICE_LAUNCH_PROTECTED_INFO = new SERVICE_LAUNCH_PROTECTED_INFO();
    }

    public class ServiceStatus {
        public SERVICE_STATUS SERVICE_STATUS = new SERVICE_STATUS();
    }

    // Based on System.ServiceProcess.ServiceControllerStatus
    // src.: https://msdn.microsoft.com/en-us/library/system.serviceprocess.servicecontrollerstatus(v=vs.110).aspx
    public enum ServiceControllerStatus {
        ContinuePending = NativeConstants.Service.SERVICE_CONTINUE_PENDING,
        Paused = NativeConstants.Service.SERVICE_PAUSED,
        PausePending = NativeConstants.Service.SERVICE_PAUSE_PENDING,
        Running = NativeConstants.Service.SERVICE_RUNNING,
        StartPending = NativeConstants.Service.SERVICE_START_PENDING,
        Stopped = NativeConstants.Service.SERVICE_STOPPED,
        StopPending = NativeConstants.Service.SERVICE_STOP_PENDING
    }

    // Based on System.ServiceProcess.ServiceStartMode
    // src.: https://msdn.microsoft.com/en-us/library/system.serviceprocess.servicestartmode(v=vs.110).aspx
    // but we added the AutomaticDelayed mode.
    public enum ServiceStartMode {
        Automatic = NativeConstants.Service.SERVICE_AUTO_START,
        // Enum numeric literals are defined at compilation time and the C#
        // language specification doesn't prevent to have duplicate values
        // for an enum. Here, when we weren't specifying an explicit value
        // for AutomaticDelayed, the latter had the same value (3) as Manual.
        // We had to specify a value not used by other fields. Another solution
        // would be to use a class instead, but this is a bit overkill for this
        // use case.
        // src.: https://stackoverflow.com/a/26827597/3514658
        // src.: https://stackoverflow.com/a/1425791/3514658
        // src.: https://stackoverflow.com/a/26828917/3514658
        AutomaticDelayed = 1000,
        Boot = NativeConstants.Service.SERVICE_BOOT_START,
        Disabled = NativeConstants.Service.SERVICE_DISABLED,
        Manual = NativeConstants.Service.SERVICE_DEMAND_START,
        System = NativeConstants.Service.SERVICE_SYSTEM_START
    }

    #region P/Invoke structures

    // P/Invoke Interop Assistant 1.0 is useful to generate declarations of
    // native code constant in their managed counterparts.
    // http://stackoverflow.com/a/5122534/3514658
    // From the struct QUERY_SERVICE_CONFIG used by the function
    // QueryServiceConfig()
    // src.: https://msdn.microsoft.com/en-us/library/windows/desktop/ms684950(v=vs.85).aspx

    // The LayoutKind.Sequential specifies that the fields of the type should
    // be laid out in memory in the same order they are declared in your
    // source code. That's often important when interoperating with native
    // code. Without the attribute the CLR is free to optimize memory use
    // by rearranging the fields.
    // src.: https://social.msdn.microsoft.com/Forums/vstudio/en-US/2abc6be8-c593-4686-93d2-89785232dacd#0455ea02-7eab-451b-8a83-fbfc4384d654
    [StructLayoutAttribute(LayoutKind.Sequential)]
    public class QUERY_SERVICE_CONFIG {
        /// DWORD->unsigned int
        public uint dwServiceType;
        /// DWORD->unsigned int
        public uint dwStartType;
        /// DWORD->unsigned int
        public uint dwErrorControl;
        /// LPWSTR->WCHAR*
        [MarshalAsAttribute(UnmanagedType.LPWStr)]
        public string lpBinaryPathName;
        /// LPWSTR->WCHAR*
        [MarshalAsAttribute(UnmanagedType.LPWStr)]
        public string lpLoadOrderGroup;
        /// DWORD->unsigned int
        public uint dwTagId;
        /// LPWSTR->WCHAR*
        [MarshalAsAttribute(UnmanagedType.LPWStr)]
        public string lpDependencies;
        /// LPWSTR->WCHAR*
        [MarshalAsAttribute(UnmanagedType.LPWStr)]
        public string lpServiceStartName;
        /// LPWSTR->WCHAR*
        [MarshalAsAttribute(UnmanagedType.LPWStr)]
        public string lpDisplayName;
    }

    // From the struct ServiceStatus
    // src.: https://msdn.microsoft.com/en-us/library/windows/desktop/ms685996(v=vs.85).aspx
    // used by the function QueryServiceStatus()
    // src.: https://msdn.microsoft.com/en-us/library/windows/desktop/ms684939(v=vs.85).aspx
    [StructLayoutAttribute(LayoutKind.Sequential)]
    // Must be a struct otherwise QueryServiceStatus complains the memory
    // is corrupted.
    public struct SERVICE_STATUS {
        /// DWORD->unsigned int
        public uint dwServiceType;
        /// DWORD->unsigned int
        public uint dwCurrentState;
        /// DWORD->unsigned int
        public uint dwControlsAccepted;
        /// DWORD->unsigned int
        public uint dwWin32ExitCode;
        /// DWORD->unsigned int
        public uint dwServiceSpecificExitCode;
        /// DWORD->unsigned int
        public uint dwCheckPoint;
        /// DWORD->unsigned int
        public uint dwWaitHint;
    }

    // From the struct SERVICE_DELAYED_AUTO_START_INFO
    // src.: https://msdn.microsoft.com/en-us/library/windows/desktop/ms685155(v=vs.85).aspx
    // used by the function QueryServiceConfig2()
    // src.: https://msdn.microsoft.com/en-us/library/windows/desktop/ms684935(v=vs.85).aspx
    [StructLayout(LayoutKind.Explicit)]
    public class SERVICE_DELAYED_AUTO_START_INFO {
        [FieldOffset(0)]
        [MarshalAs(UnmanagedType.Bool)]
        public bool fDelayedAutostart;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class SERVICE_DESCRIPTION {
        [MarshalAs(UnmanagedType.LPWStr)]
        public String lpDescription;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public class SERVICE_FAILURE_ACTIONS {
        public int dwResetPeriod;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpRebootMsg;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpCommand;
        public int cActions;
        public IntPtr lpsaActions;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class SC_ACTION {
        public Int32 type;
        public UInt32 dwDelay;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class SERVICE_FAILURE_ACTIONS_FLAG {
        public bool fFailureActionsOnNonCrashFailures;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class SERVICE_PREFERRED_NODE_INFO {
        public ushort usPreferredNode;
        public bool fDelete;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class SERVICE_PRESHUTDOWN_INFO {
        public uint dwPreshutdownTimeout;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class SERVICE_REQUIRED_PRIVILEGES_INFO {
        public string pmszRequiredPrivileges;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class SERVICE_SID_INFO {
        public uint dwServiceSidType;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class SERVICE_TRIGGER_INFO {
        public int cTriggers;
        public IntPtr pTriggers;
        public IntPtr pReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class PSERVICE_TRIGGER {
        public uint dwTriggerType;
        public uint dwAction;
        public IntPtr pTriggerSubtype;
        public uint cDataItems;
        public IntPtr pDataItems;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class SERVICE_LAUNCH_PROTECTED_INFO {
        public uint dwLaunchProtected;
    }

    #endregion P/Invoke structures

    #region P/Invoke functions

    // Some import statements are inspired from some public solutions from
    // Pinvoke.net.
    // https://webcache.googleusercontent.com/search?q=cache:4U7pz3gubesJ:www.pinvoke.net/default.aspx/advapi32.queryserviceconfig2
    // The following page is a great use to find types equivalence between
    // C++ et .NET types.
    // src.: https://www.codeproject.com/Articles/9714/Win-API-C-to-NET
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr OpenSCManager(String lpMachineName, String lpDatabaseName, UInt32 dwDesiredAccess);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr OpenService(IntPtr hSCManager, String lpServiceName, UInt32 dwDesiredAccess);

    // The EntryPoint specifies a DLL function by name or ordinal. If the name
    // of the function in our method definition is the same as the entry
    // point in the DLL, we do not have to explicitly identify the function
    // with the EntryPoint field. Here since we are mapping QueryServiceConfig
    // to QueryServiceConfigW, this property is needed.
    // src. https://msdn.microsoft.com/en-us/library/f5xe74x8(v=vs.110).aspx#Anchor_1
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "QueryServiceConfigW")]
    public static extern bool QueryServiceConfig(IntPtr hService, IntPtr lpServiceConfig, UInt32 cbBufSize, out UInt32 pcbBytesNeeded);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "QueryServiceConfig2W")]
    public static extern bool QueryServiceConfig2(IntPtr hService, UInt32 dwInfoLevel, IntPtr buffer, UInt32 cbBufSize, out UInt32 pcbBytesNeeded);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "QueryServiceStatus")]
    public static extern bool QueryServiceStatus(IntPtr hService, out SERVICE_STATUS lpServiceStatus);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "ChangeServiceConfigW")]
    public static extern bool ChangeServiceConfig(
        IntPtr hService,
        Int32 dwServiceType,
        Int32 dwStartType,
        Int32 dwErrorControl,
        IntPtr lpBinaryPathName,
        IntPtr lpLoadOrderGroup,
        IntPtr lpdwTagId,
        IntPtr lpDependencies,
        IntPtr lpServiceStartName,
        IntPtr lpPassword,
        IntPtr lpDisplayName);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "ChangeServiceConfig2W")]
    public static extern bool ChangeServiceConfig2(IntPtr hService, UInt32 dwInfoLevel, IntPtr lpInfo);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CloseServiceHandle")]
    public static extern bool CloseServiceHandle(IntPtr hSCObject);

    #endregion P/Invoke functions

    #region P/Invoke error codes

    public partial class NativeConstants {

        // From System Error Codes (0-499)
        // src.: https://msdn.microsoft.com/en-us/library/windows/desktop/ms681382(v=vs.85).aspx
        public class SystemErrorCode {

            // Errors from OpenSCManager

            /// ERROR_ACCESS_DENIED -> 5L
            public const int ERROR_ACCESS_DENIED = 5;
            /// ERROR_DATABASE_DOES_NOT_EXIST -> 1065L
            public const int ERROR_DATABASE_DOES_NOT_EXIST = 1065;


            // Errors from OpenService

            // + ERROR_ACCESS_DENIED
            /// ERROR_INVALID_HANDLE -> 6L
            public const int ERROR_INVALID_HANDLE = 6;
            /// ERROR_INVALID_NAME -> 123L
            public const int ERROR_INVALID_NAME = 123;
            /// ERROR_SERVICE_DOES_NOT_EXIST -> 1060L
            public const int ERROR_SERVICE_DOES_NOT_EXIST = 1060;


            // Errors from QueryServiceConfig

            // + ERROR_ACCESS_DENIED
            /// ERROR_INSUFFICIENT_BUFFER -> 122L
            public const int ERROR_INSUFFICIENT_BUFFER = 122;
            // + ERROR_INVALID_HANDLE


            // Errors from QueryServiceConfig2
            // + ERROR_ACCESS_DENIED
            // + ERROR_INSUFFICIENT_BUFFER
            // + ERROR_INVALID_HANDLE


            // Errors from QueryServiceStatus
            // + ERROR_ACCESS_DENIED
            // + ERROR_INVALID_HANDLE


            // Errors from ChangeServiceConfig

            // + ERROR_ACCESS_DENIED
            /// ERROR_CIRCULAR_DEPENDENCY -> 1059L
            public const int ERROR_CIRCULAR_DEPENDENCY = 1059;
            /// ERROR_DUPLICATE_SERVICE_NAME -> 1078L
            public const int ERROR_DUPLICATE_SERVICE_NAME = 1078;
            // + ERROR_INVALID_HANDLE
            /// ERROR_INVALID_PARAMETER -> 87L
            public const int ERROR_INVALID_PARAMETER = 87;
            /// ERROR_INVALID_SERVICE_ACCOUNT -> 1057L
            public const int ERROR_INVALID_SERVICE_ACCOUNT = 1057;
            /// ERROR_SERVICE_MARKED_FOR_DELETE -> 1072L
            public const int ERROR_SERVICE_MARKED_FOR_DELETE = 1072;
        }

        public class ServiceControlManager {

            // From Service Control Manager access rights
            // src.: https://msdn.microsoft.com/en-us/library/windows/desktop/ms685981(v=vs.85).aspx#access_rights_for_the_service_control_manager

            /// SC_MANAGER_ALL_ACCESS -> (STANDARD_RIGHTS_REQUIRED      |                                         SC_MANAGER_CONNECT            |                                         SC_MANAGER_CREATE_SERVICE     |                                         SC_MANAGER_ENUMERATE_SERVICE  |                                         SC_MANAGER_LOCK               |                                         SC_MANAGER_QUERY_LOCK_STATUS  |                                         SC_MANAGER_MODIFY_BOOT_CONFIG)
            public const int SC_MANAGER_ALL_ACCESS = (ServiceControlManager.STANDARD_RIGHTS_REQUIRED
                        | (ServiceControlManager.SC_MANAGER_CONNECT
                        | (ServiceControlManager.SC_MANAGER_CREATE_SERVICE
                        | (ServiceControlManager.SC_MANAGER_ENUMERATE_SERVICE
                        | (ServiceControlManager.SC_MANAGER_LOCK
                        | (ServiceControlManager.SC_MANAGER_QUERY_LOCK_STATUS | NativeConstants.ServiceControlManager.SC_MANAGER_MODIFY_BOOT_CONFIG))))));
            /// SC_MANAGER_CREATE_SERVICE -> 0x0002
            public const int SC_MANAGER_CREATE_SERVICE = 2;
            /// SC_MANAGER_CONNECT -> 0x0001
            public const int SC_MANAGER_CONNECT = 1;
            /// SC_MANAGER_ENUMERATE_SERVICE -> 0x0004
            public const int SC_MANAGER_ENUMERATE_SERVICE = 4;
            /// SC_MANAGER_LOCK -> 0x0008
            public const int SC_MANAGER_LOCK = 8;
            /// SC_MANAGER_MODIFY_BOOT_CONFIG -> 0x0020
            public const int SC_MANAGER_MODIFY_BOOT_CONFIG = 32;
            /// SC_MANAGER_QUERY_LOCK_STATUS -> 0x0010
            public const int SC_MANAGER_QUERY_LOCK_STATUS = 16;

            /// STANDARD_RIGHTS_REQUIRED -> (0x000F0000L)
            public const int STANDARD_RIGHTS_REQUIRED = 983040;
        }

        public class Service {

            // From Service access rights
            // src.: https://msdn.microsoft.com/en-us/library/windows/desktop/ms685981(v=vs.85).aspx#access_rights_for_a_service

            /// SERVICE_ALL_ACCESS -> (STANDARD_RIGHTS_REQUIRED     |                                         SERVICE_QUERY_CONFIG         |                                         SERVICE_CHANGE_CONFIG        |                                         SERVICE_QUERY_STATUS         |                                         SERVICE_ENUMERATE_DEPENDENTS |                                         SERVICE_START                |                                         SERVICE_STOP                 |                                         SERVICE_PAUSE_CONTINUE       |                                         SERVICE_INTERROGATE          |                                         SERVICE_USER_DEFINED_CONTROL)
            public const int SERVICE_ALL_ACCESS = (Service.STANDARD_RIGHTS_REQUIRED
                        | (Service.SERVICE_QUERY_CONFIG
                        | (Service.SERVICE_CHANGE_CONFIG
                        | (Service.SERVICE_QUERY_STATUS
                        | (Service.SERVICE_ENUMERATE_DEPENDENTS
                        | (Service.SERVICE_START
                        | (Service.SERVICE_STOP
                        | (Service.SERVICE_PAUSE_CONTINUE
                        | (Service.SERVICE_INTERROGATE | NativeConstants.Service.SERVICE_USER_DEFINED_CONTROL)))))))));
            /// SERVICE_CHANGE_CONFIG -> 0x0002
            public const int SERVICE_CHANGE_CONFIG = 2;
            /// SERVICE_ENUMERATE_DEPENDENTS -> 0x0008
            public const int SERVICE_ENUMERATE_DEPENDENTS = 8;
            /// SERVICE_INTERROGATE -> 0x0080
            public const int SERVICE_INTERROGATE = 128;
            /// SERVICE_PAUSE_CONTINUE -> 0x0040
            public const int SERVICE_PAUSE_CONTINUE = 64;
            /// SERVICE_QUERY_CONFIG -> 0x0001
            public const int SERVICE_QUERY_CONFIG = 1;
            /// SERVICE_QUERY_STATUS -> 0x0004
            public const int SERVICE_QUERY_STATUS = 4;
            /// SERVICE_START -> 0x0010
            public const int SERVICE_START = 16;
            /// SERVICE_STOP -> 0x0020
            public const int SERVICE_STOP = 32;
            /// SERVICE_USER_DEFINED_CONTROL -> 0x0100
            public const int SERVICE_USER_DEFINED_CONTROL = 256;

            /// STANDARD_RIGHTS_REQUIRED -> (0x000F0000L)
            public const int STANDARD_RIGHTS_REQUIRED = 983040;


            // QUERY_SERVICE_CONFIG > dwServiceType
            // same as ServiceStatus > dwServiceType


            // QUERY_SERVICE_CONFIG > dwStartType

            /// SERVICE_AUTO_START -> 0x00000002
            public const int SERVICE_AUTO_START = 2;
            /// SERVICE_BOOT_START -> 0x00000000
            public const int SERVICE_BOOT_START = 0;
            /// SERVICE_DEMAND_START -> 0x00000003
            public const int SERVICE_DEMAND_START = 3;
            /// SERVICE_DISABLED -> 0x00000004
            public const int SERVICE_DISABLED = 4;
            /// SERVICE_SYSTEM_START -> 0x00000001
            public const int SERVICE_SYSTEM_START = 1;


            // QUERY_SERVICE_CONFIG > dwErrorControl

            /// SERVICE_ERROR_CRITICAL -> 0x00000003
            public const int SERVICE_ERROR_CRITICAL = 3;
            /// SERVICE_ERROR_IGNORE -> 0x00000000
            public const int SERVICE_ERROR_IGNORE = 0;
            /// SERVICE_ERROR_NORMAL -> 0x00000001
            public const int SERVICE_ERROR_NORMAL = 1;
            /// SERVICE_ERROR_SEVERE -> 0x00000002
            public const int SERVICE_ERROR_SEVERE = 2;


            // ServiceStatus > dwServiceType

            /// SERVICE_FILE_SYSTEM_DRIVER -> 0x00000002
            public const int SERVICE_FILE_SYSTEM_DRIVER = 2;
            /// SERVICE_KERNEL_DRIVER -> 0x00000001
            public const int SERVICE_KERNEL_DRIVER = 1;
            /// SERVICE_WIN32_OWN_PROCESS -> 0x00000010
            public const int SERVICE_WIN32_OWN_PROCESS = 16;
            /// SERVICE_WIN32_SHARE_PROCESS -> 0x00000020
            public const int SERVICE_WIN32_SHARE_PROCESS = 32;
            /// SERVICE_USER_OWN_PROCESS -> 0x00000050
            public const int SERVICE_USER_OWN_PROCESS = 80;
            /// SERVICE_USER_SHARE_PROCESS -> 0x00000060
            public const int SERVICE_USER_SHARE_PROCESS = 96;

            /// SERVICE_INTERACTIVE_PROCESS -> 0x00000100
            public const int SERVICE_INTERACTIVE_PROCESS = 256;


            // ServiceStatus > dwCurrentState

            /// SERVICE_CONTINUE_PENDING -> 0x00000005
            public const int SERVICE_CONTINUE_PENDING = 5;
            /// SERVICE_PAUSE_PENDING -> 0x00000006
            public const int SERVICE_PAUSE_PENDING = 6;
            /// SERVICE_PAUSED -> 0x00000007
            public const int SERVICE_PAUSED = 7;
            /// SERVICE_RUNNING -> 0x00000004
            public const int SERVICE_RUNNING = 4;
            /// SERVICE_START_PENDING -> 0x00000002
            public const int SERVICE_START_PENDING = 2;
            /// SERVICE_STOP_PENDING -> 0x00000003
            public const int SERVICE_STOP_PENDING = 3;
            /// SERVICE_STOPPED -> 0x00000001
            public const int SERVICE_STOPPED = 1;


            // ServiceStatus > dwControlsAccepted

            /// SERVICE_ACCEPT_NETBINDCHANGE -> 0x00000010
            public const int SERVICE_ACCEPT_NETBINDCHANGE = 16;
            /// SERVICE_ACCEPT_PARAMCHANGE -> 0x00000008
            public const int SERVICE_ACCEPT_PARAMCHANGE = 8;
            /// SERVICE_ACCEPT_PAUSE_CONTINUE -> 0x00000002
            public const int SERVICE_ACCEPT_PAUSE_CONTINUE = 2;
            /// SERVICE_ACCEPT_PRESHUTDOWN -> 0x00000100
            public const int SERVICE_ACCEPT_PRESHUTDOWN = 256;
            /// SERVICE_ACCEPT_SHUTDOWN -> 0x00000004
            public const int SERVICE_ACCEPT_SHUTDOWN = 4;
            /// SERVICE_ACCEPT_STOP -> 0x00000001
            public const int SERVICE_ACCEPT_STOP = 1;

            /// SERVICE_ACCEPT_HARDWAREPROFILECHANGE -> 0x00000020
            public const int SERVICE_ACCEPT_HARDWAREPROFILECHANGE = 32;
            /// SERVICE_ACCEPT_POWEREVENT -> 0x00000040
            public const int SERVICE_ACCEPT_POWEREVENT = 64;
            /// SERVICE_ACCEPT_SESSIONCHANGE -> 0x00000080
            public const int SERVICE_ACCEPT_SESSIONCHANGE = 128;
            /// SERVICE_ACCEPT_TIMECHANGE -> 0x00000200
            public const int SERVICE_ACCEPT_TIMECHANGE = 512;
            /// SERVICE_ACCEPT_TRIGGEREVENT -> 0x00000400
            public const int SERVICE_ACCEPT_TRIGGEREVENT = 1024;
            /// SERVICE_ACCEPT_USERMODEREBOOT -> 0x00000800
            public const int SERVICE_ACCEPT_USERMODEREBOOT = 2048;



            // Parameters used by QueryServiceConfig2()
            // src.: https://msdn.microsoft.com/en-us/library/windows/desktop/ms684935(v=vs.85).aspx
            public const int SERVICE_CONFIG_DELAYED_AUTO_START_INFO = 3;
            public const int SERVICE_CONFIG_DESCRIPTION = 1;
            public const int SERVICE_CONFIG_FAILURE_ACTIONS = 2;
            public const int SERVICE_CONFIG_FAILURE_ACTIONS_FLAG = 4;
            public const int SERVICE_CONFIG_PREFERRED_NODE = 9;
            public const int SERVICE_CONFIG_PRESHUTDOWN_INFO = 7;
            public const int SERVICE_CONFIG_REQUIRED_PRIVILEGES_INFO = 6;
            public const int SERVICE_CONFIG_SERVICE_SID_INFO = 5;
            public const int SERVICE_CONFIG_TRIGGER_INFO = 8;
            public const int SERVICE_CONFIG_LAUNCH_PROTECTED = 12;

            // QueryServiceConfig2 > SERVICE_SID_INFO
            /// SERVICE_SID_INFO -> 0x00000000
            public const int SERVICE_SID_TYPE_NONE = 0;
            /// SERVICE_SID_TYPE_RESTRICTED -> 0x00000003
            public const int SERVICE_SID_TYPE_RESTRICTED = 3;
            /// SERVICE_SID_TYPE_UNRESTRICTED -> 0x00000001
            public const int SERVICE_SID_TYPE_UNRESTRICTED = 1;

            // ChangeServiceConfig > hService
            // + SERVICE_CHANGE_CONFIG

            // ChangeServiceConfig > dwServiceType
            /// SERVICE_NO_CHANGE -> 0xffffffff
            public const int SERVICE_NO_CHANGE = -1;
            // + SERVICE_FILE_SYSTEM_DRIVER
            // + SERVICE_KERNEL_DRIVER
            // + SERVICE_WIN32_OWN_PROCESS
            // + SERVICE_WIN32_SHARE_PROCESS
            // + SERVICE_INTERACTIVE_PROCESS

            // ChangeServiceConfig > dwStartType
            // + SERVICE_NO_CHANGE
            // + SERVICE_AUTO_START
            // + SERVICE_BOOT_START
            // + SERVICE_DEMAND_START
            // + SERVICE_DISABLED
            // + SERVICE_SYSTEM_START

            // ChangeServiceConfig > dwErrorControl
            // + SERVICE_NO_CHANGE
            // + SERVICE_ERROR_CRITICAL
            // + SERVICE_ERROR_IGNORE
            // + SERVICE_ERROR_NORMAL
            // + SERVICE_ERROR_SEVERE
        }
    }

    #endregion P/Invoke error codes
}