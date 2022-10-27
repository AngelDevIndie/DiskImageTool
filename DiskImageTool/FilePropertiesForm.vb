﻿Imports System.ComponentModel
Imports System.Text

Public Class FilePropertiesForm
    Private Const CREATED_FORMAT As String = "yyyy-MM-dd  h:mm:ss tt"
    Private Const LASTACCESSED_FORMAT As String = "yyyy-MM-dd"
    Private Const EMPTY_FORMAT As String = "'Empty'"
    Private ReadOnly _Disk As DiskImage.Disk
    Private ReadOnly _Items As ICollection
    Private _IsDirectory As Boolean = False
    Private _IsVolumeLabel As Boolean = False
    Private _Deleted As Boolean = False
    Private _Updated As Boolean = False

    Public ReadOnly Property Updated As Boolean
        Get
            Return _Updated
        End Get
    End Property

    Public Sub New(Disk As DiskImage.Disk, Items As ICollection)

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.
        _Disk = Disk
        _Items = Items

        DTLastWritten.Value = New Date(1980, 1, 1, 0, 0, 0)
        SetCreatedDateValue(Nothing)
        SetLastAccessedDateValue(Nothing)

        InitMultiple(Items.Count > 1)
        If Items.Count = 1 Then
            PopulateFormSingle()
        Else
            PopulateFormMultiple()
        End If
    End Sub

    Private Sub InitButtons(Visible As Boolean)
        ToggleButton(BtnLastAccessed, Not Visible, Visible)
        ToggleButton(BtnCreated, Not Visible, Visible)
        ToggleButton(BtnLastWritten, Not Visible, Visible)
        ToggleButton(BtnArchive, Not Visible, Visible)
        ToggleButton(BtnReadOnly, Not Visible, Visible)
        ToggleButton(BtnSystem, Not Visible, Visible)
        ToggleButton(BtnHidden, Not Visible, Visible)
    End Sub

    Private Sub InitMultiple(Multiple As Boolean)
        TxtFile.Visible = Not Multiple
        TxtExtension.Visible = Not Multiple
        LblMultipleFiles.Visible = Multiple

        InitButtons(Multiple)
    End Sub

    Private Sub ApplyAttributesUpdate(DirectoryEntry As DiskImage.DirectoryEntry)
        Dim Attributes As Byte = DirectoryEntry.Attributes
        Dim BitArray = New BitArray(Attributes)

        If BtnArchive.Tag Then
            Attributes = ToggleBit(Attributes, DiskImage.DirectoryEntry.AttributeFlags.ArchiveFlag, ChkArchive.Checked)
        End If
        If BtnReadOnly.Tag Then
            Attributes = ToggleBit(Attributes, DiskImage.DirectoryEntry.AttributeFlags.ReadOnly, ChkReadOnly.Checked)
        End If
        If BtnHidden.Tag Then
            Attributes = ToggleBit(Attributes, DiskImage.DirectoryEntry.AttributeFlags.Hidden, ChkHidden.Checked)
        End If
        If BtnSystem.Tag Then
            Attributes = ToggleBit(Attributes, DiskImage.DirectoryEntry.AttributeFlags.System, ChkSystem.Checked)
        End If

        If Attributes <> DirectoryEntry.Attributes Then
            DirectoryEntry.Attributes = Attributes
            _Updated = True
        End If
    End Sub

    Private Sub ApplyFileDatesUpdate(DirectoryEntry As DiskImage.DirectoryEntry)
        Dim LastWritten = DTLastWritten.Value
        Dim Created As Date? = GetDateFromPicker(DTCreated)
        Dim LastAccessed As Date? = GetDateFromPicker(DTLastAccessed)
        Dim DT As DiskImage.ExpandedDate

        If BtnLastWritten.Tag Then
            DT = DirectoryEntry.GetLastWriteDate
            If Not DT.IsValidDate Or Date.Compare(DT.DateObject, LastWritten) <> 0 Then
                DirectoryEntry.SetLastWriteDate(LastWritten)
                _Updated = True
            End If
        End If

        If BtnCreated.Tag Then
            If DirectoryEntry.HasCreationDate Then
                DT = DirectoryEntry.GetCreationDate
                If Created Is Nothing Then
                    DirectoryEntry.ClearCreationDate()
                    _Updated = True
                ElseIf Not DT.IsValidDate Or Date.Compare(DT.DateObject, Created) <> 0 Then
                    DirectoryEntry.SetCreationDate(Created)
                    _Updated = True
                End If
            ElseIf Created IsNot Nothing Then
                DirectoryEntry.SetCreationDate(Created)
                _Updated = True
            End If
        End If

        If BtnLastAccessed.Tag Then
            If DirectoryEntry.HasLastAccessDate Then
                DT = DirectoryEntry.GetLastAccessDate
                If LastAccessed Is Nothing Then
                    DirectoryEntry.ClearLastAccessDate()
                    _Updated = True
                ElseIf Not DT.IsValidDate Or Date.Compare(DT.DateObject, LastAccessed) <> 0 Then
                    DirectoryEntry.SetLastAccessDate(LastAccessed)
                    _Updated = True
                End If
            ElseIf LastAccessed IsNot Nothing Then
                DirectoryEntry.SetLastAccessDate(LastAccessed)
                _Updated = True
            End If
        End If
    End Sub

    Private Sub ApplyFileNameUpdate(DirectoryEntry As DiskImage.DirectoryEntry)
        Dim FileName() As Byte
        Dim Extension() As Byte

        If _IsVolumeLabel Then
            Dim VolumeLabel() As Byte
            ReDim FileName(7)
            ReDim Extension(2)
            VolumeLabel = Encoding.UTF8.GetBytes(TxtFile.Text)
            DiskImage.Disk.ResizeArray(VolumeLabel, 11, 32)
            Array.Copy(VolumeLabel, 0, FileName, 0, 8)
            Array.Copy(VolumeLabel, 8, Extension, 0, 3)
        Else
            FileName = Encoding.UTF8.GetBytes(TxtFile.Text)
            DiskImage.Disk.ResizeArray(FileName, 8, 32)
            Extension = Encoding.UTF8.GetBytes(TxtExtension.Text)
            DiskImage.Disk.ResizeArray(Extension, 3, 32)
        End If

        If Not FileName.SequenceEqual(DirectoryEntry.FileName) Then
            DirectoryEntry.FileName = FileName
            _Updated = True
        End If

        If Not Extension.SequenceEqual(DirectoryEntry.Extension) Then
            DirectoryEntry.Extension = Extension
            _Updated = True
        End If
    End Sub

    Private Sub ApplyUpdates()
        _Updated = False

        If _Items.Count = 1 Then
            Dim Item As ListViewItem = _Items(0)
            Dim FileData As FileData = Item.Tag
            'Dim DirectoryEntry = _Disk.GetDirectoryEntryByOffset(FileData.Offset)
            Dim DirectoryEntry = FileData.DirectoryEntry
            ApplyFileNameUpdate(DirectoryEntry)
        End If

        For Each Item As ListViewItem In _Items
            Dim FileData As FileData = Item.Tag
            'Dim DirectoryEntry = _Disk.GetDirectoryEntryByOffset(FileData.Offset)
            Dim DirectoryEntry = FileData.DirectoryEntry

            ApplyFileDatesUpdate(DirectoryEntry)
            ApplyAttributesUpdate(DirectoryEntry)
        Next
    End Sub

    Private Function GetDateFromPicker(DTP As DateTimePicker) As Date?
        If DTP.Checked Then
            Return DTP.Value
        Else
            Return Nothing
        End If
    End Function

    Private Sub PopulateFormSingle()
        Dim Item As ListViewItem = _Items(0)
        Dim FileData As FileData = Item.Tag
        'Dim DirectoryEntry = _Disk.GetDirectoryEntryByOffset(FileData.Offset)
        Dim DirectoryEntry = FileData.DirectoryEntry
        Dim DT As DiskImage.ExpandedDate

        _IsDirectory = DirectoryEntry.IsDirectory
        _IsVolumeLabel = DirectoryEntry.IsVolumeName
        _Deleted = DirectoryEntry.IsDeleted

        Dim Caption As String
        Dim Maxlength As Integer
        Dim FileName As String

        If _IsVolumeLabel Then
            Caption = "Volume Label"
            TxtFile.Width = 150
            Maxlength = 11
            TxtExtension.Visible = False
            FileName = DirectoryEntry.GetVolumeName
        Else
            TxtFile.Width = 100
            Maxlength = 8
            TxtExtension.Visible = True
            FileName = DirectoryEntry.GetFileName
            TxtExtension.Text = DirectoryEntry.GetFileExtension
            If _IsDirectory Then
                Caption = "Directory Name"
            Else
                Caption = "File Name"
            End If
        End If

        If _Deleted Then
            Caption &= " (Deleted)"
            Maxlength -= 1
            FileName = FileName.Substring(1)
        End If

        GroupFileName.Text = Caption
        TxtFile.MaxLength = Maxlength
        TxtFile.Text = FileName

        DT = DirectoryEntry.GetLastWriteDate
        If DT.IsValidDate Then
            DTLastWritten.Value = DT.DateObject
        Else
            DTLastWritten.Value = New Date(1980, 1, 1, 0, 0, 0)
        End If

        If DirectoryEntry.HasCreationDate Then
            DT = DirectoryEntry.GetCreationDate
            If DT.IsValidDate Then
                SetCreatedDateValue(DT.DateObject)
            End If
        End If

        If DirectoryEntry.HasLastAccessDate Then
            DT = DirectoryEntry.GetLastAccessDate
            If DT.IsValidDate Then
                SetLastAccessedDateValue(DT.DateObject)
            End If
        End If

        ChkArchive.Checked = DirectoryEntry.IsArchive
        ChkReadOnly.Checked = DirectoryEntry.IsReadOnly
        ChkHidden.Checked = DirectoryEntry.IsHidden
        ChkSystem.Checked = DirectoryEntry.IsSystem
    End Sub

    Private Sub PopulateFormMultiple()
        Dim DT As DiskImage.ExpandedDate
        Dim LastWritten = New Date(1980, 1, 1, 0, 0, 0)
        Dim Created As Date? = Nothing
        Dim LastAccessed As Date? = Nothing
        Dim SetArchived As Boolean = True
        Dim SetReadOnly As Boolean = True
        Dim SetSystem As Boolean = True
        Dim SetHidden As Boolean = True

        _IsDirectory = False
        _IsVolumeLabel = False
        _Deleted = False

        GroupFileName.Text = "Multiple Files"
        LblMultipleFiles.Text = "(" & _Items.Count & " Files Selected)"

        For Each Item As ListViewItem In _Items
            Dim FileData As FileData = Item.Tag
            'Dim DirectoryEntry = _Disk.GetDirectoryEntryByOffset(FileData.Offset)
            Dim DirectoryEntry = FileData.DirectoryEntry
            DT = DirectoryEntry.GetLastWriteDate
            If DT.IsValidDate Then
                If DT.DateObject > LastWritten Then
                    LastWritten = DT.DateObject
                End If
            End If
            DT = DirectoryEntry.GetCreationDate
            If DT.IsValidDate Then
                If Created Is Nothing Or DT.DateObject > Created Then
                    Created = DT.DateObject
                End If
            End If
            DT = DirectoryEntry.GetLastAccessDate
            If DT.IsValidDate Then
                If LastAccessed Is Nothing Or DT.DateObject > LastAccessed Then
                    LastAccessed = DT.DateObject
                End If
            End If

            If Not DirectoryEntry.IsArchive Then
                SetArchived = False
            End If
            If Not DirectoryEntry.IsReadOnly Then
                SetReadOnly = False
            End If
            If Not DirectoryEntry.IsHidden Then
                SetHidden = False
            End If
            If Not DirectoryEntry.IsSystem Then
                SetSystem = False
            End If
        Next

        DTLastWritten.Value = LastWritten
        SetCreatedDateValue(Created)
        SetLastAccessedDateValue(LastAccessed)

        ChkArchive.Checked = SetArchived
        ChkReadOnly.Checked = SetReadOnly
        ChkHidden.Checked = SetHidden
        ChkSystem.Checked = SetSystem
    End Sub

    Private Sub SetCreatedDateValue(Value? As Date)
        If Value Is Nothing Then
            DTCreated.CustomFormat = EMPTY_FORMAT
            DTCreated.Checked = False
        Else
            DTCreated.CustomFormat = CREATED_FORMAT
            DTCreated.Checked = True
            DTCreated.Value = Value
        End If
    End Sub

    Private Sub SetLastAccessedDateValue(Value? As Date)
        If Value Is Nothing Then
            DTLastAccessed.CustomFormat = EMPTY_FORMAT
            DTLastAccessed.Checked = False
        Else
            DTLastAccessed.CustomFormat = LASTACCESSED_FORMAT
            DTLastAccessed.Checked = True
            DTLastAccessed.Value = Value
        End If
    End Sub

    Private Function ToggleBit(Attributes As Byte, Flag As DiskImage.DirectoryEntry.AttributeFlags, Value As Boolean) As Byte
        If Value Then
            Return Attributes Or Flag
        Else
            Return Attributes And Not Flag
        End If
    End Function

    Private Sub ToggleButton(Button As Button, Enabled As Boolean, Optional Visible As Boolean = True)
        Button.Visible = Visible
        Button.Tag = Enabled
        Button.BackColor = IIf(Enabled, Color.LightGreen, SystemColors.Control)
        Button.UseVisualStyleBackColor = Not Enabled
        ToggleRelatedControls(Button)
    End Sub

    Private Sub ToggleRelatedControls(Button As Button)
        If Button Is BtnLastWritten Then
            DTLastWritten.Enabled = Button.Tag
            LblLastWritten.Enabled = Button.Tag
        ElseIf Button Is BtnLastAccessed Then
            DTLastAccessed.Enabled = Button.Tag
            LblLastAccessed.Enabled = Button.Tag
        ElseIf Button Is BtnCreated Then
            DTCreated.Enabled = Button.Tag
            LblCreated.Enabled = Button.Tag
        ElseIf Button Is BtnArchive Then
            ChkArchive.Enabled = Button.Tag
        ElseIf Button Is BtnReadOnly Then
            ChkReadOnly.Enabled = Button.Tag
        ElseIf Button Is BtnSystem Then
            ChkSystem.Enabled = Button.Tag
        ElseIf Button Is BtnHidden Then
            ChkHidden.Enabled = Button.Tag
        End If
    End Sub

    Private Sub BtnEnable_Click(sender As Object, e As EventArgs) Handles BtnLastWritten.Click, BtnCreated.Click, BtnLastAccessed.Click, BtnReadOnly.Click, BtnHidden.Click, BtnArchive.Click, BtnSystem.Click
        ToggleButton(sender, Not sender.tag)
    End Sub

    Private Sub DTCreated_ValueChanged(sender As Object, e As EventArgs) Handles DTCreated.ValueChanged
        If DTCreated.Checked Then
            DTCreated.CustomFormat = CREATED_FORMAT
        Else
            DTCreated.CustomFormat = EMPTY_FORMAT
        End If
    End Sub

    Private Sub DTLastAccessed_ValueChanged(sender As Object, e As EventArgs) Handles DTLastAccessed.ValueChanged
        If DTLastAccessed.Checked Then
            DTLastAccessed.CustomFormat = LASTACCESSED_FORMAT
        Else
            DTLastAccessed.CustomFormat = EMPTY_FORMAT
        End If
    End Sub

    Private Sub TxtExtension_Validating(sender As Object, e As CancelEventArgs) Handles TxtExtension.Validating
        If Not DiskImage.Disk.CheckValidFileName(Encoding.UTF8.GetBytes(TxtExtension.Text), True, _IsVolumeLabel) Then
            e.Cancel = True
        End If
    End Sub

    Private Sub TxtFile_KeyPress(sender As Object, e As KeyPressEventArgs) Handles TxtFile.KeyPress, TxtExtension.KeyPress
        Dim Value = Asc(e.KeyChar)

        If _IsVolumeLabel Then
            If Value < 32 And Value <> 8 Then
                e.KeyChar = Chr(0)
            End If
        Else
            If Value > 96 And Value < 123 Then
                Value -= 32
                e.KeyChar = Chr(Value)
            ElseIf Value > 255 Then
                e.KeyChar = Chr(0)
            ElseIf Value < 33 And Value <> 8 Then
                e.KeyChar = Chr(0)
            ElseIf DiskImage.Disk.InvalidFileChars.Contains(Value) Then
                e.KeyChar = Chr(0)
            End If
        End If
    End Sub

    Private Sub TxtFile_Validating(sender As Object, e As CancelEventArgs) Handles TxtFile.Validating
        If _Deleted And TxtFile.Text = "" Then
            Exit Sub
        End If

        If Not DiskImage.Disk.CheckValidFileName(Encoding.UTF8.GetBytes(TxtFile.Text), False, _IsVolumeLabel) Then
            e.Cancel = True
        End If
    End Sub

    Private Sub BtnUpdate_Click(sender As Object, e As EventArgs) Handles BtnUpdate.Click
        ApplyUpdates
    End Sub
End Class