# Partner Center credentials for Windows releases

This is the configuration contract for [Windows Store Publish](../../.github/workflows/windows-store-publish.yml). No credential values belong in this repository, screenshots, issue comments, or chat.

## 1. Prepare Partner Center

1. Reserve **Clipboard Next** under the Windows developer account. In **Product management > Product identity**, compare `Package/Identity/Name`, `Package/Identity/Publisher`, and `Package/Properties/PublisherDisplayName` with `windows\src\ClipboardNext.App\Package.appxmanifest`. These manifest values are **not** CI secrets.
2. Complete the [listing and first submission](microsoft-store-listing.md), including privacy, pricing, IARC, capabilities, and the `.msixupload`. Publish the first version manually and wait until it is live before using this repository's update workflow.
3. In Partner Center **Account settings > Tenants**, associate the Microsoft Entra tenant that will own the publishing application. Use an account with the necessary directory and Partner Center administration permissions.
4. In **Account settings > User management > Microsoft Entra applications** (also called **Users**), add or create a dedicated publishing application. Give it the **Manager** role. Merely registering an application in the Entra portal is not enough; it must also be associated with the Windows developer account in Partner Center.
5. Open that application's details and copy its **Tenant ID** and **Client ID**. Create a key with **Add new key**, or use Entra **App registrations > your application > Certificates & secrets > Client secrets > New client secret**. Copy the secret **Value** immediately, not its **Secret ID**. Record its expiry in your password manager and set a rotation reminder.

See Microsoft's [MSIX submission API prerequisites](https://learn.microsoft.com/windows/uwp/monetize/create-and-manage-submissions-using-windows-store-services#step-1-complete-prerequisites-for-using-the-microsoft-store-submission-api). The publishing identity is an Entra application, not your personal Microsoft-account password, GitHub token, or publisher certificate.

## 2. Create exactly one protected GitHub environment

In **jamesmontemagno/clipboard-next > Settings > Environments > New environment**, create:

```text
microsoft-store
```

Configure required reviewers and prevent self-review where available. Ensure deployment branch/tag rules permit your release tags (`v*-windows`) and the ref used for diagnostic runs. The diagnostics, Internal Testing, and production jobs all reference this **same** environment; do not create differently named testing/production environments unless you also change the workflow.

There are two publishing approvals: approve Internal Testing first, wait for flight availability, install and test it, and only then approve production. An API submission finishing does not mean certification has finished or that testers can already install it.

## 3. Add these four environment secrets

Open **Settings > Environments > microsoft-store > Environment secrets > Add environment secret**. Use these names exactly:

| Environment secret name | What to copy | Where it comes from | Common mistake |
| --- | --- | --- | --- |
| `PARTNER_CENTER_TENANT_ID` | Directory/tenant ID, a GUID | The publishing application's details in Partner Center; also Entra **App registrations > application > Overview > Directory (tenant) ID** | A tenant domain, subscription ID, or an ID from a different directory |
| `PARTNER_CENTER_SELLER_ID` | Numeric **Seller ID** for the Windows developer account | Partner Center **Account settings > Account details** (look in the Windows developer/publisher account details) | A GUID, publisher display name, Store product ID, or package identity |
| `PARTNER_CENTER_CLIENT_ID` | **Application (client) ID**, a GUID | The publishing application's details in Partner Center; also Entra **App registrations > application > Overview** | The application's Object ID or your app's Store ID |
| `PARTNER_CENTER_CLIENT_SECRET` | The new client secret's **Value** / Partner Center **Key** | Partner Center application's **Add new key**, or Entra **Certificates & secrets > Client secrets** | The Secret ID, an expired value, or a value from another application |

Paste raw values without quotes, spaces, or newlines. The IDs are identifiers rather than passwords, but this workflow intentionally consumes all four through `secrets.*`; keep them as **environment secrets**, not GitHub variables. Only the client secret's value authenticates the application; protect it accordingly.

