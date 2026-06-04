; ============================================================
;  BIM FIRE HIDRO CALC 3.0 — Instalador NSIS
; ============================================================

Unicode True

!include "MUI2.nsh"
!include "LogicLib.nsh"

; ── Metadados ────────────────────────────────────────────────
Name              "BIM FIRE HIDRO CALC 3.0"
OutFile           "BimFireHidroCalc_Setup.exe"
InstallDir        "$LOCALAPPDATA\BimFireHidroCalc"
RequestExecutionLevel admin
ShowInstDetails   show
SetCompressor     lzma

; ── Aparência MUI ────────────────────────────────────────────
!define MUI_ABORTWARNING
!define MUI_ICON                       "bimfire.ico"
!define MUI_WELCOMEFINISHPAGE_BITMAP   "banner.bmp"
!define MUI_INSTFILESPAGE_PROGRESSBAR  "smooth"

; Textos da página de boas-vindas
!define MUI_WELCOMEPAGE_TITLE       "Instalação — BIM FIRE HIDRO CALC"
!define MUI_WELCOMEPAGE_TEXT        "Este assistente irá instalar o plugin BIM FIRE HIDRO CALC para Autodesk Revit 2027.$\r$\n$\r$\nSerão instalados automaticamente:$\r$\n$\r$\n   • .NET 10 SDK (se necessário)$\r$\n   • Microsoft Edge WebView2 Runtime (se necessário)$\r$\n   • Plugin BIM FIRE HIDRO CALC$\r$\n$\r$\nClique em Instalar para continuar."

; Texto da página de conclusão
!define MUI_FINISHPAGE_TITLE        "Instalação concluída!"
!define MUI_FINISHPAGE_TEXT         "O plugin BIM FIRE HIDRO CALC foi instalado com sucesso.$\r$\n$\r$\nAbra o Revit 2027. A aba BIM FIRE aparecerá automaticamente na Ribbon com os botões:$\r$\n   • Abrir HIDRO CALC$\r$\n   • Enviar Trecho"
!define MUI_FINISHPAGE_NOAUTOCLOSE

; Botão instalar
InstallButtonText "Instalar"

; ── Páginas ──────────────────────────────────────────────────
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_LANGUAGE "PortugueseBR"

