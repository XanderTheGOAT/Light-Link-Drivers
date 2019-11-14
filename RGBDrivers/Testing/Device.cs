using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Timers;
using Testing.Exceptions;

namespace RGBLibrary
{
    public class Device
    {

        private enum ReportReadOrWritten
        {
            Read,
            Written
        }

        private enum ReportTypes
        {
            Input,
            Output,
            Feature
        }

        private enum SendOrGet
        {
            Send,
            Get
        }

        private enum TransferTypes
        {
            Control,
            Interrupt
        }

        private enum WmiDeviceProperties
        {
            Name,
            Caption,
            Description,
            Manufacturer,
            PNPDeviceID,
            DeviceID,
            ClassGUID
        }

        private Boolean _deviceDetected;
        private IntPtr _deviceNotificationHandle;
        private FileStream _deviceData;
        private SafeFileHandle _hidHandle;
        private String _hidUsage;
        private ManagementEventWatcher _deviceArrivedWatcher;
        private Boolean _deviceHandleObtained;
        private ManagementEventWatcher _deviceRemovedWatcher;
        private Int32 _myProductId;
        private Int32 _myVendorId;
        private Boolean _periodicTransfersRequested;
        private ReportReadOrWritten _readOrWritten;
        private ReportTypes _reportType;
        private SendOrGet _sendOrGet;
        private Boolean _transferInProgress;
        private TransferTypes _transferType;
        private Hid _myHid = new Hid();
        private readonly DeviceManagement _myDeviceManagement = new DeviceManagement();

        public Device(String vendorId, String productId)
        {
            try
            {
                SetVendorIdAndProductId(vendorId, productId);
            }
            catch (Exception)
            {
                throw new InvalidFormatException("Invalid Format of either Vendor Id or Product Id");
            }
        }

        public Boolean FindDeviceUsingWmi()
        {
            // Prepend "@" to string below to treat backslash as a normal character (not escape character):

            String deviceIdString = @"USB\VID_" + _myVendorId.ToString("X4") + "&PID_" + _myProductId.ToString("X4");

            _deviceDetected = false;
            var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_PnPEntity");

            foreach (ManagementObject queryObj in searcher.Get())
            {
                if (queryObj["PNPDeviceID"].ToString().Contains(deviceIdString))
                {
                    _deviceDetected = true;
                    Console.WriteLine("--------");
                    Console.WriteLine("My device found (WMI):");
                    // Display device properties.

                    foreach (WmiDeviceProperties wmiDeviceProperty in Enum.GetValues(typeof(WmiDeviceProperties)))
                    {
                        Debug.WriteLine(wmiDeviceProperty.ToString() + ": {0}", queryObj[wmiDeviceProperty.ToString()]);
                    }
                }
            }
            if (!_deviceDetected)
            {
                Console.WriteLine("Device not found");
            }
            return _deviceDetected;
        }


        /// <summary>
        /// Close the handle and FileStreams for a device.
        /// </summary>
        /// 
        private void CloseCommunications()
        {
            if (_deviceData != null)
            {
                _deviceData.Close();
            }

            if ((_hidHandle != null) && (!(_hidHandle.IsInvalid)))
            {
                _hidHandle.Close();
            }

            // The next attempt to communicate will get a new handle and FileStreams.

            _deviceHandleObtained = false;
        }

        private void SetVendorIdAndProductId(String myVendorId, String myProductId)
        {
            _myVendorId = Int32.Parse("1B1C", NumberStyles.AllowHexSpecifier);
            _myProductId = Int32.Parse("1B2E", NumberStyles.AllowHexSpecifier);
        }


