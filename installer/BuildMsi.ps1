param([string]$Root = (Split-Path -Parent $PSScriptRoot), [string]$WorkDirectory)
$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath($Root)
if (-not $WorkDirectory) { $WorkDirectory = Join-Path $Root 'installer-work' }
$WorkDirectory = [IO.Path]::GetFullPath($WorkDirectory)
New-Item -ItemType Directory -Path $WorkDirectory -Force | Out-Null
$app = Join-Path $Root 'app\MediaBack.exe'
$readme = Join-Path $Root 'README.txt'
$license = Join-Path $Root 'LICENSE.txt'
$notices = Join-Path $Root 'THIRD-PARTY-NOTICES.txt'
$icon = Join-Path $Root 'source\Assets\MediaBack.ico'
foreach ($file in @($app,$readme,$license,$notices,$icon)) { if (-not (Test-Path -LiteralPath $file)) { throw "Missing package file: $file" } }
$ddf = @(
'.OPTION EXPLICIT', '.Set CabinetNameTemplate=payload.cab',
('.Set DiskDirectoryTemplate="' + $WorkDirectory + '"'),
'.Set CompressionType=LZX', '.Set CompressionMemory=21', '.Set Cabinet=on', '.Set Compress=on', '.Set MaxDiskSize=0',
('"' + $app + '" MainExe'), ('"' + $readme + '" Readme'),
('"' + $license + '" License'), ('"' + $notices + '" Notices')
)
$ddfPath = Join-Path $WorkDirectory 'payload.ddf'
$ddf | Set-Content -LiteralPath $ddfPath -Encoding Ascii
& "$env:SystemRoot\System32\makecab.exe" /F $ddfPath | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'CAB creation failed' }
$msiPath = Join-Path $Root 'MediaBack-Setup.msi'
if (Test-Path -LiteralPath $msiPath) { Remove-Item -LiteralPath $msiPath -Force }
$installer = New-Object -ComObject WindowsInstaller.Installer
$db = $installer.OpenDatabase($msiPath, 3)
function ComCall($Object, $Name, $Arguments) { $Object.GetType().InvokeMember($Name, [Reflection.BindingFlags]::InvokeMethod, $null, $Object, $Arguments) }
function ComSet($Object, $Name, $Index, $Value) { $null = $Object.GetType().InvokeMember($Name, [Reflection.BindingFlags]::SetProperty, $null, $Object, @($Index,$Value)) }
function Q([string]$Name) { ([string][char]96) + $Name + ([string][char]96) }
function Execute([string]$Sql) { $view = $db.OpenView($Sql); try { $view.Execute() } finally { $view.Close(); $null = [Runtime.InteropServices.Marshal]::ReleaseComObject($view) } }
$columns = @{}
function Table([string]$Name, [string[]]$Fields, [string[]]$Keys) {
  $defs = @(); $names = @()
  foreach ($field in $Fields) { $parts = $field.Split(' ',2); $names += $parts[0]; $defs += ((Q $parts[0]) + ' ' + $parts[1]) }
  Execute ('CREATE TABLE ' + (Q $Name) + ' (' + ($defs -join ', ') + ' PRIMARY KEY ' + (($Keys | ForEach-Object { Q $_ }) -join ', ') + ')')
  $columns[$Name] = $names
}
function Row([string]$Name, [object[]]$Values) {
  $names = $columns[$Name]
  if ($names.Count -ne $Values.Count) { throw "Column mismatch in $Name" }
  $record = $installer.CreateRecord($Values.Count)
  $view = $null
  try {
    for ($i=0; $i -lt $Values.Count; $i++) {
      if ($null -eq $Values[$i]) { continue }
      if ($Values[$i] -is [int]) { ComSet $record 'IntegerData' ($i+1) $Values[$i] }
      else { ComSet $record 'StringData' ($i+1) ([string]$Values[$i]) }
    }
    $sql = 'INSERT INTO ' + (Q $Name) + ' (' + (($names | ForEach-Object { Q $_ }) -join ', ') + ') VALUES (' + ((@('?') * $Values.Count) -join ', ') + ')'
    $view = $db.OpenView($sql); $view.Execute($record)
  } finally { if ($view) { $view.Close(); $null = [Runtime.InteropServices.Marshal]::ReleaseComObject($view) }; $null = [Runtime.InteropServices.Marshal]::ReleaseComObject($record) }
}
function Binary([string]$Name, [string]$Key, [string]$Path) {
  $record = $installer.CreateRecord(2); $view = $null
  try {
    ComSet $record 'StringData' 1 $Key
    $null = ComCall $record 'SetStream' @(2,$Path)
    $dataColumn = if ($Name -eq 'Icon') { 'Data' } else { 'Data' }
    $view = $db.OpenView('INSERT INTO ' + (Q $Name) + ' (' + (Q 'Name') + ', ' + (Q $dataColumn) + ') VALUES (?, ?)')
    $view.Execute($record)
  } finally { if ($view) { $view.Close(); $null = [Runtime.InteropServices.Marshal]::ReleaseComObject($view) }; $null = [Runtime.InteropServices.Marshal]::ReleaseComObject($record) }
}
try {
  $codepagePath = Join-Path $WorkDirectory 'codepage.idt'
  ([string][char]13+[char]10+[char]13+[char]10+'1251'+[char]9+'_ForceCodepage'+[char]13+[char]10) | Set-Content -LiteralPath $codepagePath -Encoding Ascii -NoNewline
  $db.Import($WorkDirectory,'codepage.idt')
  Table Property @('Property CHAR(72) NOT NULL','Value CHAR(0) NOT NULL LOCALIZABLE') @('Property')
  Table Directory @('Directory CHAR(72) NOT NULL','Directory_Parent CHAR(72)','DefaultDir CHAR(255) NOT NULL LOCALIZABLE') @('Directory')
  Table Component @('Component CHAR(72) NOT NULL','ComponentId CHAR(38)','Directory_ CHAR(72) NOT NULL','Attributes SHORT NOT NULL','Condition CHAR(255)','KeyPath CHAR(72)') @('Component')
  Table Feature @('Feature CHAR(38) NOT NULL','Feature_Parent CHAR(38)','Title CHAR(64) LOCALIZABLE','Description CHAR(255) LOCALIZABLE','Display SHORT','Level SHORT NOT NULL','Directory_ CHAR(72)','Attributes SHORT NOT NULL') @('Feature')
  Table FeatureComponents @('Feature_ CHAR(38) NOT NULL','Component_ CHAR(72) NOT NULL') @('Feature_','Component_')
  Table File @('File CHAR(72) NOT NULL','Component_ CHAR(72) NOT NULL','FileName CHAR(255) NOT NULL LOCALIZABLE','FileSize LONG NOT NULL','Version CHAR(72)','Language CHAR(20)','Attributes SHORT','Sequence SHORT NOT NULL') @('File')
  Table Media @('DiskId SHORT NOT NULL','LastSequence SHORT NOT NULL','DiskPrompt CHAR(64) LOCALIZABLE','Cabinet CHAR(255)','VolumeLabel CHAR(32)','Source CHAR(72)') @('DiskId')
  Table Registry @('Registry CHAR(72) NOT NULL','Root SHORT NOT NULL','Key CHAR(255) NOT NULL LOCALIZABLE','Name CHAR(255) LOCALIZABLE','Value CHAR(0) LOCALIZABLE','Component_ CHAR(72) NOT NULL') @('Registry')
  Table Shortcut @('Shortcut CHAR(72) NOT NULL','Directory_ CHAR(72) NOT NULL','Name CHAR(128) NOT NULL LOCALIZABLE','Component_ CHAR(72) NOT NULL','Target CHAR(255) NOT NULL','Arguments CHAR(255)','Description CHAR(255) LOCALIZABLE','Hotkey SHORT','Icon_ CHAR(72)','IconIndex SHORT','ShowCmd SHORT','WkDir CHAR(72)') @('Shortcut')
  Table Icon @('Name CHAR(72) NOT NULL','Data OBJECT NOT NULL') @('Name')
  Table Upgrade @('UpgradeCode CHAR(38) NOT NULL','VersionMin CHAR(20)','VersionMax CHAR(20)','Language CHAR(255)','Attributes LONG NOT NULL','Remove CHAR(255)','ActionProperty CHAR(72) NOT NULL') @('UpgradeCode','VersionMin','VersionMax','Language','Attributes')
  Table LaunchCondition @('Condition CHAR(255) NOT NULL','Description CHAR(255) NOT NULL LOCALIZABLE') @('Condition')
  Table CustomAction @('Action CHAR(72) NOT NULL','Type SHORT NOT NULL','Source CHAR(72)','Target CHAR(0)') @('Action')
  Table InstallExecuteSequence @('Action CHAR(72) NOT NULL','Condition CHAR(255)','Sequence SHORT') @('Action')
  Table InstallUISequence @('Action CHAR(72) NOT NULL','Condition CHAR(255)','Sequence SHORT') @('Action')
  Table RemoveFile @('FileKey CHAR(72) NOT NULL','Component_ CHAR(72) NOT NULL','FileName CHAR(255) LOCALIZABLE','DirProperty CHAR(72) NOT NULL','InstallMode SHORT NOT NULL') @('FileKey')
  Table AppSearch @('Property CHAR(72) NOT NULL','Signature_ CHAR(72) NOT NULL') @('Property','Signature_')
  Table RegLocator @('Signature_ CHAR(72) NOT NULL','Root SHORT NOT NULL','Key CHAR(255) NOT NULL','Name CHAR(255)','Type SHORT') @('Signature_')
  Table Signature @('Signature CHAR(72) NOT NULL','FileName CHAR(255) NOT NULL','MinVersion CHAR(20)','MaxVersion CHAR(20)','MinSize LONG','MaxSize LONG','MinDate LONG','MaxDate LONG','Languages CHAR(255)') @('Signature')
  $productCode = '{3378CC73-39DF-4D67-B586-A73A44AE20C5}'
  $upgradeCode = '{735086F8-3C52-47C0-9BE4-72889DD9A3F1}'
  $props = @{
    ProductCode=$productCode; UpgradeCode=$upgradeCode; ProductName='MediaBack'; ProductVersion='0.4.3'; ProductLanguage='1049'; Manufacturer='MediaBack';
    ARPPRODUCTICON='MediaBackIcon'; ARPNOMODIFY='1'; ALLUSERS='2'; MSIINSTALLPERUSER='1'; SecureCustomProperties='OLDPRODUCTS;NEWERVERSIONDETECTED'
  }
  foreach ($name in $props.Keys) { Row Property @($name,$props[$name]) }
  Row AppSearch @('INSTALLFOLDER','PreviousInstallFolder')
  Row RegLocator @('PreviousInstallFolder',1,'Software\MediaBack','InstallFolder',18)
  Row Directory @('TARGETDIR',$null,'SourceDir')
  Row Directory @('LocalAppDataFolder','TARGETDIR','.')
  Row Directory @('UserPrograms','LocalAppDataFolder','Programs')
  Row Directory @('INSTALLFOLDER','UserPrograms','MEDIA~1|MediaBack')
  Row Directory @('ProgramMenuFolder','TARGETDIR','.')
  Row Directory @('MenuFolder','ProgramMenuFolder','MEDIA~1|MediaBack')
  Row Directory @('SystemFolder','TARGETDIR','.')
  Row Component @('ApplicationFiles','{A5A08C91-4581-41B1-9C6C-73C8595FF88A}','INSTALLFOLDER',260,$null,'RegInstalled')
  Row Component @('AutoStart','{4C8F5766-8BD3-413A-BFE7-CE7A367E88D2}','INSTALLFOLDER',260,$null,'RegStartup')
  Row Component @('MenuShortcuts','{EAE80613-F0D4-4ED7-A17B-AF71C630398D}','MenuFolder',260,$null,'RegShortcuts')
  Row Feature @('MainFeature',$null,'MediaBack','Громкость и музыка',1,1,'INSTALLFOLDER',0)
  foreach ($component in @('ApplicationFiles','AutoStart','MenuShortcuts')) { Row FeatureComponents @('MainFeature',$component) }
  Row File @('MainExe','ApplicationFiles','MEDIAB~1.EXE|MediaBack.exe',[int](Get-Item -LiteralPath $app).Length,'0.4.3.0','0',16896,1)
  Row File @('Readme','ApplicationFiles','README.txt',[int](Get-Item -LiteralPath $readme).Length,$null,$null,16896,2)
  Row File @('License','ApplicationFiles','LICENSE.txt',[int](Get-Item -LiteralPath $license).Length,$null,$null,16896,3)
  Row File @('Notices','ApplicationFiles','THIRD~1.TXT|THIRD-PARTY-NOTICES.txt',[int](Get-Item -LiteralPath $notices).Length,$null,$null,16896,4)
  Row Media @(1,4,$null,'#payload.cab',$null,$null)
  Row Registry @('RegInstalled',1,'Software\MediaBack','Installed','#1','ApplicationFiles')
  Row Registry @('RegInstallFolder',1,'Software\MediaBack','InstallFolder','[INSTALLFOLDER]','ApplicationFiles')
  Row Registry @('RegStartup',1,'Software\Microsoft\Windows\CurrentVersion\Run','MediaBack','"[INSTALLFOLDER]MediaBack.exe" --background','AutoStart')
  Row Registry @('RegShortcuts',1,'Software\MediaBack','Shortcuts','#1','MenuShortcuts')
  Row Shortcut @('SettingsShortcut','MenuFolder','MEDIA~1|MediaBack','MenuShortcuts','[INSTALLFOLDER]MediaBack.exe','--settings','Настройки / Settings',$null,'MediaBackIcon',0,1,'INSTALLFOLDER')
  Row Shortcut @('UninstallShortcut','MenuFolder','UNINST~1|Удалить - Uninstall MediaBack','MenuShortcuts','[SystemFolder]msiexec.exe','/x [ProductCode]','Удалить / Uninstall MediaBack',$null,'MediaBackIcon',0,1,'INSTALLFOLDER')
  Row RemoveFile @('RemoveAppFolder','ApplicationFiles',$null,'INSTALLFOLDER',2)
  Row RemoveFile @('RemoveMenuFolder','MenuShortcuts',$null,'MenuFolder',2)
  Row Upgrade @($upgradeCode,$null,'0.4.3',$null,513,$null,'OLDPRODUCTS')
  Row Upgrade @($upgradeCode,'0.4.3',$null,$null,2,$null,'NEWERVERSIONDETECTED')
  Row LaunchCondition @('NOT NEWERVERSIONDETECTED','Уже установлена более новая версия MediaBack. / A newer version of MediaBack is already installed.')
  Row LaunchCondition @('VersionNT64','Для MediaBack требуется 64-разрядная Windows. / MediaBack requires 64-bit Windows.')
  Row CustomAction @('StopMediaBack',18,'MainExe','--quit')
  Row CustomAction @('OpenPreferences',210,'MainExe','--settings')
  $sequence = [ordered]@{
    FindRelatedProducts=25; AppSearch=50; LaunchConditions=100; ValidateProductID=700; CostInitialize=800; FileCost=900; CostFinalize=1000;
    MigrateFeatureStates=1200; InstallValidate=1400; InstallInitialize=1500; RemoveExistingProducts=1510; ProcessComponents=1600;
    UnpublishFeatures=1800; RemoveRegistryValues=2600; RemoveShortcuts=3200; StopMediaBack=3400; RemoveFiles=3500; InstallFiles=4000;
    CreateShortcuts=4500; WriteRegistryValues=5000; RegisterUser=6000; RegisterProduct=6100; PublishFeatures=6300; PublishProduct=6400; InstallFinalize=6600; OpenPreferences=6700
  }
  foreach ($action in $sequence.Keys) {
    $condition = switch ($action) { 'StopMediaBack' { 'Installed AND REMOVE~="ALL"' } 'OpenPreferences' { 'NOT Installed AND UILevel >= 3' } 'RemoveExistingProducts' { 'OLDPRODUCTS' } default { $null } }
    Row InstallExecuteSequence @($action,$condition,[int]$sequence[$action])
  }
  foreach ($action in @('FindRelatedProducts','AppSearch','LaunchConditions','ValidateProductID','CostInitialize','FileCost','CostFinalize')) { Row InstallUISequence @($action,$null,[int]$sequence[$action]) }
  Row InstallUISequence @('ExecuteAction',$null,1300)
  Binary Icon 'MediaBackIcon' $icon
  Binary _Streams 'payload.cab' (Join-Path $WorkDirectory 'payload.cab')
  $summary = $db.SummaryInformation(20)
  try {
    foreach ($entry in @(@(1,1251),@(2,'Installation Database'),@(3,'MediaBack'),@(4,'MediaBack'),@(5,'Installer'),@(7,'x64;1049'),@(9,('{'+[Guid]::NewGuid().ToString().ToUpperInvariant()+'}')),@(14,500),@(15,10),@(18,'MediaBack native MSI builder'))) { ComSet $summary 'Property' $entry[0] $entry[1] }
    $summary.Persist()
  } finally { $null = [Runtime.InteropServices.Marshal]::ReleaseComObject($summary) }
  $db.Commit()
} finally {
  $null = [Runtime.InteropServices.Marshal]::ReleaseComObject($db)
  $null = [Runtime.InteropServices.Marshal]::ReleaseComObject($installer)
}
Write-Output $msiPath
