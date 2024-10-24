# Integrity check of 3rd party dependencies

## NuGet dependencies

All NuGet dependencies are checked and verified by Component Governance tool as part of the official build in the 1ES production pipeline.

## Android platform tools

Android platform tools are distributed as part of the XHarness.CLI NuGet package.
The tools and required dependencies are downloaded from the official Google repository (as documented in: https://developer.android.com/studio/releases/platform-tools) and embedded in the XHarness.CLI NuGet package for each supported host operating system.

- NOTE: Example URL used for downloading specific platform tools version for Windows: https://dl.google.com/android/repository/platform-tools_r30.0.5-windows.zip

This dependency is listed in the SBOM manifest generated during the official build of XHarness.CLI NuGet package.

## Apple iOS simulator runtimes

XHarness enables users to download and install a specific iOS simulator runtime (available through `xharness apple simulators install <ios-simulator-64_version>` command).
Invoking the said command will:

- Fetch information about the available simulator runtimes from Apple repository
- Download the desired simulator runtime image
- Install/Mount the desired simulator runtime image

Regarding integrity check all the above operations are using:

- If Xcode version < 16:
  - Official Apple sources: https://devimages-cdn.apple.com/downloads/xcode/simulators/index2.dvtdownloadableindex to acquire simulator image information like image build number, file size, download URL and similar
  - Official download URLs acquired from the previous step to download runtime images (example: https://download.developer.apple.com/Developer_Tools/iOS_17.5_Simulator_Runtime/iOS_17.5_Simulator_Runtime.dmg)
    - Download request uses ADC cookies
    - Downloaded image payload is compared against the specified file size
  - Official Apple tooling which performs package verification prior to installation/mounting:
    - `xcrun simctl runtime add <path>` performs the following by default (from the official documentation `xcrun simctl runtime --help`):

    ```bash
    Add a runtime disk image to the secure storage area. The image will be staged, verified, and mounted.
    ```

    - `hdiutil attach <image>` performs the following by default (from the official documentation https://ss64.com/mac/hdiutil.html):

    ```bash
    By default, hdiutil attach attempts to intelligently verify images that contain checksums before attaching them.
    ```

- If Xcode version >= 16.0
  - Official Apple tooling to download/install/mount simulator runtime images through `xcode -downloadPlatform iOS` (source: https://developer.apple.com/documentation/xcode/installing-additional-simulator-runtimes)