        public Boolean FindTheHid()
        {
            var devicePathName = new String[128];
            String myDevicePathName = "";

            _deviceHandleObtained = false;
            CloseCommunications();

            // Get the HID-class GUID.

            Guid hidGuid = _myHid.GetHidGuid();

            String functionName = "GetHidGuid";
            Console.WriteLine("  GUID for system HIDs: " + hidGuid.ToString());

            //  Fill an array with the device path names of all attached HIDs.

            Boolean availableHids = _myDeviceManagement.FindDeviceFromGuid(hidGuid, ref devicePathName);

            //  If there is at least one HID, attempt to read the Vendor ID and Product ID
            //  of each device until there is a match or all devices have been examined.

            if (availableHids)
            {
                Int32 memberIndex = 0;

                do
                {
                    // Open the handle without read/write access to enable getting information about any HID, even system keyboards and mice.

                    _hidHandle = _myHid.OpenHandle(devicePathName[memberIndex], false);

                    functionName = "CreateFile";
                    Console.WriteLine("  Returned handle: " + _hidHandle);

                    if (!_hidHandle.IsInvalid)
                    {
                        // The returned handle is valid, 
                        // so find out if this is the device we're looking for.

                        _myHid.DeviceAttributes.Size = Marshal.SizeOf(_myHid.DeviceAttributes);

                        Boolean success = _myHid.GetAttributes(_hidHandle, ref _myHid.DeviceAttributes);

                        if (success)
                        {
                            Console.WriteLine("  HIDD_ATTRIBUTES structure filled without error.");
                            Console.WriteLine("  Structure size: " + _myHid.DeviceAttributes.Size);
                            Console.WriteLine("  Vendor ID: " + Convert.ToString(_myHid.DeviceAttributes.VendorID, 16));
                            Console.WriteLine("  Product ID: " + Convert.ToString(_myHid.DeviceAttributes.ProductID, 16));
                            Console.WriteLine("  Version Number: " + Convert.ToString(_myHid.DeviceAttributes.VersionNumber, 16));

                            if ((_myHid.DeviceAttributes.VendorID == _myVendorId) && (_myHid.DeviceAttributes.ProductID == _myProductId))
                            {
                                Debug.WriteLine("  Handle obtained to my device");

                                //  Display the information in form's list box.

                                Console.WriteLine($"Handle obtained to my device:" +
                                    $"\r\n\tVendor ID = {Convert.ToString(_myHid.DeviceAttributes.VendorID, 16)}" +
                                    $"\r\n\tProduct ID = {Convert.ToString(_myHid.DeviceAttributes.ProductID, 16)}");
                                _deviceHandleObtained = true;

                                myDevicePathName = devicePathName[memberIndex];
                            }
                            else
                            {
                                //  It's not a match, so close the handle.

                                _deviceHandleObtained = false;
                                _hidHandle.Close();
                            }
                        }
                        else
                        {
                            //  There was a problem retrieving the information.

                            Debug.WriteLine("  Error in filling HIDD_ATTRIBUTES structure.");
                            _deviceHandleObtained = false;
                            _hidHandle.Close();
                        }
                    }

                    //  Keep looking until we find the device or there are no devices left to examine.

                    memberIndex = memberIndex + 1;
                }
                while (!((_deviceHandleObtained || (memberIndex == devicePathName.Length))));
            }

            if (_deviceHandleObtained)
            {
                //  The device was detected.
                //  Learn the capabilities of the device.

                _myHid.Capabilities = _myHid.GetDeviceCapabilities(_hidHandle);

                //  Find out if the device is a system mouse or keyboard.

                _hidUsage = _myHid.GetHidUsage(_myHid.Capabilities);

                //  Get the Input report buffer size.

                GetInputReportBufferSize();

                //Close the handle and reopen it with read/write access.

                _hidHandle.Close();

                _hidHandle = _myHid.OpenHandle(myDevicePathName, true);

                if (_hidHandle.IsInvalid)
                {
                }
                else
                {
                    if (_myHid.Capabilities.InputReportByteLength > 0)
                    {
                        //  Set the size of the Input report buffer. 

                        var inputReportBuffer = new Byte[_myHid.Capabilities.InputReportByteLength];

                        _deviceData = new FileStream(_hidHandle, FileAccess.Read | FileAccess.Write, inputReportBuffer.Length, false);
                    }

                    if (_myHid.Capabilities.OutputReportByteLength > 0)
                    {
                        Byte[] outputReportBuffer = null;
                    }
                    //  Flush any waiting reports in the input buffer. (optional)

                    _myHid.FlushQueue(_hidHandle);
                }
            }
            else
            {
                Console.WriteLine("Device not found");
            }
            return _deviceHandleObtained;
        }

        public void GetInputReportBufferSize()
        {
            Int32 numberOfInputBuffers = 0;
            Boolean success;
            //  Get the number of input buffers.

            _myHid.GetNumberOfInputBuffers(_hidHandle, ref numberOfInputBuffers);

            //  Display the result in the console.
            Console.WriteLine(Convert.ToString(numberOfInputBuffers));
        }

