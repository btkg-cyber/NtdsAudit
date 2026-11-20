NtdsAudit is an application to assist in auditing Active Directory databases.

Forked from https://github.com/dionach/ntdsaudit.git to continue updating and to enable cross-platform functionality.

It provides statistics relating to accounts and passwords, as shown in the following example. It can also dump password hashes for later cracking.

```
Account stats for: domain.local
  Disabled users _____________________________________________________   418 of  5164 (8%)
  Expired users ______________________________________________________    67 of  5164 (1%)
  Active users unused in 1 year ______________________________________   787 of  4679 (17%)
  Active users unused in 90 days _____________________________________  1240 of  4679 (27%)
  Active users which do not require a password _______________________   156 of  4679 (3%)
  Active users with non-expiring passwords ___________________________  3907 of  4679 (84%)
  Active users with password unchanged in 1 year _____________________  1006 of  4679 (22%)
  Active users with password unchanged in 90 days ____________________  1400 of  4679 (30%)
  Active users with Administrator rights _____________________________    63 of  4679 (1%)
  Active users with Domain Admin rights ______________________________    54 of  4679 (1%)
  Active users with Enterprise Admin rights __________________________     0 of  4679 (0%)

  Disabled computer accounts _________________________________________    86 of  1414 (6%)

Password stats for: domain.local
  Active users using LM hashing ______________________________________    40 of  4679 (1%)
  Active users with duplicate passwords ______________________________  2312 of  4679 (49%)
  Active users with password stored using reversible encryption ______  4666 of  4679 (100%)
```

This project requires the .NET SDK version 10.

### Usage

```
ntdsaudit <NTDS file> [options]

Arguments:
  <NTDS file>  The path of the NTDS.dit database to be audited. Required.

Options:
  -v, --version                 Show version information
  -h, --help                    Show help information
  -s, --system <path>           The path of the associated SYSTEM hive. Required when dumping password hashes.
  -p, --pwdump <path>           Write password hashes in pwdump format.
  -u, --users-csv <path>        Write user details to a CSV file.
  -c, --computers-csv <path>    Write computer details to a CSV file.
  --anonymise                   Anonymise pwdump and reversible-password output.
  --history-hashes              Include history hashes.
  --dump-reversible <path>      Write clear-text passwords when reversible encryption is enabled.
  --wordlist <path>             Use a wordlist for basic weak-password checks.
  --ou-filter-file <path>       Limit users and computers to OUs listed in the file.
  --base-date <date>            Set the date used for statistics. Defaults to the NTDS file timestamp.
```

For example, the following command displays statistics and writes password hashes and user details to `pwdump.txt` and `users.csv`:

```
ntdsaudit ntds.dit -s SYSTEM -p pwdump.txt -u users.csv
```

> [!WARNING]
> Using password dumping or reversible-password extraction decrypts sensitive credentials. Handle generated output securely and delete it when no longer required.

### Obtaining the required files

NtdsAudit requires the `ntds.dit` Active Directory database. Password hash dumping also requires the associated `SYSTEM` registry hive. These files are locked by a domain controller and may not be copied while the database is in active use. The recommended method is to create an install from media (IFM) snapshot with `ntdsutil`:

1. Open an administrator command prompt.
2. Run `ntdsutil`.
3. Enter `activate instance ntds`.
4. Enter `ifm`.
5. Enter `create full <Drive>:\<Folder>`.

The snapshot directory contains the required database and registry hive files.

## Linux requirements

Linux support uses a Python shim for reading the ESE-based `ntds.dit` database. Python 3 is therefore required when running NtdsAudit on Linux.

Install the [`dissect.esedb`](https://github.com/fox-it/dissect.esedb) parser before running the application:

```
python3 -m pip install dissect.esedb
```

Windows continues to use `Microsoft.Database.ManagedEsent`.

## Building

To download locally:

```
git clone https://github.com/btkg-cyber/ntdsaudit.git
```

Build the debug variant:

```
dotnet build
```

Build the release variant:

```
dotnet publish
```

## Credits

The Linux ESE database shim uses [`dissect.esedb`](https://pypi.org/project/dissect.esedb/), developed by the Dissect team at Fox-IT / NCC Group. `dissect.esedb` is released under the Apache License 2.0.

`dissect.esedb` has been superseded by [`dissect.database`](https://github.com/fox-it/dissect.database), which provides the same ESE functionality along with broader database support.
