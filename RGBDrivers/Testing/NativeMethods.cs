using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace RGBLibrary
{
    class NativeMethods
    {
        internal const Int32 DIGCF_PRESENT = 2;
        internal const Int32 DIGCF_DEVICEINTERFACE = 0X10;
        internal const Int32 FILE_ATTRIBUTE_NORMAL = 0X80;
        internal const Int32 FILE_FLAG_OVERLAPPED = 0X40000000;
        internal const Int32 FILE_SHARE_READ = 1;
        internal const Int32 FILE_SHARE_WRITE = 2;
        internal const Int32 OPEN_EXISTING = 3;
        internal const UInt32 GENERIC_READ = 0X80000000;
        internal const UInt32 GENERIC_WRITE = 0X40000000;

        internal const string MOUSE_GUID = "{745a17a0-74d3-11d0-b6fe-00a0c90f57da}";
        internal const string USB_MOUSE_GUID = "{9d7debbc-c85d-11d1-9eb4-006008c3a19a}";
        internal static Guid myGuid = Guid.Empty;
        internal static HIDD_ATTRIBUTES DeviceAttributes;

        public struct HIDD_ATTRIBUTES
        {
            public int Size;
            public int VendorId;
            public int ProductId;
            public int VersionNumber;

            public override string ToString()
            {
                return $"Size: {Size}\r\nVendorId: {VendorId}\r\nProductId: {ProductId}\r\nVersionNumber: {VersionNumber}";
            }
        }

        internal struct SP_DEVICE_INTERFACE_DATA
        {
            internal Int32 cbSize;
            internal Guid InterfaceClassGuid;
            internal Int32 Flags;
            internal IntPtr Reserved;
            public override string ToString()
            {
                return $"Size: {cbSize} \r\nInterface Class Guid: {InterfaceClassGuid} \r\nFlags {Flags} \r\nReserved: {Reserved}";
            }
        }

        internal struct SP_DEVICE_INTERFACE_DETAIL_DATA
        {
            internal Int32 cbSize;
            internal String DevicePath;
        }

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern Boolean SetupDiGetDeviceInterfaceDetail(IntPtr DeviceInfoSet, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData, IntPtr DeviceInterfaceDetailData, Int32 DeviceInterfaceDetailDataSize, ref Int32 RequiredSize, IntPtr DeviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, Int32 flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern Boolean SetupDiEnumDeviceInterfaces(IntPtr DeviceInfoSet, IntPtr DeviceInfoData, ref Guid InterfaceClassGuid, Int32 MemberIndex, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

        [DllImport("hid.dll", SetLastError = true)]
        static extern Boolean HidD_GetAttributes(SafeFileHandle HidDeviceObject, ref HIDD_ATTRIBUTES Attributes);

        [DllImport("hid.dll", SetLastError = true)]
        static extern void HidD_GetHidGuid(ref Guid HidGuid);

        [DllImport("Wdm.dll")]
        static extern void Hid_getHidGuid(ref Guid hidGuid);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern SafeFileHandle CreateFile(String IpFileName, UInt32 dwDesiredAccess, Int32 dwShareMode, IntPtr IpSecurityAttributes, Int32 dwCreationDisposition, Int32 dwFlagsAndAttributes, IntPtr hTemplateFile);
        [DllImport("kernel32.dll")]
        static extern uint GetLastError();

        private static SafeFileHandle GetDeviceHandle(Guid guid)
        {
            Guid deviceInterfaceGuid = FindDeviceInterfaceGuid(guid.ToString());
            var deviceInfoSet = GetDeviceInfoSetPointer(ref deviceInterfaceGuid);
            SP_DEVICE_INTERFACE_DATA deviceInterfaceData = new SP_DEVICE_INTERFACE_DATA();
            Int32 index = 0;
            var devicePaths = new string[128];
            while (IdentifyDeviceInterface(deviceInfoSet, ref deviceInterfaceGuid, index++, ref deviceInterfaceData))
            {
                devicePaths[index - 1] = GetDevicePathName(deviceInfoSet, ref deviceInterfaceData);
                var handle = GetFileHandle(devicePaths[index - 1]);
                if (!handle.IsInvalid)
                    return handle;
            }

            return null;
        }

        private static bool IdentifyDeviceInterface(IntPtr deviceInfoSet, ref Guid interfaceClassGuid, Int32 index, ref SP_DEVICE_INTERFACE_DATA myDeviceInterfaceData)
        {
            myDeviceInterfaceData = new SP_DEVICE_INTERFACE_DATA();
            myDeviceInterfaceData.cbSize = Marshal.SizeOf(myDeviceInterfaceData);
            return SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref interfaceClassGuid, index, ref myDeviceInterfaceData);
        }

        private static String GetDevicePathName(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData)
        {
            Int32 bufferSize = 0;
            IntPtr detailDataBuffer = IntPtr.Zero;
            SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref deviceInterfaceData, IntPtr.Zero, 0, ref bufferSize, IntPtr.Zero);
            detailDataBuffer = Marshal.AllocHGlobal(bufferSize);
            Marshal.WriteInt32(detailDataBuffer, (IntPtr.Size == 4) ? (4 + Marshal.SystemDefaultCharSize) : 8);
            SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref deviceInterfaceData, IntPtr.Zero, bufferSize, ref bufferSize, IntPtr.Zero);

            var pDevicePathName = new IntPtr(detailDataBuffer.ToInt64() + 4);

            string devicePathName = Marshal.PtrToStringAuto(pDevicePathName);
            Marshal.FreeHGlobal(detailDataBuffer);

            return devicePathName;
        }

        private static Guid FindDeviceInterfaceGuid(String guid)
        {
            var myGuid = Guid.Empty;
            HidD_GetHidGuid(ref myGuid);
            return myGuid;
        }

        private static IntPtr GetDeviceInfoSetPointer(ref Guid guid)
        {
            return SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        }

        private static SafeFileHandle GetFileHandle(String devicePathName)
        {
            var value = Environment.GetLogicalDrives();

            return CreateFile(devicePathName, (GENERIC_WRITE | GENERIC_READ), FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL | FILE_FLAG_OVERLAPPED, IntPtr.Zero);
        }

    }
}
