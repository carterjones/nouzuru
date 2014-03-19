# nouzuru
A library for simple development of memory analysis software on Windows.

## Scanner
The `Scanner` class can be used to search for specific values within the contents of the target process' memory. This can be repeated multiple times to find the address at which the target value is located within memory.

## Patcher
The `Patcher` class can be used to write arbitrary data to a specific address within the target process' memory. Additionally, an address' value can be "frozen". This means that every few milliseconds, the desired value is re-written in the target process' memory. At any point, the original value can be restored to the address in memory, since each Patcher instance maintains a list of values that it has modified and/or frozen.

## Debugger
The `Debugger` class can be used to create a pure C# implementation of a debugger. This includes setting/unsetting breakpoints (both soft and hard). A pre-made debugg loop is also available, which can be used to perform specific actions when standard Windows debug events are caught.

## DLL Injector
The `DllInjector` class can be used to inject DLLs into the target process.

## WinApi
The `WinApi` class is a static class that can be used in nouzoru or in any other C# project to provide marshalled access to standard Windows API calls.

## Getting Started

    git clone https://github.com/carterjones/nouzuru.git

Next, get the most recent version of the required
[distorm3 DLLs](https://code.google.com/p/distorm/downloads/detail?name=distorm3-3-dlls.zip). Place the platform appropriate DLL in the various working directories of nouzuru. Alternatively, a simple hack is to just place the DLL in a system directory.

## Documentation

Documentation can be found at the [wiki](https://github.com/carterjones/nouzuru/wiki).

## Greetz
fiend4u, pengo13, brainiac2k, darkbyte, chameleon, macsawd, thepope, nomuus, grimreaper6464
