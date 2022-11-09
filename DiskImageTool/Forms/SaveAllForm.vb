﻿Public Enum MyMsgBoxResult
    Ok = 1
    Cancel = 2
    Abort = 3
    Retry = 4
    Ignore = 5
    Yes = 6
    No = 7
    YesToAll = 4
    NoToAll = 5
End Enum

Public Class SaveAllForm
    Private _Result As MyMsgBoxResult = MyMsgBoxResult.Cancel

    Public Sub New(Caption As String)

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.
        LblCaption.Text = Caption
    End Sub

    Public Property Result As MyMsgBoxResult
        Get
            Return _Result
        End Get
        Set
            _Result = Value
        End Set
    End Property

#Region "Events"

    Private Sub Button_Click(sender As Object, e As EventArgs) Handles BtnYes.Click, BtnNo.Click, BtnCancel.Click, BtnYesToAll.Click, BtnNoToall.Click
        If sender Is BtnYes Then
            _Result = MyMsgBoxResult.Yes
        ElseIf sender Is BtnNo Then
            _Result = MyMsgBoxResult.No
        ElseIf sender Is BtnCancel Then
            _Result = MyMsgBoxResult.Cancel
        ElseIf sender Is BtnYesToAll Then
            _Result = MyMsgBoxResult.YesToAll
        ElseIf sender Is BtnNoToall Then
            _Result = MyMsgBoxResult.NoToAll
        End If

        Me.Close()
    End Sub

    Private Sub SaveAllForm_Paint(sender As Object, e As PaintEventArgs) Handles Me.Paint
        e.Graphics.DrawImage(SystemIcons.Question.ToBitmap, 22, 24)
    End Sub

#End Region

End Class