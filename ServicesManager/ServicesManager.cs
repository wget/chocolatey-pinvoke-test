using System;
using System.Runtime.InteropServices;


// This project must target the .NET 4 framework Client Profile for
// compatibility with choco (Posh 2 and Windows 7).
// To learn the differences between the full framework and the Client Profile:
// http://stackoverflow.com/a/2786831/3514658

class ServicesManager
{
    
    static void Main(string[] args) {
        // Use the service name and *NOT* the display name.
        ServiceProperties props = GetServiceProperties("Dnscache");
        PrintServiceProperties(props);

    }

    static public ServiceProperties GetServiceProperties(string serviceName) {
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
            throw new ExternalException("Unable to OpenService '" + serviceName + "'");
        }

        UInt32 dwBytesNeeded;
        string errMsg;

        // Take basic info. Everything is taken into account except the
        // delayed autostart.
        //
        // The 'ref' keyword tells the compiler that the object is initialized
        // before entering the function, while out tells the compiler that the
        // object will be initialized inside the function.
        // src.: http://stackoverflow.com/a/388467/3514658
        // Determine the buffer size needed (dwBytesNeeded).
        if (!QueryServiceConfig(
                serviceHandle,
                IntPtr.Zero,
                0,
                out dwBytesNeeded)) {

            if (Marshal.GetLastWin32Error() != NativeConstants.SystemErrorCode.ERROR_INSUFFICIENT_BUFFER) {
                Cleanup(databaseHandle, serviceHandle, out errMsg);
                throw new ExternalException("Unable to get service config for '" + serviceName + "': " + errMsg);
            }
        }

        // Get the main info of the service. See this struct for more info:
        // src.: https://msdn.microsoft.com/en-us/library/windows/desktop/ms684950(v=vs.85).aspx
        IntPtr ptr = Marshal.AllocHGlobal((int)dwBytesNeeded);
        if (!QueryServiceConfig(
                serviceHandle,
                ptr,
                dwBytesNeeded,
                out dwBytesNeeded)) {

            Cleanup(databaseHandle, serviceHandle, out errMsg);
            throw new ExternalException("Unable to get service config for '" + serviceName + "': " + errMsg);
        }
        QUERY_SERVICE_CONFIG serviceConfig = new QUERY_SERVICE_CONFIG();
        Marshal.PtrToStructure(ptr, serviceConfig);
        Marshal.FreeHGlobal(ptr);

        // Determine the buffer size needed (dwBytesNeeded).
        if (!QueryServiceConfig2(
                serviceHandle,
                NativeConstants.Service.SERVICE_CONFIG_DELAYED_AUTO_START_INFO,
                IntPtr.Zero,
                0,
                out dwBytesNeeded)) {

            if (Marshal.GetLastWin32Error() != NativeConstants.SystemErrorCode.ERROR_INSUFFICIENT_BUFFER) {
                Cleanup(databaseHandle, serviceHandle, out errMsg);
                throw new ExternalException("Unable to get service delayed auto start property for '" + serviceName + "': " + errMsg);
            }            
        }

        // Get the info if the service is set in delayed mode or not.
        ptr = Marshal.AllocHGlobal((int)dwBytesNeeded);
        if (!QueryServiceConfig2(
                serviceHandle,
                NativeConstants.Service.SERVICE_CONFIG_DELAYED_AUTO_START_INFO,
                ptr,
                dwBytesNeeded,
                out dwBytesNeeded)) {

            Cleanup(databaseHandle, serviceHandle, out errMsg);
            throw new ExternalException("Unable to get service delayed auto start property for '" + serviceName + "': " + errMsg);
        }
        ServiceDelayedAutoStartInfo serviceDelayed = new ServiceDelayedAutoStartInfo();
        Marshal.PtrToStructure(ptr, serviceDelayed);
        Marshal.FreeHGlobal(ptr);

        ServiceStatus serviceStatus = new ServiceStatus();
        if (!QueryServiceStatus(
                serviceHandle,
                out serviceStatus)) {
            Cleanup(databaseHandle, serviceHandle, out errMsg);
            throw new ExternalException("Unable to get service status for '" + serviceName + "': " + errMsg);
        }

        ServiceProperties props = new ServiceProperties();

