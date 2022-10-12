﻿Imports System.Text

Public Class OEMIDForm
    Private ReadOnly _DiskImage As DiskImage.Disk
    Private _Result As Boolean = False

    Public Sub New(DiskImage As DiskImage.Disk, OEMIDDictionary As Dictionary(Of UInteger, OEMIDList))

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.
        _DiskImage = DiskImage
        Dim BootstrapChecksum = Crc32.ComputeChecksum(DiskImage.BootSector.BootStrapCode)
        Dim OEMIDString As String = Encoding.UTF8.GetString(DiskImage.BootSector.OEMID)

        txtCurrentOEMID.Text = OEMIDString

        If OEMIDDictionary.ContainsKey(BootstrapChecksum) Then
            Dim OEMIDList As List(Of String) = OEMIDDictionary.Item(BootstrapChecksum).OEMIDList
            For Each OEMID In OEMIDList
                Dim Index = CboOEMID.Items.Add(OEMID)
                If OEMID = OEMIDString Then
                    CboOEMID.SelectedIndex = Index
                End If
            Next
        End If
        If CboOEMID.SelectedIndex = -1 Then
            If OEMIDString <> "" Then
                CboOEMID.Items.Add(OEMIDString)
            End If
            If CboOEMID.Items.Count > 0 Then
                CboOEMID.SelectedIndex = 0
            End If
        End If
    End Sub

    Public ReadOnly Property Result As Boolean
        Get
            Return _Result
        End Get
    End Property

    Private Sub BtnUpdate_Click(sender As Object, e As EventArgs) Handles BtnUpdate.Click
        Dim OEMIDString As String = Encoding.UTF8.GetString(_DiskImage.BootSector.OEMID)
        Dim NewOEMID As String = Strings.Left(CboOEMID.Text, 8).PadRight(8)

        If OEMIDString <> NewOEMID Then
            _DiskImage.BootSector.OEMID = Encoding.UTF8.GetBytes(NewOEMID)
            _Result = True
        End If

        Me.Close()
    End Sub
End Class