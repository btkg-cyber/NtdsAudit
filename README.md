Forked from https://github.com/dionach/ntdsaudit.git to continue updating, and to enable cross-platform functionality.

This project requires the .NET SDK version 10.

## Linux requirements

Linux support uses a Python shim for reading the ESE-based `ntds.dit` database. Python 3 is therefore required when running NTDSAudit on Linux.

Install the [`dissect.esedb`](https://github.com/fox-it/dissect.esedb) parser before running the application:

```
python3 -m pip install dissect.esedb
```

Windows continues to use `Microsoft.Database.ManagedEsent`.

To download locally:

```
git clone https://github.com/btkg-cyber/ntdsaudit.git
```

To build debug variant:
```
dotnet build
```

To build release variant:
```
dotnet publish
```

## Credits

The Linux ESE database shim is built with [`dissect.esedb`](https://pypi.org/project/dissect.esedb/), developed by the Dissect team at Fox-IT / NCC Group. `dissect.esedb` is released under the Apache License 2.0.

`dissect.esedb` has been superseded by [`dissect.database`](https://github.com/fox-it/dissect.database), which provides the same ESE functionality along with broader database support.

Testing against `dissect.database` is underway.
