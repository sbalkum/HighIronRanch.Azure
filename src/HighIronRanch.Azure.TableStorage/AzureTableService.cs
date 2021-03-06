﻿using System.Text.RegularExpressions;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace HighIronRanch.Azure.TableStorage
{
	public interface IAzureTableSettings
	{
		string AzureStorageConnectionString { get; }
	}

	public class AzureTableService : IAzureTableService
	{
		private readonly IAzureTableSettings _appSettings;

		public AzureTableService(IAzureTableSettings appSettings)
		{
			_appSettings = appSettings;
		}

		protected string CleanseTableName(string uncleanTableName)
		{
			return Regex.Replace(uncleanTableName, @"[^a-zA-Z0-9]", "");
		}

	    protected CloudTableClient GetClient()
	    {
            var client = CloudStorageAccount.Parse(_appSettings.AzureStorageConnectionString).CreateCloudTableClient();
            //client.PayloadFormat = TablePayloadFormat.AtomPub;
	        return client;
	    }

        public CloudTable GetTable(string tableName, bool shouldCreateIfNotExists)
        {
            var client = GetClient();

			var cleansedTableName = CleanseTableName(tableName);
			
			var table = client.GetTableReference(cleansedTableName);
			if(shouldCreateIfNotExists)
				table.CreateIfNotExists();
			return table;
		}

		public CloudTable GetTable(string tableName)
		{
			return GetTable(tableName, true);
		}

	}
}