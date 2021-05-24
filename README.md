# Olfactory test platform

Dedicated for running tests associated with odorant type/level recognition.
Implements the following tests:
1. [Odor Threshold Test](https://www.google.com/url?sa=t&rct=j&q=&esrc=s&source=web&cd=&cad=rja&uact=8&ved=2ahUKEwiD9LbT3eHwAhX_hf0HHQRkBgoQFjAAegQIAhAD&url=https%3A%2F%2Fpubmed.ncbi.nlm.nih.gov%2F9056084%2F&usg=AOvVaw1iUPjwuMuh9dTPXtWpaVxh)

## Development environment and features

- Microsoft Visual Studio Community 2019, Version 16.9.4 (May 2021)
- .NET Core 3.1 (LTS)
- C# 9.0
- Tested only as x86-32 application.

## Dependencies

NuGet packages:
- System.IO.Ports
- System.Management

## Using

Select COM ports for MFC and PID, click both "Open" buttons.
Press F5 any time to check the list of events and data measured from PID.
Optionally, set the flag "Monitor" on the MFC log panel to observe MFC status.