        props.QueryServiceConfig.lpServiceConfig.dwServiceType = serviceConfig.dwServiceType;
        props.QueryServiceConfig.lpServiceConfig.dwStartType = serviceConfig.dwStartType;
        props.QueryServiceConfig.lpServiceConfig.dwErrorControl = serviceConfig.dwErrorControl;
        props.QueryServiceConfig.lpServiceConfig.lpBinaryPathName = serviceConfig.lpBinaryPathName;
        props.QueryServiceConfig.lpServiceConfig.lpLoadOrderGroup = serviceConfig.lpLoadOrderGroup;
        props.QueryServiceConfig.lpServiceConfig.dwTagId = serviceConfig.dwTagId;
        props.QueryServiceConfig.lpServiceConfig.lpDependencies = serviceConfig.lpDependencies;
        props.QueryServiceConfig.lpServiceConfig.lpServiceStartName = serviceConfig.lpServiceStartName;
        props.QueryServiceConfig.lpServiceConfig.lpDisplayName = serviceConfig.lpDisplayName;

        props.QueryServiceConfig2.lpBuffer.SERVICE_DELAYED_AUTO_START_INFO.fDelayedAutostart = serviceDelayed.fDelayedAutostart;

        props.QueryServiceStatus.lpServiceStatus.dwServiceType = serviceStatus.dwServiceType;
        props.QueryServiceStatus.lpServiceStatus.dwCurrentState = serviceStatus.dwCurrentState;
        props.QueryServiceStatus.lpServiceStatus.dwControlsAccepted = serviceStatus.dwControlsAccepted;
        props.QueryServiceStatus.lpServiceStatus.dwWin32ExitCode = serviceStatus.dwWin32ExitCode;
        props.QueryServiceStatus.lpServiceStatus.dwServiceSpecificExitCode = serviceStatus.dwServiceSpecificExitCode;
        props.QueryServiceStatus.lpServiceStatus.dwCheckPoint = serviceStatus.dwCheckPoint;
        props.QueryServiceStatus.lpServiceStatus.dwWaitHint = serviceStatus.dwWaitHint;

        props.Name = serviceName;
        props.DisplayName = serviceConfig.lpDisplayName;

