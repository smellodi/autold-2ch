using System;
using System.Diagnostics;
using System.Management;

namespace Olfactory
{
    internal class USB
    {
        public event EventHandler<string> Inserted = delegate { };
        public event EventHandler<string> Removed = delegate { };

        public USB()
        {
            Listen("__InstanceCreationEvent", "Win32_SerialPort", Inserted);  // CIM_SerialController
            Listen("__InstanceDeletionEvent", "Win32_SerialPort", Removed);   // Win32_USBHub
        }

        // Internal

        private void Listen(string evt, string target, EventHandler<string> action)
        {
            WqlEventQuery query = new WqlEventQuery($"SELECT * FROM {evt} WITHIN 2 WHERE TargetInstance ISA '{target}'");
            ManagementEventWatcher watcher = new ManagementEventWatcher(query);
            
            watcher.EventArrived += (s, e) =>
            {
                Debug.WriteLine($"{evt} => TargetInstance = {target}");
                string portName = "";

                try
                {
                    var props = ((ManagementBaseObject)e.NewEvent["TargetInstance"]).Properties;
                    portName = FindPortName(props);
                }
                catch (Exception ex) { Debug.WriteLine("ERROR: " + ex.Message); }

                if (action != null && portName.StartsWith("COM"))
                {
                    action(this, portName);
                }
            };
            
            watcher.Start();
        }

        private string FindPortName(PropertyDataCollection props)
        {
            string result = "";

            foreach (var property in props)
            {
                Debug.WriteLine("   " + property.Name + " = " + property.Value);
                if (property.Name == "DeviceID")
                {
                    result = (string)property.Value;
                }
            }

            return result;
        }
    }
}