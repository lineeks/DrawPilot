Attribute VB_Name = "DrawPilotShell"
Option Explicit

' ============================================================
'  DrawPilot - CorelDRAW entry macros
'  Starts the .NET host that lives next to this GMS file.
'  Keep this module free of business logic.
' ============================================================

Private Const GmsProjectName As String = "DrawPilot"
Private Const HostRelativePath As String = "DrawPilot\app\DrawPilot.Host.exe"
Private Const DocumentInfoCommand As String = "doc-info"
Private Const BatchExportCommand As String = "batch-export"

Private Declare PtrSafe Function ShellExecuteW Lib "shell32.dll" ( _
    ByVal hWnd As LongPtr, _
    ByVal lpOperation As LongPtr, _
    ByVal lpFile As LongPtr, _
    ByVal lpParameters As LongPtr, _
    ByVal lpDirectory As LongPtr, _
    ByVal nShowCmd As Long) As LongPtr

Public Sub ShowMain()
    LaunchHost DocumentInfoCommand
End Sub

Public Sub ShowBatchExport()
    LaunchHost BatchExportCommand
End Sub

Private Sub LaunchHost(ByVal commandId As String)
    Dim hostPath As String
    Dim parameters As String
    Dim result As LongPtr

    hostPath = GetHostPath()
    If Len(hostPath) = 0 Then
        MsgBox "Could not resolve the DrawPilot GMS path.", _
               vbExclamation, "DrawPilot"
        Exit Sub
    End If

    parameters = "--command=" & commandId
    result = ShellExecuteW(0, StrPtr("open"), StrPtr(hostPath), StrPtr(parameters), 0, 1)
    If result <= 32 Then
        MsgBox "DrawPilot host could not be started:" & vbCrLf & hostPath & vbCrLf & vbCrLf & _
               "ShellExecuteW error " & CLng(result) & ".", _
               vbExclamation, "DrawPilot"
    End If
End Sub

Private Function GetHostPath() As String
    Dim project As Object
    Dim gmsPath As String
    Dim baseDir As String

    For Each project In Application.GMSManager.Projects
        If project.Name = GmsProjectName Then
            gmsPath = project.FullFileName
            Exit For
        End If
    Next project

    If Len(gmsPath) = 0 Then
        GetHostPath = ""
        Exit Function
    End If

    baseDir = Left$(gmsPath, InStrRev(gmsPath, "\"))
    GetHostPath = baseDir & HostRelativePath
End Function
