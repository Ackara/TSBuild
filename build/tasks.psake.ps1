# SYNOPSIS: This is a psake task file.
Join-Path $PSScriptRoot "toolkit.psm1" | Import-Module -Force;
FormatTaskName "$(Out-StringHeader)`r`n  {0}`r`n$(Out-StringHeader)";

Properties {
	$Dependencies = @("Ncrement");

	# Files & Folders
	$SolutionFolder = (Split-Path $PSScriptRoot -Parent);
	$SolutionName =   (Split-Path $SolutionFolder -Leaf);
	$ArtifactsFolder = (Join-Path $SolutionFolder "artifacts");
	$ManifestFilePath = (Join-Path $PSScriptRoot  "manifest.json");
	$SecretsFilePath = "";
	$ToolsFolder = "";
	$MSBuildExe = "";

	# Arguments
    $ShouldCommitChanges = $true;
	$Interactive = $true;
	$CurrentBranch = "";
	$Configuration = "";
	$Filter = $null;
	$DryRun = $false;
	$Major = $false;
	$Minor = $false;
	$Force = $false;
}

Task "Default" -depends @("configure", "compile", "test", "pack");

Task "Publish" -depends @("clean", "compile", "test", "pack", "push-nuget", "push-vsix", "tag") `
-description "This task compiles, test then publish all packages to their respective destination.";

# ======================================================================

Task "Configure Local Environment" -alias "configure" -description "This task generates all files required for development." `
-depends @("restore") -action {
	# Generating the build manifest file.
	if (-not (Test-Path $ManifestFilePath)) { New-NcrementManifest | ConvertTo-Json | Out-File $ManifestFilePath -Encoding utf8; }
	Write-Host "  * added 'build/$(Split-Path $ManifestFilePath -Leaf)' to the solution.";

	# Generating a secrets file template to store sensitive information.
	if (-not (Test-Path $SecretsFilePath))
	{
		$content = "{ `"nugetKey`": null }";
		$content | Out-File $SecretsFilePath -Encoding utf8;
	}
	Write-Host "  * added '$(Split-Path $SecretsFilePath -Leaf)' to the solution.";
}

Task "Package Solution" -alias "pack" -description "This task generates all deployment packages." `
-depends @("restore") -action {
	if (Test-Path $ArtifactsFolder) { Remove-Item $ArtifactsFolder -Recurse -Force; }
	New-Item $ArtifactsFolder -ItemType Directory | Out-Null;

	# Copy .vsix package to artifacts folder.
	Join-Path $SolutionFolder "src/*.VSIX/bin/$Configuration/*.vsix" | Get-Item | Copy-Item -Destination $ArtifactsFolder;

	# Generate MSBuild tools (FDD) binaries.
	$proj = Join-Path $SolutionFolder "src\$SolutionName\*.*proj" | Get-Item;
	Write-Header "dotnet: publish '$($proj.Name)'";
	Exec { &dotnet publish $proj.FullName --configuration $Configuration; }

	# Generate nuget package.
	Write-Header "dotnet: pack '$($proj.Name)'";
	Exec { &dotnet pack $proj.FullName --output $ArtifactsFolder --configuration $Configuration; }

	# Expand .nupkg for debugging
	$nupkg = Join-Path $ArtifactsFolder "*.nupkg" | Get-Item;
	$zip = [IO.Path]::ChangeExtension($nupkg.FullName, ".zip");
	Copy-Item $nupkg.FullName -Destination $zip -Force;
	Expand-Archive $zip -DestinationPath "$ArtifactsFolder\debug";

	# Clean-up
	Remove-Item $zip -Force;
	Join-Path $proj.DirectoryName "bin/$Configuration/*/publish" | Resolve-Path | Remove-Item -Recurse -Force;
}

#region ----- COMPILATION ----------------------------------------------

Task "Clean" -description "This task removes all generated files and folders from the solution." `
-action {
	Join-Path $SolutionFolder "*.sln" | Get-Item | Remove-GeneratedProjectItem -AdditionalItems @("artifacts");
	Get-ChildItem $SolutionFolder -Recurse -File -Filter "*.*proj" | Remove-GeneratedProjectItem;
}

Task "Import Build Dependencies" -alias "restore" -description "This task imports all build dependencies." `
-action {
	foreach ($moduleId in $Dependencies)
	{
		$modulePath = Join-Path $ToolsFolder "$moduleId/*/*.psd1";
		if (-not (Test-Path $modulePath)) { Save-Module $moduleId -Path $ToolsFolder; }
		Import-Module $modulePath -Force;
		Write-Host "  * imported the '$moduleId-$(Split-Path (Get-Item $modulePath).DirectoryName -Leaf)' powershell module.";
	}
}

Task "Increment Version Number" -alias "version" -description "This task increments all of the projects version number." `
-depends @("restore") -action {
	$manifest = $ManifestFilePath | Step-NcrementVersionNumber -Major:$Major -Minor:$Minor -Patch | Edit-NcrementManifest $ManifestFilePath -Stage:$ShouldCommitChanges;
	$newVersion = $ManifestFilePath | Select-NcrementVersionNumber $CurrentBranch;

	Join-Path $SolutionFolder "src/*/*.*" | Resolve-Path | Get-ChildItem | Where { ($_.Extension -ieq ".vsixmanifest") -or ($_.Extension -ieq ".csproj") } `
		| Update-NcrementProjectFile $ManifestFilePath -Commit:$ShouldCommitChanges | Split-Path -Leaf `
		| Out-StringFormat "  * incremented '{0}' version number to '$newVersion'." | Write-Host;
}

Task "Build Solution" -alias "compile" -description "This task compiles projects in the solution." `
-action { Get-Item "$SolutionFolder/*.sln" | Invoke-MSBuild $Configuration $MSBuildExe; }

Task "Run Tests" -alias "test" -description "This task invoke all tests within the 'tests' folder." `
-action { Join-Path $SolutionFolder "tests" | Get-ChildItem -Recurse -File -Filter "*MSTest.*proj" | Invoke-MSTest $Configuration; }

Task "Run Benchmarks" -alias "benchmark" -description "This task invoke all benchmark tests within the 'tests' folder." `
-action { $projectFile = Join-Path $SolutionFolder "tests/*.Benchmark/*.*proj" | Get-Item | Invoke-BenchmarkDotNet -Filter $Filter -DryRun:$DryRun; }

#endregion

#region ----- PUBLISHING -----------------------------------------------

Task "Publish NuGet Packages" -alias "push-nuget" -description "This task publish all nuget packages to nuget.org." `
-precondition { return ($Configuration -ieq "Release") -and (Test-Path $ArtifactsFolder -PathType Container) } `
-action { Get-ChildItem $ArtifactsFolder -Recurse -Filter "*.nupkg" | Publish-NugetPackage $SecretsFilePath "nugetKey"; }

Task "Publish VSIX Packages" -alias "push-vsix" -description "This task publish all vsix packages to the marketplace." `
-precondition { return ($Configuration -ieq "Release") -and (Test-Path $ArtifactsFolder -PathType Container) } `
-action { Get-ChildItem $ArtifactsFolder -Recurse -Filter "*.vsix" | Publish-VsixPackage $SecretsFilePath "vsixGalleryKey"; }

Task "Add-GitReleaseTag" -alias "tag" -description "This task tags the last commit with the version number." `
-precondition { return $CurrentBranch -eq "master"; } `
-depends @("restore") -action { $ManifestFilePath | Select-NcrementVersionNumber | New-Tag; }

#endregion