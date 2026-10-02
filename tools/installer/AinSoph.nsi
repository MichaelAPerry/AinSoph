; Ain Soph — Windows installer (NSIS 3)
;
; Built by tools/package-windows.sh from the Steam Windows build
; (build/steam/windows: AinSoph.exe, data_AinSoph_windows_x86_64/, models/).
; Installs per user — no administrator prompt — to %LOCALAPPDATA%\Programs\Ain Soph.
; Saved worlds live in %APPDATA%\AinSoph and are kept on uninstall.

Target amd64-unicode   ; the game is x86-64 only, so the installer is too
!include "MUI2.nsh"

!ifndef VERSION
  !define VERSION "0.2.0"
!endif
!ifndef SRC
  !define SRC "../../build/steam/windows"
!endif
!ifndef OUT
  !define OUT "../../build/AinSoph-Setup-${VERSION}.exe"
!endif

!define APP        "Ain Soph"
!define PUBLISHER  "Michael Perry"
!define UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\AinSoph"

Name "${APP}"
OutFile "${OUT}"
InstallDir "$LOCALAPPDATA\Programs\${APP}"
InstallDirRegKey HKCU "Software\AinSoph" "InstallDir"
RequestExecutionLevel user
SetCompressor zlib
BrandingText "${APP} ${VERSION}"

VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName"     "${APP}"
VIAddVersionKey "CompanyName"     "${PUBLISHER}"
VIAddVersionKey "FileDescription" "${APP} installer"
VIAddVersionKey "FileVersion"     "${VERSION}"
VIAddVersionKey "LegalCopyright"  "Copyright (c) 2026 ${PUBLISHER}. MIT License."

!define MUI_ICON   "../../assets/icon.ico"
!define MUI_UNICON "../../assets/icon.ico"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TEXT "This will install ${APP} ${VERSION}.$\r$\n$\r$\nA persistent world whose people are voiced by an AI that runs entirely on your own computer. Nothing phones home.$\r$\n$\r$\nNeeds about 1.4 GB of disk and 8 GB of RAM. No graphics card required."
!define MUI_FINISHPAGE_RUN "$INSTDIR\AinSoph.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Launch ${APP}"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "../../LICENSE"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Section "Ain Soph" SecMain
  SectionIn RO
  SetOutPath "$INSTDIR"
  File "${SRC}/AinSoph.exe"
  File /r "${SRC}/data_AinSoph_windows_x86_64"

  ; The model barely compresses — store it as-is so packaging stays fast
  SetOutPath "$INSTDIR\models"
  SetCompress off
  File "${SRC}/models/qwen2.5-1.5b.gguf"
  SetCompress auto

  SetOutPath "$INSTDIR"
  File "/oname=icon.ico" "../../assets/icon.ico"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateDirectory "$SMPROGRAMS\${APP}"
  CreateShortcut "$SMPROGRAMS\${APP}\${APP}.lnk" "$INSTDIR\AinSoph.exe" "" "$INSTDIR\icon.ico"
  CreateShortcut "$SMPROGRAMS\${APP}\Uninstall ${APP}.lnk" "$INSTDIR\Uninstall.exe"
  CreateShortcut "$DESKTOP\${APP}.lnk" "$INSTDIR\AinSoph.exe" "" "$INSTDIR\icon.ico"

  WriteRegStr   HKCU "Software\AinSoph" "InstallDir" "$INSTDIR"
  WriteRegStr   HKCU "${UNINST_KEY}" "DisplayName"     "${APP}"
  WriteRegStr   HKCU "${UNINST_KEY}" "DisplayVersion"  "${VERSION}"
  WriteRegStr   HKCU "${UNINST_KEY}" "Publisher"       "${PUBLISHER}"
  WriteRegStr   HKCU "${UNINST_KEY}" "DisplayIcon"     "$INSTDIR\icon.ico"
  WriteRegStr   HKCU "${UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr   HKCU "${UNINST_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoRepair" 1
  WriteRegDWORD HKCU "${UNINST_KEY}" "EstimatedSize" 1400000
SectionEnd

Section "Uninstall"
  Delete "$DESKTOP\${APP}.lnk"
  RMDir /r "$SMPROGRAMS\${APP}"
  RMDir /r "$INSTDIR\data_AinSoph_windows_x86_64"
  RMDir /r "$INSTDIR\models"
  Delete "$INSTDIR\AinSoph.exe"
  Delete "$INSTDIR\icon.ico"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  DeleteRegKey HKCU "${UNINST_KEY}"
  DeleteRegKey HKCU "Software\AinSoph"
SectionEnd
