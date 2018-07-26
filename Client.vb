﻿Imports System
Imports System.Text
Imports System.Net.Sockets
Imports System.Threading

Namespace SocketDriver


    Public Class Client : Implements IFTcpClient

        Private TCPIPAddress As String                      ' TCP IP Address
        Private TCPPort As Integer                          ' TCP Port
        Private client As System.Net.Sockets.TcpClient      ' TCP Client 物件
        Private listener As IFMessageListener               ' Message Listener

        Private startReceive As Thread                      ' 負責 Receive Message 的 Thread

        Private bufferlist As ArrayList                     ' 暫存區


        ' 建構子
        Public Sub New(ByVal slistener As IFMessageListener)

			TCPIPAddress = "127.0.0.1"
			TCPPort = 36000
			listener = slistener

            ' New ArrayList (暫存區)
            bufferlist = New ArrayList

            ' 連線
            connect()

		End Sub


		' 建構子
		Public Sub New(ByVal slistener As IFMessageListener, ByVal sIPAddress As String, ByVal sPort As Integer)

			TCPIPAddress = sIPAddress
			TCPPort = sPort
			listener = slistener

            ' New ArrayList (暫存區)
            bufferlist = New ArrayList

            ' 連線
            connect()

		End Sub


		' 連線
		Public Sub connect() Implements IFTcpClient.connect

            Try
                client = New Net.Sockets.TcpClient()
                client.Connect(TCPIPAddress, TCPPort)
                listener.sysMessage("Connected")

                ' New Thread (負責 Receive Message 的 Thread)
                startReceive = New Thread(AddressOf receive)
                startReceive.IsBackground = True
                startReceive.Start()

            Catch e As Exception

                listener.sysMessage("NotConnected")

            End Try

        End Sub


		' 重新連線
		Public Sub Reconnect()

			Try
				client.Close()
				client.Dispose()

			Catch e As Exception

                listener.sysMessage("NotConnected")

			End Try

            ' 連線
            connect()

		End Sub


		' 關閉連線
		Public Sub disconnect() Implements IFTcpClient.disconnect

            startReceive.Abort()
            client.Close()

		End Sub


		' Send Message
		Public Sub send(ByVal message As Byte()) Implements IFTcpClient.send

			Try
                If client.Client.Connected Then

                    client.Client.Send(message)
                Else

                    listener.sysMessage("NotConnected")

                End If

			Catch e As Exception

                listener.sysMessage("NotConnected")

			End Try
		End Sub


		' Receive Message
		Public Sub receive() Implements IFTcpClient.receive

            ' 暫存變數 : 是否繼續 Receive 迴圈
            Dim IsOpen As Boolean = True

            ' 測試用的 Byte
            Dim testByte(1) As Byte

			While IsOpen

                Try
                    ' 檢查連線
                    If client.Client.Poll(0, SelectMode.SelectRead) = False Then

                        ' 使用 Peek 測試連線是否還存在
                        ' 如果對方斷線時，會在這邊處理
                        Try
                            client.Client.Receive(testByte, SocketFlags.Peek)

                        Catch ex As Exception

                            IsOpen = False
                            listener.sysMessage("NotConnected")
                            Exit Try

                        End Try

                    End If

                    ' 取得 Socket 狀態
                    If client.Client.Connected Then

                        ' 當有收到資料時，才 Receive Message
                        If client.Client.Available > 0 Then

                            ' 暫存變數 : 將資料先儲存到此變數
                            Dim tempBytes(client.Client.Available - 1) As Byte

                            ' 當收到的資料位元組數為 0 時
                            If client.Client.Receive(tempBytes) = 0 Then

                                Application.DoEvents()
                                Thread.Sleep(1000)

                            Else
                                ' 將資料收到暫存區
                                For i As Integer = 0 To tempBytes.Length - 1 Step +1

                                    bufferlist.Add(tempBytes(i))
                                Next

                                ' 取前四個 Bytes 來計算出 Message Length
                                Dim temp(3) As Byte
                                For i As Integer = 0 To 3 Step +1
                                    temp(i) = bufferlist(i)
                                Next
                                Array.Reverse(temp)
                                Dim MessageLength As Integer = BitConverter.ToInt32(temp, 0)

                                ' 如果 Bytes 數量能組成 SecsMessage 時，便傳出 Message
                                ' 如果 Bytes 數量不能組成 SecsMessage 時，便設定 T8 Timeout
                                If bufferlist.Count >= MessageLength + 4 Then

                                    Dim messageByte As Byte() = New Byte(MessageLength + 3) {}

                                    For i As Integer = 0 To MessageLength + 3 Step +1

                                        messageByte(i) = bufferlist(0)
                                        bufferlist.RemoveAt(0)
                                    Next

                                    listener.onMessage(messageByte)

                                    Application.DoEvents()
                                    Thread.Sleep(1)

                                End If

                            End If

                        End If

                    End If

                Catch e As Exception

                    IsOpen = False
                    listener.sysMessage("Separate")

                End Try

            End While

		End Sub

	End Class

End Namespace

