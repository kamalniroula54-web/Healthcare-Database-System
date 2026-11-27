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
Set `MK_HOSPITAL_DATA_DIRECTORY` to use a different local folder when needed.
This demo can model multiple hospitals in that one local database, but it does
not synchronize hospitals across devices or provide a shared production service.

## Data and credentials

- The first administrator password is hashed before it is saved.
- Administrators can create employee accounts. Employees receive a one-time
  password and must complete their profile and choose a personal password on
  first sign-in. Profiles include name, date of birth, email, phone, and
  emergency-contact name, phone, and relationship. Employees also select a
  clinical position from grouped options; that job title does not change their
  application permissions. Phone fields let users select a country and store
  validated numbers in international format.
- Global administrators can switch between hospitals and manage system-wide
  settings, hospital administrators, employees, and records. They can create
  hospital administrators, assign each account to one hospital, and separately
  grant or revoke that account's authority to approve cross-hospital patient
  access requests. Hospital administrators cannot switch hospitals.
- Hospital administrators can manage employee accounts and patient demographics
  only at their assigned hospital. They cannot reassign employees or manage
  hospitals or global administrator accounts. A delegated hospital administrator
  can approve or reject requests only for patients owned by their assigned
  hospital; approval gives the requesting hospital access to the patient's full
  visit history, while edits remain limited to the hospital that owns each visit.
- Global administrators can create additional global administrator accounts.
  New administrators complete their profile and set a personal password on
  first sign-in; one administrator cannot directly reset their own account.
- Global and hospital administrators can edit and reset employee accounts
  within their scope. Only global administrators can assign employees to a
  different hospital.
  Reset passwords are one-time passwords; employees must choose a new password
  when they next sign in. Employees can update their own profiles.
- Global administrators can add hospitals and switch the active hospital from
  the Hospitals menu. Existing records are assigned to the initial `MK Hospital`
  during the automatic database upgrade. New employee accounts are assigned
  to   the administrator's active hospital. Global administrators can reassign an
  employee from the employee profile page; doing so invalidates the employee's
  existing session. Employees cannot switch hospitals, and their signed-in
  hospital is checked against their account assignment.
- Administrators can edit each hospital's license number, address, contact
  phone, contact email, and logo. The active hospital's logo appears beside
  the Everest Health system name, and its profile details appear in the footer.
  Logo uploads are limited to PNG/JPEG files up to 2 MB and 4096 by 4096 pixels.
- Employees may request access to a patient at another hospital by submitting
  the hospital, a patient name or record number, and a reason. A global
  administrator or a hospital administrator with delegated authority identifies
  the patient and approves or rejects the request. Hospital administrators can
  only review requests for patients owned by their assigned hospital. Approval grants
  access to the requesting employee's hospital: all employees there can see
  visits recorded at every hospital, but can change only visits owned by their
  own hospital. Source patient details cannot be changed through cross-hospital
  access. Global administrators can revoke any hospital grant; delegated
  hospital administrators can revoke grants for their assigned hospital. Each decision and
  revocation is retained in the database.
- Employees and administrators can request a password reset by email or phone.
  Email requests send a one-time password immediately to the profile email;
  email requests are throttled to one attempt per account every ten minutes.
  Phone requests are reviewed by an administrator, who verifies identity before
  sharing a code manually. If an account holder cannot access their profile
  email, they can contact an administrator through another trusted channel.
  Another administrator can recover an administrator account; the sole
  administrator must contact the local system owner.
- Automatic email delivery uses Microsoft Graph with delegated `Mail.Send`
  permission. Register a public-client app that supports personal Microsoft
  accounts, enable public-client flows, and add delegated `Mail.Send`. Set the
  application (client) ID in the `Outlook__ClientId` environment variable,
  restart the app, then connect `passwordresethospital@outlook.com` from
  **Admin > Outlook email**. The app verifies the connected sender mailbox.
  No mailbox password or client secret is stored in the repository;
  MSAL persists its token cache in the local app-data folder using platform
  protection. Outlook must be connected before automatic email recovery works.
- Employees can create full patient records, but can only view patient name,
  ID, admission date, and discharge date in the patient list afterwards. All
  signed-in staff can add, view, edit, and delete patient medical-history
  visits in their assigned hospital. Each visit uses structured chief-complaint, OLDCARTS, past medical
  and surgical history, medications, allergies, family and social history,
  immunizations, and review-of-systems sections. Multiple test results can be
  added to a visit; each includes date, name, result, optional unit, reference
  range, and notes. Up to 50 tests can be saved with a visit, and the visit
  and its test changes are saved together. Existing test results remain
  available in the legacy unlinked-results section. Entries record who created
  and last updated them.
  Employees can update admission/discharge dates; each actual change is logged
  and shown to administrators as an in-app notification. Global administrators
  can edit any patient record by switching hospitals; hospital administrators
  can edit demographics or delete patients only at their assigned hospital.
- Patient records include optional emergency contact name and phone fields.
  Patient phone fields support country selection and international formatting.
- Patients can create a read-only portal account with a one-time registration
  code generated by staff from their patient record. Staff must deliver the
  code through a verified channel; the app does not send it. The code expires
  after seven days and is used once for signup. Each patient record has a shared
  patient ID. Staff must use the same ID for that person's records at different
  hospitals; administrators can update existing records to use that ID. Existing
  records receive distinct IDs during upgrade and are not automatically matched.
  After signup, the portal shows all visits and tests for records with the same
  shared ID, with hospital and employee attribution; no additional patient code
  or access request is needed for those records. Patient accounts cannot edit
  clinical records. Records from multiple hospitals are available only when
  those hospitals use the same local database; this demo does not synchronize
  records between devices or installations.
- Existing local databases are upgraded automatically when the app starts.
- Login cookies are HTTP-only and secure in production. Local HTTP development
  uses a non-prefixed cookie name so browsers accept the cookie without HTTPS.
- The database is local to the device; copying the source code does not copy
  patient records.
- To back up this demo's data, stop the app and copy `patients.db` from the
  application-data folder.
- Do not use real patient information or expose this demo directly to the
  internet.

## Developer and disclaimer

Developed by [kamalniroula54-web](https://github.com/kamalniroula54-web).

Copyright © 2026 kamalniroula54-web. All rights reserved.

This software is provided "as is" for demonstration purposes only.
It is not intended for real patient information, clinical use, or medical advice.
Use fictional data only. The developer makes no warranties and is not
responsible for loss or damage resulting from use of this software.

## Build

```powershell
dotnet build "MK Hospital.csproj"
```
