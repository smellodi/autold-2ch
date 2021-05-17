using System;
using System.Diagnostics;
using System.Management;

namespace Olfactory
{
    internal class USB
    {
        public event EventHandler<PropertyDataCollection> Inserted = delegate { };
        public event EventHandler<PropertyDataCollection> Removed = delegate { };

        public USB()
        {
            WqlEventQuery insertQuery = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_USBHub'");

            ManagementEventWatcher insertWatcher = new ManagementEventWatcher(insertQuery);
            insertWatcher.EventArrived += new EventArrivedEventHandler(DeviceInsertedEvent);
            insertWatcher.Start();

            WqlEventQuery removeQuery = new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_USBHub'");
            ManagementEventWatcher removeWatcher = new ManagementEventWatcher(removeQuery);
            removeWatcher.EventArrived += (s, e) => Removed(this, ((ManagementBaseObject)e.NewEvent["TargetInstance"]).Properties);
            removeWatcher.Start();

            // For debug only:
            //
            // I wish to figure out which WMI event fires when serial port is connected.
            // After figuring this out, I can probably modify the listeners above to make them
            // to fire Inserted and Removed only when a COM port is connected/disconnected,
            // and not just any USB device as it happens now.
            Start("__InstanceModificationEvent", "CIM_SerialInterface");
            Start("__InstanceModificationEvent", "CIM_SerialController");
            Start("__InstanceModificationEvent", "Win32_SerialPort");

            Start("__InstanceCreationEvent", "CIM_SerialInterface");
            Start("__InstanceCreationEvent", "CIM_SerialController");
            Start("__InstanceCreationEvent", "Win32_SerialPort");
        }

        private void DeviceInsertedEvent(object sender, EventArrivedEventArgs e)
        {
            ManagementBaseObject instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            Debug.WriteLine($"USB device inserted");
            Log(instance.Properties);

            Inserted(this, instance.Properties);
        }

        void Start(string evt, string target)
        {
            WqlEventQuery query = new WqlEventQuery($"SELECT * FROM {evt} WITHIN 2 WHERE TargetInstance ISA '{target}'");
            ManagementEventWatcher watcher = new ManagementEventWatcher(query);
            watcher.EventArrived += (s, e) =>
            {
                try
                {
                    Debug.WriteLine($"{evt} => TargetInstance = {target}");
                    Log(((ManagementBaseObject)e.NewEvent["TargetInstance"]).Properties);
                }
                catch (Exception ex) { Debug.WriteLine("ERROR: " + ex.Message); }
            };
            watcher.Start();
        }

        void Log(PropertyDataCollection data)
        {
            foreach (var property in data)
            {
                Debug.WriteLine("   " + property.Name + " = " + property.Value);
            }
        }
    }
}