        public void RequestToGetFeatureReport()
        {
            String byteValue = null;

            //  If the device hasn't been detected, was removed, or timed out on a previous attempt
            //  to access it, look for the device.

            if (!_deviceHandleObtained)
            {
                _deviceHandleObtained = FindTheHid();
            }

            if (_deviceHandleObtained)
            {
                Byte[] inFeatureReportBuffer = null;

                if ((_myHid.Capabilities.FeatureReportByteLength > 0))
                {
                    //  The HID has a Feature report.	
                    //  Read a report from the device.

                    //  Set the size of the Feature report buffer. 

                    if ((_myHid.Capabilities.FeatureReportByteLength > 0))
                    {
                        inFeatureReportBuffer = new Byte[_myHid.Capabilities.FeatureReportByteLength];
                    }

                    //  Read a report.

                    Boolean success = _myHid.GetFeatureReport(_hidHandle, ref inFeatureReportBuffer);

                    if (!success)
                    {
                        CloseCommunications();
                    }
                }
            }
            _transferInProgress = false;
        }

        public async void RequestToGetInputReport()
        {
            const Int32 readTimeout = 5000;

            String byteValue = null;
            Byte[] inputReportBuffer = null;

            Boolean success = false;

            //  If the device hasn't been detected, was removed, or timed out on a previous attempt
            //  to access it, look for the device.

            if (!_deviceHandleObtained)
            {
                _deviceHandleObtained = FindTheHid();
            }

            if (_deviceHandleObtained)
            {
                //  Don't attempt to exchange reports if valid handles aren't available
                //  (as for a mouse or keyboard under Windows 2000 and later.)

                if (!_hidHandle.IsInvalid)
                {
                    //  Read an Input report.

                    //  Don't attempt to send an Input report if the HID has no Input report.
                    //  (The HID spec requires all HIDs to have an interrupt IN endpoint,
                    //  which suggests that all HIDs must support Input reports.)

                    if (_myHid.Capabilities.InputReportByteLength > 0)
                    {
                        //  Set the size of the Input report buffer. 

                        inputReportBuffer = new Byte[_myHid.Capabilities.InputReportByteLength];

                        if (_transferType.Equals(TransferTypes.Control))
                        {
                            {
                                _transferInProgress = true;

                                //  Read a report using a control transfer.

                                success = _myHid.GetInputReportViaControlTransfer(_hidHandle, ref inputReportBuffer);
                                _transferInProgress = false;
                            }
                        }
                        else
                        {
                            {
                                _transferInProgress = true;

                                //  Read a report using interrupt transfers. 
                                //  Timeout if no report available.
                                //  To enable reading a report without blocking the calling thread, uses Filestream's ReadAsync method.                                               

                                // Create a delegate to execute on a timeout.

                                Action onReadTimeoutAction = OnReadTimeout;

                                // The CancellationTokenSource specifies the timeout value and the action to take on a timeout.

                                var cts = new CancellationTokenSource();

                                // Cancel the read if it hasn't completed after a timeout.

                                cts.CancelAfter(readTimeout);

                                // Specify the function to call on a timeout.

                                cts.Token.Register(onReadTimeoutAction);

                                // Stops waiting when data is available or on timeout:

                                Int32 bytesRead = await _myHid.GetInputReportViaInterruptTransfer(_deviceData, inputReportBuffer, cts);

                                // Arrive here only if the operation completed.

                                // Dispose to stop the timeout timer. 

                                cts.Dispose();

                                _transferInProgress = false;

                                if (bytesRead > 0)
                                {
                                    success = true;
                                    Debug.Print("bytes read (includes report ID) = " + Convert.ToString(bytesRead));
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("No attempt to read an Input report was made.");
                        Console.WriteLine("The HID doesn't have an Input report.");
                    }
                }
                else
                {
                    Console.WriteLine("Invalid handle");
                    Console.WriteLine("No attempt to write an Output report or read an Input report was made.");
                }

                if (!success)
                {
                    CloseCommunications();
                }
            }

        }

        public void RequestToSendFeatureReport(byte[] commands)
        {
            _transferType = TransferTypes.Control;
            _transferInProgress = true;
            //  If the device hasn't been detected, was removed, or timed out on a previous attempt
            //  to access it, look for the device.

            if (!_deviceHandleObtained)
            {
                _deviceHandleObtained = FindTheHid();
            }

            if (_deviceHandleObtained)
            {

                if ((_myHid.Capabilities.FeatureReportByteLength > 0))
                {
                    //  The HID has a Feature report.
                    //  Set the size of the Feature report buffer. 

                    var outFeatureReportBuffer = new Byte[_myHid.Capabilities.FeatureReportByteLength];
                    //  Store the report ID in the buffer.
                    outFeatureReportBuffer[0] = 0;

                    TransferCommandsToBuffer(outFeatureReportBuffer, commands);
                    //  Store the report data following the report ID.
                    //  Use the data in the combo boxes on the form.
                    //  Write a report to the device

                    Boolean success = _myHid.SendFeatureReport(_hidHandle, outFeatureReportBuffer);

                    if (!success)
                    {
                        CloseCommunications();
                    }
                }

                else
                {
                    Console.WriteLine("The HID doesn't have a Feature report.");
                }

            }
            _transferInProgress = false;

        }

        private void TransferCommandsToBuffer(byte[] buffer, byte[] commands)
        {
            if (commands.Length > buffer.Length)
                throw new InvalidFormatException("Commands is longer than the size of the buffer");

            Array.Copy(commands, 0, buffer, 1, commands.Length);

        }

        ///  <summary>
        ///  Sends an Output report.
        ///  Assumes report ID = 0.
        ///  </summary>

        public async void RequestToSendOutputReport(byte[] commands)
        {
            const Int32 writeTimeout = 5000;
            String byteValue = null;

            //  If the device hasn't been detected, was removed, or timed out on a previous attempt
            //  to access it, look for the device.

            if (!_deviceHandleObtained)
            {
                _deviceHandleObtained = FindTheHid();
            }

            if (_deviceHandleObtained)
            {
            }
            //  Don't attempt to exchange reports if valid handles aren't available
            //  (as for a mouse or keyboard.)

            if (!_hidHandle.IsInvalid)
            {
                //  Don't attempt to send an Output report if the HID has no Output report.

                if (_myHid.Capabilities.OutputReportByteLength > 0)
                {
                    //  Set the size of the Output report buffer.   

                    var outputReportBuffer = new Byte[_myHid.Capabilities.OutputReportByteLength];
                    //  Store the report ID in the first byte of the buffer:

                    outputReportBuffer[0] = 0;

                    //  Store the report data following the report ID.
                    //  Use the data in the combo boxes on the form.
                    TransferCommandsToBuffer(outputReportBuffer, commands);
                    //  Write a report.
                    Boolean success;

                    if (_transferType.Equals(TransferTypes.Control))
                    {
                        {
                            _transferInProgress = true;

                            //  Use a control transfer to send the report,
                            //  even if the HID has an interrupt OUT endpoint.

                            success = _myHid.SendOutputReportViaControlTransfer(_hidHandle, outputReportBuffer);

                            _transferInProgress = false;
                        }
                    }
                    else
                    {
                        Debug.Print("interrupt");
                        _transferInProgress = true;

                        // The CancellationTokenSource specifies the timeout value and the action to take on a timeout.

                        var cts = new CancellationTokenSource();

                        // Create a delegate to execute on a timeout.

                        Action onWriteTimeoutAction = OnWriteTimeout;

                        // Cancel the read if it hasn't completed after a timeout.

                        cts.CancelAfter(writeTimeout);

                        // Specify the function to call on a timeout.

                        cts.Token.Register(onWriteTimeoutAction);

                        // Send an Output report and wait for completion or timeout.

                        success = await _myHid.SendOutputReportViaInterruptTransfer(_deviceData, _hidHandle, outputReportBuffer, cts);

                        // Get here only if the operation completes without a timeout.

                        _transferInProgress = false;

                        // Dispose to stop the timeout timer.

                        cts.Dispose();
                    }
                    if (!success)
                    {
                        CloseCommunications();
                    }
                }
            }
            else
            {
                Console.WriteLine("The HID doesn't have an Output report.");
            }

        }

        private void FillBufferWithTemporaryNumber(byte[] outputReportBuffer)
        {
            for (int i = 0; i < outputReportBuffer.Length; i++)
            {
                outputReportBuffer[i] = 128;
            }
        }

        /// <summary>
        /// Timeout if write via interrupt transfer doesn't return.
        /// </summary>
        private void OnWriteTimeout()
        {
            Console.WriteLine("The attempt to write a report timed out.");
            CloseCommunications();
            Console.WriteLine();
            _transferInProgress = false;
            _sendOrGet = SendOrGet.Get;
        }



        /// <summary>
        /// Timeout if read via interrupt transfer doesn't return.
        /// </summary>

        private void OnReadTimeout()
        {
            Console.WriteLine("The report timed out\r\n");
            CloseCommunications();
            _transferInProgress = false;
            _sendOrGet = SendOrGet.Send;
        }

    }
}
