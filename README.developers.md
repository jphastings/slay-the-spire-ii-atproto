# Developer guide

## Releasing

Releases are triggered automatically when the `<Version>` in `mod/atproto-tracker.csproj` is updated on `main`. The workflow can also be triggered manually via `workflow_dispatch`.

The release workflow builds the mod and cross-platform installers, signs and notarizes the macOS builds, then publishes everything as a GitHub release.

## GitHub Actions configuration

### Secrets

| Name | Description |
|------|-------------|
| `APPLE_CERTIFICATE_P12` | Base64-encoded `.p12` export of your Developer ID Application certificate (see below) |
| `APPLE_CERTIFICATE_PASSWORD` | Password used when exporting the `.p12` |
| `APP_STORE_CONNECT_API_KEY` | Contents of the `.p8` App Store Connect API key file |

### Variables

| Name | Description |
|------|-------------|
| `APP_STORE_CONNECT_KEY_ID` | Key ID shown in App Store Connect when you created the API key |
| `APP_STORE_CONNECT_ISSUER_ID` | Issuer ID from App Store Connect > Users and Access > Integrations > App Store Connect API |

### Generating the signing certificate

You need a **Developer ID Application** certificate from your Apple Developer account. This is the certificate type used for distributing apps outside the Mac App Store.

1. Check if you already have one:

   ```bash
   security find-identity -v -p codesigning | grep "Developer ID Application"
   ```

2. **If you already have it**, export it from Keychain Access:
   - Open Keychain Access
   - Search for "Developer ID Application"
   - Find the certificate with a disclosure triangle that reveals the private key underneath -- you need both
   - Right-click the certificate > Export Items...
   - Choose **Personal Information Exchange (.p12)** format
   - Set a password (this becomes `APPLE_CERTIFICATE_PASSWORD`)

3. **If you don't have one**, create it at [developer.apple.com](https://developer.apple.com/account/resources/certificates/list):
   - Certificates > **+** > Developer ID Application
   - Follow the prompts to generate a Certificate Signing Request (CSR) from Keychain Access
   - Upload the CSR, download the certificate, double-click to install
   - Then export as described above

4. Base64 encode the `.p12` for the GitHub secret:

   ```bash
   base64 -i certificate.p12 | pbcopy
   ```

   Paste the clipboard contents as the `APPLE_CERTIFICATE_P12` secret.
