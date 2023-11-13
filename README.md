# Automatic Olfactory Display (AutOlD) for 2 channels

Dedicated for running tests associated with scented air production and odorant type recognition by participants.

## Development environment and features

- Microsoft Visual Studio Community 2022
- .NET 6.0 (LTS)
- C# 9.0
- Zeta Resource Editor
- Tested only as x86-32 application.

## Dependencies

NuGet packages:
- System.IO.Ports
- System.Management
- System.Text.Json
- WPFLocalizeExtension
- ScottPlot.WPF
- RestSharp

External dependencies:
- `..\..\smop\smop.ion-vision\bin\Debug\net6.0-windows\Smop.IonVision.dll`

    1. step one level up from the autold_2ch root folder
    2. `git clone https://github.com/smellodi/smop.git`
    3. build the `smop.ion-vision` project
