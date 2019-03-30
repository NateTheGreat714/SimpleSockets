﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using AsyncClientServer.StateObject;
using AsyncClientServer.StateObject.StateObjectState;

namespace AsyncClientServer.Client
{

	


	/// <summary>
	/// The Following code handles the client in an Async fashion.
	/// <para>To send messages to the corresponding Server, you should use the class "SendToServer"</para>
	/// <para>Extends <see cref="SendToServer"/>, Implements
	/// <seealso cref="ITcpClient"/>
	/// </para>
	/// </summary>
	public class AsyncClient : TcpClient
	{
		/// <summary>
		/// Starts the client.
		/// <para>requires server ip, port number and how many seconds the client should wait to try to connect again. Default is 5 seconds</para>
		/// </summary>
		public override void StartClient(string ipServer, int port, int reconnectInSeconds)
		{

			if (string.IsNullOrEmpty(ipServer))
				throw new ArgumentNullException(nameof(ipServer));
			if (port < 1)
				throw new ArgumentOutOfRangeException(nameof(port));
			if (reconnectInSeconds < 3)
				throw new ArgumentOutOfRangeException(nameof(reconnectInSeconds));


			IpServer = ipServer;
			Port = port;
			ReconnectInSeconds = reconnectInSeconds;
			_keepAliveTimer.Enabled = false;

			var host = Dns.GetHostEntry(ipServer);
			var ip = host.AddressList[0];
			_endpoint = new IPEndPoint(ip, port);

			try
			{
				//Try and connect
				_listener = new Socket(_endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
				_listener.BeginConnect(_endpoint, this.OnConnectCallback, _listener);
				_connected.WaitOne();

				//If client is connected activate connected event
				if (IsConnected())
				{
					InvokeConnected(this);
				}
				else
				{
					_keepAliveTimer.Enabled = false;
					InvokeDisconnected(this);
					Close();
					_connected.Reset();
					_listener.BeginConnect(_endpoint, OnConnectCallback, _listener);
				}

			}
			catch (Exception ex)
			{
				throw new Exception(ex.ToString());
			}
		}



		protected override void OnConnectCallback(IAsyncResult result)
		{
			var server = (Socket)result.AsyncState;

			try
			{
				//Client is connected to server and set connected variable
				server.EndConnect(result);
				_connected.Set();
				_keepAliveTimer.Enabled = true;
				Receive();
			}
			catch (SocketException)
			{
				Thread.Sleep(ReconnectInSeconds * 1000);
				_listener.BeginConnect(_endpoint, this.OnConnectCallback, _listener);
			}
		}

		/// <summary>
		/// Sends data to server
		/// <para>This method should not be used,instead use methods in <see cref="SendToServer"/></para>
		/// </summary>
		/// <param name="bytes"></param>
		/// <param name="close"></param>
		protected override void SendBytes(byte[] bytes, bool close)
		{

			try
			{

				if (!this.IsConnected())
				{
					throw new Exception("Destination socket is not connected.");
				}
				else
				{
					var send = bytes;

					_close = close;
					_listener.BeginSend(send, 0, send.Length, SocketFlags.None, SendCallback, _listener);
				}
			}
			catch (Exception ex)
			{
				throw new Exception(ex.Message, ex);
			}
		}

		//Send message and invokes MessageSubmitted.
		protected override void SendCallback(IAsyncResult result)
		{
			try
			{
				var receiver = (Socket)result.AsyncState;
				receiver.EndSend(result);
			}
			catch (SocketException se)
			{
				throw new Exception(se.ToString());
			}
			catch (ObjectDisposedException se)
			{
				throw new Exception(se.ToString());
			}

			InvokeMessageSubmitted(_close);

			_sent.Set();
		}

		//Start receiving
		public  override void StartReceiving(IStateObject state, int offset = 0)
		{
			if (state.Buffer.Length < state.BufferSize && offset == 0)
			{
				state.ChangeBuffer(new byte[state.BufferSize]);
			}

			state.Listener.BeginReceive(state.Buffer, offset, state.BufferSize - offset, SocketFlags.None,
				this.ReceiveCallback, state);
		}

		//Handle a message
		protected override void HandleMessage(IAsyncResult result)
		{

			try
			{

				var state = (StateObject.StateObject)result.AsyncState;
				var receive = state.Listener.EndReceive(result);

				if (state.Flag == 0)
				{
					state.CurrentState = new InitialHandlerState(state, this, null);
				}


				if (receive > 0)
				{
					state.CurrentState.Receive(receive);
				}

				/*When the full message has been received. */
				if (state.Read == state.MessageSize)
				{
					StartReceiving(state);
					return;
				}

				/*Check if there still are messages to be received.*/
				if (receive == state.BufferSize)
				{
					StartReceiving(state);
					return;
				}

				//When something goes wrong
				state.Reset();
				StartReceiving(state);


			}
			catch (Exception ex)
			{
				throw new Exception(ex.ToString());
			}
		}

	}
}
