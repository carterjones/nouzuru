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

    git clone https://github.com/section-9/nouzuru.git --recursive

#### 2. Set up distorm3

Get the most recent version of
[distorm](http://code.google.com/p/distorm/downloads). Compile the project as a DLL that is targeted at the architecture of interest (32-bit or 64-bit) and place the resulting DLL in the various working directories of nouzuru. Alternatively, a simple hack is to just place the DLL in a system directory.

#### 3. Run nouzuru

Open Nouzuru.sln in Visual Studio 2010, set the startup project to the GUI, and run it in either debug mode or release mode.

#### 4. Make it better

If you would like to contribute back to this project, please fork this repository and submit a pull request for each improvement that you wish to provide.

Within your commit, you *must* include the following text in the proposed commit's message:

>  I dedicate any and all copyright interest in this software to the
>  public domain. I make this dedication for the benefit of the public at
>  large and to the detriment of my heirs and successors. I intend this
>  dedication to be an overt act of relinquishment in perpetuity of all
>  present and future rights to this software under copyright law.

For more information, please see the [unlicense.org explanation](http://unlicense.org/#unlicensing-contributions).

## Documentation

Documentation can be found at http://section-9.github.com/nouzuru/doc/html/annotated.html

## License

This is free and unencumbered software released into the public domain.

Anyone is free to copy, modify, publish, use, compile, sell, or
distribute this software, either in source code form or as a compiled
binary, for any purpose, commercial or non-commercial, and by any
means.

In jurisdictions that recognize copyright laws, the author or authors
of this software dedicate any and all copyright interest in the
software to the public domain. We make this dedication for the benefit
of the public at large and to the detriment of our heirs and
successors. We intend this dedication to be an overt act of
relinquishment in perpetuity of all present and future rights to this
software under copyright law.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.

For more information, please refer to <http://unlicense.org/>


## Greetz
fiend4u, pengo13, brainiac2k, darkbyte, chameleon, macsawd, thepope, nomuus, grimreaper6464
