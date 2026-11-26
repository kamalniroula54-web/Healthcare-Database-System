# MK Hospital

MK Hospital is a small browser-based patient-record demo. It runs on your own
device and stores data in a separate local database for each user/device. It
does not connect to a shared online database.

Use fictional test data only.

## Run locally

1. Install the .NET 11 SDK.
2. Download or clone this repository.
3. Open a terminal in the project folder and run:

   ```powershell
   dotnet run
   ```

4. Open the local address printed in the terminal. On first run, create a local
   administrator account.

The app creates its SQLite database automatically in the current user's local
application-data folder (`%LOCALAPPDATA%\MKHospital\patients.db` on Windows).
The database is not stored in the repository. Each device has its own records.

## Data and credentials

- The first administrator password is hashed before it is saved.
- Login cookies are HTTP-only and secure in production.
- The database is local to the device; copying the source code does not copy
  patient records.
- To back up this demo's data, stop the app and copy `patients.db` from the
  application-data folder.
- Do not use real patient information or expose this demo directly to the
  internet.

## Build

```powershell
dotnet build "MK Hospital.csproj"
```
