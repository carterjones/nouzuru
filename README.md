# nouzuru
A library for simple development of memory analysis software on Windows.

### Scanner
The `Scanner` class can be used to search for specific values within the contents of the target process' memory. This can be repeated multiple times to find the address at which the target value is located within memory.

#### Example Usage
bla bla bla

### Patcher
The `Patcher` class can be used to write arbitrary data to a specific address within the target process' memory. Additionally, an address' value can be "frozen". This means that every few milliseconds, the desired value is re-written in the target process' memory. At any point, the original value can be restored to the address in memory, since each Patcher instance maintains a list of values that it has modified and/or frozen.

#### Example Usage
bla bla bla

### Debugger
The `Debugger` class can be used to create a pure C# implementation of a debugger. This includes setting/unsetting breakpoints (both soft and hard). A pre-made debugg loop is also available, which can be used to perform specific actions when standard Windows debug events are caught.

#### Example Usage
bla bla bla

### DLL Injector
The `DllInjector` class can be used to inject DLLs into the target process.

#### Example Usage
bla bla bla

### WinApi
The `WinApi` class is a static class that can be used in nouzoru or in any other C# project to provide marshalled access to standard Windows API calls.

#### Example Usage
bla bla bla

## Getting Started

#### 1. Get the source

    git clone https://gitlab.com/carterjones/nouzuru.git

#### 2. Set up distorm3

Get the most recent version of
[distorm](http://code.google.com/p/distorm/downloads). Compile the project as a DLL that is targeted at the architecture of interest (32-bit or 64-bit) and place the resulting DLL in the various working directories of nouzuru. Alternatively, a simple hack is to just place the DLL in a system directory.

#### 3. Run nouzuru

Open Nouzuru.sln in Visual Studio 2010, set the startup project to the GUI, and run it in either debug mode or release mode.

## Documentation

Documentation can be found at http://section-9.github.com/nouzuru/doc/html/annotated.html

## Greetz
fiend4u, pengo13, brainiac2k, darkbyte, chameleon, macsawd, thepope, nomuus, grimreaper6464
