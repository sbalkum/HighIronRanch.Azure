﻿using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HighIronRanch.Core.Services;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace HighIronRanch.Azure.ServiceBus
{
	public interface IServiceBusSettings
	{
		string AzureServiceBusConnectionString { get; }
		string ServiceBusSubscriptionNamePrefix { get; }
		string ServiceBusMasterPrefix { get; }
	}

	public interface IServiceBus
	{
		Task CreateQueueAsync(string name, bool isSessionRequired);
		Task<QueueClient> CreateQueueClientAsync(string name);
		Task<QueueClient> CreateQueueClientAsync(string name, bool isSessionRequired);
		Task DeleteQueueAsync(string name);
		Task<TopicClient> CreateTopicClientAsync(string name);
		Task DeleteTopicAsync(string name);
		Task<SubscriptionClient> CreateSubscriptionClientAsync(string topicName, string subscriptionName);
		Task DeleteSubscriptionAsync(string topicName, string subscriptionName);
	}

	public class ServiceBus : IServiceBus
	{
	    private readonly ILogger _logger;
	    private readonly IServiceBusSettings _settings;
		protected NamespaceManager _manager;
#if USE_MESSAGING_FACTORY
        protected MessagingFactory _messagingFactory;
#endif

		public ServiceBus(
            IServiceBusSettings settings, 
            INamespaceManagerBuilder managerBuilder
#if USE_MESSAGING_FACTORY
            , IMessagingFactoryBuilder factoryBuilder
#endif
            )
        {
		    _settings = settings;

			_manager = managerBuilder
				.CreateNamespaceBuilder()
				.WithConnectionString(_settings.AzureServiceBusConnectionString)
				.Build();

#if USE_MESSAGING_FACTORY
            _messagingFactory = factoryBuilder
                .CreateMessagingFactoryBuilder()
                .WithConnectionString(_settings.AzureServiceBusConnectionString)
                .Build();
#endif
        }

        protected static string CleanseName(string name)
		{
			// Entity segments can contain only letters, numbers, periods (.), hyphens (-), and underscores (_)
			return Regex.Replace(name, @"[^a-zA-Z0-9\.\-_]", "_");
		}

		protected string CreatePrefix()
		{
			if (string.IsNullOrEmpty(_settings.ServiceBusMasterPrefix))
				return "";
			return _settings.ServiceBusMasterPrefix + ".";
		}

		protected string CreateQueueName(string name)
		{
			var queueName = string.Format("q.{0}{1}", CreatePrefix(), CleanseName(name));
			return queueName;
		}

		protected string CreateTopicName(string name)
		{
			var topicName = string.Format("t.{0}{1}", CreatePrefix(), CleanseName(name));
			return topicName;
		}

		protected string CreateSubscriptionName(string name)
		{
			// Use the hashcode to shorten the name and still have a good guarantee of uniqueness
			var subname = string.Format("s.{0}{1}.{2}", 
				CreatePrefix(),
				_settings.ServiceBusSubscriptionNamePrefix, 
				CleanseName(name).GetHashCode());

			if(subname.Length > 50)
				throw new ArgumentException("Resulting subscription name '" + subname + "' is longer than 50 character limit", "name");

			return subname;
		}

		public async Task CreateQueueAsync(string name, bool isSessionRequired)
		{
			var queueName = CreateQueueName(name);
			await CreateCleansedNameQueueAsync(queueName, isSessionRequired);
		}

		private async Task CreateCleansedNameQueueAsync(string cleansedName, bool isSessionRequired)
		{
			var qd = new QueueDescription(cleansedName);
			qd.RequiresSession = isSessionRequired;
			qd.EnableDeadLetteringOnMessageExpiration = true;
			qd.RequiresDuplicateDetection = true;

			if (!_manager.QueueExists(cleansedName))
				await _manager.CreateQueueAsync(qd);
		}

		public async Task<QueueClient> CreateQueueClientAsync(string name)
		{
			return await CreateQueueClientAsync(name, false);
		}

		public async Task<QueueClient> CreateQueueClientAsync(string name, bool isSessionRequired)
		{
			var queueName = CreateQueueName(name);
			await CreateCleansedNameQueueAsync(queueName, isSessionRequired);

#if USE_MESSAGING_FACTORY
		    var client = _messagingFactory.CreateQueueClient(queueName);
#else
            var client = QueueClient.CreateFromConnectionString(_settings.AzureServiceBusConnectionString, queueName);
#endif
		    return client;
		}

		public async Task DeleteQueueAsync(string name)
		{
			var cleansedName = CreateQueueName(name);
			await _manager.DeleteQueueAsync(cleansedName);
		}

		public async Task<TopicClient> CreateTopicClientAsync(string name)
		{
			var topicName = CreateTopicName(name);
			var td = new TopicDescription(topicName);

			if (!_manager.TopicExists(topicName))
				await _manager.CreateTopicAsync(td);

			return TopicClient.CreateFromConnectionString(_settings.AzureServiceBusConnectionString, topicName);
		}

		public async Task DeleteTopicAsync(string name)
		{
			var topicName = CreateTopicName(name);
			await _manager.DeleteTopicAsync(topicName);
		}

		public async Task<SubscriptionClient> CreateSubscriptionClientAsync(string topicName, string subscriptionName)
		{
			var cleansedTopicName = CreateTopicName(topicName);
			var cleansedSubscriptionName = CreateSubscriptionName(subscriptionName);
			var sd = new SubscriptionDescription(cleansedTopicName, cleansedSubscriptionName);

			if (!_manager.SubscriptionExists(cleansedTopicName, cleansedSubscriptionName))
				await _manager.CreateSubscriptionAsync(sd);

			return SubscriptionClient.CreateFromConnectionString(_settings.AzureServiceBusConnectionString, cleansedTopicName, cleansedSubscriptionName);
		}

		public async Task DeleteSubscriptionAsync(string topicName, string subscriptionName)
		{
			var cleansedTopicName = CreateTopicName(topicName);
			var cleansedSubscriptionName = CreateSubscriptionName(subscriptionName);
			await _manager.DeleteSubscriptionAsync(cleansedTopicName, cleansedSubscriptionName);
		}
	}
}