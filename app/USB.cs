﻿using System;
using System.Management;

namespace AutOlD2Ch;

internal class USB
{
    public event EventHandler<string>? Inserted;
    public event EventHandler<string>? Removed;

    public USB()
    {
        Listen("__InstanceCreationEvent", "Win32_SerialPort", ActionType.Inserted);  // CIM_SerialController
        Listen("__InstanceDeletionEvent", "Win32_SerialPort", ActionType.Removed);   // Win32_USBHub
    }

    // Internal

    enum ActionType
    {
        Inserted,
        Removed
    }

    private void Listen(string evt, string target, ActionType actionType)
    {
        var query = new WqlEventQuery($"SELECT * FROM {evt} WITHIN 2 WHERE TargetInstance ISA '{target}'");
        var watcher = new ManagementEventWatcher(query);

        watcher.EventArrived += (s, e) =>
        {
            string portName = "";

            try
            {
                var props = ((ManagementBaseObject)e.NewEvent["TargetInstance"]).Properties;
                portName = FindPortName(props);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("ERROR: " + ex.Message); }

            switch (actionType)
            {
                case ActionType.Inserted:
                    Inserted?.Invoke(this, portName);
                    break;
                case ActionType.Removed:
                    Removed?.Invoke(this, portName);
                    break;
            }
        };

        watcher.Start();
    }

    private static string FindPortName(PropertyDataCollection props)
    {
        string result = "";

        foreach (var property in props)
        {
            // System.Diagnostics.Debug.WriteLine("   " + property.Name + " = " + property.Value);
            if (property.Name == "DeviceID")
            {
                result = (string)property.Value;
            }
        }

        return result;
    }
}