Attribute VB_Name = "CorelDRAWToolShell"
Option Explicit

' ============================================================
'  CorelDRAWTool - CorelDRAW entry macros
'  Starts the .NET host that lives next to this GMS file.
'  Keep this module free of business logic.
' ============================================================

Private Const GmsProjectName As String = "CorelDRAWTool"
Private Const HostRelativePath As String = "CorelDRAWTool\app\CorelDRAWTool.Host.exe"
Private Const DocumentInfoCommand As String = "doc-info"
Private Const BatchExportCommand As String = "batch-export"

Private Declare PtrSafe Function ShellExecuteW Lib "shell32.dll" ( _
    ByVal hWnd As LongPtr, _
    ByVal lpOperation As LongPtr, _
    ByVal lpFile As LongPtr, _
    ByVal lpParameters As LongPtr, _
    ByVal lpDirectory As LongPtr, _
    ByVal nShowCmd As Long) As LongPtr

Public Sub ShowCorelDRAWTool()
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
        MsgBox "Could not resolve the CorelDRAWTool GMS path.", _
               vbExclamation, "CorelDRAWTool"
        Exit Sub
    End If

    parameters = "--command=" & commandId
    result = ShellExecuteW(0, StrPtr("open"), StrPtr(hostPath), StrPtr(parameters), 0, 1)
    If result <= 32 Then
        MsgBox "CorelDRAWTool host could not be started:" & vbCrLf & hostPath & vbCrLf & vbCrLf & _
               "ShellExecuteW error " & CLng(result) & ".", _
               vbExclamation, "CorelDRAWTool"
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
