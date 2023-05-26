﻿Imports System.ComponentModel
Imports System.IO
Imports System.IO.Compression
Imports System.Net
Imports System.Text
Imports DiskImageTool.DiskImage
Imports BootSectorOffsets = DiskImageTool.DiskImage.BootSector.BootSectorOffsets

Public Enum ItemScanTypes
    Disk = 1
    OEMName = 2
    DiskType = 4
    UnusedClusters = 8
    Directory = 16
    All = 31
End Enum

Public Structure FileData
    Dim DirectoryEntry As DiskImage.DirectoryEntry
    Dim FilePath As String
    Dim Index As Integer
    Dim IsLastEntry As Boolean
    Dim LFNFileName As String
    Dim ParentOffset As Integer
End Structure

Public Class MainForm
    Private WithEvents ContextMenuCopy1 As ContextMenuStrip
    Private WithEvents ContextMenuCopy2 As ContextMenuStrip
    Private WithEvents Debounce As Timer
    Public Const NULL_CHAR As Char = "�"
    Public Const SITE_URL = "https://github.com/Digitoxin1/DiskImageTool"
    Public Const UPDATE_URL = "https://api.github.com/repos/Digitoxin1/DiskImageTool/releases/latest"
    Private ReadOnly _ArchiveFilterExt As New List(Of String) From {".zip"}
    Private ReadOnly _FileFilterExt As New List(Of String) From {".ima", ".img"}
    Private ReadOnly _lvwColumnSorter As ListViewColumnSorter
    Private _BootStrap As Bootstrap
    Private _CheckAll As Boolean = False
    Private _Disk As DiskImage.Disk
    Private _FileFilter As String = ""
    Private _FileVersion As String = ""
    Private _FilterCounts() As Integer
    Private _FilterDiskType As ComboFilter
    Private _FilterOEMName As ComboFilter
    Private _FiltersApplied As Boolean = False
    Private _LoadedFileNames As Dictionary(Of String, LoadedImageData)
    Private _ScanRun As Boolean = False
    Private _SuppressEvent As Boolean = False
    Public Sub New()
        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.
        _lvwColumnSorter = New ListViewColumnSorter
    End Sub

    Friend Function DiskImageLoad(ImageData As LoadedImageData) As DiskImage.Disk
        Dim Data() As Byte

        Try
            If ImageData.Compressed Then
                ImageData.ReadOnly = True
                Data = OpenFileFromZIP(ImageData.SourceFile, ImageData.CompressedFile)
            Else
                ImageData.ReadOnly = IsFileReadOnly(ImageData.SourceFile)
                Data = IO.File.ReadAllBytes(ImageData.SourceFile)
            End If
        Catch ex As Exception
            Return Nothing
        End Try

        Return New DiskImage.Disk(Data, ImageData.Modifications)
    End Function

    Friend Sub DiskTypeFilterUpdate(Disk As DiskImage.Disk, ImageData As LoadedImageData, Optional UpdateFilters As Boolean = False, Optional Remove As Boolean = False)
        Dim DiskType As String = ""

        If Not Remove Then
            If Disk.IsValidImage Then
                DiskType = GetFloppyDiskTypeName(Disk.BootSector)
            Else
                DiskType = GetFloppyDiskTypeName(Disk.Data.Length)
            End If
        End If

        If Not ImageData.Scanned Or DiskType <> ImageData.ScanInfo.DiskType Then
            If ImageData.Scanned Then
                _FilterDiskType.Remove(ImageData.ScanInfo.DiskType, UpdateFilters)
            End If

            ImageData.ScanInfo.DiskType = DiskType

            If Not Remove Then
                _FilterDiskType.Add(ImageData.ScanInfo.DiskType, UpdateFilters)
            End If
        End If
    End Sub

    Friend Function ItemScanDirectory(Disk As DiskImage.Disk, ImageData As LoadedImageData, Optional UpdateFilters As Boolean = False, Optional Remove As Boolean = False) As DirectoryScanResponse
        Dim Response As DirectoryScanResponse

        If Not Remove And Disk.IsValidImage Then
            Response = ProcessDirectoryEntries(Disk.Directory, 0, "", True)
        Else
            Response = New DirectoryScanResponse(Nothing)
        End If

        If Not ImageData.Scanned Or Response.HasValidCreated <> ImageData.ScanInfo.HasValidCreated Then
            ImageData.ScanInfo.HasValidCreated = Response.HasValidCreated
            If Response.HasValidCreated Then
                _FilterCounts(FilterTypes.HasCreated) += 1
            ElseIf ImageData.Scanned Then
                _FilterCounts(FilterTypes.HasCreated) -= 1
            End If
            If UpdateFilters Then
                FilterUpdate(FilterTypes.HasCreated)
            End If
        End If

        If Not ImageData.Scanned Or Response.HasValidLastAccessed <> ImageData.ScanInfo.HasValidLastAccessed Then
            ImageData.ScanInfo.HasValidLastAccessed = Response.HasValidLastAccessed
            If Response.HasValidLastAccessed Then
                _FilterCounts(FilterTypes.HasLastAccessed) += 1
            ElseIf ImageData.Scanned Then
                _FilterCounts(FilterTypes.HasLastAccessed) -= 1
            End If
            If UpdateFilters Then
                FilterUpdate(FilterTypes.HasLastAccessed)
            End If
        End If

        If Not ImageData.Scanned Or Response.HasLFN <> ImageData.ScanInfo.HasLongFileNames Then
            ImageData.ScanInfo.HasLongFileNames = Response.HasLFN
            If Response.HasLFN Then
                _FilterCounts(FilterTypes.HasLongFileNames) += 1
            ElseIf ImageData.Scanned Then
                _FilterCounts(FilterTypes.HasLongFileNames) -= 1
            End If
            If UpdateFilters Then
                FilterUpdate(FilterTypes.HasLongFileNames)
            End If
        End If

        If Not ImageData.Scanned Or Response.HasAdditionalData <> ImageData.ScanInfo.DirectoryHasAdditionalData Then
            ImageData.ScanInfo.DirectoryHasAdditionalData = Response.HasAdditionalData
            If Response.HasAdditionalData Then
                _FilterCounts(FilterTypes.DirectoryHasAdditionalData) += 1
            ElseIf ImageData.Scanned Then
                _FilterCounts(FilterTypes.DirectoryHasAdditionalData) -= 1
            End If
            If UpdateFilters Then
                FilterUpdate(FilterTypes.DirectoryHasAdditionalData)
            End If
        End If

        If Not ImageData.Scanned Or Response.HasBootSector <> ImageData.ScanInfo.DirectoryHasBootSector Then
            ImageData.ScanInfo.DirectoryHasBootSector = Response.HasBootSector
            If Response.HasBootSector Then
                _FilterCounts(FilterTypes.DirectoryHasBootSector) += 1
            ElseIf ImageData.Scanned Then
                _FilterCounts(FilterTypes.DirectoryHasBootSector) -= 1
            End If
            If UpdateFilters Then
                FilterUpdate(FilterTypes.DirectoryHasBootSector)
            End If
        End If

        If Not ImageData.Scanned Or Response.HasInvalidDirectoryEntries <> ImageData.ScanInfo.HasInvalidDirectoryEntries Then
            ImageData.ScanInfo.HasInvalidDirectoryEntries = Response.HasInvalidDirectoryEntries
            If Response.HasInvalidDirectoryEntries Then
                _FilterCounts(FilterTypes.HasInvalidDirectoryEntries) += 1
            ElseIf ImageData.Scanned Then
                _FilterCounts(FilterTypes.HasInvalidDirectoryEntries) -= 1
            End If
            If UpdateFilters Then
                FilterUpdate(FilterTypes.HasInvalidDirectoryEntries)
            End If
        End If

        If Not ImageData.Scanned Or Response.HasFATChainingErrors <> ImageData.ScanInfo.HasFATChainingErrors Then
            ImageData.ScanInfo.HasFATChainingErrors = Response.HasFATChainingErrors
            If Response.HasFATChainingErrors Then
                _FilterCounts(FilterTypes.HasFATChainingErrors) += 1
            ElseIf ImageData.Scanned Then
                _FilterCounts(FilterTypes.HasFATChainingErrors) -= 1
            End If
            If UpdateFilters Then
                FilterUpdate(FilterTypes.HasFATChainingErrors)
            End If
        End If

        Return Response
    End Function

    Friend Sub ItemScanDisk(Disk As DiskImage.Disk, ImageData As LoadedImageData, Optional UpdateFilters As Boolean = False, Optional Remove As Boolean = False)
        Dim IsValidImage As Boolean = Disk.IsValidImage
        Dim HasBadSectors As Boolean = False
        Dim HasMismatchedFATs As Boolean = False
        Dim HasInvalidImageSize As Boolean = False
        Dim UnknownDiskType As Boolean = False

        If Not Remove And IsValidImage Then
            HasBadSectors = Disk.FAT.BadClusters.Count > 0
            HasMismatchedFATs = Not Disk.CompareFATTables
            HasInvalidImageSize = Disk.CheckImageSize <> 0
            UnknownDiskType = GetFloppyDiskType(Disk.BootSector) = FloppyDiskType.FloppyUnknown
        End If

        If Remove Then
            IsValidImage = True
        End If

        If Not ImageData.Scanned Or IsValidImage <> ImageData.ScanInfo.IsValidImage Then
            ImageData.ScanInfo.IsValidImage = IsValidImage
            If Not IsValidImage Then
                _FilterCounts(FilterTypes.HasInvalidImage) += 1
            ElseIf ImageData.Scanned Then
                _FilterCounts(FilterTypes.HasInvalidImage) -= 1
            End If
            If UpdateFilters Then
                FilterUpdate(FilterTypes.HasInvalidImage)
            End If
        End If

        If Not ImageData.Scanned Or HasBadSectors <> ImageData.ScanInfo.HasBadSectors Then
            ImageData.ScanInfo.HasBadSectors = HasBadSectors
            If HasBadSectors Then
                _FilterCounts(FilterTypes.HasBadSectors) += 1
            ElseIf ImageData.Scanned Then
                _FilterCounts(FilterTypes.HasBadSectors) -= 1
            End If
            If UpdateFilters Then
                FilterUpdate(FilterTypes.HasBadSectors)
            End If
        End If

        If Not ImageData.Scanned Or HasMismatchedFATs <> ImageData.ScanInfo.HasMismatchedFATs Then
            ImageData.ScanInfo.HasMismatchedFATs = HasMismatchedFATs
            If HasMismatchedFATs Then
                _FilterCounts(FilterTypes.HasMismatchedFATs) += 1
            ElseIf ImageData.Scanned Then
                _FilterCounts(FilterTypes.HasMismatchedFATs) -= 1
            End If
            If UpdateFilters Then
                FilterUpdate(FilterTypes.HasMismatchedFATs)
            End If
        End If

        If Not ImageData.Scanned Or HasInvalidImageSize <> ImageData.ScanInfo.HasInvalidImageSize Then
            ImageData.ScanInfo.HasInvalidImageSize = HasInvalidImageSize
            If HasInvalidImageSize Then
                _FilterCounts(FilterTypes.HasInvalidImageSize) += 1
            ElseIf ImageData.Scanned Then
                _FilterCounts(FilterTypes.HasInvalidImageSize) -= 1
            End If
            If UpdateFilters Then
                FilterUpdate(FilterTypes.HasInvalidImageSize)
            End If
        End If

        If Not ImageData.Scanned Or UnknownDiskType <> ImageData.ScanInfo.UnknownDiskType Then
            ImageData.ScanInfo.UnknownDiskType = UnknownDiskType
            If UnknownDiskType Then
                _FilterCounts(FilterTypes.UnknownDiskType) += 1
            ElseIf ImageData.Scanned Then
                _FilterCounts(FilterTypes.UnknownDiskType) -= 1
            End If
            If UpdateFilters Then
                FilterUpdate(FilterTypes.UnknownDiskType)
            End If
        End If
    End Sub

    Friend Sub ItemScanModified(Disk As DiskImage.Disk, ImageData As LoadedImageData, Optional UpdateFilters As Boolean = False, Optional Remove As Boolean = False)
        Dim IsModified As Boolean = Not Remove And (Disk IsNot Nothing AndAlso Disk.Data.Modified)

        If IsModified <> ImageData.Modified Then
            ImageData.Modified = IsModified
            If IsModified Then
                _FilterCounts(FilterTypes.ModifiedFiles) += 1
            Else
                _FilterCounts(FilterTypes.ModifiedFiles) -= 1
            End If
            If UpdateFilters Then
                FilterUpdate(FilterTypes.ModifiedFiles)
            End If
        End If
    End Sub

    Friend Sub ItemScanOEMName(Disk As DiskImage.Disk, ImageData As LoadedImageData, Optional UpdateFilters As Boolean = False, Optional Remove As Boolean = False)
        Dim OEMNameMatched As Boolean = True
        Dim OEMNameFound As Boolean = True
        Dim OEMNameWin9x As Boolean = False

        If Not Remove And Disk.IsValidImage Then
            Dim OEMName = Disk.BootSector.OEMName
            OEMNameWin9x = Disk.BootSector.IsWin9xOEMName

            Dim BootstrapType = _BootStrap.FindMatch(Disk.BootSector.BootStrapCode)
            OEMNameFound = BootstrapType IsNot Nothing
            If OEMNameFound AndAlso Not OEMNameWin9x AndAlso Not BootstrapType.ExactMatch Then
                OEMNameMatched = False
                For Each KnownOEMName In BootstrapType.KnownOEMNames
                    If KnownOEMName.Name.CompareTo(OEMName) Then
                        OEMNameMatched = True
                        Exit For
                    End If
                Next
            Else
                OEMNameMatched = True
            End If
        End If

        If Not ImageData.Scanned Or OEMNameWin9x <> ImageData.ScanInfo.OEMNameWin9x Then
            ImageData.ScanInfo.OEMNameWin9x = OEMNameWin9x
            If OEMNameWin9x Then
                _FilterCounts(FilterTypes.Windows9xOEMName) += 1
            ElseIf ImageData.Scanned Then
                _FilterCounts(FilterTypes.Windows9xOEMName) -= 1
            End If
            If UpdateFilters Then
                FilterUpdate(FilterTypes.Windows9xOEMName)
            End If
        End If

        If Not ImageData.Scanned Or OEMNameMatched <> ImageData.ScanInfo.OEMNameMatched Then
            ImageData.ScanInfo.OEMNameMatched = OEMNameMatched
            If Not OEMNameMatched Then
                _FilterCounts(FilterTypes.MismatchedOEMName) += 1
            ElseIf ImageData.Scanned Then
                _FilterCounts(FilterTypes.MismatchedOEMName) -= 1
            End If
            If UpdateFilters Then
                FilterUpdate(FilterTypes.MismatchedOEMName)
            End If
        End If

        If Not ImageData.Scanned Or OEMNameFound <> ImageData.ScanInfo.OEMNameFound Then
            ImageData.ScanInfo.OEMNameFound = OEMNameFound
            If Not OEMNameFound Then
                _FilterCounts(FilterTypes.UnknownOEMName) += 1
            ElseIf ImageData.Scanned Then
                _FilterCounts(FilterTypes.UnknownOEMName) -= 1
            End If
            If UpdateFilters Then
                FilterUpdate(FilterTypes.UnknownOEMName)
            End If
        End If
    End Sub

    Friend Sub ItemScanUnusedClusters(Disk As DiskImage.Disk, ImageData As LoadedImageData, Optional UpdateFilters As Boolean = False, Optional Remove As Boolean = False)
        Dim HasUnusedClusters As Boolean = False

        If Not Remove And Disk.IsValidImage Then
            HasUnusedClusters = Disk.FAT.HasUnusedClusters(True)
        End If

        If Not ImageData.Scanned Or HasUnusedClusters <> ImageData.ScanInfo.HasUnusedClusters Then
            ImageData.ScanInfo.HasUnusedClusters = HasUnusedClusters
            If HasUnusedClusters Then
                _FilterCounts(FilterTypes.UnusedClusters) += 1
            ElseIf ImageData.Scanned Then
                _FilterCounts(FilterTypes.UnusedClusters) -= 1
            End If
            If UpdateFilters Then
                FilterUpdate(FilterTypes.UnusedClusters)
            End If
        End If
    End Sub

    Friend Sub OEMNameFilterUpdate(Disk As DiskImage.Disk, ImageData As LoadedImageData, Optional UpdateFilters As Boolean = False, Optional Remove As Boolean = False)
        If Disk.IsValidImage Then
            Dim OEMNameString As String = ""

            If Not Remove Then
                OEMNameString = Disk.BootSector.GetOEMNameString.TrimEnd(NULL_CHAR)
            End If

            If Not ImageData.Scanned Or OEMNameString <> ImageData.ScanInfo.OEMName Then
                If ImageData.Scanned Then
                    _FilterOEMName.Remove(ImageData.ScanInfo.OEMName, UpdateFilters)
                End If

                ImageData.ScanInfo.OEMName = OEMNameString

                If Not Remove Then
                    OEMNameFilterAdd(ImageData.ScanInfo.OEMName, UpdateFilters)
                End If
            End If
        End If
    End Sub

    Friend Function Win9xClean(Disk As Disk, DoRefresh As Boolean) As Boolean
        Dim Result As Boolean = False

        Disk.Data.BatchEditMode = True

        If Disk.BootSector.IsWin9xOEMName Then
            Dim BootstrapType = _BootStrap.FindMatch(Disk.BootSector.BootStrapCode)
            If BootstrapType IsNot Nothing Then
                If BootstrapType.KnownOEMNames.Count > 0 Then
                    Disk.BootSector.OEMName = BootstrapType.KnownOEMNames.Item(0).Name
                    Result = True
                End If
            End If
        End If

        Dim FileList = Disk.GetFileList()

        For Each DirectoryEntry In FileList
            If DirectoryEntry.IsValid Then
                If DirectoryEntry.HasLastAccessDate Then
                    If DirectoryEntry.GetLastAccessDate.IsValidDate Then
                        DirectoryEntry.ClearLastAccessDate()
                        Result = True
                    End If
                End If

                If DirectoryEntry.HasCreationDate Then
                    If DirectoryEntry.GetCreationDate.IsValidDate Then
                        DirectoryEntry.ClearCreationDate()
                        Result = True
                    End If
                End If
            End If
        Next

        Disk.Data.BatchEditMode = False

        If Result And DoRefresh Then
            ComboItemRefresh(True, True)
        End If

        Return Result
    End Function

    Private Function AreFiltersApplied() As Boolean
        Dim FilterCount As Integer = [Enum].GetNames(GetType(FilterTypes)).Length

        For Counter = 0 To FilterCount - 1
            Dim Item As ToolStripMenuItem = ContextMenuFilters.Items("key_" & Counter)
            If Item.Checked Then
                Return True
                Exit For
            End If
        Next

        Return False
    End Function

    Private Sub BootSectorEdit()
        Dim BootSectorForm As New BootSectorForm(_Disk, _BootStrap)

        BootSectorForm.ShowDialog()

        Dim Result As Boolean = BootSectorForm.DialogResult = DialogResult.OK

        If Result Then
            DiskImageProcess(True, True)
        End If
    End Sub

    Private Sub BootSectorRemove()
        If _Disk.Directory.Data.HasBootSector Then
            Dim BootSectorBytes(BootSector.BOOT_SECTOR_SIZE - 1) As Byte

            For Counter = 0 To BootSectorBytes.Length - 1
                BootSectorBytes(Counter) = 0
            Next

            _Disk.Data.SetBytes(BootSectorBytes, _Disk.Directory.Data.BootSectorOffset)

            ComboItemRefresh(True, True)
        End If
    End Sub

    Private Sub BootSectorRestore()
        If _Disk.Directory.Data.HasBootSector Then
            Dim BootSectorBytes = _Disk.Data.GetBytes(_Disk.Directory.Data.BootSectorOffset, BootSector.BOOT_SECTOR_SIZE)
            _Disk.Data.SetBytes(BootSectorBytes, 0)

            DiskImageProcess(True, True)
        End If
    End Sub

    Private Sub CheckForUpdates()
        Dim DownloadVersion As String = ""
        Dim DownloadURL As String = ""
        Dim Body As String = ""
        Dim UpdateAvailable As Boolean = False

        Cursor.Current = Cursors.WaitCursor
        Try
            Dim Request As HttpWebRequest = WebRequest.Create(UPDATE_URL)
            Request.UserAgent = "DiskImageTool"
            Dim Response As HttpWebResponse = Request.GetResponse
            Dim Reader As New StreamReader(Response.GetResponseStream)
            Dim ResponseText = Reader.ReadToEnd

            Dim JSON As Dictionary(Of String, Object) = CompactJson.Serializer.Parse(Of Dictionary(Of String, Object))(ResponseText)

            If JSON.ContainsKey("tag_name") Then
                DownloadVersion = JSON.Item("tag_name").ToString
                If DownloadVersion.StartsWith("v", StringComparison.CurrentCultureIgnoreCase) Then
                    DownloadVersion = DownloadVersion.Remove(0, 1)
                End If
            End If

            If JSON.ContainsKey("assets") Then
                Dim assets() As Dictionary(Of String, Object) = CompactJson.Serializer.Parse(Of Dictionary(Of String, Object)())(JSON.Item("assets").ToString)
                If assets.Length > 0 Then
                    If assets(0).ContainsKey("browser_download_url") Then
                        DownloadURL = assets(0).Item("browser_download_url").ToString
                    End If
                End If
            End If

            If JSON.ContainsKey("body") Then
                Body = JSON.Item("body").ToString
            End If
        Catch ex As Exception
            MsgBox("An error occured while checking for updates.  Please try again later.", MsgBoxStyle.Exclamation)
            Exit Sub
        End Try
        Cursor.Current = Cursors.Default
        If DownloadVersion <> "" And DownloadURL <> "" Then
            Dim CurrentVersion = GetVersionString()
            UpdateAvailable = Version.Parse(DownloadVersion) > Version.Parse(CurrentVersion)
        End If

        If UpdateAvailable Then
            Dim Msg = My.Application.Info.Title & " v" & DownloadVersion & " Is available."
            If Body <> "" Then
                Msg = Msg & vbCrLf & vbCrLf & "Whats New" & vbCrLf & New String("—", 6) & vbCrLf & Body & vbCrLf
            End If
            Msg = Msg & vbCrLf & vbCrLf & "Do you wish to download it at this time?"
            If MsgBox(Msg, MsgBoxStyle.Question + MsgBoxStyle.YesNo + MsgBoxStyle.DefaultButton2) = MsgBoxResult.Yes Then
                Dim Dialog As New SaveFileDialog With {
                    .Filter = GetFileFilter("Zip Archive", "*.zip"),
                    .FileName = Path.GetFileName(DownloadURL),
                    .InitialDirectory = GetDownloadsFolder(),
                    .RestoreDirectory = True
                }
                Dialog.ShowDialog()
                If Dialog.FileName <> "" Then
                    Cursor.Current = Cursors.WaitCursor
                    Try
                        Dim Client As New WebClient()
                        Client.DownloadFile(DownloadURL, Dialog.FileName)
                    Catch ex As Exception
                        MsgBox("An error occured while downloading the file.", MsgBoxStyle.Exclamation)
                    End Try
                    Cursor.Current = Cursors.Default
                End If
            End If
        Else
            MsgBox("You are running the latest version of " & My.Application.Info.Title & ".", MsgBoxStyle.Information)
        End If
    End Sub

    Private Sub ClearFilesPanel()
        MenuDisplayDirectorySubMenuClear()
        ListViewFiles.ListViewItemSorter = Nothing
        ListViewFiles.Items.Clear()
        BtnWin9xClean.Enabled = False
        ItemSelectionChanged()
    End Sub

    Private Sub ClearSort(Reset As Boolean)
        If Reset Then
            _lvwColumnSorter.Order = SortOrder.Ascending
            _lvwColumnSorter.SortColumn = 0
            ListViewFiles.Sort()
        Else
            _lvwColumnSorter.Order = SortOrder.None
            _lvwColumnSorter.SortColumn = -1
        End If
        ListViewFiles.SetSortIcon(_lvwColumnSorter.SortColumn, _lvwColumnSorter.Order)
        BtnResetSort.Enabled = False
    End Sub

    Private Function CloseAll() As Boolean
        Dim BatchResult As MyMsgBoxResult = MyMsgBoxResult.Yes
        Dim Result As MyMsgBoxResult = MyMsgBoxResult.Yes

        Dim ModifyImageList = GetModifiedImageList()

        If ModifyImageList.Count > 0 Then
            Dim ShowDialog As Boolean = True

            For Each ImageData In ModifyImageList
                Dim NewFilePath As String = ""
                If ShowDialog Then
                    If ModifyImageList.Count = 1 Then
                        Result = MsgBoxSave(ImageData.FileName)
                    Else
                        Result = MsgBoxSaveAll(ImageData.FileName)
                    End If
                Else
                    Result = BatchResult
                End If

                If Result = MyMsgBoxResult.YesToAll Or Result = MyMsgBoxResult.NoToAll Then
                    ShowDialog = False
                    If Result = MyMsgBoxResult.NoToAll Then
                        BatchResult = MyMsgBoxResult.No
                    End If
                End If

                If Result = MyMsgBoxResult.Yes Or Result = MyMsgBoxResult.YesToAll Then
                    If ImageData.ReadOnly Then
                        If Not ShowDialog Then
                            If MsgBoxNewFileName(ImageData.FileName) <> MsgBoxResult.Ok Then
                                Result = MyMsgBoxResult.Cancel
                                Exit For
                            End If
                        End If
                        If Result <> MyMsgBoxResult.No Then
                            NewFilePath = GetNewFilePath(ImageData.SaveFile)
                            If NewFilePath = "" Then
                                Result = MyMsgBoxResult.Cancel
                                Exit For
                            End If
                        End If
                    End If
                End If

                If Result = MyMsgBoxResult.Yes Or Result = MyMsgBoxResult.YesToAll Then
                    If Not DiskImageSave(ImageData, NewFilePath) Then
                        Result = MyMsgBoxResult.Cancel
                        Exit For
                    End If
                ElseIf Result = MyMsgBoxResult.Cancel Then
                    Exit For
                End If
            Next
        End If

        If Result <> MyMsgBoxResult.Cancel Then
            ResetAll()
        End If
        Return (Result <> MyMsgBoxResult.Cancel)
    End Function

    Private Sub CloseCurrent()
        Dim NewFilePath As String = ""
        Dim CurrentImageData As LoadedImageData = ComboImages.SelectedItem
        Dim Result As MsgBoxResult

        If CurrentImageData.Modified Then
            Result = MsgBoxSave(CurrentImageData.FileName)
        Else
            Result = MsgBoxResult.No
        End If

        If Result = MsgBoxResult.Yes Then
            If CurrentImageData.ReadOnly Then
                NewFilePath = GetNewFilePath(CurrentImageData.SaveFile)
                If NewFilePath = "" Then
                    Result = MsgBoxResult.Cancel
                End If
            End If
            If Result = MsgBoxResult.Yes Then
                If Not DiskImageSave(CurrentImageData, NewFilePath) Then
                    Result = MsgBoxResult.Cancel
                End If
            End If
        End If

        If Result <> MsgBoxResult.Cancel Then
            FileClose(CurrentImageData)
        End If
    End Sub

    Private Sub ComboImagesRefreshCurrentItemText()
        ComboImages.Invalidate()
        ComboImagesFiltered.Invalidate()
    End Sub

    Private Sub ComboImagesRefreshItemText()
        _SuppressEvent = True

        For Index = 0 To ComboImages.Items.Count - 1
            ComboImages.Items(Index) = ComboImages.Items(Index)
        Next

        For Index = 0 To ComboImagesFiltered.Items.Count - 1
            ComboImagesFiltered.Items(Index) = ComboImagesFiltered.Items(Index)
        Next

        _SuppressEvent = False
    End Sub

    Private Sub ComboImagesRefreshPaths()
        ComboImages.BeginUpdate()
        LoadedImageData.StringOffset = GetPathOffset()
        ComboImagesRefreshItemText()
        ComboImages.EndUpdate()
    End Sub

    Private Sub ComboItemRefresh(FullRefresh As Boolean, DoItemScan As Boolean)
        Dim CurrentImageData As LoadedImageData = ComboImages.SelectedItem

        If FullRefresh Then
            _Disk.Directory.RefreshData()
        End If

        If DoItemScan Then
            ItemScan(ItemScanTypes.All, _Disk, CurrentImageData, True)
        End If

        ComboImagesRefreshCurrentItemText()

        PopulateSummary(_Disk, CurrentImageData)

        If FullRefresh Then
            If _Disk.IsValidImage Then
                PopulateFilesPanel()
            Else
                ClearFilesPanel()
            End If
        End If

        FATSubMenuRefresh()
        RefreshSaveButtons()
    End Sub

    Private Sub DeleteSelectedFiles(Clear As Boolean)
        Dim Msg As String
        Dim Item As ListViewItem
        Dim FileData As FileData
        Dim Result As Boolean = False

        If ListViewFiles.SelectedItems.Count = 0 Then
            Exit Sub
        ElseIf ListViewFiles.SelectedItems.Count = 1 Then
            Item = ListViewFiles.SelectedItems(0)
            FileData = Item.Tag
            Msg = "Are you sure you with to delete " & FileData.DirectoryEntry.GetFullFileName & "?"
        Else
            Msg = "Are you sure you with to delete the selected files?"
        End If

        If MsgBox(Msg, MsgBoxStyle.YesNo + MsgBoxStyle.DefaultButton2) = MsgBoxResult.No Then
            Exit Sub
        End If

        _Disk.Data.BatchEditMode = True

        For Each Item In ListViewFiles.SelectedItems
            FileData = Item.Tag
            If DirectoryEntryCanDelete(FileData.DirectoryEntry, Clear) Then
                FileData.DirectoryEntry.Remove(Clear)
                Result = True
            ElseIf Clear And DirectoryEntryCanDelete(FileData.DirectoryEntry, False) Then
                FileData.DirectoryEntry.Remove(False)
                Result = True
            End If
        Next

        _Disk.Data.BatchEditMode = False

        If Result Then
            FilePropertiesRefresh(ListViewFiles.SelectedItems, True)
        End If
    End Sub

    Private Function DirectoryEntryCanDelete(DirectoryEntry As DiskImage.DirectoryEntry, Clear As Boolean) As Boolean
        If DirectoryEntry.IsDeleted Then
            Return False
        ElseIf Clear And Not DirectoryEntry.IsValidFile Then
            Return False
        ElseIf DirectoryEntry.IsValidDirectory Then
            Return DirectoryEntry.SubDirectory.Data.FileCount - DirectoryEntry.SubDirectory.Data.DeletedFileCount = 0
        Else
            Return True
        End If
    End Function

    Private Function DirectoryEntryCanExport(DirectoryEntry As DiskImage.DirectoryEntry) As Boolean
        Return DirectoryEntry.IsValidFile _
            And Not DirectoryEntry.IsDeleted _
            And Not DirectoryEntry.HasInvalidFilename _
            And Not DirectoryEntry.HasInvalidExtension
    End Function

    Private Sub DirectoryEntryDisplayText(DirectoryEntry As DiskImage.DirectoryEntry)
        Dim frmTextView As TextViewForm

        If Not DirectoryEntry.IsValidFile _
            Or DirectoryEntry.IsDeleted Then
            Exit Sub
        End If

        Dim Caption As String = "File - " & DirectoryEntry.GetFullFileName
        Dim Bytes = DirectoryEntry.GetContent
        Dim Content As String

        Using Stream As New IO.MemoryStream
            Dim PrevByte As Byte = 0
            For Counter = 0 To Bytes.Length - 1
                Dim B = Bytes(Counter)
                If B = 0 Then
                    Stream.WriteByte(32)
                ElseIf Counter > 0 And B = 10 And PrevByte <> 13 Then
                    Stream.WriteByte(13)
                    Stream.WriteByte(10)
                Else
                    Stream.WriteByte(B)
                End If
                PrevByte = B
            Next
            Content = Encoding.UTF7.GetString(Stream.GetBuffer)
        End Using

        frmTextView = New TextViewForm(Caption, Content)
        frmTextView.ShowDialog()
    End Sub

    Private Function DirectoryEntryGetStats(DirectoryEntry As DiskImage.DirectoryEntry) As DirectoryStats
        Dim Stats As DirectoryStats

        With Stats
            .IsDirectory = DirectoryEntry.IsDirectory And Not DirectoryEntry.IsVolumeName
            .IsDeleted = DirectoryEntry.IsDeleted
            .IsModified = DirectoryEntry.IsModified
            .IsValidFile = DirectoryEntry.IsValidFile
            .IsValidDirectory = DirectoryEntry.IsValidDirectory
            .CanExport = DirectoryEntryCanExport(DirectoryEntry)
            .FileSize = DirectoryEntry.FileSize
            .FullFileName = DirectoryEntry.GetFullFileName
            .CanDelete = DirectoryEntryCanDelete(DirectoryEntry, False)
            .CanDeleteWithFill = DirectoryEntryCanDelete(DirectoryEntry, True)
        End With

        Return Stats
    End Function

    Private Sub DiskImageProcess(DoItemScan As Boolean, Reinitialize As Boolean)
        Dim CurrentImageData As LoadedImageData = ComboImages.SelectedItem

        If Reinitialize Then
            _Disk?.Reinitialize()
        End If

        InitButtonState()
        PopulateSummary(_Disk, CurrentImageData)
        ClearSort(False)

        If _Disk IsNot Nothing AndAlso _Disk.IsValidImage Then
            If CurrentImageData.CachedRootDir Is Nothing Then
                CurrentImageData.CachedRootDir = _Disk.Directory.GetContent
            End If
            PopulateFilesPanel()
        Else
            ClearFilesPanel()
        End If

        If DoItemScan Then
            ItemScan(ItemScanTypes.All, _Disk, CurrentImageData, True)
            ComboImagesRefreshCurrentItemText()
            RefreshSaveButtons()
        End If
    End Sub

    Private Function DiskImageSave(ImageData As LoadedImageData, Optional NewFilePath As String = "") As Boolean
        Dim Disk As DiskImage.Disk
        Dim CurrentImageData As LoadedImageData = ComboImages.SelectedItem
        Dim Success As Boolean

        Do
            If ImageData Is CurrentImageData Then
                Disk = _Disk
            Else
                Disk = DiskImageLoad(ImageData)
            End If
            Success = Disk IsNot Nothing
            If Success Then
                If NewFilePath = "" Then
                    NewFilePath = ImageData.SaveFile
                End If
                Success = SaveDiskImageToFile(Disk, NewFilePath)
            End If
            If Not Success Then
                Dim Msg As String = "Error saving file '" & IO.Path.GetFileName(NewFilePath) & "'."
                Dim ErrorResult = MsgBox(Msg, MsgBoxStyle.Critical + MsgBoxStyle.RetryCancel)
                If ErrorResult = MsgBoxResult.Cancel Then
                    Exit Do
                End If
            End If
        Loop Until Success

        If Success Then
            ItemScanModified(Disk, ImageData)
        End If

        Return Success
    End Function

    Private Sub DiskImagesScan(NewOnly As Boolean)
        Me.UseWaitCursor = True
        Dim T = Stopwatch.StartNew

        BtnScanNew.Visible = False
        BtnScan.Enabled = False
        If _FiltersApplied Then
            FiltersClear(False)
            ImageCountUpdate()
        End If

        Dim CurrentImageData As LoadedImageData = ComboImages.SelectedItem
        Dim ItemScanForm As New ItemScanForm(Me, ComboImages.Items, CurrentImageData, _Disk, NewOnly, ScanType.ScanTypeFilters)
        ItemScanForm.ShowDialog()
        BtnScanNew.Visible = ItemScanForm.ItemsRemaining > 0

        For Counter = 0 To [Enum].GetNames(GetType(FilterTypes)).Length - 1
            FilterUpdate(Counter)
        Next

        RefreshModifiedState()
        SubFiltersPopulate()

        ComboOEMName.Visible = True
        ToolStripOEMName.Visible = True
        ComboDiskType.Visible = True
        ToolStripDiskType.Visible = True

        BtnScan.Text = "Rescan Images"
        BtnScan.Enabled = True
        _ScanRun = True

        T.Stop()
        Debug.Print("ScanImages Time Taken: " & T.Elapsed.ToString)
        Me.UseWaitCursor = False

        Dim Handle = WindowsAPI.GetForegroundWindow()
        If Handle = Me.Handle Then
            MainMenuFilters.ShowDropDown()
        Else
            WindowsAPI.FlashWindow(Me.Handle, True, True, 5, True)
        End If
    End Sub

    Private Sub DiskTypeFilterReset()
        _FilterDiskType.Clear()
        For Each ImageData As LoadedImageData In ComboImages.Items
            _FilterDiskType.Add(ImageData.ScanInfo.DiskType, False)
        Next
        _FilterDiskType.Populate()
    End Sub

    Private Sub DisplayCrossLinkedFiles(DirectoryEntry As DiskImage.DirectoryEntry)
        Dim Msg As String = DirectoryEntry.GetFullFileName() & " is crosslinked with the following files:" & vbCrLf

        For Each Crosslink In DirectoryEntry.CrossLinks
            If Crosslink <> DirectoryEntry.Offset Then
                Dim CrossLinkedFile = _Disk.GetDirectoryEntryByOffset(Crosslink)
                Msg &= vbCrLf & CrossLinkedFile.GetFullFileName()
            End If
        Next
        MsgBox(Msg, MsgBoxStyle.Information + MsgBoxStyle.OkOnly)
    End Sub

    Private Sub DragDropSelectedFiles()
        If ListViewFiles.SelectedItems.Count = 0 Then
            Exit Sub
        End If

        Dim TempPath As String = IO.Path.GetTempPath() & Guid.NewGuid().ToString() & "\"

        FileExportSelected(False, TempPath)

        If IO.Directory.Exists(TempPath) Then
            Dim FileList = IO.Directory.EnumerateDirectories(TempPath)
            For Each FilePath In IO.Directory.GetFiles(TempPath)
                FileList = FileList.Append(FilePath)
            Next
            If FileList.Count > 0 Then
                Dim Data = New DataObject(DataFormats.FileDrop, FileList.ToArray)
                ListViewFiles.DoDragDrop(Data, DragDropEffects.Copy)
            End If
            IO.Directory.Delete(TempPath, True)
        End If
    End Sub

    Private Sub ExportDebugScript()
        Dim CurrentImageData As LoadedImageData = ComboImages.SelectedItem
        GenerateDebugPackage(_Disk, CurrentImageData)
    End Sub

    Private Sub FATEdit(Index As UShort, DisplaySync As Boolean)
        Dim frmFATEdit As New FATEditForm(_Disk, Index, DisplaySync)

        frmFATEdit.ShowDialog()

        If frmFATEdit.Updated Then
            DiskImageProcess(True, True)
        End If
    End Sub

    Private Sub FATSubMenuRefresh()
        For Each Item As ToolStripMenuItem In BtnEditFAT.DropDownItems
            RemoveHandler Item.Click, AddressOf BtnEditFAT_Click
        Next
        BtnEditFAT.DropDownItems.Clear()
        BtnEditFAT.Tag = Nothing

        If _Disk IsNot Nothing AndAlso _Disk.IsValidImage Then
            If _Disk.BootSector.NumberOfFATs = 1 OrElse _Disk.CompareFATTables Then
                BtnEditFAT.Tag = -1
            Else
                For Counter = 0 To _Disk.BootSector.NumberOfFATs - 1
                    Dim Item As New ToolStripMenuItem With {
                       .Text = "FAT &" & Counter + 1,
                       .Tag = Counter
                    }
                    BtnEditFAT.DropDownItems.Add(Item)
                    AddHandler Item.Click, AddressOf BtnEditFAT_Click
                Next
            End If
        End If
    End Sub

    Private Sub FileClose(ImageData As LoadedImageData)
        ItemScan(ItemScanTypes.All, _Disk, ImageData, True, True)
        _LoadedFileNames.Remove(ImageData.DisplayPath)

        Dim ActiveComboBox As ComboBox = IIf(_FiltersApplied, ComboImagesFiltered, ComboImages)

        Dim SelectedIndex = ActiveComboBox.SelectedIndex

        ComboImages.Items.Remove(ImageData)
        ComboImagesFiltered.Items.Remove(ImageData)

        If ActiveComboBox.SelectedIndex = -1 Then
            If SelectedIndex > ActiveComboBox.Items.Count - 1 Then
                SelectedIndex = ActiveComboBox.Items.Count - 1
            End If
            ActiveComboBox.SelectedIndex = SelectedIndex
        End If

        If ComboImages.Items.Count = 0 Then
            ResetAll()
        Else
            ImageCountUpdate()
        End If
    End Sub

    Private Sub FileDropStart(e As DragEventArgs)
        If _SuppressEvent Then
            Exit Sub
        End If

        If e.Data.GetDataPresent(DataFormats.FileDrop) Then
            e.Effect = DragDropEffects.Copy
        End If
    End Sub

    Private Sub FileExport()
        If ListViewFiles.SelectedItems.Count = 1 Then
            FileExportCurrent()
        ElseIf ListViewFiles.SelectedItems.Count > 1 Then
            FileExportSelected(True)
        End If
    End Sub

    Private Sub FileExportCurrent()
        Dim FileData As FileData = ListViewFiles.SelectedItems(0).Tag
        Dim DirectoryEntry = FileData.DirectoryEntry

        Dim Dialog = New SaveFileDialog With {
            .FileName = DirectoryEntry.GetFullFileName
        }
        If Dialog.ShowDialog <> DialogResult.OK Then
            Exit Sub
        End If

        Dim Result = FileSave(Dialog.FileName, DirectoryEntry)

        If Not Result Then
            Dim Msg As String = "Error saving file '" & IO.Path.GetFileName(Dialog.FileName) & "'."
            MsgBox(Msg, MsgBoxStyle.Critical + MsgBoxStyle.OkOnly)
        End If
    End Sub

    Private Sub FileExportSelected(ShowDialog As Boolean, Optional Path As String = "")
        Dim ShowResults As Boolean = ShowDialog

        If Path = "" Then
            Dim Dialog = New FolderBrowserDialog

            If Dialog.ShowDialog <> DialogResult.OK Then
                Exit Sub
            End If
            Path = Dialog.SelectedPath
        End If

        Dim BatchResult As MyMsgBoxResult = MyMsgBoxResult.Yes
        Dim Result As MyMsgBoxResult = MyMsgBoxResult.Yes
        Dim FileCount As Integer = 0
        Dim TotalFiles As Integer = 0
        For Each Item As ListViewItem In ListViewFiles.SelectedItems
            Dim FileData As FileData = Item.Tag
            Dim DirectoryEntry = FileData.DirectoryEntry
            If DirectoryEntryCanExport(DirectoryEntry) Then
                TotalFiles += 1
                If Result <> MyMsgBoxResult.Cancel Then
                    Dim FilePath = IO.Path.Combine(Path, FileData.FilePath, DirectoryEntry.GetFullFileName)
                    If Not IO.Directory.Exists(IO.Path.GetDirectoryName(FilePath)) Then
                        IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(FilePath))
                    End If
                    If IO.File.Exists(FilePath) Then
                        If ShowDialog Then
                            Result = MsgBoxOverwrite(FilePath)
                        Else
                            Result = BatchResult
                        End If
                    Else
                        Result = MyMsgBoxResult.Yes
                    End If
                    If Result = MyMsgBoxResult.YesToAll Or Result = MyMsgBoxResult.NoToAll Then
                        ShowDialog = False
                        If Result = MyMsgBoxResult.NoToAll Then
                            BatchResult = MyMsgBoxResult.No
                        End If
                    End If
                    If Result = MyMsgBoxResult.Yes Or Result = MyMsgBoxResult.YesToAll Then
                        If FileSave(FilePath, DirectoryEntry) Then
                            FileCount += 1
                        End If
                    End If
                End If
            End If
        Next

        If ShowResults Then
            Dim Msg As String = FileCount & " of " & TotalFiles & " file" & IIf(TotalFiles <> 1, "s", "") & " exported successfully."
            MsgBox(Msg, MsgBoxStyle.Information + MsgBoxStyle.OkOnly)
        End If
    End Sub

    Private Sub FileInfoUpdate()
        If ListViewFiles.SelectedItems.Count > 0 Then
            ToolStripFileCount.Text = ListViewFiles.SelectedItems.Count & " of " & ListViewFiles.Items.Count & " File" & IIf(ListViewFiles.Items.Count <> 1, "s", "") & " Selected"
        Else
            ToolStripFileCount.Text = ListViewFiles.Items.Count & " File" & IIf(ListViewFiles.Items.Count <> 1, "s", "")
        End If
        ToolStripFileCount.Visible = True

        If ListViewFiles.SelectedItems.Count = 1 Then
            Dim FileData As FileData = ListViewFiles.SelectedItems(0).Tag

            If FileData.DirectoryEntry.StartingCluster >= 2 Then

                Dim Sector = _Disk.BootSector.ClusterToSector(FileData.DirectoryEntry.StartingCluster)
                ToolStripFileSector.Text = "Sector " & Sector
                ToolStripFileSector.Visible = True

                Dim Track = _Disk.BootSector.SectorToTrack(Sector)
                Dim Side = _Disk.BootSector.SectorToSide(Sector)

                ToolStripFileTrack.Text = "Track " & Track & "." & Side
                ToolStripFileTrack.Visible = True

                ToolStripFileTrack.GetCurrentParent.Refresh()
            Else
                ToolStripFileSector.Visible = False
                ToolStripFileTrack.Visible = False
            End If
        Else
            ToolStripFileSector.Visible = False
            ToolStripFileTrack.Visible = False
        End If
    End Sub

    Private Function FilePropertiesEdit() As Boolean
        Dim Result As Boolean = False

        Dim frmFileProperties As New FilePropertiesForm(_Disk, ListViewFiles.SelectedItems)
        frmFileProperties.ShowDialog()

        If frmFileProperties.DialogResult = DialogResult.OK Then
            Result = frmFileProperties.Updated

            If Result Then
                FilePropertiesRefresh(ListViewFiles.SelectedItems, False)
            End If
        End If

        Return Result
    End Function

    Private Sub FilePropertiesRefresh(Item As ListViewItem, RefreshSummary As Boolean)
        Dim List As New List(Of ListViewItem) From {
            Item
        }

        FilePropertiesRefresh(List, RefreshSummary)
    End Sub

    Private Sub FilePropertiesRefresh(List As IList, RefreshSummary As Boolean)
        Dim Response = ProcessDirectoryEntries(_Disk.Directory, 0, "", True)

        ListViewFiles.BeginUpdate()

        For Each Item As ListViewItem In List
            ListViewFilesRefreshItem(Item.Index)
        Next
        ProcessDirectoryScanResponse(Response)

        ListViewFiles.EndUpdate()

        Dim CurrentImageData As LoadedImageData = ComboImages.SelectedItem
        ItemScan(ItemScanTypes.Directory, _Disk, CurrentImageData, True)
        ComboImagesRefreshCurrentItemText()
        RefreshSaveButtons()
        RefreshFileButtons()
        If RefreshSummary Then
            PopulateSummaryPanel(_Disk)
        End If
        PopulateHashPanel(_Disk)
        RefreshButtonStatus(_Disk, CurrentImageData)
    End Sub

    Private Sub FileReplace(Item As ListViewItem)
        Dim Dialog = New OpenFileDialog
        If Dialog.ShowDialog <> DialogResult.OK Then
            Exit Sub
        End If

        Dim FileInfo As New IO.FileInfo(Dialog.FileName)
        Dim Msg As String

        Dim FileData As FileData = Item.Tag

        If FileInfo.Length < FileData.DirectoryEntry.FileSize Then
            Msg = FileInfo.Name & " Is smaller than " & FileData.DirectoryEntry.GetFullFileName & " And will be padded With nulls." & vbCrLf & vbCrLf
        ElseIf FileInfo.Length > FileData.DirectoryEntry.FileSize Then
            Msg = FileInfo.Name & " Is larger than " & FileData.DirectoryEntry.GetFullFileName & " And will be truncated." & vbCrLf & vbCrLf
        Else
            Msg = ""
        End If
        Msg &= "Do you wish To replace " & FileData.DirectoryEntry.GetFullFileName & " With " & FileInfo.FullName & "?"

        If MsgBox(Msg, MsgBoxStyle.Question + MsgBoxStyle.YesNo + MsgBoxStyle.DefaultButton2) = MsgBoxResult.Yes Then
            Dim ClusterSize = _Disk.BootSector.BytesPerCluster
            Dim FileSize = Math.Min(FileData.DirectoryEntry.FileSize, FileData.DirectoryEntry.FATChain.Clusters.Count * ClusterSize)
            Dim BytesToFill As Integer
            Dim ClusterIndex As Integer = 0

            _Disk.Data.BatchEditMode = True

            Using fs = FileInfo.OpenRead()
                Dim BytesToRead As Integer = Math.Min(fs.Length, FileSize)
                Dim n As Integer
                Do While BytesToRead > 0
                    Dim b(ClusterSize - 1) As Byte
                    Dim Cluster = FileData.DirectoryEntry.FATChain.Clusters(ClusterIndex)
                    Dim ClusterOffset = _Disk.BootSector.ClusterToOffset(Cluster)
                    If BytesToRead < ClusterSize Then
                        b = _Disk.Data.GetBytes(ClusterOffset, ClusterSize)
                    End If
                    n = fs.Read(b, 0, Math.Min(ClusterSize, BytesToRead))
                    BytesToRead -= n
                    FileSize -= n
                    If BytesToRead <= 0 And FileSize > 0 And n < ClusterSize Then
                        BytesToFill = Math.Min(b.Length - n, FileSize)
                        For Counter = n To n + BytesToFill - 1
                            b(Counter) = 0
                        Next
                        FileSize -= BytesToFill
                    End If
                    _Disk.Data.SetBytes(b, ClusterOffset)

                    ClusterIndex += 1
                Loop
            End Using

            Do While FileSize > 0
                Dim b(ClusterSize - 1) As Byte
                Dim Cluster = FileData.DirectoryEntry.FATChain.Clusters(ClusterIndex)
                Dim ClusterOffset = _Disk.BootSector.ClusterToOffset(Cluster)
                BytesToFill = Math.Min(FileSize, ClusterSize)
                If BytesToFill < ClusterSize Then
                    b = _Disk.Data.GetBytes(ClusterOffset, ClusterSize)
                End If
                For Counter = 0 To BytesToFill - 1
                    b(Counter) = 0
                Next
                FileSize -= BytesToFill
                _Disk.Data.SetBytes(b, ClusterOffset)
                ClusterIndex += 1
            Loop

            _Disk.Data.BatchEditMode = False

            FilePropertiesRefresh(Item, False)
        End If
    End Sub

    Private Function FileSave(FilePath As String, DirectoryEntry As DiskImage.DirectoryEntry) As Boolean
        Try
            IO.File.WriteAllBytes(FilePath, DirectoryEntry.GetContent)
            Dim D = DirectoryEntry.GetLastWriteDate
            If D.IsValidDate Then
                IO.File.SetLastWriteTime(FilePath, D.DateObject)
            End If
        Catch ex As Exception
            Return False
        End Try

        Return True
    End Function

    Private Sub FilesOpen()
        Dim FileFilter = _FileFilter
        FileFilter = AppendFileFilter(FileFilter, "Zip Archive", "*.zip")
        FileFilter = AppendFileFilter(FileFilter, "All files (*.*)", "*.*")

        Dim Dialog = New OpenFileDialog With {
            .Filter = FileFilter,
            .Multiselect = True
        }
        If Dialog.ShowDialog <> DialogResult.OK Then
            Exit Sub
        End If

        ProcessFileDrop(Dialog.FileNames)
    End Sub

    Private Sub FiltersApply(ResetSubFilters As Boolean)
        Dim FilterCount As Integer = [Enum].GetNames(GetType(FilterTypes)).Length

        If ResetSubFilters Then
            SubFiltersClearFilter()
        End If

        Dim FiltersChecked As Boolean = AreFiltersApplied()

        Dim TextFilter As String = TxtSearch.Text.Trim.ToLower
        Dim OEMNameItem As ComboFilterItem = ComboOEMName.SelectedItem
        Dim DiskTypeItem As ComboFilterItem = ComboDiskType.SelectedItem
        Dim HasOEMNameFilter = OEMNameItem IsNot Nothing AndAlso Not OEMNameItem.AllItems
        Dim HasDiskTypeFilter = DiskTypeItem IsNot Nothing AndAlso Not DiskTypeItem.AllItems

        If FiltersChecked Or TextFilter.Length > 0 Or HasOEMNameFilter Or HasDiskTypeFilter Then
            Cursor.Current = Cursors.WaitCursor

            If ResetSubFilters Then
                SubFiltersClear()
            End If

            ComboImagesFiltered.BeginUpdate()
            ComboImagesFiltered.Items.Clear()

            Dim AppliedFilters As Integer = 0
            For Counter = 0 To FilterCount - 1
                Dim Item As ToolStripMenuItem = ContextMenuFilters.Items("key_" & Counter)
                If Item.Checked Then
                    AppliedFilters += Item.Tag
                End If
            Next

            For Each ImageData As LoadedImageData In ComboImages.Items
                Dim ShowItem As Boolean = True

                If ShowItem AndAlso FiltersChecked Then
                    ShowItem = Not IsFiltered(ImageData, AppliedFilters)
                End If

                If ShowItem And ResetSubFilters Then
                    OEMNameFilterAdd(ImageData.ScanInfo.OEMName, False)
                    _FilterDiskType.Add(ImageData.ScanInfo.DiskType, False)
                End If

                If ShowItem AndAlso TextFilter.Length > 0 Then
                    Dim FilePath = ImageData.DisplayPath.ToLower
                    ShowItem = FilePath.StartsWith(TextFilter) OrElse FilePath.Contains(" " & TextFilter) OrElse FilePath.Contains("\" & TextFilter)
                End If

                If ShowItem AndAlso HasOEMNameFilter Then
                    ShowItem = ImageData.ScanInfo.IsValidImage And OEMNameItem.Name = ImageData.ScanInfo.OEMName
                End If

                If ShowItem AndAlso HasDiskTypeFilter Then
                    ShowItem = DiskTypeItem.Name = ImageData.ScanInfo.DiskType
                End If

                If ShowItem Then
                    Dim Index = ComboImagesFiltered.Items.Add(ImageData)
                    If ImageData Is ComboImages.SelectedItem Then
                        ComboImagesFiltered.SelectedIndex = Index
                    End If
                End If
            Next

            If ResetSubFilters Then
                SubFiltersPopulate()
            End If

            If ComboImagesFiltered.SelectedIndex = -1 AndAlso ComboImagesFiltered.Items.Count > 0 Then
                ComboImagesFiltered.SelectedIndex = 0
            End If

            MainMenuFilters.BackColor = Color.LightGreen

            ComboImagesFiltered.EndUpdate()
            ComboImagesFiltered.Visible = True
            ComboImagesFiltered.Enabled = ComboImagesFiltered.Items.Count > 0
            If ComboImagesFiltered.Enabled Then
                ComboImagesFiltered.DrawMode = DrawMode.OwnerDrawFixed
            Else
                ComboImagesFiltered.DrawMode = DrawMode.Normal
            End If
            ComboImages.Visible = False

            _FiltersApplied = True
            BtnClearFilters.Enabled = True

            Cursor.Current = Cursors.Default
        Else
            If _FiltersApplied Then
                FiltersClear(True)
            End If
        End If

        ImageCountUpdate()
    End Sub

    Private Sub FiltersClear(ResetSubFilters As Boolean)
        Cursor.Current = Cursors.WaitCursor

        Dim FiltersApplied = AreFiltersApplied()

        _SuppressEvent = True
        For Counter = 0 To [Enum].GetNames(GetType(FilterTypes)).Length - 1
            Dim Item As ToolStripMenuItem = ContextMenuFilters.Items("key_" & Counter)
            If Item.CheckState = CheckState.Checked Then
                Item.CheckState = CheckState.Unchecked
            End If
        Next
        _SuppressEvent = False

        SubFiltersClearFilter()

        If FiltersApplied Or ResetSubFilters Then
            SubFiltersReset()
        End If

        ComboImages.Visible = True
        ComboImagesFiltered.Visible = False
        ComboImagesFiltered.Items.Clear()

        MainMenuFilters.BackColor = SystemColors.Control
        _FiltersApplied = False
        BtnClearFilters.Enabled = False

        Cursor.Current = Cursors.Default
    End Sub

    Private Sub FiltersInitialize()
        Dim Separator = New ToolStripSeparator With {
            .Name = "FilterSeparator",
            .Visible = False,
            .Tag = 0
        }
        ContextMenuFilters.Items.Add(Separator)

        Dim FilterCount As Integer = [Enum].GetNames(GetType(FilterTypes)).Length
        ReDim _FilterCounts(FilterCount - 1)
        For Counter = 0 To FilterCount
            Dim Item = New ToolStripMenuItem With {
                .Text = FilterGetCaption(Counter, 0),
                .CheckOnClick = True,
                .Name = "key_" & Counter,
                .Visible = False,
                .Enabled = False,
                .Tag = 2 ^ Counter
            }
            AddHandler Item.CheckStateChanged, AddressOf ContextMenuFilters_CheckStateChanged
            ContextMenuFilters.Items.Add(Item)
        Next

        _FilterOEMName = New ComboFilter(ComboOEMName)
        _FilterDiskType = New ComboFilter(ComboDiskType)
    End Sub

    Private Sub FiltersReset()
        For Counter = 0 To _FilterCounts.Length - 1
            _FilterCounts(Counter) = 0
            FilterUpdate(Counter)
        Next
        RefreshModifiedState()

        BtnScan.Text = "Scan Images"
        TxtSearch.Text = ""
        BtnClearFilters.Enabled = False
        _FilterOEMName.Clear()
        ComboOEMName.Visible = False
        ToolStripOEMName.Visible = False
        ComboDiskType.Visible = False
        ToolStripDiskType.Visible = False
    End Sub

    Private Function FilterUpdate(ID As FilterTypes) As Boolean
        Dim Count As Integer = _FilterCounts(ID)
        Dim Item As ToolStripMenuItem = ContextMenuFilters.Items("key_" & ID)
        Dim Enabled As Boolean = (Count > 0)
        Dim CheckstateChanged As Boolean = False

        Item.Text = FilterGetCaption(ID, Count)

        If Enabled <> Item.Enabled Then
            Item.Visible = Enabled
            Item.Enabled = Enabled
            If Not Enabled And Item.CheckState = CheckState.Checked Then
                Item.CheckState = CheckState.Unchecked
                CheckstateChanged = True
            End If
            With ContextMenuFilters.Items.Item("FilterSeparator")
                .Tag = .Tag + IIf(Enabled, 1, -1)
                .Visible = (.Tag > 0)
            End With
        End If

        Return CheckstateChanged
    End Function

    Private Sub FixFileSize(DirectoryEntry As DirectoryEntry)
        If DirectoryEntry.HasIncorrectFileSize() Then
            DirectoryEntry.FileSize = DirectoryEntry.GetAllocatedSizeFromFAT
            ComboItemRefresh(True, True)
        End If
    End Sub

    Private Sub FixImageSize()
        Dim Result As Boolean = True
        Dim Compare = _Disk.CheckImageSize

        If Compare = 0 Then
            Result = False
        ElseIf Compare < 0 Then
            Result = MsgBox("The image size is smaller than the detected size." & vbCrLf & vbCrLf & "Are you sure you wish to increase the image size?", MsgBoxStyle.Question + MsgBoxStyle.YesNo + MsgBoxStyle.DefaultButton2) = MsgBoxResult.Yes
        Else
            Dim ReportedSize = _Disk.ReportedImageSize
            Dim Data = _Disk.Data.GetBytes(ReportedSize, _Disk.Data.Length - ReportedSize)
            Dim HasData As Boolean = False
            For Each b In Data
                If b <> 0 Then
                    HasData = True
                    Exit For
                End If
            Next
            If HasData Then
                Result = MsgBox("There is data in the overdumped region of the image." & vbCrLf & vbCrLf & "Are you sure you wish to truncate the image?", MsgBoxStyle.Question + MsgBoxStyle.YesNo + MsgBoxStyle.DefaultButton2) = MsgBoxResult.Yes
            End If
        End If

        If Result Then
            If _Disk.FixImageSize Then
                ComboItemRefresh(True, True)
            End If
        End If
    End Sub

    Private Function GetFileDataFromDirectoryEntry(Index As Integer, DirectoryEntry As DiskImage.DirectoryEntry, ParentOffset As UInteger, FilePath As String, IsLastEntry As Boolean, LFNFileName As String) As FileData
        Dim Response As FileData
        With Response
            .Index = Index
            .FilePath = FilePath
            .DirectoryEntry = DirectoryEntry
            .IsLastEntry = IsLastEntry
            .ParentOffset = ParentOffset
            .LFNFileName = LFNFileName
        End With

        Return Response
    End Function

    Private Function GetModifiedImageList() As List(Of LoadedImageData)
        Dim ModifyImageList As New List(Of LoadedImageData)

        For Each ImageData As LoadedImageData In ComboImages.Items
            If ImageData.Modified Then
                ModifyImageList.Add(ImageData)
            End If
        Next

        Return ModifyImageList
    End Function

    Private Function GetNewFilePath(FilePath As String) As String
        Dim NewFilePath As String = ""

        Dim FileFilter = _FileFilter
        FileFilter = AppendFileFilter(FileFilter, "All files (*.*)", "*.*")

        Dim Dialog = New SaveFileDialog With {
            .InitialDirectory = IO.Path.GetDirectoryName(FilePath),
            .FileName = IO.Path.GetFileName(FilePath),
            .Filter = FileFilter
        }

        AddHandler Dialog.FileOk, Sub(sender As Object, e As CancelEventArgs)
                                      If Dialog.FileName <> FilePath AndAlso _LoadedFileNames.ContainsKey(Dialog.FileName) Then
                                          Dim Msg As String = Path.GetFileName(Dialog.FileName) _
                                            & vbCrLf & "This file Is currently open In " & Application.ProductName & "." _
                                            & vbCrLf & "Try again With a different file name."
                                          MsgBox(Msg, MsgBoxStyle.Exclamation, "Save As")
                                          e.Cancel = True
                                      End If
                                  End Sub

        If Dialog.ShowDialog = DialogResult.OK Then
            NewFilePath = Dialog.FileName
        End If

        Return NewFilePath
    End Function

    Private Function GetPathOffset() As Integer
        Dim PathName As String = ""
        Dim CheckPath As Boolean = False

        For Each ImageData As LoadedImageData In ComboImages.Items
            Dim CurrentPathName As String = IO.Path.GetDirectoryName(ImageData.DisplayPath)
            If CheckPath Then
                If Len(CurrentPathName) > Len(PathName) Then
                    CurrentPathName = Strings.Left(CurrentPathName, Len(PathName))
                End If
                Do While PathName <> CurrentPathName
                    PathName = IO.Path.GetDirectoryName(PathName)
                    CurrentPathName = IO.Path.GetDirectoryName(CurrentPathName)
                Loop
            Else
                PathName = CurrentPathName
            End If
            If PathName = "" Then
                Exit For
            End If
            CheckPath = True
        Next
        PathName = PathAddBackslash(PathName)

        Return Len(PathName)
    End Function

    Private Function GetWindowCaption() As String
        Return My.Application.Info.ProductName & " v" & _FileVersion
    End Function

    Private Sub HexDisplayBadSectors()
        Dim HexViewSectorData = HexViewBadSectors(_Disk)

        If DisplayHexViewForm(HexViewSectorData) Then
            ComboItemRefresh(False, True)
        End If
    End Sub

    Private Sub HexDisplayBootSector()
        Dim HexViewSectorData = HexViewBootSector(_Disk)

        If DisplayHexViewForm(HexViewSectorData) Then
            DiskImageProcess(True, True)
        End If
    End Sub

    Private Sub HexDisplayDirectoryEntry(Offset As UInteger)
        Dim HexViewSectorData = HexViewDirectoryEntry(_Disk, Offset)

        If HexViewSectorData IsNot Nothing Then
            If DisplayHexViewForm(HexViewSectorData) Then
                ComboItemRefresh(True, True)
            End If
        End If
    End Sub

    Private Sub HexDisplayDiskImage()
        Dim HexViewSectorData = New HexViewSectorData(_Disk, 0, _Disk.Data.Length) With {
            .Description = "Disk"
        }

        If DisplayHexViewForm(HexViewSectorData, True, True, False) Then
            ComboItemRefresh(True, True)
        End If
    End Sub

    Private Sub HexDisplayFAT()
        Dim HexViewSectorData = HexViewFAT(_Disk)

        If DisplayHexViewForm(HexViewSectorData, True) Then
            DiskImageProcess(True, True)
        End If
    End Sub

    Private Sub HexDisplayUnusedClusters()
        Dim HexViewSectorData = New HexViewSectorData(_Disk, _Disk.FAT.GetUnusedClusters(True)) With {
            .Description = "Unused Clusters"
        }

        If DisplayHexViewForm(HexViewSectorData) Then
            ComboItemRefresh(False, True)
        End If
    End Sub

    Private Sub ImageCountUpdate()
        If _FiltersApplied Then
            ToolStripImageCount.Text = ComboImagesFiltered.Items.Count & " Of " & ComboImages.Items.Count & " Image" & IIf(ComboImages.Items.Count <> 1, "s", "")
        Else
            ToolStripImageCount.Text = ComboImages.Items.Count & " Image" & IIf(ComboImages.Items.Count <> 1, "s", "")
        End If
    End Sub

    Private Sub InitButtonState()
        If _Disk IsNot Nothing Then
            BtnDisplayBootSector.Enabled = _Disk.CheckSize
            BtnEditBootSector.Enabled = _Disk.CheckSize
            BtnDisplayDisk.Enabled = _Disk.CheckSize
            BtnDisplayFAT.Enabled = _Disk.IsValidImage
            BtnEditFAT.Enabled = _Disk.IsValidImage
            BtnDisplayDirectory.Enabled = _Disk.IsValidImage
        Else
            BtnDisplayBootSector.Enabled = False
            BtnDisplayDisk.Enabled = False
            BtnDisplayFAT.Enabled = False
            BtnEditBootSector.Enabled = False
            BtnEditFAT.Enabled = False
            BtnDisplayDirectory.Enabled = False
        End If
        BtnWin9xClean.Enabled = False

        MenuDisplayDirectorySubMenuClear()
        FATSubMenuRefresh()

        RefreshSaveButtons()
    End Sub

    Private Function IsFileReadOnly(fileName As String) As Boolean
        Dim fInfo As New FileInfo(fileName)
        Return fInfo.IsReadOnly
    End Function

    Private Function IsWin9xDisk(Disk As Disk) As Boolean
        Dim Response As Boolean = False
        Dim OEMNameWin9x = Disk.BootSector.IsWin9xOEMName
        Dim OEMName = Disk.BootSector.OEMName

        If OEMNameWin9x Then
            Dim BootstrapType = _BootStrap.FindMatch(Disk.BootSector.BootStrapCode)

            If BootstrapType IsNot Nothing Then
                For Each KnownOEMName In BootstrapType.KnownOEMNames
                    If KnownOEMName.Name.CompareTo(OEMName) Or KnownOEMName.Win9xId Then
                        Return True
                        Exit For
                    End If
                Next
            End If
        End If

        Return Response
    End Function

    Private Function IsZipArchive(FileName As String) As ZipArchive
        Try
            Dim Buffer(1) As Byte
            Using fs = New FileStream(FileName, FileMode.Open, FileAccess.Read)
                Dim BytesRead = fs.Read(Buffer, 0, Buffer.Length)
                fs.Close()
            End Using

            If Buffer(0) = &H50 And Buffer(1) = &H4B Then
                Return ZipFile.OpenRead(FileName)
            End If
        Catch ex As Exception
            Return Nothing
        End Try

        Return Nothing
    End Function

    Private Sub ItemScan(Type As ItemScanTypes, Disk As DiskImage.Disk, ImageData As LoadedImageData, Optional UpdateFilters As Boolean = False, Optional Remove As Boolean = False)
        ItemScanModified(Disk, ImageData, UpdateFilters, Remove)

        If UpdateFilters Then
            RefreshModifiedState()
        End If

        If ImageData.Scanned Then
            If Type And ItemScanTypes.Disk Then
                ItemScanDisk(Disk, ImageData, UpdateFilters, Remove)
            End If
            If Type And ItemScanTypes.OEMName Then
                ItemScanOEMName(Disk, ImageData, UpdateFilters, Remove)
                OEMNameFilterUpdate(Disk, ImageData, UpdateFilters, Remove)
            End If
            If Type And ItemScanTypes.DiskType Then
                DiskTypeFilterUpdate(Disk, ImageData, UpdateFilters, Remove)
            End If
            If Type And ItemScanTypes.UnusedClusters Then
                ItemScanUnusedClusters(Disk, ImageData, UpdateFilters, Remove)
            End If
            If Type And ItemScanTypes.Directory Then
                ItemScanDirectory(Disk, ImageData, UpdateFilters, Remove)
            End If
        End If
    End Sub

    Private Sub ItemSelectionChanged()
        RefreshFileButtons()
        FileInfoUpdate()
        RefreshCheckAll()
    End Sub

    Private Function ListViewFilesAddGroup(Directory As DiskImage.IDirectory, Path As String) As ListViewGroup
        Dim FileCount As UInteger = Directory.Data.FileCount
        Dim GroupName As String = IIf(Path = "", "(Root)", Path)
        GroupName = GroupName & "  (" & FileCount & IIf(FileCount <> 1, " entries", " entry") _
            & IIf(Directory.Data.HasBootSector, ", Boot Sector", "") _
            & IIf(Directory.Data.HasAdditionalData, ", Additional Data", "") _
            & ")"
        Dim Group = New ListViewGroup(GroupName)
        ListViewFiles.Groups.Add(Group)

        Return Group
    End Function

    Private Sub ListViewFilesAddItem(FilePath As String, DirectoryEntry As DirectoryEntry, LFNFileName As String, ParentOffset As UInteger, Index As Integer, Count As Integer, Group As ListViewGroup)
        Dim IsLastEntry = (Index = Count - 1)
        Dim FileData = GetFileDataFromDirectoryEntry(Index, DirectoryEntry, ParentOffset, FilePath, IsLastEntry, LFNFileName)
        Dim Item = ListViewFilesGetItem(Group, FileData)
        ListViewFiles.Items.Add(Item)
    End Sub

    Private Function ListViewFilesGetItem(Group As ListViewGroup, FileData As FileData) As ListViewItem
        Dim SI As ListViewItem.ListViewSubItem
        Dim ForeColor As Color
        Dim IsDeleted As Boolean = FileData.DirectoryEntry.IsDeleted
        Dim HasInvalidFileSize As Boolean = FileData.DirectoryEntry.HasInvalidFileSize

        Dim Attrib As String = IIf(FileData.DirectoryEntry.IsArchive, "A ", "- ") _
            & IIf(FileData.DirectoryEntry.IsReadOnly, "R ", "- ") _
            & IIf(FileData.DirectoryEntry.IsSystem, "S ", "- ") _
            & IIf(FileData.DirectoryEntry.IsHidden, "H ", "- ") _
            & IIf(FileData.DirectoryEntry.IsDirectory, "D ", "- ") _
            & IIf(FileData.DirectoryEntry.IsVolumeName, "V ", "- ")

        If IsDeleted Then
            ForeColor = Color.Gray
        ElseIf FileData.DirectoryEntry.IsValidValumeName Then
            ForeColor = Color.Green
        ElseIf FileData.DirectoryEntry.IsDirectory And Not FileData.DirectoryEntry.IsVolumeName Then
            ForeColor = Color.Blue
        End If

        Dim ModifiedString As String = IIf(FileData.DirectoryEntry.IsModified, "#", "")

        Dim Item = New ListViewItem(ModifiedString, Group) With {
            .UseItemStyleForSubItems = False,
            .Tag = FileData
        }
        If ModifiedString = "" Then
            Item.ForeColor = ForeColor
        Else
            Item.ForeColor = Color.Blue
        End If

        SI = Item.SubItems.Add(FileData.DirectoryEntry.GetFileName)
        SI.Name = "FileName"
        If Not IsDeleted And FileData.DirectoryEntry.HasInvalidFilename Then
            SI.ForeColor = Color.Red
        Else
            SI.ForeColor = ForeColor
        End If

        SI = Item.SubItems.Add(FileData.DirectoryEntry.GetFileExtension)
        SI.Name = "FileExtension"
        If Not IsDeleted And FileData.DirectoryEntry.HasInvalidExtension Then
            SI.ForeColor = Color.Red
        Else
            SI.ForeColor = ForeColor
        End If

        If HasInvalidFileSize Then
            SI = Item.SubItems.Add("Invalid")
            If Not IsDeleted Then
                SI.ForeColor = Color.Red
            Else
                SI.ForeColor = ForeColor
            End If
        ElseIf Not IsDeleted And FileData.DirectoryEntry.HasIncorrectFileSize Then
            SI = Item.SubItems.Add(Format(FileData.DirectoryEntry.FileSize, "N0"))
            SI.ForeColor = Color.Red
        Else
            SI = Item.SubItems.Add(Format(FileData.DirectoryEntry.FileSize, "N0"))
            SI.ForeColor = ForeColor
        End If
        SI.Name = "FileSize"

        SI = Item.SubItems.Add(ExpandedDateToString(FileData.DirectoryEntry.GetLastWriteDate, True, True, False, True))
        SI.Name = "FileLastWriteDate"
        If FileData.DirectoryEntry.GetLastWriteDate.IsValidDate Or IsDeleted Then
            SI.ForeColor = ForeColor
        Else
            SI.ForeColor = Color.Red
        End If

        Dim ErrorText As String = ""
        Dim SubItemForeColor As Color = ForeColor
        If FileData.DirectoryEntry.HasInvalidStartingCluster Then
            SI = Item.SubItems.Add("Invalid")
            If Not IsDeleted Then
                SubItemForeColor = Color.Red
            End If
        Else
            SI = Item.SubItems.Add(Format(FileData.DirectoryEntry.StartingCluster, "N0"))
        End If
        SI.Name = "FileStartingCluster"

        If Not IsDeleted And FileData.DirectoryEntry.IsCrossLinked Then
            SubItemForeColor = Color.Red
            ErrorText = "CL"
        ElseIf Not IsDeleted And FileData.DirectoryEntry.HasCircularChain Then
            SubItemForeColor = Color.Red
            ErrorText = "CC"
        End If
        SI.ForeColor = SubItemForeColor

        SI = Item.SubItems.Add(ErrorText)
        SI.Name = "FileClusterError"
        SI.ForeColor = Color.Red

        SI = Item.SubItems.Add(Attrib)
        If Not IsDeleted And FileData.DirectoryEntry.HasInvalidAttributes Then
            SI.ForeColor = Color.Red
        Else
            SI.ForeColor = ForeColor
        End If

        If FileData.DirectoryEntry.IsValidFile Then
            SI = Item.SubItems.Add(Crc32.ComputeChecksum(FileData.DirectoryEntry.GetContent).ToString("X8"))
        Else
            SI = Item.SubItems.Add("")
        End If
        SI.ForeColor = ForeColor

        If FileData.DirectoryEntry.HasCreationDate Then
            SI = Item.SubItems.Add(ExpandedDateToString(FileData.DirectoryEntry.GetCreationDate, True, True, True, True))
            If FileData.DirectoryEntry.GetCreationDate.IsValidDate Or IsDeleted Then
                SI.ForeColor = ForeColor
            Else
                SI.ForeColor = Color.Red
            End If
        Else
            SI = Item.SubItems.Add("")
        End If
        SI.Name = "FileCreationDate"

        If FileData.DirectoryEntry.HasLastAccessDate Then
            SI = Item.SubItems.Add(ExpandedDateToString(FileData.DirectoryEntry.GetLastAccessDate))
            If FileData.DirectoryEntry.GetLastAccessDate.IsValidDate Or IsDeleted Then
                SI.ForeColor = ForeColor
            Else
                SI.ForeColor = Color.Red
            End If
        Else
            SI = Item.SubItems.Add("")
        End If
        SI.Name = "FileLastAccessDate"

        SI = Item.SubItems.Add(FileData.LFNFileName)
        SI.ForeColor = ForeColor

        Return Item
    End Function

    Private Sub ListViewFilesRefreshItem(Index As Integer)
        Dim Item = ListViewFiles.Items(Index)
        Dim FileData As FileData = Item.Tag

        If FileData.DirectoryEntry.IsEmpty Then
            ListViewFiles.Items.Remove(Item)
        Else
            Dim NewItem = ListViewFilesGetItem(Item.Group, FileData)
            For Counter = 0 To Item.SubItems.Count - 1
                Item.SubItems(Counter) = NewItem.SubItems(Counter)
            Next
        End If
    End Sub

    Private Function MediaTypeGet(MediaDescriptor As Byte, SectorsPerTrack As UShort, SectorsPerFAT As UShort) As String
        Select Case MediaDescriptor
            Case &HF0
                If SectorsPerTrack = 36 Then
                    Return "2.88M Floppy"
                ElseIf SectorsPerTrack = 21 Then
                    If SectorsPerFAT = 5 Then
                        Return "DMF (1024) Floppy"
                    ElseIf SectorsPerFAT = 3 Then
                        Return "DMF (2048) Floppy"
                    End If
                ElseIf SectorsPerTrack = 18 Then
                    Return "1.44M Floppy"
                End If
            Case &HF8
                Return "Fixed Disk"
            Case &HF9
                If SectorsPerTrack = 15 Then
                    Return "1.2M Floppy"
                ElseIf SectorsPerTrack = 9 Then
                    Return "720KB Floppy"
                End If
            Case &HFC
                Return "180KB Floppy"
            Case &HFD
                Return "360KB Floppy"
            Case &HFE
                Return "160KB Floppy"
            Case &HFF
                Return "320KB Floppy"
        End Select

        Return MediaDescriptor.ToString("X2") & " Hex"
    End Function

    Private Sub MenuDisplayDirectorySubMenuClear()
        For Each Item As ToolStripMenuItem In BtnDisplayDirectory.DropDownItems
            RemoveHandler Item.Click, AddressOf BtnDisplayDirectory_Click
        Next
        BtnDisplayDirectory.DropDownItems.Clear()
        BtnDisplayDirectory.Text = "Root &Directory"
    End Sub

    Private Sub MenuDisplayDirectorySubMenuItemAdd(Path As String, Offset As UInteger, Index As Integer)
        Dim Item As New ToolStripMenuItem With {
            .Text = Path,
            .Tag = Offset
        }
        If Index = -1 Then
            BtnDisplayDirectory.DropDownItems.Add(Item)
        Else
            BtnDisplayDirectory.DropDownItems.Insert(Index, Item)
        End If
        AddHandler Item.Click, AddressOf BtnDisplayDirectory_Click
    End Sub

    Private Function MsgBoxNewFileName(FileName As String) As MsgBoxResult
        Dim Msg As String = "'" & FileName & "' is a read-only file.  Please specify a new file name."
        Return MsgBox(Msg, MsgBoxStyle.OkCancel)
    End Function

    Private Function MsgBoxOverwrite(FilePath As String) As MyMsgBoxResult
        Dim MSg As String = IO.Path.GetFileName(FilePath) & " already exists." & vbCrLf & "Do you want to replace it?"

        Dim SaveAllForm As New SaveAllForm(MSg)
        SaveAllForm.ShowDialog()
        Return SaveAllForm.Result
    End Function

    Private Function MsgBoxSave(FileName As String) As MsgBoxResult
        Dim Msg As String = "Save file '" & FileName & "'?"
        Return MsgBox(Msg, MsgBoxStyle.Question + MsgBoxStyle.YesNoCancel + MsgBoxStyle.DefaultButton3, "Save")
    End Function

    Private Function MsgBoxSaveAll(FileName As String) As MyMsgBoxResult
        Dim Msg As String = "Save file '" & FileName & "'?"

        Dim SaveAllForm As New SaveAllForm(Msg)
        SaveAllForm.ShowDialog()
        Return SaveAllForm.Result
    End Function

    Private Sub OEMNameFilterAdd(OEMName As String, UpdateFilters As Boolean)
        If OEMName.EndsWith("IHC") Then
            Return
        End If

        _FilterOEMName.Add(OEMName, UpdateFilters)
    End Sub

    Private Sub OEMNameFilterReset()
        _FilterOEMName.Clear()
        For Each ImageData As LoadedImageData In ComboImages.Items
            OEMNameFilterAdd(ImageData.ScanInfo.OEMName, False)
        Next
        _FilterOEMName.Populate()
    End Sub
    Private Sub PopulateFilesPanel()
        MenuDisplayDirectorySubMenuClear()

        ListViewFiles.BeginUpdate()

        ListViewFiles.ListViewItemSorter = Nothing
        ListViewFiles.Items.Clear()
        ListViewFiles.MultiSelect = True

        If BtnDisplayDirectory.DropDownItems.Count > 0 Then
            BtnDisplayDirectory.Text = "Directory"
            MenuDisplayDirectorySubMenuItemAdd("(Root)", 0, 0)
            BtnDisplayDirectory.Tag = Nothing
        Else
            BtnDisplayDirectory.Tag = 0
        End If

        Dim Response = ProcessDirectoryEntries(_Disk.Directory, 0, "", False)
        ProcessDirectoryScanResponse(Response)

        ListViewFiles.ListViewItemSorter = _lvwColumnSorter
        ListViewFiles.EndUpdate()

        ItemSelectionChanged()
    End Sub

    Private Sub PopulateHashPanel(Disk As Disk)
        ListViewHashes.BeginUpdate()

        With ListViewHashes.Items
            .Clear()
            If Disk IsNot Nothing Then
                .AddItem("CRC32", Crc32.ComputeChecksum(Disk.Data.Data).ToString("X8"))
                .AddItem("MD5", MD5Hash(Disk.Data.Data))
                .AddItem("SHA-1", SHA1Hash(Disk.Data.Data))
            End If
        End With

        ListViewHashes.EndUpdate()
        ListViewHashes.Refresh()
    End Sub

    Private Sub PopulateSummary(Disk As Disk, ImageData As LoadedImageData)
        Dim FileName = ImageData.FileName

        Me.Text = GetWindowCaption() & " - " & FileName

        ToolStripFileName.Text = FileName
        ToolStripFileName.Visible = True

        PopulateSummaryPanel(Disk)
        PopulateHashPanel(Disk)
        RefreshButtonStatus(Disk, ImageData)
    End Sub

    Private Sub PopulateSummaryPanel(Disk As Disk)
        Dim Value As String
        Dim ForeColor As Color

        ListViewSummary.BeginUpdate()

        With ListViewSummary.Items
            .Clear()

            Dim DiskGroup = ListViewSummary.Groups.Add("Disk", "Disk")

            If Disk IsNot Nothing Then
                Dim BootStrapStart = Disk.BootSector.GetBootStrapOffset
                Dim BootstrapType As BootstrapLookup = Nothing
                Dim KnownOEMNameMatch As KnownOEMName = Nothing
                Dim OEMName() As Byte = Nothing

                If Disk.IsValidImage Then
                    Dim OEMNameWin9x = Disk.BootSector.IsWin9xOEMName
                    OEMName = Disk.BootSector.OEMName

                    BootstrapType = _BootStrap.FindMatch(Disk.BootSector.BootStrapCode)

                    If BootstrapType IsNot Nothing Then
                        Dim Win9xOEMName As KnownOEMName = Nothing
                        For Each KnownOEMName In BootstrapType.KnownOEMNames
                            If OEMNameWin9x And KnownOEMName.Win9xId Then
                                Win9xOEMName = KnownOEMName
                            End If
                            If KnownOEMName.Name.CompareTo(OEMName) Then
                                KnownOEMNameMatch = KnownOEMName
                                Exit For
                            End If
                        Next
                        If KnownOEMNameMatch Is Nothing And Win9xOEMName IsNot Nothing Then
                            KnownOEMNameMatch = Win9xOEMName
                        End If
                        If KnownOEMNameMatch Is Nothing And BootstrapType.ExactMatch Then
                            BootstrapType = Nothing
                        End If
                    End If
                End If

                If Disk.IsValidImage AndAlso Disk.CheckImageSize <> 0 Then
                    ForeColor = Color.Red
                Else
                    ForeColor = SystemColors.WindowText
                End If
                .AddItem(DiskGroup, "Image Size", Disk.Data.Length.ToString("N0"), ForeColor)

                Dim DiskType As String
                If Disk.IsValidImage Then
                    DiskType = GetFloppyDiskTypeName(Disk.BootSector)
                Else
                    DiskType = GetFloppyDiskTypeName(Disk.Data.Length)
                End If
                DiskType &= " Floppy"
                .AddItem(DiskGroup, "Disk Type", DiskType)

                If Disk.IsValidImage Then
                    Dim BadSectors = GetBadSectors(Disk.BootSector, Disk.FAT.BadClusters)
                    Dim CopyProtection As String = GetCopyProtection(Disk, BadSectors)

                    If CopyProtection.Length > 0 Then
                        .AddItem(DiskGroup, "Copy Protection", CopyProtection)
                    End If

                    Dim BroderbundCopyright = GetBroderbundCopyright(Disk, BadSectors)

                    If BroderbundCopyright <> "" Then
                        .AddItem(DiskGroup, "Broderbund Copyright", BroderbundCopyright)
                    End If
                Else
                    .AddItem(DiskGroup, "Error", "Unknown Image Format", Color.Red)
                End If

                Dim BootRecordGroup = ListViewSummary.Groups.Add("BootRecord", "Boot Record")

                If BootstrapType IsNot Nothing Then
                    If KnownOEMNameMatch Is Nothing Then
                        ForeColor = Color.Red
                    Else
                        ForeColor = Color.Green
                    End If
                Else
                    ForeColor = SystemColors.WindowText
                End If

                .AddItem(BootRecordGroup, BootSectorDescription(BootSectorOffsets.OEMName), Disk.BootSector.GetOEMNameString.TrimEnd(NULL_CHAR), ForeColor)
                .AddItem(BootRecordGroup, BootSectorDescription(BootSectorOffsets.BytesPerSector), Disk.BootSector.BytesPerSector)
                .AddItem(BootRecordGroup, BootSectorDescription(BootSectorOffsets.SectorsPerCluster), Disk.BootSector.SectorsPerCluster)
                .AddItem(BootRecordGroup, BootSectorDescription(BootSectorOffsets.ReservedSectorCount), Disk.BootSector.ReservedSectorCount)

                Value = Disk.BootSector.NumberOfFATs
                If Disk.BootSector.IsValidImage AndAlso Not Disk.CompareFATTables Then
                    Value &= " (Mismatched)"
                    ForeColor = Color.Red
                Else
                    ForeColor = SystemColors.WindowText
                End If

                .AddItem(BootRecordGroup, BootSectorDescription(BootSectorOffsets.NumberOfFATs), Value, ForeColor)
                .AddItem(BootRecordGroup, BootSectorDescription(BootSectorOffsets.RootEntryCount), Disk.BootSector.RootEntryCount)
                .AddItem(BootRecordGroup, BootSectorDescription(BootSectorOffsets.SectorCountSmall), Disk.BootSector.SectorCount)
                .AddItem(BootRecordGroup, BootSectorDescription(BootSectorOffsets.MediaDescriptor), Disk.BootSector.MediaDescriptor.ToString("X2") & " Hex")
                .AddItem(BootRecordGroup, BootSectorDescription(BootSectorOffsets.SectorsPerFAT), Disk.BootSector.SectorsPerFAT)
                .AddItem(BootRecordGroup, BootSectorDescription(BootSectorOffsets.SectorsPerTrack), Disk.BootSector.SectorsPerTrack)
                .AddItem(BootRecordGroup, BootSectorDescription(BootSectorOffsets.NumberOfHeads), Disk.BootSector.NumberOfHeads)

                'If Debugger.IsAttached Then
                If Disk.BootSector.HiddenSectors > 0 Then
                    .AddItem(BootRecordGroup, BootSectorDescription(BootSectorOffsets.HiddenSectors), Disk.BootSector.HiddenSectors)
                End If
                'End If

                If BootStrapStart >= BootSectorOffsets.BootStrapCode Then
                    If Disk.BootSector.DriveNumber > 0 Then
                        .AddItem(BootRecordGroup, BootSectorDescription(BootSectorOffsets.DriveNumber), Disk.BootSector.DriveNumber)
                    End If

                    If Disk.BootSector.HasValidExtendedBootSignature Then
                        .AddItem(BootRecordGroup, BootSectorDescription(BootSectorOffsets.VolumeSerialNumber), Disk.BootSector.VolumeSerialNumber.ToString("X8").Insert(4, "-"))
                        If Disk.BootSector.ExtendedBootSignature = BootSector.ValidExtendedBootSignature(1) Then
                            .AddItem(BootRecordGroup, BootSectorDescription(BootSectorOffsets.VolumeLabel), Disk.BootSector.GetVolumeLabelString.TrimEnd(NULL_CHAR))
                            .AddItem(BootRecordGroup, BootSectorDescription(BootSectorOffsets.FileSystemType), Disk.BootSector.GetFileSystemTypeString)
                        End If
                    End If
                End If

                If Disk.BootSector.BootStrapSignature <> BootSector.ValidBootStrapSignature Then
                    .AddItem(BootRecordGroup, BootSectorDescription(BootSectorOffsets.BootStrapSignature), Disk.BootSector.BootStrapSignature.ToString("X4"))
                End If

                If Not Disk.BootSector.HasValidJumpInstruction(True) Then
                    ForeColor = Color.Red
                Else
                    ForeColor = SystemColors.WindowText
                End If

                If Debugger.IsAttached Then
                    .AddItem(BootRecordGroup, BootSectorDescription(BootSectorOffsets.JmpBoot), BitConverter.ToString(Disk.BootSector.JmpBoot), ForeColor)
                End If

                If Disk.IsValidImage Then
                    Dim FileSystemGroup = ListViewSummary.Groups.Add("FileSystem", "File System")

                    Dim VolumeLabel = Disk.GetVolumeLabel

                    If VolumeLabel IsNot Nothing Then
                        .AddItem(FileSystemGroup, "Volume Label", VolumeLabel.GetVolumeName.TrimEnd(NULL_CHAR))
                        Dim VolumeDate = VolumeLabel.GetLastWriteDate
                        .AddItem(FileSystemGroup, "Volume Date", ExpandedDateToString(VolumeDate, True, False, False, False))
                    End If

                    .AddItem(FileSystemGroup, "Total Space", Format(Disk.SectorToBytes(Disk.BootSector.DataRegionSize), "N0") & " bytes")
                    .AddItem(FileSystemGroup, "Free Space", Format(Disk.FAT.GetFreeSpace(Disk.BootSector.BytesPerCluster), "N0") & " bytes")

                    If Disk.FAT.BadClusters.Count > 0 Then
                        Value = Format(Disk.FAT.BadClusters.Count * Disk.BootSector.BytesPerCluster, "N0") & " bytes"
                        ForeColor = Color.Red
                        .AddItem(FileSystemGroup, "Bad Sectors", Value, ForeColor)
                    End If

                    Dim BootStrapGroup = ListViewSummary.Groups.Add("Bootstrap", "Bootstrap")

                    If Debugger.IsAttached Then
                        .AddItem(BootStrapGroup, "Bootstrap CRC32", Crc32.ComputeChecksum(Disk.BootSector.BootStrapCode).ToString("X8"))
                    End If

                    If BootstrapType IsNot Nothing Then
                        If BootstrapType.Language.Length > 0 Then
                            .AddItem(BootStrapGroup, "Language", BootstrapType.Language)
                        End If

                        If KnownOEMNameMatch Is Nothing And BootstrapType.KnownOEMNames.Count = 1 Then
                            KnownOEMNameMatch = BootstrapType.KnownOEMNames(0)
                        End If

                        If KnownOEMNameMatch IsNot Nothing Then
                            If KnownOEMNameMatch.Company <> "" Then
                                .AddItem(BootStrapGroup, "Company", KnownOEMNameMatch.Company)
                            End If
                            If KnownOEMNameMatch.Description <> "" Then
                                .AddItem(BootStrapGroup, "Description", KnownOEMNameMatch.Description)
                            End If
                            If KnownOEMNameMatch.Note <> "" Then
                                .AddItem(BootStrapGroup, "Note", KnownOEMNameMatch.Note, Color.Blue)
                            End If
                        End If

                        If Not BootstrapType.ExactMatch Then
                            For Each KnownOEMName In BootstrapType.KnownOEMNames
                                If KnownOEMName.Name.Length > 0 AndAlso KnownOEMName.Suggestion AndAlso Not KnownOEMName.Name.CompareTo(OEMName) Then
                                    .AddItem(BootStrapGroup, "Alternative OEM Name", KnownOEMName.GetNameAsString)
                                End If
                            Next
                        End If
                    End If
                End If
            Else
                .AddItem(DiskGroup, "Error", "Error Loading File", Color.Red)
            End If

        End With

        ListViewSummary.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent)
        ListViewSummary.EndUpdate()
        ListViewSummary.Refresh()
    End Sub

    Private Sub PositionForm()
        Dim WorkingArea = Screen.FromControl(Me).WorkingArea
        Dim Width As Integer = Me.Width
        Dim Height As Integer = Me.Height

        If My.Settings.WindowWidth > 0 Then
            Width = My.Settings.WindowWidth
        End If
        If My.Settings.WindowHeight > 0 Then
            Height = My.Settings.WindowHeight
        End If

        Width = Math.Min(Width, WorkingArea.Width)
        Height = Math.Min(Height, WorkingArea.Height)

        Me.Size = New Size(Width, Height)
        Me.Location = New Point(WorkingArea.Left + (WorkingArea.Width - Width) / 2, WorkingArea.Top + (WorkingArea.Height - Height) / 2)
    End Sub

    Private Function ProcessDirectoryEntries(Directory As DiskImage.IDirectory, Offset As UInteger, Path As String, ScanOnly As Boolean) As DirectoryScanResponse
        Dim Response As New DirectoryScanResponse(Directory)
        Dim Group As ListViewGroup = Nothing

        If Not ScanOnly Then
            Group = ListViewFilesAddGroup(Directory, Path)
        End If

        Dim DirectoryEntryCount = Directory.Data.EntryCount

        If DirectoryEntryCount > 0 Then
            Dim LFNFileName As String = ""

            For Counter = 0 To DirectoryEntryCount - 1
                Dim DirectoryEntry = Directory.GetFile(Counter)

                If Not DirectoryEntry.IsLink Then
                    If DirectoryEntry.IsLFN Then
                        LFNFileName = DirectoryEntry.GetLFNFileName & LFNFileName
                    Else
                        If Not ScanOnly Then
                            ListViewFilesAddItem(Path, DirectoryEntry, LFNFileName, Offset, Counter, DirectoryEntryCount, Group)
                        End If

                        Response.ProcessDirectoryEntry(DirectoryEntry, LFNFileName)

                        LFNFileName = ""
                    End If

                    If DirectoryEntry.IsDirectory And DirectoryEntry.SubDirectory IsNot Nothing Then
                        If DirectoryEntry.SubDirectory.Data.EntryCount > 0 Then
                            Dim NewPath = DirectoryEntry.GetFullFileName
                            If Path <> "" Then
                                NewPath = Path & "\" & NewPath
                            End If

                            If Not ScanOnly Then
                                MenuDisplayDirectorySubMenuItemAdd(NewPath, DirectoryEntry.Offset, -1)
                            End If

                            Dim SubResponse = ProcessDirectoryEntries(DirectoryEntry.SubDirectory, DirectoryEntry.Offset, NewPath, ScanOnly)
                            Response.Combine(SubResponse)
                        End If
                    End If
                End If
            Next
        End If

        Return Response
    End Function

    Private Sub ProcessDirectoryScanResponse(Response As DirectoryScanResponse)
        If Response.HasCreated Then
            FileCreateDate.Width = 140
        Else
            FileCreateDate.Width = 0
        End If

        If Response.HasLastAccessed Then
            FileLastAccessDate.Width = 90
        Else
            FileLastAccessDate.Width = 0
        End If

        If Response.HasLFN Then
            FileLFN.Width = 200
        Else
            FileLFN.Width = 0
        End If

        If Response.HasFATChainingErrors Then
            FileClusterError.Width = 30
            RefreshClusterErrors()
        Else
            FileClusterError.Width = 0
        End If

        BtnWin9xClean.Enabled = Response.HasValidCreated Or Response.HasValidLastAccessed Or _Disk.BootSector.IsWin9xOEMName
    End Sub

    Private Sub ProcessFile(FileName As String, ByRef SelectedImageData As LoadedImageData)
        If Not File.Exists(FileName) Then
            Return
        End If

        Dim Archive = IsZipArchive(FileName)

        If Archive IsNot Nothing Then
            ProcessZipFile(FileName, Archive, SelectedImageData)
            Exit Sub
        End If

        If Not _LoadedFileNames.ContainsKey(FileName) Then
            Dim ImageData As New LoadedImageData(FileName)
            _LoadedFileNames.Add(FileName, ImageData)
            ComboImages.Items.Add(ImageData)
            If SelectedImageData Is Nothing Then
                SelectedImageData = ImageData
            End If
        End If
    End Sub

    Private Sub ProcessFileDrop(File As String)
        Dim Files(0) As String
        Files(0) = File

        ProcessFileDrop(Files)
    End Sub

    Private Sub ProcessFileDrop(Files() As String)
        Dim FilePath As String
        Dim FileInfo As IO.FileInfo
        Dim SelectedImageData As LoadedImageData = Nothing

        Cursor.Current = Cursors.WaitCursor

        If _FiltersApplied Then
            FiltersClear(False)
        End If

        ComboImages.BeginUpdate()

        LoadedImageData.StringOffset = 0

        For Each FilePath In Files.OrderBy(Function(f) f)
            Dim FAttributes = IO.File.GetAttributes(FilePath)
            If (FAttributes And IO.FileAttributes.Directory) > 0 Then
                Dim DirectoryInfo As New IO.DirectoryInfo(FilePath)
                For Each FileInfo In DirectoryInfo.GetFiles("*.*", IO.SearchOption.AllDirectories)
                    Dim Extension = FileInfo.Extension.ToLower
                    If _FileFilterExt.Contains(Extension) OrElse _ArchiveFilterExt.Contains(Extension) Then
                        ProcessFile(FileInfo.FullName, SelectedImageData)
                    End If
                Next
            Else
                ProcessFile(FilePath, SelectedImageData)
            End If
        Next

        LoadedImageData.StringOffset = GetPathOffset()

        If SelectedImageData IsNot Nothing Then
            ComboImages.Enabled = True
            ComboImages.DrawMode = DrawMode.OwnerDrawFixed
            ComboImagesRefreshItemText()
            ImageCountUpdate()

            ComboImages.SelectedItem = SelectedImageData
            If ComboImages.SelectedIndex = -1 Then
                ComboImages.SelectedIndex = 0
            End If

            SetImagesLoaded(True)
        End If

        ComboImages.EndUpdate()

        Cursor.Current = Cursors.Default
    End Sub

    Private Sub ProcessZipFile(FileName As String, Archive As ZipArchive, ByRef SelectedImageData As LoadedImageData)
        For Each Entry In Archive.Entries.OrderBy(Function(e) e.FullName)
            If _FileFilterExt.Contains(Path.GetExtension(Entry.Name).ToLower) Then
                Dim FilePath = Path.Combine(FileName, Entry.FullName)
                If Not _LoadedFileNames.ContainsKey(FilePath) Then
                    Dim ImageData As New LoadedImageData(FileName) With {
                        .Compressed = True,
                        .CompressedFile = Entry.FullName
                    }
                    _LoadedFileNames.Add(FilePath, ImageData)
                    ComboImages.Items.Add(ImageData)
                    If SelectedImageData Is Nothing Then
                        SelectedImageData = ImageData
                    End If
                End If
            End If
        Next
    End Sub

    Private Sub RefreshButtonStatus(Disk As Disk, ImageData As LoadedImageData)
        If Disk IsNot Nothing AndAlso Disk.IsValidImage Then
            BtnDisplayClusters.Enabled = Disk.FAT.HasUnusedClusters(True)
            BtnDisplayBadSectors.Enabled = Disk.FAT.BadClusters.Count > 0
            BtnFixImageSize.Enabled = Disk.CheckImageSize <> 0
            If Disk.Directory.Data.HasBootSector Then
                Dim BootSectorBytes = Disk.Data.GetBytes(Disk.Directory.Data.BootSectorOffset, BootSector.BOOT_SECTOR_SIZE)
                BtnRestoreBootSector.Enabled = Not BootSectorBytes.CompareTo(Disk.BootSector.Data)
            Else
                BtnRestoreBootSector.Enabled = False
            End If
            BtnRemoveBootSector.Enabled = Disk.Directory.Data.HasBootSector
        Else
            BtnDisplayClusters.Enabled = False
            BtnDisplayBadSectors.Enabled = False
            BtnFixImageSize.Enabled = False
            BtnRestoreBootSector.Enabled = False
            BtnRemoveBootSector.Enabled = False
        End If

        BtnRevert.Enabled = Disk IsNot Nothing AndAlso Disk.Data.Modified

        BtnUndo.Enabled = Disk IsNot Nothing AndAlso Disk.Data.UndoEnabled
        ToolStripBtnUndo.Enabled = BtnUndo.Enabled

        BtnRedo.Enabled = Disk IsNot Nothing AndAlso Disk.Data.RedoEnabled
        ToolStripBtnRedo.Enabled = BtnRedo.Enabled

        If Disk IsNot Nothing Then
            ToolStripStatusModified.Visible = Disk.Data.Modified
            ToolStripStatusReadOnly.Visible = ImageData.ReadOnly
            ToolStripStatusReadOnly.Text = IIf(ImageData.Compressed, "Compressed", "Read Only")
        Else
            ToolStripStatusModified.Visible = False
            ToolStripStatusReadOnly.Visible = False
        End If
    End Sub

    Private Sub RefreshCheckAll()
        Dim CheckAll = (ListViewFiles.SelectedItems.Count = ListViewFiles.Items.Count And ListViewFiles.Items.Count > 0)
        If CheckAll <> _CheckAll Then
            _CheckAll = CheckAll
            ListViewFiles.Invalidate(New Rectangle(0, 0, 20, 20), True)
        End If
    End Sub

    Private Sub RefreshClusterErrors()
        For Each Item As ListViewItem In ListViewFiles.Items
            Dim FileData As FileData = Item.Tag
            If FileData.DirectoryEntry.IsCrossLinked Then
                Item.SubItems.Item("FileStartingCluster").ForeColor = Color.Red
                Item.SubItems.Item("FileClusterError").Text = "CL"
            End If
        Next
    End Sub

    Private Sub RefreshFileButtons()
        Dim Stats As DirectoryStats
        Dim DirectoryOffset As Integer = -1

        If ListViewFiles.SelectedItems.Count = 0 Then
            BtnExportFile.Text = "&Export File"
            BtnExportFile.Enabled = False

            BtnReplaceFile.Enabled = False
            BtnFileProperties.Enabled = False
            BtnFileMenuViewCrosslinked.Visible = False

            BtnFileMenuViewFileText.Visible = False
            BtnFileMenuViewFileText.Enabled = False

            BtnDisplayFile.Tag = Nothing
            BtnDisplayFile.Visible = False

            BtnFileMenuViewFile.Text = "&View File"
            BtnFileMenuViewFile.Enabled = False

            BtnFileMenuRemoveDeletedFile.Visible = False
            BtnFileMenuRemoveDeletedFile.Enabled = False

            BtnFileMenuDeleteFile.Visible = True
            BtnFileMenuDeleteFile.Enabled = False
            BtnFileMenuDeleteFile.Text = "&Delete File"

            BtnFileMenuDeleteFileWithFill.Visible = False
            BtnFileMenuDeleteFileWithFill.Enabled = False
            BtnFileMenuDeleteFileWithFill.Text = "&Delete File and Clear Sectors"

        ElseIf ListViewFiles.SelectedItems.Count = 1 Then
            Dim FileData As FileData = ListViewFiles.SelectedItems(0).Tag
            DirectoryOffset = FileData.ParentOffset
            Stats = DirectoryEntryGetStats(FileData.DirectoryEntry)

            BtnExportFile.Text = "&Export File"
            BtnExportFile.Enabled = Stats.CanExport

            BtnReplaceFile.Enabled = Stats.IsValidFile And Not Stats.IsDeleted
            BtnFileProperties.Enabled = True
            BtnFileMenuViewCrosslinked.Visible = FileData.DirectoryEntry.IsCrossLinked
            BtnFileMenuFixSize.Enabled = FileData.DirectoryEntry.HasIncorrectFileSize

            BtnFileMenuViewFileText.Visible = Stats.IsValidFile And Not Stats.IsDeleted
            BtnFileMenuViewFileText.Enabled = Stats.FileSize > 0

            If Stats.IsDeleted Then
                BtnFileMenuRemoveDeletedFile.Visible = True
                BtnFileMenuRemoveDeletedFile.Enabled = True

                BtnFileMenuDeleteFile.Visible = False
                BtnFileMenuDeleteFile.Enabled = False

                BtnFileMenuDeleteFileWithFill.Visible = False
                BtnFileMenuDeleteFileWithFill.Enabled = False
            Else
                BtnFileMenuRemoveDeletedFile.Visible = False
                BtnFileMenuRemoveDeletedFile.Enabled = False

                BtnFileMenuDeleteFile.Visible = True
                BtnFileMenuDeleteFile.Enabled = Stats.CanDelete
                BtnFileMenuDeleteFile.Text = "&Delete File"

                BtnFileMenuDeleteFileWithFill.Visible = Stats.CanDeleteWithFill
                BtnFileMenuDeleteFileWithFill.Enabled = Stats.CanDeleteWithFill
                BtnFileMenuDeleteFileWithFill.Text = "&Delete File and Clear Sectors"
            End If

            If Stats.IsValidFile Or Stats.IsValidDirectory Then
                If Stats.IsDeleted Then
                    BtnDisplayFile.Text = "Deleted &File:  " & Stats.FullFileName
                Else
                    BtnDisplayFile.Text = "&File:  " & Stats.FullFileName
                End If
                BtnDisplayFile.Tag = FileData.DirectoryEntry.Offset
                BtnDisplayFile.Visible = Not Stats.IsDirectory And Stats.FileSize > 0

                Dim Caption As String = "&View "
                If Stats.IsDeleted Then
                    Caption &= "Deleted "
                End If
                If Stats.IsDirectory Then
                    Caption &= "Directory"
                Else
                    Caption &= "File"
                End If
                BtnFileMenuViewFile.Text = Caption
                BtnFileMenuViewFile.Enabled = Stats.IsDirectory Or Stats.FileSize > 0
            Else
                BtnDisplayFile.Tag = Nothing
                BtnDisplayFile.Visible = False

                BtnFileMenuViewFile.Text = "&View File"
                BtnFileMenuViewFile.Enabled = False
            End If
        Else
            Dim FileData As FileData
            Dim ExportEnabled As Boolean = False
            Dim DeleteEnabled As Boolean = False
            FileData = ListViewFiles.SelectedItems(0).Tag
            DirectoryOffset = FileData.ParentOffset
            Dim ParentOffset As UInteger = FileData.ParentOffset

            For Each Item As ListViewItem In ListViewFiles.SelectedItems
                FileData = Item.Tag
                Stats = DirectoryEntryGetStats(FileData.DirectoryEntry)
                If Stats.CanExport Then
                    ExportEnabled = True
                End If
                If Not Stats.IsDeleted And Stats.CanDelete Then
                    DeleteEnabled = True
                End If
                If FileData.ParentOffset <> ParentOffset Then
                    DirectoryOffset = -1
                End If
            Next
            BtnExportFile.Text = "&Export Selected Files"
            BtnExportFile.Enabled = ExportEnabled

            BtnReplaceFile.Enabled = False
            BtnFileProperties.Enabled = True
            BtnFileMenuViewCrosslinked.Visible = False

            BtnFileMenuViewFileText.Visible = True
            BtnFileMenuViewFileText.Enabled = False

            BtnDisplayFile.Tag = Nothing
            BtnDisplayFile.Visible = False

            BtnFileMenuViewFile.Text = "&View File"
            BtnFileMenuViewFile.Enabled = False

            BtnFileMenuRemoveDeletedFile.Visible = False
            BtnFileMenuRemoveDeletedFile.Enabled = False

            BtnFileMenuDeleteFile.Visible = True
            BtnFileMenuDeleteFile.Enabled = DeleteEnabled
            BtnFileMenuDeleteFile.Text = "&Delete Selected Files"

            BtnFileMenuDeleteFileWithFill.Visible = True
            BtnFileMenuDeleteFileWithFill.Enabled = DeleteEnabled
            BtnFileMenuDeleteFileWithFill.Text = "&Delete Selected Files and Clear Sectors"
        End If

        BtnFileMenuExportFile.Text = BtnExportFile.Text
        BtnFileMenuExportFile.Enabled = BtnExportFile.Enabled
        ToolStripBtnExportFile.Enabled = BtnExportFile.Enabled
        BtnFileMenuReplaceFile.Enabled = BtnReplaceFile.Enabled
        BtnFileMenuFileProperties.Enabled = BtnFileProperties.Enabled
        ToolStripBtnFileProperties.Enabled = BtnFileProperties.Enabled
        ToolStripBtnViewFile.Text = BtnFileMenuViewFile.Text
        ToolStripBtnViewFile.Enabled = BtnFileMenuViewFile.Enabled
        ToolStripBtnViewFileText.Enabled = BtnFileMenuViewFileText.Enabled

        If DirectoryOffset = -1 Then
            BtnFileMenuViewDirectory.Visible = False
            BtnFileMenuViewDirectory.Enabled = False
            FileMenuSeparatorDirectory.Visible = False
        Else
            If DirectoryOffset = 0 Then
                BtnFileMenuViewDirectory.Visible = True
                BtnFileMenuViewDirectory.Text = "View Root D&irectory"
                BtnFileMenuViewDirectory.Enabled = True
            Else
                BtnFileMenuViewDirectory.Visible = True
                BtnFileMenuViewDirectory.Text = "View Parent D&irectory"
                BtnFileMenuViewDirectory.Enabled = True
            End If
            BtnFileMenuViewDirectory.Tag = DirectoryOffset
            FileMenuSeparatorDirectory.Visible = True
        End If
    End Sub

    Private Sub RefreshModifiedState()
        Dim Count As Integer = _FilterCounts(FilterTypes.ModifiedFiles)

        ToolStripModified.Text = Count & " Image" & IIf(Count <> 1, "s", "") & " Modified"
        ToolStripModified.Visible = (Count > 0)
        BtnSaveAll.Enabled = (Count > 0)
        ToolStripBtnSaveAll.Enabled = BtnSaveAll.Enabled
    End Sub

    Private Sub RefreshSaveButtons()
        Dim CurrentImageData As LoadedImageData = ComboImages.SelectedItem

        If CurrentImageData Is Nothing Then
            BtnSave.Enabled = False
            BtnSaveAs.Enabled = False
            BtnExportDebug.Enabled = False
        Else
            BtnSave.Enabled = CurrentImageData.Modified And Not CurrentImageData.ReadOnly
            BtnSaveAs.Enabled = CurrentImageData.Modified
            'BtnExportDebug.Enabled = (CurrentImageData.Modified Or CurrentImageData.SessionModifications.Count > 0)
            BtnExportDebug.Enabled = False
        End If
        ToolStripBtnSave.Enabled = BtnSave.Enabled
        ToolStripBtnSaveAs.Enabled = BtnSaveAs.Enabled
    End Sub

    Private Sub ReloadCurrentImage(DoItemScan As Boolean)
        Dim CurrentImageData As LoadedImageData = ComboImages.SelectedItem
        _Disk = DiskImageLoad(CurrentImageData)
        If _Disk IsNot Nothing Then
            CurrentImageData.Modifications = _Disk.Data.Changes
        End If
        DiskImageProcess(DoItemScan, False)
    End Sub

    Private Sub RemoveDeletedFile(Item As ListViewItem)
        Dim FileData As FileData = Item.Tag

        If Not FileData.DirectoryEntry.IsDeleted Then
            Exit Sub
        End If

        FileData.DirectoryEntry.BatchEditMode = True

        FileData.DirectoryEntry.Clear(FileData.IsLastEntry)
        If FileData.IsLastEntry And FileData.Index > 0 Then
            Dim Offset = FileData.DirectoryEntry.Offset
            For Counter = FileData.Index - 1 To 0 Step -1
                Offset -= DirectoryEntry.DIRECTORY_ENTRY_SIZE
                Dim PrevEntry = _Disk.GetDirectoryEntryByOffset(Offset)
                If PrevEntry.IsLFN Then
                    PrevEntry.Clear(True)
                Else
                    Exit For
                End If
            Next
        End If

        FileData.DirectoryEntry.BatchEditMode = False

        FilePropertiesRefresh(Item, False)
    End Sub
    Private Sub ResetAll()
        _Disk = Nothing
        Me.Text = GetWindowCaption()
        _FiltersApplied = False
        _CheckAll = False
        _LoadedFileNames.Clear()
        _ScanRun = False
        BtnDisplayClusters.Enabled = False
        BtnDisplayBadSectors.Enabled = False
        BtnRevert.Enabled = False
        BtnFixImageSize.Enabled = False
        BtnRestoreBootSector.Enabled = False
        BtnRemoveBootSector.Enabled = False

        BtnUndo.Enabled = False
        ToolStripBtnUndo.Enabled = False

        BtnRedo.Enabled = False
        ToolStripBtnRedo.Enabled = False

        ToolStripFileName.Visible = False
        ToolStripModified.Visible = False
        ToolStripStatusModified.Visible = False
        ToolStripStatusReadOnly.Visible = False
        ToolStripFileCount.Visible = False
        ToolStripFileSector.Visible = False
        ToolStripFileTrack.Visible = False

        BtnSaveAll.Enabled = False
        ToolStripBtnSaveAll.Enabled = BtnSaveAll.Enabled

        ComboImages.Enabled = False
        ComboImages.DrawMode = DrawMode.Normal
        ComboImages.Visible = True
        ComboImagesFiltered.Visible = False
        ListViewSummary.Items.Clear()
        ListViewHashes.Items.Clear()

        ClearSort(False)
        ListViewFiles.BeginUpdate()
        ListViewFiles.Items.Clear()
        ListViewFiles.Groups.Clear()
        ListViewFiles.MultiSelect = False
        ListViewFiles.EndUpdate()

        ComboImages.Items.Clear()
        ComboImagesFiltered.Items.Clear()
        MainMenuFilters.BackColor = SystemColors.Control
        RefreshFileButtons()
        SetImagesLoaded(False)
        FiltersReset()
        InitButtonState()
    End Sub

    Private Sub RevertChanges()
        If _Disk.Data.Modified Then
            Dim CurrentImageData As LoadedImageData = ComboImages.SelectedItem
            CurrentImageData.Modifications.Clear()
            ReloadCurrentImage(True)
        End If
    End Sub

    Private Sub SaveAll()
        _SuppressEvent = True
        For Index = 0 To ComboImages.Items.Count - 1
            Dim NewFilePath As String = ""
            Dim DoSave As Boolean = True
            Dim ImageData As LoadedImageData = ComboImages.Items(Index)
            If ImageData.Modified Then
                If ImageData.ReadOnly Then
                    If MsgBoxNewFileName(ImageData.FileName) = MsgBoxResult.Ok Then
                        NewFilePath = GetNewFilePath(ImageData.SaveFile)
                        DoSave = (NewFilePath <> "")
                    Else
                        DoSave = False
                    End If
                End If
                If DoSave Then
                    Dim Result = DiskImageSave(ImageData, NewFilePath)
                    If Result Then
                        If ImageData.ReadOnly Then
                            SetNewFilePath(ImageData, NewFilePath)
                        End If
                        If ImageData Is ComboImages.SelectedItem Then
                            ComboItemRefresh(True, False)
                        End If
                    End If
                End If
            End If
        Next Index
        _SuppressEvent = False

        FilterUpdate(FilterTypes.ModifiedFiles)
        RefreshModifiedState()
    End Sub

    Private Sub SaveCurrent(NewFileName As Boolean)
        Dim NewFilePath As String = ""

        Dim CurrentImageData As LoadedImageData = ComboImages.SelectedItem

        If NewFileName Then
            NewFilePath = GetNewFilePath(CurrentImageData.SaveFile)
            If NewFilePath = "" Then
                Exit Sub
            End If
        End If


        Dim Result = DiskImageSave(CurrentImageData, NewFilePath)
        If Result Then
            FilterUpdate(FilterTypes.ModifiedFiles)
            RefreshModifiedState()

            If NewFileName Then
                SetNewFilePath(CurrentImageData, NewFilePath)
            End If
            ComboItemRefresh(True, False)
        End If
    End Sub

    Private Function SaveDiskImageToFile(Disk As DiskImage.Disk, FilePath As String) As Boolean
        Try
            If IO.File.Exists(FilePath) Then
                Dim BackupPath As String = FilePath & ".bak"
                IO.File.Copy(FilePath, BackupPath, True)
            End If
            Disk.SaveFile(FilePath)
        Catch ex As Exception
            Return False
        End Try

        Return True
    End Function

    Private Sub SetImagesLoaded(Value As Boolean)
        ToolStripImageCount.Visible = Value
        LabelDropMessage.Visible = Not Value
        BtnScan.Enabled = Value
        BtnScanNew.Enabled = Value
        BtnScanNew.Visible = _ScanRun
        BtnClose.Enabled = Value
        ToolStripBtnClose.Enabled = BtnClose.Enabled
        BtnCloseAll.Enabled = Value
        ToolStripBtnCloseAll.Enabled = BtnCloseAll.Enabled
        TxtSearch.Enabled = Value
        BtnWin9xCleanBatch.Enabled = Value
    End Sub

    Private Sub SetNewFilePath(CurrentImageData As LoadedImageData, NewFilePath As String)
        If CurrentImageData.SourceFile <> NewFilePath Then
            _LoadedFileNames.Remove(CurrentImageData.DisplayPath)

            CurrentImageData.SourceFile = NewFilePath
            CurrentImageData.Compressed = False
            CurrentImageData.CompressedFile = ""
            CurrentImageData.ReadOnly = IsFileReadOnly(NewFilePath)

            If _LoadedFileNames.ContainsKey(CurrentImageData.DisplayPath) Then
                FileClose(_LoadedFileNames.Item(CurrentImageData.DisplayPath))
            End If

            _LoadedFileNames.Add(CurrentImageData.DisplayPath, CurrentImageData)

            ComboImagesRefreshPaths()
        End If
    End Sub

    Private Sub SubFiltersClear()
        _SuppressEvent = True

        _FilterOEMName.Clear()
        _FilterDiskType.Clear()

        _SuppressEvent = False
    End Sub

    Private Sub SubFiltersClearFilter()
        _SuppressEvent = True

        TxtSearch.Text = ""
        _FilterOEMName.ClearFilter()
        _FilterDiskType.ClearFilter()

        _SuppressEvent = False
    End Sub

    Private Sub SubFiltersPopulate()
        _SuppressEvent = True

        _FilterOEMName.Populate()
        _FilterDiskType.Populate()

        _SuppressEvent = False
    End Sub

    Private Sub SubFiltersReset()
        _SuppressEvent = True

        OEMNameFilterReset()
        DiskTypeFilterReset()

        _SuppressEvent = False
    End Sub

    Private Sub Win9xCleanBatch()
        Dim Msg = "This will restore the OEM Name and remove any Creation and Last Accessed Dates from all loaded images." _
                & vbCrLf & vbCrLf & "Do you wish to proceed??"

        If MsgBox(Msg, MsgBoxStyle.Question + MsgBoxStyle.YesNo + MsgBoxStyle.DefaultButton2) = MsgBoxResult.No Then
            Exit Sub
        End If

        Me.UseWaitCursor = True
        Dim T = Stopwatch.StartNew

        Dim CurrentImageData As LoadedImageData = ComboImages.SelectedItem
        Dim ItemScanForm As New ItemScanForm(Me, ComboImages.Items, CurrentImageData, _Disk, False, ScanType.ScanTypeWin9xClean)
        ItemScanForm.ShowDialog()

        For Counter = 0 To [Enum].GetNames(GetType(FilterTypes)).Length - 1
            FilterUpdate(Counter)
        Next

        RefreshModifiedState()
        If _ScanRun Then
            SubFiltersPopulate()
        End If

        For Index = 0 To ComboImages.Items.Count - 1
            Dim ImageData As LoadedImageData = ComboImages.Items(Index)
            If ImageData.BatchUpdated Then
                If ImageData Is CurrentImageData Then
                    ComboItemRefresh(True, False)
                End If
                ImageData.BatchUpdated = False
            End If
        Next Index

        T.Stop()
        Debug.Print("Batch Process Time Taken: " & T.Elapsed.ToString)
        Me.UseWaitCursor = False
    End Sub
    Private Structure DirectoryStats
        Dim CanDelete As Boolean
        Dim CanDeleteWithFill As Boolean
        Dim CanExport As Boolean
        Dim FileSize As UInteger
        Dim FullFileName As String
        Dim IsDeleted As Boolean
        Dim IsDirectory As Boolean
        Dim IsModified As Boolean
        Dim IsValidDirectory As Boolean
        Dim IsValidFile As Boolean
    End Structure

