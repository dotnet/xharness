function Install-Gdn {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [string]$Version
    )

    $installArgs = @("install", "Microsoft.Guardian.Cli", "-Source https://securitytools.pkgs.visualstudio.com/_packaging/Guardian/nuget/v3/index.json", "-OutputDirectory $Path", "-NonInteractive", "-NoCache")

    if($Version)
    {
        $installArgs += "-Version $Version"
    }

    Start-Process nuget -Verbose -ArgumentList $installArgs -NoNewWindow -Wait
    
    $gdnCliPath = Get-ChildItem -Filter guardian.cmd -Recurse -Path $Path
    return $gdnCliPath.FullName
}

function Initialize-Gdn {
    param(
        [Parameter(Mandatory)]
        [string]$GuardianCliLocation,
        [Parameter(Mandatory)]
        [string]$WorkingDirectory,
        [ValidateSet("Trace", "Verbose", "Standard", "Warning", "Error")]
        [string]$LoggerLevel = "Standard"
    )

    if (!(Test-Path $GuardianCliLocation)) {
        throw "GuardianCliLocation not found"
    }

    & $GuardianCliLocation init --working-directory $WorkingDirectory --logger-level $LoggerLevel
}

function New-GdnSemmleConfig {
    param(
        [Parameter(Mandatory)]
        [string]$GuardianCliLocation,
        
        [Parameter(Mandatory)]
        [string]$Language,

        [Parameter(Mandatory)]
        [string]$WorkingDirectory,
        
        [ValidateSet("Trace", "Verbose", "Standard", "Warning", "Error")]
        [string]$LoggerLevel = "Standard",
        
        [ValidateSet("SuiteSDLRequired", "SuiteSDLRecommended")]
        [string]$Suite = "SuiteSDLRequired",
        
        [string]$BuildCommand,
        
        [Parameter(Mandatory)]
        [string]$SourceCodeDirectory,

        [Parameter(Mandatory)]
        [string]$OutputPath,

        # Additional semmle configuration in splat form, "ParameterName < ParameterValue"
        [string[]]$AdditionalSemmleParameters,

        [switch]$Force
    )
    
    if (!(Test-Path $GuardianCliLocation)) {
        throw "GuardianCliLocation not found"
    }

    [string[]]$splatParameters = "SourceCodeDirectory < $SourceCodeDirectory", "Language < $Language"

    if ($BuildCommand) {
        $splatParameters = $splatParameters + "BuildCommands < $BuildCommand"
    }

    if ($Suite) {
        $splatParameters = $splatParameters + "Suite < `$($Suite)"
    }

    if ($AdditionalSemmleParameters) {
        $splatParameters = $splatParameters + $AdditionalSemmleParameters
    }

    # Surround each parameter name-value pair with quotes and separate with a comma
    $splatParametersString = '"' + $($splatParameters -Join '" "') + '"'

    if ($Force) {
        Start-Process -Verbose -NoNewWindow -Wait $GuardianCliLocation -ArgumentList "configure", "--noninteractive", "--working-directory $WorkingDirectory", "--tool semmle", "--output-path $OutputPath", "--logger-level $LoggerLevel", "--args $splatParametersString", "-Force"
    } else { 
        Start-Process -Verbose -NoNewWindow -Wait $GuardianCliLocation -ArgumentList "configure", "--noninteractive", "--working-directory $WorkingDirectory", "--tool semmle", "--output-path $OutputPath", "--logger-level $LoggerLevel", "--args $splatParametersString"
    }

    Get-Content $OutputPath
}

function Invoke-GdnSemmle {
    param(
        [Parameter(Mandatory)]
        [string]$GuardianCliLocation,
        [Parameter(Mandatory)]
        [string]$ConfigurationPath,
        [Parameter(Mandatory)]
        [string]$WorkingDirectory,
        [ValidateSet("Trace", "Verbose", "Standard", "Warning", "Error")]
        [string]$LoggerLevel = "Standard"
    )

    if (!(Test-Path $GuardianCliLocation)) {
        throw "GuardianCliLocation not found"
    }

    & $GuardianCliLocation run --not-break-on-detections --working-directory $WorkingDirectory --logger-level $LoggerLevel --config $ConfigurationPath
}

function Publish-GdnArtifacts {
    param(
        [Parameter(Mandatory)]
        [string]$GuardianCliLocation,
        [Parameter(Mandatory)]
        [string]$WorkingDirectory,
        [ValidateSet("Trace", "Verbose", "Standard", "Warning", "Error")]
        [string]$LoggerLevel = "Standard"
    )

    if (!(Test-Path $GuardianCliLocation)) {
        throw "GuardianCliLocation not found"
    }
    
    & $GuardianCliLocation publish-artifacts --working-directory $WorkingDirectory --logger-level $LoggerLevel
}

function Invoke-GdnBuildBreak {
    param (
        [Parameter(Mandatory)]
        [string]$GuardianCliLocation,
        [Parameter(Mandatory)]
        [string]$WorkingDirectory,
        [ValidateSet("Trace", "Verbose", "Standard", "Warning", "Error")]
        [string]$LoggerLevel = "Standard"
    )

    if (!(Test-Path $GuardianCliLocation)) {
        throw "GuardianCliLocation not found"
    }

    & $GuardianCliLocation break --working-directory $WorkingDirectory --logger-level $LoggerLevel
}

function Publish-GdnTsa {
    param (
        [Parameter(Mandatory)]
        [string]$GuardianCliLocation,
        [Parameter(Mandatory)]
        [string]$WorkingDirectory,
        [ValidateSet("Trace", "Verbose", "Standard", "Warning", "Error")]
        [string]$LoggerLevel = 'Standard',
        
        [Parameter(Mandatory)]
        [string]$TsaRepositoryName,
        [Parameter(Mandatory)]
        [string]$TsaCodebaseName,
        [Parameter(Mandatory)]
        [string]$TsaNotificationEmail,
        [Parameter(Mandatory)]
        [string]$TsaCodebaseAdmin,
        [Parameter(Mandatory)]
        # Must be known to TSA
        [string]$TsaInstanceUrl,
        [Parameter(Mandatory)]
        # Must be known to TSA
        [string]$TsaProjectName,
        [Parameter(Mandatory)]
        [string]$TsaBugAreaPath,
        [Parameter(Mandatory)]
        [string]$TsaIterationPath,
        [bool]$OnBoard = $true,
        [bool]$TsaPublish = $true
    )

    if (!$TsaPublish)
    {
        Write-Host "TsaPublish is 'false', skipping publish"
        return
    }

    if (!(Test-Path $GuardianCliLocation)) {
        throw "GuardianCliLocation not found"
    }

    & $guardianCliLocation tsa-publish --all-tools `
        --working-directory $workingDirectory `
        --logger-level $LoggerLevel `
        --repository-name "$TsaRepositoryName" `
        --codebase-name "$TsaCodebaseName" `
        --notification-alias "$TsaNotificationEmail" `
        --codebase-admin "$TsaCodebaseAdmin" `
        --instance-url "$TsaInstanceUrl" `
        --project-name "$TsaProjectName" `
        --area-path "$TsaBugAreaPath" `
        --iteration-path "$TsaIterationPath" `
        --onboard $OnBoard
}