; ── Seção principal ──────────────────────────────────────────
Section "Plugin BIM FIRE HIDRO CALC" SecMain

    SetOutPath "$INSTDIR"
    SetOverwrite on
    File /r "files\*.*"

    DetailPrint "-----------------------------------------------"
    DetailPrint " BIM FIRE HIDRO CALC — Iniciando instalacao..."
    DetailPrint "-----------------------------------------------"

    ; ── 1. WebView2 ─────────────────────────────────────────
    DetailPrint ""
    DetailPrint "[1/4] Verificando WebView2 Runtime..."

    ReadRegStr $0 HKLM "SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" "pv"
    ${If} $0 == ""
        ReadRegStr $0 HKCU "Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" "pv"
    ${EndIf}

    ${If} $0 == ""
        DetailPrint "      Baixando WebView2 Runtime..."
        NSISdl::download "https://go.microsoft.com/fwlink/p/?LinkId=2124703" "$TEMP\webview2setup.exe"
        Pop $0
        ${If} $0 != "success"
            MessageBox MB_ICONSTOP "Falha ao baixar WebView2.$\nVerifique sua conexao com a internet.$\nErro: $0"
            Abort
        ${EndIf}
        DetailPrint "      Instalando WebView2..."
        ExecWait '"$TEMP\webview2setup.exe" /silent /install'
        DetailPrint "      WebView2 instalado com sucesso."
    ${Else}
        DetailPrint "      WebView2 ja instalado. OK"
    ${EndIf}

    ; ── 2. .NET 10 SDK ──────────────────────────────────────
    DetailPrint ""
    DetailPrint "[2/4] Verificando .NET 10 SDK..."

    nsExec::ExecToStack '"$WINDIR\System32\cmd.exe" /C dotnet --list-sdks 2>nul'
    Pop $0
    Pop $1

    StrCpy $2 $1 3
    ${If} $2 != "10."
        DetailPrint "      Baixando .NET 10 SDK (~200 MB, aguarde)..."
        NSISdl::download "https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.100/dotnet-sdk-10.0.100-win-x64.exe" "$TEMP\dotnet-sdk-10-win-x64.exe"
        Pop $0
        ${If} $0 != "success"
            MessageBox MB_ICONSTOP "Falha ao baixar .NET 10 SDK.$\nVerifique sua conexao com a internet.$\nErro: $0"
            Abort
        ${EndIf}
        DetailPrint "      Instalando .NET 10 SDK (pode demorar alguns minutos)..."
        ExecWait '"$TEMP\dotnet-sdk-10-win-x64.exe" /quiet /norestart'
        DetailPrint "      .NET 10 SDK instalado com sucesso."
    ${Else}
        DetailPrint "      .NET 10 SDK ja instalado. OK"
    ${EndIf}

    ; ── 3. Compilar ─────────────────────────────────────────
    DetailPrint ""
    DetailPrint "[3/4] Compilando o plugin..."

    IfFileExists "C:\Program Files\Autodesk\Revit 2027\RevitAPI.dll" tem_revit sem_revit

    tem_revit:
        DetailPrint "      Revit 2027 encontrado. Compilando..."
        nsExec::ExecToLog '"$WINDIR\System32\cmd.exe" /C dotnet build "$INSTDIR\src\BimFireHidroCalc.csproj" -c Release -o "$INSTDIR\build" --nologo -v quiet'
        Pop $0
        ${If} $0 != 0
            MessageBox MB_ICONSTOP "Falha na compilacao do plugin.$\nCertifique-se de que o Revit 2027 esta instalado corretamente."
            Abort
        ${EndIf}
        DetailPrint "      Plugin compilado com sucesso."
        Goto instalar_dll

    sem_revit:
        DetailPrint "      Revit 2027 nao encontrado neste computador."
        DetailPrint "      O plugin sera ativado ao instalar o Revit 2027."

    instalar_dll:
    ; ── 4. Copiar para pasta do Revit ───────────────────────
    DetailPrint ""
    DetailPrint "[4/4] Instalando na pasta do Revit 2027..."

    CreateDirectory "$APPDATA\Autodesk\Revit\Addins\2027"

    IfFileExists "$INSTDIR\build\BimFireHidroCalc.dll" 0 so_addin
        CopyFiles /SILENT "$INSTDIR\build\*.dll" "$APPDATA\Autodesk\Revit\Addins\2027\"
        DetailPrint "      DLLs copiadas com sucesso."

    so_addin:
    ; Criar arquivo .addin
    FileOpen $9 "$APPDATA\Autodesk\Revit\Addins\2027\BimFireHidroCalc.addin" w
    FileWrite $9 '<?xml version="1.0" encoding="utf-8"?>'
    FileWrite $9 "$\r$\n<RevitAddIns>"
    FileWrite $9 "$\r$\n  <AddIn Type=$\"Application$\">"
    FileWrite $9 "$\r$\n    <Name>BIM FIRE HIDRO CALC</Name>"
    FileWrite $9 "$\r$\n    <Assembly>$APPDATA\Autodesk\Revit\Addins\2027\BimFireHidroCalc.dll</Assembly>"
    FileWrite $9 "$\r$\n    <AddInId>A1B2C3D4-E5F6-7890-ABCD-EF1234567890</AddInId>"
    FileWrite $9 "$\r$\n    <FullClassName>BimFireHidroCalc.App</FullClassName>"
    FileWrite $9 "$\r$\n    <VendorId>BIMFIRE</VendorId>"
    FileWrite $9 "$\r$\n    <VendorDescription>BIM FIRE HIDRO CALC</VendorDescription>"
    FileWrite $9 "$\r$\n  </AddIn>"
    FileWrite $9 "$\r$\n</RevitAddIns>"
    FileClose $9
    DetailPrint "      Arquivo .addin criado."

    DetailPrint ""
    DetailPrint "-----------------------------------------------"
    DetailPrint " INSTALACAO CONCLUIDA! Abra o Revit 2027."
    DetailPrint "-----------------------------------------------"

SectionEnd
