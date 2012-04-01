﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using System.Data.Common;

using LibOfLegends;

namespace RiotControl
{
	public partial class Worker
	{
		public EngineRegionProfile Profile
		{
			get;
			private set;
		}

		public RegionType Region
		{
			get;
			private set;
		}

		public bool Connected
		{
			get;
			private set;
		}

		Program Program;
		StatisticsService StatisticsService;
		Database Provider;

		Configuration Configuration;
		AuthenticationProfile AuthenticationProfile;
		RPCService RPC;

		Profiler Profiler;

		HashSet<int> ActiveAccountIds;

		int AutomaticUpdateInterval;

		public Worker(Program program, StatisticsService statisticsService, EngineRegionProfile regionProfile, Configuration configuration, Database provider)
		{
			Program = program;
			StatisticsService = statisticsService;
			Provider = provider;

			Configuration = configuration;
			Profile = regionProfile;

			Connected = false;

			Profiler = new Profiler();
			ActiveAccountIds = new HashSet<int>();

			Region = (RegionType)Profile.Identifier;

			AutomaticUpdateInterval = configuration.AutomaticUpdateInterval;

			InitialiseAuthenticationProfile();
		}

		void InitialiseAuthenticationProfile()
		{
			//Create a new authentication profile that uses the client version from the master server instead of the (null) one provided by the configuration file
			AuthenticationProfile = new AuthenticationProfile();
			AuthenticationProfile.ClientVersion = Profile.ClientVersion;
			AuthenticationProfile.Domain = Configuration.Authentication.Domain;
			AuthenticationProfile.IPAddress = Configuration.Authentication.IPAddress;
			AuthenticationProfile.Locale = Configuration.Authentication.Locale;
		}

		DatabaseCommand Command(string query, DbConnection connection, params object[] arguments)
		{
			return new DatabaseCommand(query, connection, Profiler, arguments);
		}

		void WriteLine(string input, params object[] arguments)
		{
			Program.WriteLine(string.Format("{0} [{1} {2}] {3}", Nil.Time.Timestamp(), Profile.Abbreviation, Profile.Login.Username, input), arguments);
		}

		void SummonerMessage(string message, Summoner summoner, params object[] arguments)
		{
			WriteLine(string.Format("{0} ({1}): {2}", summoner.SummonerName, summoner.AccountId, message), arguments);
		}

		public void Run()
		{
			Connect();
		}	

		void Connect()
		{
			//Obtain a lock on the profile to avoid race conditions while the user is editing the data
			lock (Profile)
			{
				if (Profile.Login == null)
				{
					//The user has removed the login for this worker after the worker had been previously connecting - cancel
					WriteLine("No login specified");
					return;
				}
				else
				{
					ConnectionProfile connectionData = new ConnectionProfile(AuthenticationProfile, Profile.Region, Configuration.Proxy, Profile.Login.Username, Profile.Login.Password);
					RPC = new RPCService(connectionData, OnConnect, OnDisconnect);
					WriteLine("Connecting to the server");
				}
			}
			RPC.Connect();
		}

		void ConnectInThread()
		{
			(new Thread(Connect)).Start();
		}

		void OnConnect(RPCConnectResult result)
		{
			if (result.Success())
			{
				Connected = true;
				WriteLine("Successfully connected to the server");
				(new Thread(RunAutomaticUpdates)).Start();
			}
			else
			{
				WriteLine(result.GetMessage());
				//Just reconnect right away
				//This is a bit of a hack, required to make this work with Mono because connections will just randomly fail there
				ConnectInThread();
			}
		}

		void OnDisconnect()
		{
			//You get disconnected after idling for two hours
			Connected = false;
			WriteLine("Disconnected");
			//Reconnect
			Thread.Sleep(5000);
			ConnectInThread();
		}

		string GetGroupString(string[] fields)
		{
			return String.Join(", ", fields);
		}

		string GetPlaceholderString(string[] fields)
		{
			var mapped = from x in fields
						 select string.Format(":{0}", x);
			return GetGroupString(mapped.ToArray());
		}

		string GetUpdateString(string[] fields)
		{
			var mapped = from x in fields
						 select string.Format("{0} = :{0}", x);
			return GetGroupString(mapped.ToArray());
		}

		int GetInsertId(DbConnection connection)
		{
			return (int)(long)Command("select last_insert_rowid()", connection).ExecuteScalar();
		}

		void Reconnect()
		{
			RPC.Disconnect();
			Connect();
		}

		void Timeout()
		{
			WriteLine("A remote call has timed out, attempting to reconnect");
			Reconnect();
		}

		void RunAutomaticUpdates()
		{
			while (Connected)
			{
				List<Summoner> summoners = StatisticsService.GetAutomaticUpdateSummoners(Region);
				if (summoners.Count > 0)
					WriteLine("Performing automatic updates for {0} summoner(s)", summoners.Count);
				foreach (var summoner in summoners)
				{
					WriteLine("Performing automatic updates for summoner " + summoner.SummonerName);
					OperationResult result = UpdateSummonerByAccountId(summoner.AccountId);
					if (!Connected)
						break;
					if (result != OperationResult.Success && result != OperationResult.NotFound)
					{
						//There might be something fishy going on with the connection, delay the next operation
						Thread.Sleep(10000);
					}
				}
				if (summoners.Count > 0)
					WriteLine("Done performing automatic updates for {0} summoner(s)", summoners.Count);
				if (Connected)
					Thread.Sleep(AutomaticUpdateInterval * 1000);
			}
		}
	}
}