        switch (serviceConfig.dwStartType) {
            case NativeConstants.Service.SERVICE_AUTO_START:
                if (serviceDelayed.fDelayedAutostart) {
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

    static private void PrintServiceProperties(ServiceProperties props) {
        
        Console.WriteLine("DisplayName: '" + props.DisplayName + "'");
        Console.WriteLine("StartMode: '" + props.StartMode + "'");
        Console.WriteLine("Status: '" + props.Status + "'");
        Console.ReadLine();
    }

    static private void Cleanup(
        IntPtr databaseHandle,
        IntPtr serviceHandle,
        out string errMsg) {

        int errCode = Marshal.GetLastWin32Error();
        CloseServiceHandle(serviceHandle);
        CloseServiceHandle(databaseHandle);
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
            default:
                errMsg = errCode.ToString();
                break;
        }
    }

   public class ServiceProperties {
        public QueryServiceConfigClass QueryServiceConfig = new QueryServiceConfigClass();
        public QueryServiceConfig2Class QueryServiceConfig2 = new QueryServiceConfig2Class();
        public QueryServiceStatusClass QueryServiceStatus = new QueryServiceStatusClass();

        public String Name { get; set; }
        public String DisplayName { get; set; }
        public ServiceControllerStatus Status { get; set; }
        public ServiceStartMode StartMode { get; set; }
    }
    
    public class QueryServiceConfigClass {
        public ServiceConfigClass lpServiceConfig = new ServiceConfigClass();
    }

    public class ServiceConfigClass {
        // From the struct QUERY_SERVICE_CONFIG
        // src.: https://msdn.microsoft.com/en-us/library/windows/desktop/ms684950(v=vs.85).aspx
        // used by the function QueryServiceConfig()
        // src.: https://msdn.microsoft.com/en-us/library/windows/desktop/ms684932(v=vs.85).aspx
        public uint dwServiceType { get; set; }
        public uint dwStartType { get; set; }
        public uint dwErrorControl { get; set; }
        public string lpBinaryPathName { get; set; }
        public string lpLoadOrderGroup { get; set; }
        public uint dwTagId { get; set; }
        public string lpDependencies { get; set; }
        public string lpServiceStartName { get; set; }
        public string lpDisplayName { get; set; }
    }

    public class QueryServiceConfig2Class {
        public ServiceConfig2Class lpBuffer = new ServiceConfig2Class();
    }

    public class ServiceConfig2Class {
        public ServiceConfig2ServiceDelayedAutoStartInfo SERVICE_DELAYED_AUTO_START_INFO = new ServiceConfig2ServiceDelayedAutoStartInfo();
    }

    public class ServiceConfig2ServiceDelayedAutoStartInfo {
        // From the struct SERVICE_DELAYED_AUTO_START_INFO 
        // src.: https://msdn.microsoft.com/en-us/library/windows/desktop/ms685155(v=vs.85).aspx
        // used by the function QueryServiceConfig2()
        // src.: https://msdn.microsoft.com/en-us/library/windows/desktop/ms684935(v=vs.85).aspx
        public bool fDelayedAutostart { get; set; }
    }
    
    public class QueryServiceStatusClass {
        public ServiceStatus lpServiceStatus = new ServiceStatus();
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
        AutomaticDelayed,
        Boot = NativeConstants.Service.SERVICE_BOOT_START,
        Disabled = NativeConstants.Service.SERVICE_DISABLED,
        Manual = NativeConstants.Service.SERVICE_DEMAND_START,
        System = NativeConstants.Service.SERVICE_SYSTEM_START
    }

    #region P/Invoke declarations

    // The LayoutKind.Sequential specifies that the fields of the type should
    // be laid out in memory in the same order they are declared in your
    // source code. That's often important when interoperating with native
    // code. Without the attribute the CLR is free to optimize memory use
    // by rearranging the fields.
    // src.: https://social.msdn.microsoft.com/Forums/vstudio/en-US/2abc6be8-c593-4686-93d2-89785232dacd#0455ea02-7eab-451b-8a83-fbfc4384d654
    [StructLayout(LayoutKind.Sequential)]
    public class SERVICE_DESCRIPTION
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public String lpDescription;
    }
    
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public class SERVICE_FAILURE_ACTIONS
    {
        public int dwResetPeriod;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpRebootMsg;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpCommand;
        public int cActions;
        public IntPtr lpsaActions;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class SC_ACTION
    {
        public Int32 type;
        public UInt32 dwDelay;
    }

    // P/Invoke Interop Assistant 1.0 is useful to generate declarations of
    // native code constant in their managed counterparts.
    // http://stackoverflow.com/a/5122534/3514658
    [StructLayoutAttribute(LayoutKind.Sequential)]
    public class QUERY_SERVICE_CONFIG
    {
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

    [StructLayout(LayoutKind.Sequential)]
    public class ServiceDelayedAutoStartInfo {
        [MarshalAs(UnmanagedType.Bool)]
        public bool fDelayedAutostart;
    }

    // From the struct ServiceStatus
    // src.: https://msdn.microsoft.com/en-us/library/windows/desktop/ms685996(v=vs.85).aspx
    // used by the function QueryServiceStatus()
    // src.: https://msdn.microsoft.com/en-us/library/windows/desktop/ms684939(v=vs.85).aspx
    [StructLayoutAttribute(LayoutKind.Sequential)]
    public struct ServiceStatus {

        /// DWORD->unsigned int
        public uint dwServiceType { get; set; }
        /// DWORD->unsigned int
        public uint dwCurrentState { get; set; }
        /// DWORD->unsigned int
        public uint dwControlsAccepted { get; set; }
        /// DWORD->unsigned int
        public uint dwWin32ExitCode { get; set; }
        /// DWORD->unsigned int
        public uint dwServiceSpecificExitCode { get; set; }
        /// DWORD->unsigned int
        public uint dwCheckPoint { get; set; }
        /// DWORD->unsigned int
        public uint dwWaitHint { get; set; }
    }

    // Some import statements are inspired from some public solutions from
    // Pinvoke.net.
    // https://webcache.googleusercontent.com/search?q=cache:4U7pz3gubesJ:www.pinvoke.net/default.aspx/advapi32.queryserviceconfig2
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
    public static extern bool QueryServiceStatus(IntPtr hService, out ServiceStatus lpServiceStatus);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CloseServiceHandle")]
    public static extern bool CloseServiceHandle(IntPtr hSCObject);

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
        }
    }

    #endregion // P/Invoke declarations
}