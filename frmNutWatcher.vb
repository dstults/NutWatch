Public Class FrmNutWatcher

    '===================================================================================================================
    ' DECLARATIONS!
    '===================================================================================================================

    Public programFullNameAndVersion = "NutWatcher v0.80"
    Public programMajorVersionReleaseDate = "2020-01-11"
    Public myLongIntervalSecs = 30 ' s
    Public myTimeoutMs = 2000 ' ms
    Public defaultFile As String = "config.csv"
    Public bulkScanWorking As Boolean = False
    Public checkIterations As Integer = 0

    Public ReadOnly PingNoProblem As Color = Color.FromArgb(160, 160, 196)
    Public ReadOnly PingProblem As Color = Color.FromArgb(255, 64, 128)
    Public ReadOnly TcpNoProblem As Color = Color.FromArgb(160, 196, 160)
    Public ReadOnly TcpProblem As Color = Color.FromArgb(255, 128, 64)

    Public Class ClsScanPingIp
        Public Name As String
        Public Ip As Net.IPAddress
        Public RowIndex As Integer = 0
        'Public Interval As Integer = 5
        'Public NextScanTime As DateTime
        Public PingTest As New Net.NetworkInformation.Ping
        Public Message As String
    End Class

    Public Class ClsScanPingDns
        Public Name As String
        Public Dns As String
        Public FirstKnownIP As Net.IPAddress
        Public RowIndex As Integer = 0
        'Public Interval As Integer = 10
        'Public NextScanTime As DateTime
        Public PingTest As New Net.NetworkInformation.Ping
        Public Message As String
    End Class

    Public Class ClsScanTcpIp
        Public Name As String
        Public Ip As Net.IPAddress
        Public Port As Integer
        Public RowIndex As Integer = 0
        'Public Interval As Integer = 5
        'Public NextScanTime As DateTime
        Public TcpClient As New Net.Sockets.TcpClient
        Public Message As String
        Public Sub TcpReset()
            TcpClient.Client.Close()
            TcpClient.Client.Dispose()
            TcpClient.Close()
            TcpClient.Dispose()
            TcpClient = New Net.Sockets.TcpClient
        End Sub

    End Class

    Public Class ClsScanTcpDns
        Public Name As String
        Public Dns As String
        Public FirstKnownIP As Net.IPAddress
        Public Port As Integer
        Public RowIndex As Integer = 0
        'Public Interval As Integer = 10
        'Public NextScanTime As DateTime
        Public TcpClient As New Net.Sockets.TcpClient
        Public Message As String
        Public Sub TcpReset()
            TcpClient.GetStream.Close()
            TcpClient.Client.Close()
            TcpClient.Client.Dispose()
            TcpClient.Close()
            TcpClient.Dispose()
            TcpClient = New Net.Sockets.TcpClient
        End Sub

    End Class

    Public IpPingScanSet As New HashSet(Of ClsScanPingIp)
    Public DnsPingScanSet As New HashSet(Of ClsScanPingDns)
    Public IpTcpScanSet As New HashSet(Of ClsScanTcpIp)
    Public DnsTcpScanSet As New HashSet(Of ClsScanTcpDns)

    '===================================================================================================================
    ' BEGIN!
    '===================================================================================================================

    Private Sub FrmNutMonitor_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.Text = "Darren's " & programFullNameAndVersion

        Dim myfileName As String = Dir(defaultFile)
        'Dim myFile As IO.FileStream
        If myfileName = "" Then
            WriteDefaultData()
            MsgBox("Created 'config.csv' with several default examples." & vbNewLine & "Customize and continue to use program.")
            Application.Exit()
        Else
            ReadData()
        End If
        UpdateDisplay()
    End Sub

    Public Sub WriteDefaultData()
        Dim tmpFile As IO.FileStream = IO.File.Create(defaultFile)
        tmpFile.Close()
        Dim textDump(9) As String
        textDump(0) = "Router,IP 192.168.1.1,PING"
        textDump(1) = "  - GUI,IP 192.168.1.1,TCP 80"
        textDump(2) = "DNS Server,IP 192.168.1.2,PING"
        textDump(3) = "  - SSH,IP 192.168.1.2,TCP 22"
        textDump(4) = "  - DNS,IP 192.168.1.2,TCP 53"
        textDump(5) = "SMB Server,IP 192.168.1.3,PING"
        textDump(6) = "  - SMB-NETBIOS,IP: 192.168.1.89,TCP 139"
        textDump(7) = "  - SMB-IP,IP 192.168.1.89,TCP 445"
        textDump(8) = "  - RDP,IP 192.168.1.89,TCP 3389"
        textDump(9) = "Remote Website,DNS google.com,TCP 80"
        IO.File.WriteAllLines(defaultFile, textDump)
    End Sub

    Public Sub WriteActiveData()
        Dim textDump(IpPingScanSet.Count + DnsPingScanSet.Count + IpTcpScanSet.Count + DnsTcpScanSet.Count - 1) As String
        Dim myLine As Integer = 0
        For Each NetJob As ClsScanPingIp In IpPingScanSet
            textDump(myLine) = NetJob.Name & "," & "IP " & NetJob.Ip.ToString & ",PING" & vbNewLine
            myLine += 1
        Next
        For Each NetJob As ClsScanPingDns In DnsPingScanSet
            textDump(myLine) = NetJob.Name & "," & "DNS " & NetJob.Dns & ",PING" & vbNewLine
            myLine += 1
        Next
        For Each NetJob As ClsScanTcpIp In IpTcpScanSet
            textDump(myLine) = NetJob.Name & "," & "IP " & NetJob.Ip.ToString & ",TCP " & NetJob.Port & vbNewLine
            myLine += 1
        Next
        For Each NetJob As ClsScanTcpDns In DnsTcpScanSet
            textDump(myLine) = NetJob.Name & "," & "DNS " & NetJob.Dns & ",TCP " & NetJob.Port & vbNewLine
            myLine += 1
        Next
        Try
            IO.File.WriteAllLines(defaultFile, textDump)
        Catch ex As Exception
            MsgBox("File IO Error: '" & defaultFile & vbNewLine & "Error Message: " & ex.Message)
            Application.Exit()
        End Try
    End Sub

    Public Sub ReadData()
        Dim textDump() As String = IO.File.ReadAllLines(defaultFile)
        Dim iInt As Integer
        Me.Height = 43 ' 32 + 11
        DataGrid.Height = 11 ' 0 + 11
        For Each iStr As String In textDump
            AddScan(iStr)
            Me.Height += 22
            DataGrid.Height += 22
        Next
    End Sub

    Public Sub AddScan(inText As String)
        Dim csvs() = Split(inText, ",")
        Dim setName As String = csvs(0), setTargetData As String = csvs(1), setScanData As String = csvs(2)
        Dim parseTargetData() As String = Split(setTargetData, " ")
        Dim parseScanData() As String = Split(setScanData, " ")

        DataGrid.Rows.Add()
        Dim currentRow As Integer = DataGrid.Rows.Count - 1
        Dim column2text = ""
        Dim rowFontColor As Color = Nothing
        DataGrid.Rows(currentRow).Cells(0).Value = Strings.Left(setName, 20)
        If parseTargetData(0) = "IP" And parseScanData(0) = "PING" Then
            Dim myScan As New ClsScanPingIp
            IpPingScanSet.Add(myScan)
            myScan.RowIndex = currentRow
            rowFontColor = PingNoProblem

            myScan.Name = setName
            If Net.IPAddress.TryParse(parseTargetData(1), myScan.Ip) Then
                column2text = Strings.Left(myScan.Ip.ToString & "                       ", 23)
            Else
                MsgBox("Error, invalid IPv4: " & parseTargetData(1))
            End If
            'myScan.NextScanTime = DateTime.Now.AddSeconds(myScan.Interval)

        ElseIf parseTargetData(0) = "IP" And parseScanData(0) = "TCP" Then
            Dim myScan As New ClsScanTcpIp
            IpTcpScanSet.Add(myScan)
            myScan.RowIndex = currentRow
            rowFontColor = TcpNoProblem

            myScan.Name = setName
            myScan.Port = CInt(parseScanData(1))
            If Net.IPAddress.TryParse(parseTargetData(1), myScan.Ip) Then
                column2text = Strings.Left(myScan.Ip.ToString & "        ", 15) & " : " & myScan.Port
            Else
                MsgBox("Error, invalid IPv4: " & parseTargetData(1))
            End If
            'myScan.NextScanTime = DateTime.Now.AddSeconds(myScan.Interval)

        ElseIf parseTargetData(0) = "DNS" And parseScanData(0) = "PING" Then
            Dim myScan As New ClsScanPingDns
            DnsPingScanSet.Add(myScan)
            myScan.RowIndex = currentRow
            rowFontColor = PingNoProblem

            myScan.Name = setName
            myScan.Dns = parseTargetData(1)
            column2text = Strings.Left(myScan.Dns & "                       ", 23)
            'myScan.NextScanTime = DateTime.Now.AddSeconds(myScan.Interval)

        ElseIf parseTargetData(0) = "DNS" And parseScanData(0) = "TCP" Then
            Dim myScan As New ClsScanTcpDns
            DnsTcpScanSet.Add(myScan)
            myScan.RowIndex = currentRow
            rowFontColor = TcpNoProblem

            myScan.Name = setName
            myScan.Dns = parseTargetData(1)
            myScan.Port = CInt(parseScanData(1))
            column2text = Strings.Left(myScan.Dns & "               ", 15) & " : " & myScan.Port
            'myScan.NextScanTime = DateTime.Now.AddSeconds(myScan.Interval)

        End If

        DataGrid.Rows(currentRow).DefaultCellStyle.ForeColor = rowFontColor
        DataGrid.Rows(currentRow).Cells(1).Value = column2text

    End Sub

    Private Sub TmrUpdate_Tick(sender As Object, e As EventArgs) Handles tmrUpdate.Tick
        UpdateDisplay()
    End Sub

    Public Function AllJobsDone()
        Dim result As Boolean = True
        For Each netJob As ClsScanPingIp In IpPingScanSet
            If netJob.Message = "" Then result = False
        Next
        For Each netJob As ClsScanPingDns In DnsPingScanSet
            If netJob.Message = "" Then result = False
        Next
        For Each netJob As ClsScanTcpIp In IpTcpScanSet
            If netJob.Message = "" Then result = False
        Next
        For Each netJob As ClsScanTcpDns In DnsTcpScanSet
            If netJob.Message = "" Then result = False
        Next
        Return result
    End Function

    Private Sub UpdateDisplay()
        Select Case bulkScanWorking
            Case True
                If AllJobsDone() Then
                    bulkScanWorking = False
                    tmrUpdate.Interval = myLongIntervalSecs * 1000
                Else
                    checkIterations += 1
                    ' Pings have their own event trigger.
                    CheckForNewOpenTcpPorts()
                End If
            Case False
                tmrUpdate.Enabled = True
                StartAllScans()
                bulkScanWorking = True
                tmrUpdate.Interval = 10
                checkIterations = 0
                For iRow As Integer = 0 To DataGrid.Rows.Count - 1
                    DataGrid.Rows(iRow).DefaultCellStyle.ForeColor = Color.White
                Next
        End Select
    End Sub

    Private Sub StartAllScans()
        For Each pingJob As ClsScanPingIp In IpPingScanSet
            Try
                pingJob.PingTest.SendAsync(pingJob.Ip.ToString, myTimeoutMs, pingJob)
                AddHandler pingJob.PingTest.PingCompleted, AddressOf GetPingResult
                pingJob.Message = ""
            Catch ex As Exception
                LogError(pingJob, ex)
            End Try
        Next
        For Each pingJob As ClsScanPingDns In DnsPingScanSet
            Dim myIp As Net.IPAddress
            Try
                myIp = Net.Dns.GetHostEntry(pingJob.Dns).AddressList.First
                Try
                    pingJob.PingTest.SendAsync(myIp, myTimeoutMs, pingJob)
                    AddHandler pingJob.PingTest.PingCompleted, AddressOf GetPingResult
                    pingJob.Message = ""
                Catch ex As Exception
                    LogError(pingJob, ex)
                End Try
            Catch ex As Exception
                LogError(pingJob, ex)
            End Try
        Next
        For Each tcpJob As ClsScanTcpIp In IpTcpScanSet
            Try
                tcpJob.TcpClient.ConnectAsync(tcpJob.Ip, tcpJob.Port)
                tcpJob.Message = ""
            Catch ex As Exception
                LogError(tcpJob, ex)
            End Try
        Next
        For Each tcpJob As ClsScanTcpDns In DnsTcpScanSet
            Dim myIp As Net.IPAddress
            Try
                myIp = Net.Dns.GetHostEntry(tcpJob.Dns).AddressList.First
                Try
                    tcpJob.TcpClient.ConnectAsync(myIp, tcpJob.Port)
                    tcpJob.Message = ""
                Catch ex As Exception
                    LogError(tcpJob, ex)
                End Try
            Catch ex As Exception
                LogError(tcpJob, ex)
            End Try
        Next
    End Sub

    'Private Function ScanReport() As String
    '    Dim textDump As String = ""
    '    If IpPingScanSet.Count + DnsPingScanSet.Count + IpTcpScanSet.Count + DnsTcpScanSet.Count = 0 Then
    '        textDump = "Error: No scans in program memory."
    '    Else
    '        Dim lotsOfSpaces = "                    "
    '        For Each NetJob As ClsScanPingIp In IpPingScanSet
    '            textDump &= Strings.Left(NetJob.Name & lotsOfSpaces, 20) & "    "
    '            textDump &= Strings.Left(NetJob.Ip.ToString & lotsOfSpaces, 23) & "    "
    '            textDump &= Strings.Left(NetJob.Message & lotsOfSpaces & lotsOfSpaces, 40)
    '            textDump &= vbNewLine
    '        Next
    '        For Each NetJob As ClsScanPingDns In DnsPingScanSet
    '            textDump &= Strings.Left(NetJob.Name & lotsOfSpaces, 20) & "    "
    '            textDump &= Strings.Left(NetJob.Dns & lotsOfSpaces, 23) & "    "
    '            textDump &= Strings.Left(NetJob.Message & lotsOfSpaces & lotsOfSpaces, 40)
    '            textDump &= vbNewLine
    '        Next
    '        For Each NetJob As ClsScanTcpIp In IpTcpScanSet
    '            textDump &= Strings.Left(NetJob.Name & lotsOfSpaces, 20) & "    "
    '            textDump &= Strings.Left(NetJob.Ip.ToString & lotsOfSpaces, 15) & " : "
    '            textDump &= Strings.Left(NetJob.Port & "     ", 5) & "    "
    '            textDump &= Strings.Left(NetJob.Message & lotsOfSpaces & lotsOfSpaces, 40)
    '            textDump &= vbNewLine
    '        Next
    '        For Each NetJob As ClsScanTcpDns In DnsTcpScanSet
    '            textDump &= Strings.Left(NetJob.Name & lotsOfSpaces, 20) & "    "
    '            textDump &= Strings.Left(NetJob.Dns & lotsOfSpaces, 15) & " : "
    '            textDump &= Strings.Left(NetJob.Port & "     ", 5) & "    "
    '            textDump &= Strings.Left(NetJob.Message & lotsOfSpaces & lotsOfSpaces, 40)
    '            textDump &= vbNewLine
    '        Next
    '    End If
    '    Return textDump
    'End Function

    Private Sub CheckForNewOpenTcpPorts()
        For Each tcpJob As ClsScanTcpIp In IpTcpScanSet
            If tcpJob.Message = "" Then
                If tcpJob.TcpClient.Connected Then
                    tcpJob.Message = "< " & (1 + checkIterations) * tmrUpdate.Interval & " ms"
                    DataGrid.Rows(tcpJob.RowIndex).Cells(2).Value = tcpJob.Message
                    DataGrid.Rows(tcpJob.RowIndex).DefaultCellStyle.ForeColor = TcpNoProblem
                    tcpJob.TcpReset()
                ElseIf checkIterations > myTimeoutMs / tmrUpdate.Interval Then
                    tcpJob.Message = "closed"
                    DataGrid.Rows(tcpJob.RowIndex).Cells(2).Value = tcpJob.Message
                    DataGrid.Rows(tcpJob.RowIndex).DefaultCellStyle.ForeColor = TcpProblem
                    tcpJob.TcpReset()
                End If
            End If
        Next
        For Each tcpJob As ClsScanTcpDns In DnsTcpScanSet
            If tcpJob.Message = "" Then
                If tcpJob.TcpClient.Connected Then
                    tcpJob.Message = "< " & (1 + checkIterations) * tmrUpdate.Interval & " ms"
                    DataGrid.Rows(tcpJob.RowIndex).Cells(2).Value = tcpJob.Message
                    DataGrid.Rows(tcpJob.RowIndex).DefaultCellStyle.ForeColor = TcpNoProblem
                    tcpJob.TcpReset()
                ElseIf checkIterations > myTimeoutMs / tmrUpdate.Interval Then
                    tcpJob.Message = "closed"
                    DataGrid.Rows(tcpJob.RowIndex).Cells(2).Value = tcpJob.Message
                    DataGrid.Rows(tcpJob.RowIndex).DefaultCellStyle.ForeColor = TcpProblem
                    tcpJob.TcpReset()
                End If
            End If
        Next
    End Sub

    Public Sub LogError(netJob As Object, ex As Exception)
        netJob.Message = ex.Message
        DataGrid.Rows(netJob.RowIndex).Cells(2).Value = ex.Message
    End Sub

    Private Sub GetPingResult(ByVal sender As Object, ByVal e As System.Net.NetworkInformation.PingCompletedEventArgs)
        ' e.UserState is the UserToken passed to the pingAsync, we will use it to find the matching ping job
        For Each pingJob As ClsScanPingIp In IpPingScanSet
            If pingJob Is e.UserState Then
                Select Case e.Reply.Status
                    Case Net.NetworkInformation.IPStatus.Success
                        pingJob.Message = e.Reply.RoundtripTime & " ms"
                        DataGrid.Rows(pingJob.RowIndex).Cells(2).Value = pingJob.Message
                        DataGrid.Rows(pingJob.RowIndex).DefaultCellStyle.ForeColor = PingNoProblem
                    Case Else
                        pingJob.Message = "no reply"
                        DataGrid.Rows(pingJob.RowIndex).Cells(2).Value = pingJob.Message
                        DataGrid.Rows(pingJob.RowIndex).DefaultCellStyle.ForeColor = PingProblem
                End Select
            End If
        Next
        For Each pingJob As ClsScanPingDns In DnsPingScanSet
            If pingJob Is e.UserState Then
                Select Case e.Reply.Status
                    Case Net.NetworkInformation.IPStatus.Success
                        pingJob.Message = e.Reply.RoundtripTime & " ms"
                        DataGrid.Rows(pingJob.RowIndex).Cells(2).Value = pingJob.Message
                        DataGrid.Rows(pingJob.RowIndex).DefaultCellStyle.ForeColor = PingNoProblem
                    Case Else
                        pingJob.Message = "no reply"
                        DataGrid.Rows(pingJob.RowIndex).Cells(2).Value = pingJob.Message
                        DataGrid.Rows(pingJob.RowIndex).DefaultCellStyle.ForeColor = PingProblem
                End Select
            End If
        Next

        With DirectCast(sender, Net.NetworkInformation.Ping)
            ' Remove handler because it is no longer needed
            RemoveHandler .PingCompleted, AddressOf GetPingResult
            ' Clean up unmanaged resources
            '.Dispose()
        End With
    End Sub

    Private Sub DataGrid_MouseDoubleClick(sender As Object, e As MouseEventArgs) Handles DataGrid.MouseDoubleClick
        If Not bulkScanWorking Then
            UpdateDisplay()
        End If
    End Sub

    '===================================================================================================================
    ' FIN
    '===================================================================================================================

End Class