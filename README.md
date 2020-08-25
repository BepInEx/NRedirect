# NRedirect

NRedirect allows for custom code execution in a .NET framework executable process without needing to edit any files.

Concept is identical to [UnityDoorstop](https://github.com/NeighTools/UnityDoorstop), however this is for generic .NET framework applications.

## Features

- **Same-process execution**: Executes in the same process as the target application, and without requiring a separate launcher.
- **Clean application domain**: Injected code is run in a new AppDomain, and is not polluted by the target application's libraries
- **Patch-free**: Requires no editing of application files. Easy to update the target application, and survives verification checks

## Limitations

- If the generated proxy library becomes too different to the original targetted library, then the application can refuse to load.
- There must be a class library that the application references, which does not have a public key token.

## How to use

1. Drag the .NET framework .exe file onto `NRedirect.Generator.exe`, which will find a suitable proxy library, create a proxy for it and generate the required .exe.config file.
2. Place `NRedirect.dll` in the same directory as the .NET framework .exe.
3. Edit your generated `<application>.exe.config` file, and change the "Executable" config entry to point to the assembly you would like to launch.

You're done! You should be able to launch the application now, and your custom code will execute in a new appdomain.

### How it works:

NRedirect makes use of [binding redirects](https://docs.microsoft.com/en-us/dotnet/framework/configure-apps/file-schema/runtime/bindingredirect-element) in the .exe.config file to tell the .NET CLR to load a proxy class library instead of the original .dll file.

In the generated proxy assembly, it contains a call in the module initializer to NRedirect, which initializes a new AppDomain and executes your custom injected code.

The appdomain is configured to ignore the .exe.config file, otherwise a loop will occur. (Un)fortunately this doesn't require any calls to the unmanaged COM API of the framework to achieve, which is what we did research on for a few days.
