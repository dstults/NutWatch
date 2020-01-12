Public Class FrmNutWatcher

    '===================================================================================================================
    ' DECLARATIONS!
    '===================================================================================================================

    Public programFullNameAndVersion = "NutWatcher v0.80"
    Public programMajorVersionReleaseDate = "2020-01-11"
    Public myTimeout = 1000 ' ms
    Public defaultFile As String = "config.csv"

    Public Class ClsScanPingIp
        Public Name As String
        Public Color As Color = Color.Azure
        Public Ip As Net.IPAddress
        Public Interval As Integer = 5
        Public NextScanTime As DateTime
        Public PingTest As Net.NetworkInformation.Ping
        Public LastResult As Integer
        Public LastResultType As Integer
    End Class

    Public Class ClsScanPingDns
        Public Name As String
        Public Color As Color = Color.LightBlue
        Public Dns As String
        Public FirstKnownIP As Net.IPAddress
        Public Interval As Integer = 10
        Public NextScanTime As DateTime
        Public PingTest As Net.NetworkInformation.Ping
        Public LastResult As Integer
        Public LastResultType As Integer
    End Class

    Public Class ClsScanTcpIp
        Public Name As String
        Public Color As Color = Color.Coral
        Public Ip As Net.IPAddress
        Public Port As Integer
        Public Interval As Integer = 5
        Public NextScanTime As DateTime
        Public TcpClient As Net.Sockets.TcpClient
        Public LastResult As Integer
        Public LastResultType As Integer
    End Class

    Public Class ClsScanTcpDns
        Public Name As String
        Public Color As Color = Color.Pink
        Public Dns As String
        Public FirstKnownIP As Net.IPAddress
        Public Port As Integer
        Public Interval As Integer = 10
        Public NextScanTime As DateTime
        Public TcpClient As Net.Sockets.TcpClient
        Public LastResult As Integer
        Public LastResultType As Integer
    End Class

    Public Enum ResultType
        nodata
        replied
        open
        noreply
        closed
        dnsIpChanged
    End Enum

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
        For Each iStr As String In textDump
            AddScan(iStr)
        Next
    End Sub

    Public Sub AddScan(inText As String)
        Dim csvs() = Split(inText, ",")
        Dim setName As String = csvs(0), setTargetData As String = csvs(1), setScanData As String = csvs(2)
        Dim parseTargetData() As String = Split(setTargetData, " ")
        Dim parseScanData() As String = Split(setScanData, " ")
        If parseTargetData(0) = "IP" And parseScanData(0) = "PING" Then
            Dim myScan As New ClsScanPingIp
            IpPingScanSet.Add(myScan)

            myScan.Name = setName
            If Net.IPAddress.TryParse(parseTargetData(1), myScan.Ip) Then
                ' Do nothing
            Else
                MsgBox("Error, invalid IPv4: " & parseTargetData(1))
            End If
            myScan.NextScanTime = DateTime.Now.AddSeconds(myScan.Interval)

        ElseIf parseTargetData(0) = "IP" And parseScanData(0) = "TCP" Then
            Dim myScan As New ClsScanTcpIp
            IpTcpScanSet.Add(myScan)

            myScan.Name = setName
            If Net.IPAddress.TryParse(parseTargetData(1), myScan.Ip) Then
                ' Do nothing
                'MsgBox(myScan.Ip.ToString) ' Debug
            Else
                MsgBox("Error, invalid IPv4: " & parseTargetData(1))
            End If
            myScan.Port = CInt(parseScanData(1))
            myScan.NextScanTime = DateTime.Now.AddSeconds(myScan.Interval)

        ElseIf parseTargetData(0) = "DNS" And parseScanData(0) = "PING" Then
            Dim myScan As New ClsScanPingDns
            DnsPingScanSet.Add(myScan)

            myScan.Name = setName
            myScan.Dns = parseTargetData(1)
            myScan.NextScanTime = DateTime.Now.AddSeconds(myScan.Interval)

        ElseIf parseTargetData(0) = "DNS" And parseScanData(0) = "TCP" Then
            Dim myScan As New ClsScanTcpDns
            DnsTcpScanSet.Add(myScan)

            myScan.Name = setName
            myScan.Dns = parseTargetData(1)
            myScan.Port = CInt(parseScanData(1))
            myScan.NextScanTime = DateTime.Now.AddSeconds(myScan.Interval)

        End If

    End Sub

    Private Sub TmrSlowUpdate_Tick(sender As Object, e As EventArgs) Handles tmrUpdate.Tick
        UpdateDisplay()
    End Sub

    Private Sub UpdateDisplay()
        Static lastString As String
        PerformAllScans()
        Dim newString As String = ScanReport()

        If lastString <> newString Then
            txtReport.Text = newString
            lastString = newString
        Else
            ' Do nothing.
        End If
    End Sub

    Private Sub PerformAllScans()
        For Each pingJob As ClsScanPingIp In IpPingScanSet
            Try
                pingJob.PingTest.SendAsync(pingJob.Ip.ToString, myTimeout, pingJob)
                AddHandler pingJob.PingTest.PingCompleted, AddressOf GetPingResult
            Catch ex As Exception
                'LogError(ex)
            End Try
        Next
    End Sub

    Private Function ScanReport() As String
        Dim textDump As String = ""
        If IpPingScanSet.Count + DnsPingScanSet.Count + IpTcpScanSet.Count + DnsTcpScanSet.Count = 0 Then
            textDump = "Error: No scans in program memory."
        Else
            Dim lotsOfSpaces = "                       "
            For Each NetJob As ClsScanPingIp In IpPingScanSet
                textDump &= Strings.Left(NetJob.Name & lotsOfSpaces, 20) & ", "
                textDump &= Strings.Left(NetJob.Ip.ToString & lotsOfSpaces, 23) & ", "
                textDump &= GetResultTypeString(NetJob.LastResultType)
                textDump &= vbNewLine
            Next
            For Each NetJob As ClsScanPingDns In DnsPingScanSet
                textDump &= Strings.Left(NetJob.Name & lotsOfSpaces, 20) & ", "
                textDump &= Strings.Left(NetJob.Dns & lotsOfSpaces, 23) & ", "
                textDump &= GetResultTypeString(NetJob.LastResultType)
                textDump &= vbNewLine
            Next
            For Each NetJob As ClsScanTcpIp In IpTcpScanSet
                textDump &= Strings.Left(NetJob.Name & lotsOfSpaces, 20) & ", "
                textDump &= Strings.Left(NetJob.Ip.ToString & " : " & NetJob.Port & lotsOfSpaces, 23) & ", "
                textDump &= GetResultTypeString(NetJob.LastResultType)
                textDump &= vbNewLine
            Next
            For Each NetJob As ClsScanTcpDns In DnsTcpScanSet
                textDump &= Strings.Left(NetJob.Name & lotsOfSpaces, 20) & ", "
                textDump &= Strings.Left(NetJob.Dns & " : " & NetJob.Port & lotsOfSpaces, 23) & ", "
                textDump &= GetResultTypeString(NetJob.LastResultType)
                textDump &= vbNewLine
            Next
        End If
        Return textDump
    End Function

    Public Function GetResultTypeString(resultNum As Integer) As String
        Dim result As String = ""
        Select Case resultNum
            Case ResultType.nodata
                result = "No data"
            Case ResultType.replied
                result = "Pinged"
            Case ResultType.noreply
                result = "No ping reply"
            Case ResultType.dnsIpChanged
                result = "DNS ip different than before"
            Case ResultType.open
                result = "port open"
            Case ResultType.closed
                result = "Port closed"
        End Select
        Return Strings.Left(result & "                    ", 20)
    End Function

    Private Sub GetPingResult(ByVal sender As Object, ByVal e As System.Net.NetworkInformation.PingCompletedEventArgs)
        ' e.UserState is the UserToken passed to the pingAsync, we will use it to find the matching ping job
        For Each pingJob As ClsScanPingIp In IpPingScanSet
            If pingJob Is e.UserState Then
                pingJob.LastResult = e.Reply.RoundtripTime
                Select Case e.Reply.Status
                    Case Net.NetworkInformation.IPStatus.Success
                        pingJob.LastResultType = ResultType.replied
                    Case Else
                        pingJob.LastResultType = ResultType.noreply
                End Select
            End If
        Next
        For Each pingJob As ClsScanPingDns In DnsPingScanSet
            If pingJob Is e.UserState Then

            End If
        Next

        With DirectCast(sender, Net.NetworkInformation.Ping)
            ' Remove handler because it is no longer needed
            RemoveHandler .PingCompleted, AddressOf GetPingResult
            ' Clean up unmanaged resources
            .Dispose()
        End With
    End Sub

    '===================================================================================================================
    ' FIN
    '===================================================================================================================

End Class