﻿using System;
using System.Threading;
using System.Net.Sockets;
using System.Text;
using ByteBufferDLL;
using EnumsServer;
using System.Net;
using System.Collections.Generic;
using MongoDB.Driver;
using System.IO;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace ServerEcho
{
	class Globals //Class used to share data between threades
	{
		public static TcpClient[] clients = new TcpClient[20];
		public static TcpClient[] httpClient = new TcpClient[2];
		//public static Dictionary<string, Player> dicPlayers1 = new Dictionary<string, Player>();
		public static Dictionary<int, Player> dicPlayers = new Dictionary<int, Player>();
		public static int i = -1;
		private static Player[] p = new Player[2];
		public static bool restart = false;
		public static bool mainRun = true;
		public static bool threadsRun = false;

		public static void FeedDataToArray()
		{
			p[0] = new Player();
			p[0].cName = "ubaduba";
			p[0].uName = "Player1";
			p[0].head = 4;
			p[0].body = 0;
			p[0].cloths = 4;

			p[1] = new Player();
			p[1].cName = "sfsadfsafd";
			p[1].uName = "Player2";
			p[1].head = 3;
			p[1].body = 0;
			p[1].cloths = 3;
		}

		public static Player GetPlayer()
		{
			i++;
			return p[i];
		}

		public static void ChangeNoOfPlayers(int n)
		{
			clients = new TcpClient[n];
		}
	}

	/// <summary>
	/// The class that starts the server
	/// Contains method to load the server config and start the server.
	/// </summary>
	class TCP_Server
	{
		private bool run = true;
		TcpListener serverSocket;
		TcpListener httpSocket;
		int counter = 0;

		public TCP_Server(string path)
		{
			StreamReader reader = new StreamReader(path);
			string line = reader.ReadLine();
			int port = int.Parse(line.Substring(line.LastIndexOf('=')));

			line = reader.ReadLine();

			int nOfPlayers = int.Parse(line.Substring(line.LastIndexOf('=')));
			Globals.ChangeNoOfPlayers(nOfPlayers);

			serverSocket  = new TcpListener(IPAddress.Any, port);
			httpSocket = new TcpListener(IPAddress.Any, 5000);
		}

		static void Main(string[] args)
		{
			Globals.FeedDataToArray();
			TCP_Server tcp = new TCP_Server("");
			tcp.Start();
			
		}

		public void Start()
		{
			httpSocket.Start();
			serverSocket.Start();

			Task task = Task.Run(() => 
			{
				while (run)
				{
					for (int i = 0; i < Globals.httpClient.Length; i++)
					{
						Globals.httpClient[i] = new TcpClient();
						Globals.httpClient[i] = httpSocket.AcceptTcpClient();
					}
				}
			});

			int counter = 0;

			JwtTokens.LoadKey("path to file containing key");
			
			Console.WriteLine(" >> TCP IP Server Started");

			serverstart:
			while (Globals.mainRun)
			{
				for (int i = 0; i < Globals.clients.Length; i++)
				{
					if (Globals.clients[i] == null)
					{
						Globals.clients[i] = new TcpClient();
						Globals.clients[i] = serverSocket.AcceptTcpClient();
						Console.WriteLine(" >> " + "Client No:" + Convert.ToString(counter) + " started! " + Globals.clients[i].Client.LocalEndPoint);
						HandleClinet client = new HandleClinet();
						client.startClient(Globals.clients[i], counter, Globals.clients[i].Client.LocalEndPoint.ToString());
						counter++;
					}
				}

			}

			
			serverSocket.Stop();
			Console.WriteLine(" >> " + "exit");

			if (Globals.restart)
			{
				//re read config file
				Globals.restart = false;
				Globals.threadsRun = true;
				Globals.mainRun = true;
				goto serverstart;
			}
		}

		public void CloseSocket()
		{
			for (int i = 0; i < Globals.clients.Length; i++)
			{
				if (Globals.clients[i] != null)
				{
					//update db
					Globals.clients[i].Close();
				}
			}
			serverSocket.Stop();
		}
	}

	/// <summary>
	/// The class that handles the incoming connections from clients
	/// Contains all methods to handle received data packets and send packets to clients
	/// </summary>
	public class HandleClinet
	{
		private TcpClient clientSocket;
		private int clNo;
		private int count = 1;
		private string ip;

		public void startClient(TcpClient inClientSocket, int clineNo, string ip)
		{
			this.clientSocket = inClientSocket;
			this.clNo = clineNo;
			Thread ctThread = new Thread(doClient);
			ctThread.Start();
		}

		private void doClient() 
		{
			int requestCount = 0;
			byte[] bytesFrom = new byte[4096];
			bool run = true;
			requestCount = 0;
			NetworkStream networkStream = clientSocket.GetStream();

			
			while (!networkStream.DataAvailable) {Thread.Sleep(50);}// waits for package with the auth key
			
			ByteBuffer buffer = new ByteBuffer();

			networkStream.Read(bytesFrom, 0, 4096);
			buffer.WriteBytes(bytesFrom);

			JwtTokens.EvaluateToken(buffer.ReadString());
			Player pl = new Player();
			pl.uName = buffer.ReadString();
			pl.cName = buffer.ReadString();
			pl.head = buffer.ReadInt();
			pl.body = buffer.ReadInt();
			pl.cloths = buffer.ReadInt();
			pl.socketID = clNo;
			
			Globals.dicPlayers.Add(clNo, pl);

			

			//IF YOU WANT TO TEST WITH THE TEST SCENE, USE THIS CODE
			/*ByteBuffer buffer = new ByteBuffer();
			//buffer.WriteInt(0);
			buffer.WriteInt((int)Enums.AllEnums.SSendingPlayerID);
			//buffer.WriteInt(clNo);

			Player pl = Globals.GetPlayer();
			Globals.dicPlayers.Add(clNo, pl); 
			buffer.WriteString(pl.uName);
			buffer.WriteString(pl.cName);
			buffer.WriteInt(pl.head);
			buffer.WriteInt(pl.body);
			buffer.WriteInt(pl.cloths);*/

			/*byte[] size = BitConverter.GetBytes(buffer.Size());
			byte[] aux = buffer.ToArray();

			aux[0] = size[0];
			aux[1] = size[1];
			aux[2] = size[2];
			aux[3] = size[3];

			/*Console.WriteLine(buffer.Size());
			Console.WriteLine(buffer.ToArray().Length);*/
			//networkStream = clientSocket.GetStream();

			Globals.clients[clNo].GetStream().Write(buffer.ToArray(), 0, buffer.ToArray().Length);
			networkStream.Flush();
			Console.WriteLine(buffer.ToArray().Length+" to "+clNo);

			NotifyAlreadyConnected(clNo, pl);
			NotifyMainPlayerOfAlreadyConnected(clNo);

			count++;
			while (Globals.threadsRun)
			{
				try
				{
					requestCount++;
					networkStream = clientSocket.GetStream();

					if (networkStream.DataAvailable)
					{
						buffer = new ByteBuffer();
						networkStream.Read(bytesFrom, 0, 4096);
						buffer.WriteBytes(bytesFrom);

						buffer.ReadInt(); // ignoring package size
						int packageID = buffer.ReadInt();

						if (packageID == (int)Enums.AllEnums.SCloseConnection)
						{
							run = false;
						}

						HandleMessage(packageID, clNo,buffer.ToArray()); 
						
					}

					Thread.Sleep(50);

				}
				catch (Exception ex)
				{
					Console.WriteLine(" >> " + ex.ToString());
				}
			}

			CloseConnection(clNo);
		}

		static void HandleMessage(int mID,int id, byte[] data)
		{
			switch (mID)
			{
				case (int)Enums.AllEnums.SSyncingPlayerMovement:
					{
						//Console.WriteLine("Packet movement: " + id);
						SendToAllBut(id, data);
						break;
					}
				case (int)Enums.AllEnums.SSendingMessage:
					{
						SendToAllBut(id, data);
						break;
					}
			}
		}
			
		static void NotifyMainPlayerOfAlreadyConnected(int id) // sends already connected to players current player
		{
			for (int i = 0; i < 20; i++)
			{
				if (Globals.clients[i] != null && Globals.clients[i].Connected)
				{
					if (i != id)
					{
						Console.WriteLine(i);
						ByteBuffer buffer = new ByteBuffer();
						//buffer.WriteInt(0);
						buffer.WriteInt((int)Enums.AllEnums.SSendingAlreadyConnectedToMain);
						buffer.WriteString(Globals.dicPlayers[i].uName);
						buffer.WriteString(Globals.dicPlayers[i].cName);
						buffer.WriteInt(Globals.dicPlayers[i].head);
						buffer.WriteInt(Globals.dicPlayers[i].body);
						buffer.WriteInt(Globals.dicPlayers[i].cloths);

						/*byte[] size = BitConverter.GetBytes(buffer.Size());
						byte[] aux = buffer.ToArray();

						aux[0] = size[0];
						aux[1] = size[1];
						aux[2] = size[2];
						aux[3] = size[3];*/

						//Thread.Sleep(1500); //If the thread doesnt sleep, the packet is not sent

						Console.WriteLine(buffer.ToArray().Length);

						Globals.clients[id].GetStream().Write(buffer.ToArray(), 0, buffer.ToArray().Length);
						//Globals.clients[id].GetStream().Flush();
						Console.WriteLine("Sending sync to "+id);
					}
				}
			}
		}

		static void NotifyAlreadyConnected(int id, Player p) // sends current player to already connected player 
		{
			ByteBuffer buffer = new ByteBuffer();

			buffer.WriteInt((int)Enums.AllEnums.SSendingMainToAlreadyConnected);
			buffer.WriteString(p.uName);
			buffer.WriteString(p.cName);
			buffer.WriteInt(p.head);
			buffer.WriteInt(p.body);
			buffer.WriteInt(p.cloths);

			/*byte[] size = BitConverter.GetBytes(buffer.Size());
			byte[] aux = buffer.ToArray();

			aux[0] = size[0];
			aux[1] = size[1];
			aux[2] = size[2];
			aux[3] = size[3];*/

			for (int i = 0; i < 20; i++)
			{
				if (Globals.clients[i] != null && Globals.clients[i].Connected)
				{
					if (i != id)
					{
						
						Globals.clients[i].GetStream().Write(buffer.ToArray(), 0, buffer.ToArray().Length);
						Globals.clients[i].GetStream().Flush();
					}
				}
			}
		}

		static void SendToAllBut(int id, byte[] data)
		{
			
			for (int i = 0; i < 20; i++)
			{
				if (Globals.clients[i] != null && Globals.clients[i].Connected)
				{
					if (i != id)
					{
						Console.WriteLine("Sending move from "+id+" to " + i);
						Globals.clients[i].GetStream().Write(data, 0, data.Length);
						Globals.clients[i].GetStream().Flush();
					}
				}
			}
		}

		static void SendMessage()
		{

		}

		//UPDATE DB
		static void CloseConnection(int id)
		{
			byte[] buffer = new byte[4]; //because the client expects an int
			buffer[0] = (int)Enums.AllEnums.SCloseConnection;
			SendToAllBut(id, buffer);
			Globals.clients[id].Client.Close();
			//Update player playtime
		}
	}

	public static class JwtTokens
	{
		private static string key;

		public static bool EvaluateToken(string text)
		{
			try
			{
				Jose.JWT.Decode(text, Encoding.ASCII.GetBytes(key));
				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}

		public static void LoadKey(string path)
		{
			key = "";
			/*StreamReader reader = new StreamReader(path);
			key = reader.ReadLine();
			reader.Close();*/
		}

	}

	public class DB //Singleton
	{
		private DB _db;
		private IMongoDatabase mongodb;
		MongoClient client;

		private DB() { }
		public DB getInstance(string path, string port, string dbName)
		{
			if (_db == null)
			{
				_db = new DB();
				client = new MongoClient();
				mongodb = client.GetDatabase(dbName);
			}

			return _db;
		}
		public Player GetPlayer(string uName,string cName)
		{
			var coll = mongodb.GetCollection<Player>(""); //collection's name in db

			var p = coll.Find(pl => pl.uName == uName && pl.cName==cName);

			return (Player)p;
		}
		public void UpdatePlayer(Player p)
		{ }
	}

	/// <summary>
	/// The class that handles the incoming connections from clients
	/// Contains all methods to handle received data packets and send packets to clients
	/// </summary>
	public class HandleHttpClient
	{
		private TcpClient clientSocket;
		private int clNo;

		public void StartHttpClient(TcpClient inClientSocket, int clineNo)
		{
			this.clientSocket = inClientSocket;
			this.clNo = clineNo;
			Thread ctThread = new Thread(HandleClient);
			ctThread.Start();
		}

		private void HandleClient()
		{
			byte[] bytesFrom = new byte[4096];
			bool run = true;
			NetworkStream networkStream = clientSocket.GetStream();

			while (run)
			{
				if(!networkStream.DataAvailable) { Thread.Sleep(50); }

				networkStream.Read(bytesFrom, 0, 4096);

				ByteBuffer buffer = new ByteBuffer();
				buffer.WriteBytes(bytesFrom);
				short id=buffer.ReadByte();
				string token = buffer.ReadString();
				if (!JwtTokens.EvaluateToken(token))
				{ }
				else { HandleID(id, bytesFrom); }
			}
		}

		private void HandleID(short id,byte[] data)
		{
			switch (id)
			{
				case (short)Enums.AllEnums.HChangeSettings:
					{
						HChangeSettings(data);
						break;
					}
				case (short)Enums.AllEnums.HGetSettings:
					{
						HGetSettings();
						break;
					}
				case (short)Enums.AllEnums.HKickPlayer:
					{
						HKickPlayer(data);
						break;
					}
				case (short)Enums.AllEnums.HListPlayers:
					{
						HListPlayers();
						break;
					}
				case (short)Enums.AllEnums.HRestartServer:
					{
						HRestartServer();
						break;
					}
			}
		}

		/// <summary>
		/// Changes the server's cofiguration file
		/// </summary>
		/// <param name="data">Byte array containing data received from http server</param>
		private void HChangeSettings(byte[] data)
		{
			ByteBuffer buffer = new ByteBuffer();
			buffer.WriteBytes(data);

			int port = buffer.ReadInt();
			int nOfPlayers = buffer.ReadInt();

			string configFile = "";
			string line = "";

			StreamReader reader = new StreamReader("config.txt");
			while ((line = reader.ReadLine()) != null)
			{
				if (Regex.IsMatch("^port", line))
				{
					configFile += line.Substring(0, line.IndexOf('=')) + port;
				}
				else if (Regex.IsMatch("^number", line))
				{
					configFile += line.Substring(0, line.IndexOf('=')) + nOfPlayers;
				}
				configFile += line;
			}

			reader.Close();

			StreamWriter writer = new StreamWriter("config.txt");
			writer.Write(configFile);
			writer.Close();
			
		}

		/// <summary>
		/// Returns server current configuration 
		/// </summary>
		private void HGetSettings()
		{
			StreamReader reader = new StreamReader("config.txt");
			string file = reader.ReadToEnd();

			byte[] buffer = new byte[Encoding.ASCII.GetByteCount(file)];
			buffer = Encoding.ASCII.GetBytes(file);

			Globals.httpClient[clNo].GetStream().Write(buffer, 0, buffer.Length);
		}

		/// <summary>
		/// ********NOT FISHED*********
		/// Closes the connection of a specific player
		/// </summary>
		private void HKickPlayer(byte[] data)
		{
			ByteBuffer buffer = new ByteBuffer();
			buffer.WriteBytes(data);

			string playerid = buffer.ReadString();
			foreach (Player p in Globals.dicPlayers.Values)
			{
				if (p.uName == playerid)
				{
					//update playtime on db
					//update index
					Globals.clients[p.socketID].Close();
					Globals.clients[p.socketID] = null;
					break;
				}
			}

			
		}
		
		/// <summary>
		/// Returns stats of all current connected players
		/// </summary>
		private void HListPlayers()
		{
			ByteBuffer buffer = new ByteBuffer();
			TimeSpan aux = new TimeSpan();
			foreach (Player p in Globals.dicPlayers.Values)
			{
				buffer.WriteString(p.uName);
				buffer.WriteString(p.cName);
				buffer.WriteString(p.playerIP);
				aux = DateTime.Now - p.currentPlaytime;
				buffer.WriteInt(aux.Hours*60+aux.Minutes); 
				buffer.WriteInt(p.totalPlaytime+(aux.Hours * 60 + aux.Minutes)); 
			}

			Globals.httpClient[clNo].GetStream().Write(buffer.ToArray(), 0, buffer.ToArray().Length);
		}

		/// <summary>
		/// 
		/// </summary>
		private void HRestartServer()
		{ }
	}
}