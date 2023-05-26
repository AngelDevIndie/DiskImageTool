﻿Imports DiskImageTool.DiskImage
Imports Hb.Windows.Forms

Public Class HexViewData
    Private ReadOnly _HexViewSectorData As HexViewSectorData
    Public Sub New(HexViewSectorData As HexViewSectorData, Index As Integer)
        _HexViewSectorData = HexViewSectorData
        _Index = Index
        _SectorBlock = _HexViewSectorData.SectorData.GetBlock(Index)
        _ByteProvider = New MyByteProvider(_HexViewSectorData.SectorData.Data, _SectorBlock.Offset, _SectorBlock.Size)
    End Sub
    Public ReadOnly Property ByteProvider As IByteProvider

    Public ReadOnly Property Disk As DiskImage.Disk
        Get
            Return _HexViewSectorData.Disk
        End Get
    End Property
    Public ReadOnly Property Index As Integer
    Public ReadOnly Property SectorBlock As SectorBlock

    Public Overrides Function ToString() As String
        Dim Sector = _SectorBlock.SectorStart
        Dim SectorEnd = _SectorBlock.SectorStart + _SectorBlock.SectorCount - 1

        Dim Cluster As UShort
        Dim ClusterEnd As UShort
        If _HexViewSectorData.Disk.IsValidImage Then
            Cluster = _HexViewSectorData.Disk.BootSector.SectorToCluster(Sector)
            ClusterEnd = _HexViewSectorData.Disk.BootSector.SectorToCluster(SectorEnd)
        Else
            Cluster = 0
            ClusterEnd = 0
        End If

        Dim Header As String = "Sector " & Sector & IIf(SectorEnd > Sector, "-" & SectorEnd, "")
        If Cluster > 0 Then
            Header = "Cluster " & Cluster & IIf(ClusterEnd > Cluster, "-" & ClusterEnd, "") & "; " & Header
        End If
        If _SectorBlock.Description <> "" Then
            Header = _SectorBlock.Description & "  (" & Header & ")"
        End If

        Return Header
    End Function
End Class
