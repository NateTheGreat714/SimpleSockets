﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using AsyncClientServer.Server;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace AsyncClientServer.Example.Server
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
		}

		private AsyncSocketSSLListener _listener;

		//Starts the server thread
		private void Window_Loaded_1(object sender, RoutedEventArgs e)
		{
			_listener = AsyncSocketSSLListener.Instance;
			//_listener = AsyncSocketListener.Instance;

			Thread t = new Thread(StartServer);
			t.Start();
		}

		private void StartServer()
		{
			try
			{
				int port = 13000;
				string ip = "127.0.0.1";


				_listener.ProgressFileReceived += new FileTransferProgressHandler(Progress);
				_listener.MessageReceived += new MessageReceivedHandler(MessageReceived);
				_listener.MessageSubmitted += new MessageSubmittedHandler(MessageSubmitted);
				_listener.ClientDisconnected += new ClientDisconnectedHandler(ClientDisconnected);
				_listener.ClientConnected += new ClientConnectedHandler(ClientConnected);
				_listener.FileReceived += new FileFromClientReceivedHandler(FileReceived);
				_listener.ServerHasStarted += new ServerHasStartedHandler(ServerHasStarted);


				_listener.StartListening(ip, port,
					@"C:\Users\CloetOMEN\Downloads\Cert\bin\signtool\PFXClientServerTest.pfx",
					"TestCertificate");
				_listener.StartListening(ip, port);
			}
			catch (Exception ex)
			{
				AppendRichtTextBox(ex.Message);
			}


		}

		//Append to textbox from separate thread.
		private void AppendRichtTextBox(string append)
		{
			Dispatcher.Invoke(() => { RichTextBoxOutput.AppendText(Environment.NewLine + append); });
		}

		//Events
		private void MessageReceived(int id, string header, string msg)
		{
			AppendRichtTextBox("Client " + id + " has send a " + header + ": " + msg);
			_listener.SendMessage(id, "The message has been received.", false);
		}

		private void MessageSubmitted(int id, bool close)
		{
			AppendRichtTextBox("Server sent a message to client " + id);
		}

		private void FileReceived(int id, string path)
		{
			_listener.SendMessage(id, "File has been received.", false);
			Dispatcher.Invoke(() => { ProgressBarProgress.Value = 0; });
			AppendRichtTextBox("Client " + id + "has send a file/folder and has been saved at \n" + path);
		}

		private void Progress(int id, int bytes, int messageSize)
		{
			double b = double.Parse(bytes.ToString());
			double m = double.Parse(messageSize.ToString());

			double percentageDone = b / m * 100;

			Dispatcher.Invoke(() => ProgressBarProgress.Value = percentageDone);
		}

		private void ServerHasStarted()
		{
			AppendRichtTextBox("\nThe server has started");
		}

		private void ClientConnected(int id)
		{
			AppendRichtTextBox("A new Client has connected with id " + id);
		}

		private void ClientDisconnected(int id)
		{
			AppendRichtTextBox("Client with id " + id + " has disconnected from the server.");
		}

		//End Events

		private string _selectedFileFolder = string.Empty;

		//Search Folder
		private void ButtonFolder_Click(object sender, RoutedEventArgs e)
		{
			FolderBrowserDialog dialog = new FolderBrowserDialog();
			DialogResult result = dialog.ShowDialog();

			if (result == System.Windows.Forms.DialogResult.OK)
			{
				_selectedFileFolder = dialog.SelectedPath;
			}

			TextBlockSource.Text = _selectedFileFolder;
		}

		//Search file.
		private void Button_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog dialog = new OpenFileDialog();

			dialog.InitialDirectory = "c:\\";
			var filter = "All Files | *.*";
			dialog.Filter = filter;
			dialog.FilterIndex = 1;
			dialog.RestoreDirectory = true;
			dialog.Multiselect = false;

			if (dialog.ShowDialog() == true)
			{
				_selectedFileFolder = dialog.FileNames[0];
			}

			TextBlockSource.Text = _selectedFileFolder;
		}

		//Buttons

		//Send File or folder.
		private async void ButtonSendFileFolder_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if (TextBlockFileFolderClientId.Text == string.Empty)
					throw new Exception("Please enter a client id.");

				string id = TextBlockFileFolderClientId.Text;
				int clientId = 0;

				if (int.TryParse(id, out int result))
				{
					clientId = result;
				}
				else
					throw new Exception("Please enter a number.");



				try
				{

					if (TextBlockTarget.Text == string.Empty)
						throw new Exception("The target cannot be empty.");
					if (_selectedFileFolder == string.Empty)
						throw new Exception("The source cannot be empty.");


					bool encrypt = CheckBoxFileFolder.IsChecked == true;

					if (Directory.Exists(Path.GetFullPath(_selectedFileFolder)))
					{
						await _listener.SendFolderAsync(clientId, Path.GetFullPath(_selectedFileFolder), Path.GetFullPath(TextBlockTarget.Text), encrypt, false);
					}
					else
					{
						await _listener.SendFileAsync(clientId, Path.GetFullPath(_selectedFileFolder),
							Path.GetFullPath(TextBlockTarget.Text), encrypt, true, false);
					}

				}
				catch (Exception ex)
				{
					AppendRichtTextBox("\nError \n" + ex.Message);
				}
			}
			catch (Exception ex)
			{
				AppendRichtTextBox("\nError \n" + ex.Message);
			}
		}

		//Send command
		private async void ButtonSendCommand_Click(object sender, RoutedEventArgs e)
		{
			try
			{

				if (TextBoxCommandContent.Text == string.Empty)
					throw new Exception("The command cannot be empty.");

				if (TextBoxCommandClientId.Text == String.Empty)
					throw new Exception("The client id has to filled in.");

				int clientId = 0;
				string content = TextBoxCommandContent.Text;

				if (int.TryParse(TextBoxCommandClientId.Text, out int result))
				{
					clientId = result;
				}
				else
				{
					throw new Exception("Enter a valid client id.");
				}

				bool encrypt = CheckBoxCommand.IsChecked == true;


				await _listener.SendCommandAsync(clientId, content, encrypt, false);
			}
			catch (Exception ex)
			{
				AppendRichtTextBox("\nError \n" + ex.Message);
			}
		}

		//Send Message
		private async void ButtonSendMessage_Click(object sender, RoutedEventArgs e)
		{
			try
			{

				if (TextBoxMessageContent.Text == string.Empty)
					throw new Exception("The message content cannot be empty.");

				if (TextBoxMessageClientId.Text == String.Empty)
					throw new Exception("The client id has to filled in.");

				int clientId = 0;
				string content = TextBoxMessageContent.Text;

				if (int.TryParse(TextBoxMessageClientId.Text, out int result))
				{
					clientId = result;
				}
				else
				{
					throw new Exception("Enter a valid client id.");
				}

				bool encrypt = CheckBoxMessage.IsChecked == true;

				await _listener.SendMessageAsync(clientId, content, encrypt, false);
			}
			catch (Exception ex)
			{
				AppendRichtTextBox("\nError \n" + ex.Message);
			}
		}

		//Close all threads when the app stops
		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			Environment.Exit(0);
		}
	}
}