#Region "Events"

    Private Function AppendFileFilter(FileFilter As String, Description As String, Pattern As String) As String
        Return FileFilter & "|" & GetFileFilter(Description, Pattern)
    End Function

    Private Sub BtnClearFilters_Click(sender As Object, e As EventArgs) Handles BtnClearFilters.Click
        If _FiltersApplied Then
            FiltersClear(False)
            ImageCountUpdate()
            ContextMenuFilters.Invalidate()
        End If
    End Sub

    Private Sub BtnClose_Click(sender As Object, e As EventArgs) Handles BtnClose.Click, ToolStripBtnClose.Click
        CloseCurrent()
    End Sub

    Private Sub BtnCloseAll_Click(sender As Object, e As EventArgs) Handles BtnCloseAll.Click, ToolStripBtnCloseAll.Click
        CloseAll()
    End Sub

    Private Sub BtnDisplayBadSectors_Click(sender As Object, e As EventArgs) Handles BtnDisplayBadSectors.Click
        HexDisplayBadSectors()
    End Sub

    Private Sub BtnDisplayBootSector_Click(sender As Object, e As EventArgs) Handles BtnDisplayBootSector.Click
        HexDisplayBootSector()
    End Sub

    Private Sub BtnDisplayClusters_Click(sender As Object, e As EventArgs) Handles BtnDisplayClusters.Click
        HexDisplayUnusedClusters()
    End Sub

    Private Sub BtnDisplayDirectory_Click(sender As Object, e As EventArgs) Handles BtnDisplayDirectory.Click, BtnFileMenuViewDirectory.Click
        If sender.Tag IsNot Nothing Then
            HexDisplayDirectoryEntry(sender.tag)
        End If
    End Sub

    Private Sub BtnDisplayDisk_Click(sender As Object, e As EventArgs) Handles BtnDisplayDisk.Click
        HexDisplayDiskImage()
    End Sub

    Private Sub BtnDisplayFAT_Click(sender As Object, e As EventArgs) Handles BtnDisplayFAT.Click
        HexDisplayFAT()
    End Sub

    Private Sub BtnDisplayFile_Click(sender As Object, e As EventArgs) Handles BtnDisplayFile.Click
        If sender.tag IsNot Nothing Then
            HexDisplayDirectoryEntry(sender.tag)
        End If
    End Sub

    Private Sub BtnEditBootSector_Click(sender As Object, e As EventArgs) Handles BtnEditBootSector.Click
        BootSectorEdit()
    End Sub

    Private Sub BtnEditFAT_Click(sender As Object, e As EventArgs) Handles BtnEditFAT.Click
        If sender.tag IsNot Nothing Then
            If sender.tag = -1 Then
                FATEdit(0, False)
            Else
                FATEdit(sender.tag, True)
            End If
        End If
    End Sub
    Private Sub BtnExit_Click(sender As Object, e As EventArgs) Handles BtnExit.Click
        If CloseAll() Then
            Me.Close()
        End If
    End Sub

    Private Sub BtnExportDebug_Click(sender As Object, e As EventArgs) Handles BtnExportDebug.Click
        ExportDebugScript()
    End Sub

    Private Sub BtnExportFile_Click(sender As Object, e As EventArgs) Handles BtnExportFile.Click, BtnFileMenuExportFile.Click, ToolStripBtnExportFile.Click
        FileExport()
    End Sub

    Private Sub BtnFileMenuDeleteFile_Click(sender As Object, e As EventArgs) Handles BtnFileMenuDeleteFile.Click
        DeleteSelectedFiles(False)
    End Sub

    Private Sub BtnFileMenuDeleteFileWithFill_Click(sender As Object, e As EventArgs) Handles BtnFileMenuDeleteFileWithFill.Click
        DeleteSelectedFiles(True)
    End Sub

    Private Sub BtnFileMenuFixSize_Click(sender As Object, e As EventArgs) Handles BtnFileMenuFixSize.Click
        If ListViewFiles.SelectedItems.Count = 1 Then
            Dim Item As ListViewItem = ListViewFiles.SelectedItems(0)
            Dim FileData As FileData = Item.Tag

            If FileData.DirectoryEntry.HasIncorrectFileSize Then
                FixFileSize(FileData.DirectoryEntry)
            End If
        End If
    End Sub

    Private Sub BtnFileMenuRemoveDeletedFile_Click(sender As Object, e As EventArgs) Handles BtnFileMenuRemoveDeletedFile.Click
        If ListViewFiles.SelectedItems.Count = 1 Then
            RemoveDeletedFile(ListViewFiles.SelectedItems(0))
        End If
    End Sub

    Private Sub BtnFileMenuViewCrosslinked_Click(sender As Object, e As EventArgs) Handles BtnFileMenuViewCrosslinked.Click
        If ListViewFiles.SelectedItems.Count = 1 Then
            Dim Item As ListViewItem = ListViewFiles.SelectedItems(0)
            Dim FileData As FileData = Item.Tag
            DisplayCrossLinkedFiles(FileData.DirectoryEntry)
        End If
    End Sub

    Private Sub BtnFileMenuViewFile_Click(sender As Object, e As EventArgs) Handles BtnFileMenuViewFile.Click, ToolStripBtnViewFile.Click
        If ListViewFiles.SelectedItems.Count = 1 Then
            Dim Item As ListViewItem = ListViewFiles.SelectedItems(0)
            Dim FileData As FileData = Item.Tag
            HexDisplayDirectoryEntry(FileData.DirectoryEntry.Offset)
        End If
    End Sub

    Private Sub BtnFileMenuViewFileText_Click(sender As Object, e As EventArgs) Handles BtnFileMenuViewFileText.Click, ToolStripBtnViewFileText.Click
        If ListViewFiles.SelectedItems.Count = 1 Then
            Dim Item As ListViewItem = ListViewFiles.SelectedItems(0)
            Dim FileData As FileData = Item.Tag
            DirectoryEntryDisplayText(FileData.DirectoryEntry)
        End If
    End Sub

    Private Sub BtnFileProperties_Click(sender As Object, e As EventArgs) Handles BtnFileProperties.Click, BtnFileMenuFileProperties.Click, ToolStripBtnFileProperties.Click
        FilePropertiesEdit()
    End Sub

    Private Sub BtnFixImageSize_Click(sender As Object, e As EventArgs) Handles BtnFixImageSize.Click
        FixImageSize()
    End Sub

    Private Sub BtnHelpAbout_Click(sender As Object, e As EventArgs) Handles BtnHelpAbout.Click
        Dim AboutBox As New AboutBox()
        AboutBox.ShowDialog()
    End Sub

    Private Sub BtnHelpProjectPage_Click(sender As Object, e As EventArgs) Handles BtnHelpProjectPage.Click
        Process.Start(SITE_URL)
    End Sub

    Private Sub BtnHelpUpdateCheck_Click(sender As Object, e As EventArgs) Handles BtnHelpUpdateCheck.Click
        CheckForUpdates()
    End Sub

    Private Sub BtnOpen_Click(sender As Object, e As EventArgs) Handles BtnOpen.Click, ToolStripBtnOpen.Click
        FilesOpen()
    End Sub

    Private Sub BtnRedo_Click(sender As Object, e As EventArgs) Handles BtnRedo.Click, ToolStripBtnRedo.Click
        _Disk.Data.Redo()
        If _Disk.ReinitializeRequired Then
            DiskImageProcess(True, True)
        Else
            ComboItemRefresh(True, True)
        End If
    End Sub

    Private Sub BtnRemoveBootSector_Click(sender As Object, e As EventArgs) Handles BtnRemoveBootSector.Click
        BootSectorRemove()
    End Sub

    Private Sub BtnReplaceFile_Click(sender As Object, e As EventArgs) Handles BtnReplaceFile.Click, BtnFileMenuReplaceFile.Click
        If ListViewFiles.SelectedItems.Count = 1 Then
            FileReplace(ListViewFiles.SelectedItems(0))
        End If
    End Sub

    Private Sub BtnResetSort_Click(sender As Object, e As EventArgs) Handles BtnResetSort.Click
        ClearSort(True)
    End Sub

    Private Sub BtnRestoreBootSector_Click(sender As Object, e As EventArgs) Handles BtnRestoreBootSector.Click
        BootSectorRestore()
    End Sub

    Private Sub BtnRevert_Click(sender As Object, e As EventArgs) Handles BtnRevert.Click
        RevertChanges()
    End Sub

    Private Sub BtnSave_Click(sender As Object, e As EventArgs) Handles BtnSave.Click, ToolStripBtnSave.Click
        SaveCurrent(False)
    End Sub

    Private Sub BtnSaveAll_Click(sender As Object, e As EventArgs) Handles BtnSaveAll.Click, ToolStripBtnSaveAll.Click
        SaveAll()
    End Sub

    Private Sub BtnSaveAs_Click(sender As Object, e As EventArgs) Handles BtnSaveAs.Click, ToolStripBtnSaveAs.Click
        SaveCurrent(True)
    End Sub

    Private Sub BtnScan_Click(sender As Object, e As EventArgs) Handles BtnScan.Click
        ContextMenuFilters.Close()
        DiskImagesScan(False)
    End Sub

    Private Sub BtnScanNew_Click(sender As Object, e As EventArgs) Handles BtnScanNew.Click
        ContextMenuFilters.Close()
        DiskImagesScan(True)
    End Sub

    Private Sub BtnUndo_Click(sender As Object, e As EventArgs) Handles BtnUndo.Click, ToolStripBtnUndo.Click
        _Disk.Data.Undo()
        If _Disk.ReinitializeRequired Then
            DiskImageProcess(True, True)
        Else
            ComboItemRefresh(True, True)
        End If
    End Sub

    Private Sub BtnWin9xClean_Click(sender As Object, e As EventArgs) Handles BtnWin9xClean.Click
        Win9xClean(_Disk, True)
    End Sub
    Private Sub BtnWin9xCleanBatch_Click(sender As Object, e As EventArgs) Handles BtnWin9xCleanBatch.Click
        Win9xCleanBatch()
    End Sub

    Private Sub ComboFilter_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboOEMName.SelectedIndexChanged, ComboDiskType.SelectedIndexChanged
        If _SuppressEvent Then
            Exit Sub
        End If

        FiltersApply(False)
    End Sub

    Private Sub ComboImages_DrawItem(sender As Object, e As DrawItemEventArgs) Handles ComboImages.DrawItem, ComboImagesFiltered.DrawItem
        Dim CB As ComboBox = sender

        If e.Index >= -1 Then
            Dim Brush As Brush

            If e.State And DrawItemState.Selected Then
                Brush = New SolidBrush(SystemColors.Highlight)
            Else
                Brush = New SolidBrush(SystemColors.Window)
            End If

            e.Graphics.FillRectangle(Brush, e.Bounds)

            Brush.Dispose()

            If e.Index > -1 Then
                Dim tBrush As Brush
                If e.State And DrawItemState.Selected Then
                    tBrush = New SolidBrush(SystemColors.HighlightText)
                Else
                    Dim CurrentImageData As LoadedImageData = CB.Items(e.Index)
                    If CurrentImageData IsNot Nothing AndAlso CurrentImageData.Modified Then
                        tBrush = New SolidBrush(Color.Blue)
                    Else
                        tBrush = New SolidBrush(SystemColors.WindowText)
                    End If
                End If

                Dim Format As New StringFormat With {
                    .Trimming = StringTrimming.None,
                    .FormatFlags = StringFormatFlags.NoWrap
                }

                e.Graphics.DrawString(CB.Items(e.Index).ToString, e.Font, tBrush, e.Bounds, Format)

                tBrush.Dispose()
            End If
        End If

        e.DrawFocusRectangle()
    End Sub

    Private Sub ComboImages_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboImages.SelectedIndexChanged
        If _SuppressEvent Then
            Exit Sub
        End If

        ReloadCurrentImage(False)
    End Sub

    Private Sub ComboImagesFiltered_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboImagesFiltered.SelectedIndexChanged
        If _SuppressEvent Then
            Exit Sub
        End If

        ComboImages.SelectedItem = ComboImagesFiltered.SelectedItem
    End Sub
    Private Sub ContextMenuCopy_ItemClicked(sender As Object, e As ToolStripItemClickedEventArgs) Handles ContextMenuCopy1.ItemClicked, ContextMenuCopy2.ItemClicked
        Dim LV As ListView = Nothing

        If sender.name = "Summary" Then
            LV = ListViewSummary
        ElseIf sender.name = "Hashes" Then
            LV = ListViewHashes
        End If

        If LV IsNot Nothing AndAlso LV.FocusedItem IsNot Nothing Then
            Dim Value As String = LV.FocusedItem.SubItems.Item(1).Text
            Clipboard.SetText(Value)
        End If
    End Sub

    Private Sub ContextMenuCopy_Opening(sender As Object, e As CancelEventArgs) Handles ContextMenuCopy1.Opening, ContextMenuCopy2.Opening
        Dim LV As ListView = Nothing
        Dim CM As ContextMenuStrip = sender

        If CM.Name = "Summary" Then
            LV = ListViewSummary
        ElseIf CM.Name = "Hashes" Then
            LV = ListViewHashes
        End If

        If LV IsNot Nothing AndAlso LV.FocusedItem IsNot Nothing Then
            CM.Items(0).Text = "&Copy " & LV.FocusedItem.Text
        Else
            e.Cancel = True
        End If
    End Sub

    Private Sub ContextMenuFiles_Opening(sender As Object, e As CancelEventArgs) Handles ContextMenuFiles.Opening
        If ListViewFiles.SelectedItems.Count = 0 Then
            e.Cancel = True
        End If
    End Sub

    Private Sub ContextMenuFilters_CheckStateChanged(sender As Object, e As EventArgs)
        If _SuppressEvent Then
            Exit Sub
        End If

        FiltersApply(True)
    End Sub

    Private Sub ContextMenuFilters_Closing(sender As Object, e As ToolStripDropDownClosingEventArgs) Handles ContextMenuFilters.Closing
        If e.CloseReason = ToolStripDropDownCloseReason.ItemClicked Then
            e.Cancel = True
        End If
    End Sub

    Private Sub Debounce_Tick(sender As Object, e As EventArgs) Handles Debounce.Tick
        Debounce.Stop()

        FiltersApply(False)
    End Sub

    Private Sub File_DragDrop(sender As Object, e As DragEventArgs) Handles ComboImages.DragDrop, ComboImagesFiltered.DragDrop, LabelDropMessage.DragDrop, ListViewFiles.DragDrop, ListViewHashes.DragDrop, ListViewSummary.DragDrop
        Dim Files As String() = e.Data.GetData(DataFormats.FileDrop)
        ProcessFileDrop(Files)
    End Sub

    Private Sub File_DragEnter(sender As Object, e As DragEventArgs) Handles ComboImages.DragEnter, ComboImagesFiltered.DragEnter, LabelDropMessage.DragEnter, ListViewFiles.DragEnter, ListViewHashes.DragEnter, ListViewSummary.DragEnter
        FileDropStart(e)
    End Sub

    Private Function GetFileFilter(Description As String, Pattern As String) As String
        Return Description & "|" & Pattern
    End Function

    Private Sub InitFileFilter()
        Dim ExtensionList As New List(Of String)

        For Each Extension In _FileFilterExt
            ExtensionList.Add("*" & Extension)
        Next

        _FileFilter = GetFileFilter("Disk Image Files (" & String.Join("; ", ExtensionList.ToArray) & ")", String.Join(";", ExtensionList.ToArray))
    End Sub

    Private Sub ListViewFiles_ColumnClick(sender As Object, e As ColumnClickEventArgs) Handles ListViewFiles.ColumnClick
        If ListViewFiles.Items.Count = 0 Then
            Exit Sub
        End If

        If e.Column = 0 Then
            _CheckAll = Not _CheckAll
            _SuppressEvent = True
            For Each Item As ListViewItem In ListViewFiles.Items
                Item.Selected = _CheckAll
            Next
            _SuppressEvent = False
            ItemSelectionChanged()
        Else
            If e.Column = _lvwColumnSorter.SortColumn Then
                If _lvwColumnSorter.Order = SortOrder.Ascending Then
                    _lvwColumnSorter.Order = SortOrder.Descending
                Else
                    _lvwColumnSorter.Order = SortOrder.Ascending
                End If
            Else
                _lvwColumnSorter.SortColumn = e.Column
                _lvwColumnSorter.Order = SortOrder.Ascending
            End If
            ListViewFiles.Sort()
            ListViewFiles.SetSortIcon(_lvwColumnSorter.SortColumn, _lvwColumnSorter.Order)
            BtnResetSort.Enabled = True
        End If
    End Sub

    Private Sub ListViewFiles_ColumnWidthChanging(sender As Object, e As ColumnWidthChangingEventArgs) Handles ListViewFiles.ColumnWidthChanging
        e.NewWidth = Me.ListViewFiles.Columns(e.ColumnIndex).Width
        e.Cancel = True
    End Sub

    Private Sub ListViewFiles_DrawColumnHeader(sender As Object, e As DrawListViewColumnHeaderEventArgs) Handles ListViewFiles.DrawColumnHeader
        If e.ColumnIndex = 0 Then
            'Dim Offset As Integer
            'If (e.State And ListViewItemStates.Selected) > 0 Then
            ' Offset = 1
            ' Else
            '    Offset = 0
            'End If
            Dim State = IIf(_CheckAll, VisualStyles.CheckBoxState.CheckedNormal, VisualStyles.CheckBoxState.UncheckedNormal)
            Dim Size = CheckBoxRenderer.GetGlyphSize(e.Graphics, State)
            CheckBoxRenderer.DrawCheckBox(e.Graphics, New Point(4, (e.Bounds.Height - Size.Height) \ 2), State)
            'e.Graphics.DrawString(e.Header.Text, e.Font, New SolidBrush(Color.Black), New Point(20 + Offset, (e.Bounds.Height - Size.Height) \ 2 + Offset))
            'e.Graphics.DrawLine(New Pen(Color.FromArgb(229, 229, 229), 1), New Point(e.Bounds.Width - 1, 0), New Point(e.Bounds.Width - 1, e.Bounds.Height))
        Else
            e.DrawDefault = True
        End If
    End Sub

    Private Sub ListViewFiles_DrawItem(sender As Object, e As DrawListViewItemEventArgs) Handles ListViewFiles.DrawItem
        e.DrawDefault = True
    End Sub

    Private Sub ListViewFiles_DrawSubItem(sender As Object, e As DrawListViewSubItemEventArgs) Handles ListViewFiles.DrawSubItem
        e.DrawDefault = True
    End Sub

    Private Sub ListViewFiles_ItemDrag(sender As Object, e As ItemDragEventArgs) Handles ListViewFiles.ItemDrag
        _SuppressEvent = True
        DragDropSelectedFiles()
        _SuppressEvent = False
    End Sub

    Private Sub ListViewFiles_ItemSelectionChanged(sender As Object, e As ListViewItemSelectionChangedEventArgs) Handles ListViewFiles.ItemSelectionChanged
        If _SuppressEvent Then
            Exit Sub
        End If

        ItemSelectionChanged()
    End Sub

    Private Sub ListViewHashes_ItemSelectionChanged(sender As Object, e As ListViewItemSelectionChangedEventArgs) Handles ListViewHashes.ItemSelectionChanged
        e.Item.Selected = False
    End Sub

    Private Sub ListViewSummary_ItemSelectionChanged(sender As Object, e As ListViewItemSelectionChangedEventArgs) Handles ListViewSummary.ItemSelectionChanged
        e.Item.Selected = False
    End Sub

    Private Sub MainForm_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        If Not CloseAll() Then
            e.Cancel = True
        End If
    End Sub

    Private Sub MainForm_Load(sender As Object, e As EventArgs) Handles Me.Load
        _FileVersion = GetVersionString()
        Me.Text = GetWindowCaption()

        ParseCustomFilters()
        InitFileFilter()

        PositionForm()

        ListViewFiles.DoubleBuffer
        FiltersInitialize()
        _LoadedFileNames = New Dictionary(Of String, LoadedImageData)
        _BootStrap = New Bootstrap
        Debounce = New Timer With {
            .Interval = 750
        }
        ContextMenuCopy1 = New ContextMenuStrip With {
            .Name = "Summary"
        }
        ContextMenuCopy1.Items.Add("&Copy Value")
        ListViewSummary.ContextMenuStrip = ContextMenuCopy1

        ContextMenuCopy2 = New ContextMenuStrip With {
            .Name = "Hashes"
        }
        ContextMenuCopy2.Items.Add("&Copy Value")
        ListViewHashes.ContextMenuStrip = ContextMenuCopy2

        ResetAll()

        Dim Args = Environment.GetCommandLineArgs.Skip(1).ToArray

        If Args.Length > 0 Then
            ProcessFileDrop(Args)
        End If
    End Sub

    Private Sub MainForm_ResizeEnd(sender As Object, e As EventArgs) Handles Me.ResizeEnd
        My.Settings.WindowWidth = Me.Width
        My.Settings.WindowHeight = Me.Height
    End Sub

    Private Sub ParseCustomFilters()
        If My.Settings.CustomFileExtensions.Length > 0 Then
            For Each Extension In My.Settings.CustomFileExtensions.Split(",")
                If Not Extension.StartsWith(".") Then
                    Extension = "." & Extension
                End If
                _FileFilterExt.Add(Extension)
            Next
        End If
    End Sub

    Private Function PathAddBackslash(Path As String) As String
        If Len(Path) > 0 Then
            If Not Path.EndsWith("\") Then
                Path &= "\"
            End If
        End If
        Return Path
    End Function
    Private Sub TxtSearch_TextChanged(sender As Object, e As EventArgs) Handles TxtSearch.TextChanged
        If _SuppressEvent Then
            Exit Sub
        End If

        Debounce.Stop()
        Debounce.Start()
    End Sub
#End Region

End Class