Do not add a PFX, signing password, Azure subscription ID, redirect URI, or Graph API token. This Store-only pipeline builds an unsigned upload and Microsoft signs the distributed package. No interactive sign-in is used in CI.

## 4. Add these two repository variables

Open **Settings > Secrets and variables > Actions > Variables > New repository variable**. These are identifiers, not credentials:

| Repository variable name | Value and source | Required when |
| --- | --- | --- |
| `MICROSOFT_STORE_PRODUCT_ID` | The app's **Store ID** from Partner Center **Product management > Product identity**, also returned as the app ID by `msstore apps list`. This is the alphanumeric Store product ID, not `23875RefractoredLLC.ClipboardNext`, a package family name, or an Entra GUID. | Every publishing run; diagnostics alone do not validate this product ID. |
| `MICROSOFT_STORE_FLIGHT_GROUP_IDS` | Comma-separated **tester group IDs** from the app's package flight/tester-group setup in Partner Center, for example `groupId1,groupId2`. Use actual IDs, not group names, email addresses, or a flight ID. The [package-flight API](https://learn.microsoft.com/windows/uwp/monetize/create-a-flight) describes `groupIds`. | Only when CI must create a missing flight named exactly `Internal Testing`. An existing flight and its tester assignments are left unchanged. |

If the portal does not expose a group's ID directly, retrieve it through the Store submission API's flight data for a flight using that group, or create **Internal Testing** and assign its testers in Partner Center first. The workflow discovers the flight ID automatically; there is no `MICROSOFT_STORE_FLIGHT_ID` secret or variable.

Keep these at repository scope as the workflow messages specify. Environment-scoped variables with the same names override repository values, so remove stale duplicates.

## 5. Validate without publishing

1. Open **Actions > Windows Store Publish > Run workflow** on the branch containing the workflow.
2. Check **diagnose_only**. Leave **publish** and **replace_flight_draft** unchecked. No release tag is needed for this mode.
3. Approve access to `microsoft-store` when requested.
4. Confirm diagnostics report both a successful Entra token request and a successful Store submission API request. This mode does **not** build, change the listing, or submit a package. It also does not validate the product ID, tester groups, or package identity.
5. Confirm the product ID belongs to Clipboard Next and the first Store release is live. For a build-only manual run, specify an existing `v1.2.3-windows` tag and leave **publish** unchecked. A **tag push** always requests the publishing path, subject to environment approvals; it is not a build-only action.

When ready, a `v*-windows` tag or a manual run with **publish** checked packages and tests the upload, then submits it to Internal Testing and production after their separate approvals. The same verified `.msixupload` and symbols are used for both. Do not approve production until the flight is available and tested.

## Troubleshooting and rotation

| Symptom | Check |
| --- | --- |
| Missing configuration | Exact spelling and scope: four secrets under `microsoft-store`, product ID under repository Variables; diagnostic approval and deployment-ref rules. |
| `AADSTS700016` | Client ID belongs to the configured tenant; use Application ID, not Object ID. |
| `AADSTS90002` | Tenant ID is the correct directory GUID. |
| `AADSTS7000215` / `AADSTS7000222` | Use the secret **Value**, not Secret ID; check expiry. Create a replacement and update `PARTNER_CENTER_CLIENT_SECRET`. |
| Token succeeds, Store API returns 401/403 | Associate the same Entra application with the correct Windows developer account in Partner Center and assign Manager. |
| Product cannot be read | Store product ID, developer-account association, and manual first submission/live status. |
| Missing Internal Testing flight | Create it and assign testers manually, or supply valid `MICROSOFT_STORE_FLIGHT_GROUP_IDS`. |
| Portal-created flight draft blocks upload | Resolve it in Partner Center. Only use **replace_flight_draft** after explicitly deciding to discard that unsubmitted flight draft; it preserves the flight and tester groups. |

For rotation: create a new key, update the existing environment secret, run diagnostics successfully, then revoke the old key. Never print the secret or access token to verify it. No Partner Center credentials have been created or configured merely by adding this documentation.