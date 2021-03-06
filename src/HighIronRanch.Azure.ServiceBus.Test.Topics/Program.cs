﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HighIronRanch.Azure.ServiceBus.Contracts;
using HighIronRanch.Azure.ServiceBus.Test.Common;
using HighIronRanch.Core.Services;

namespace HighIronRanch.Azure.ServiceBus.Test.Topics
{
	class Program
	{
		public static ILogger Logger;

		static void Main(string[] args)
		{
			var settings = ServiceBusSettings.Create();
			settings.ServiceBusSubscriptionNamePrefix = DateTime.Now.ToString("hhmmss");

			var nsManager = new NamespaceManagerBuilder();
#if USE_MESSAGING_FACTORY
            var factoryBuilder = new MessagingFactoryBuilder();
#endif

			var serviceBus = new ServiceBus(settings, nsManager
#if USE_MESSAGING_FACTORY
                , factoryBuilder
#endif
                );

			Logger = new Logger();
			var activator = new HandlerActivator();

			var busBuilder = new ServiceBusWithHandlersBuilder(serviceBus, activator, Logger);

			Logger.Information("Main", "Building bus");
			busBuilder.CreateServiceBus()
				.WithEventHandlers(new List<Type>() { typeof(TestEventHandler), typeof(SecondTestEventHandler) });
			var task = busBuilder.BuildAsync();
			task.Wait();
			var bus = task.Result;

			Logger.Information("Main", "Ready. Press 'p' to publish an event. Press 'q' to quit.");

			while (true)
			{
				var key = Console.ReadKey(true);
				if (key.KeyChar == 'q')
					break;
				if (key.KeyChar == 'p')
				{
					var testContent = Guid.NewGuid().ToString();

					Logger.Information("Main", "Publishing event for {0}", testContent);
					bus.PublishAsync(new TestEvent() { Content = testContent }).Wait();
					Logger.Information("Main", "Published");
				}

				Thread.Sleep(100);
			}

			Logger.Information("Main", "Cleanup");
			var cleanupTask = serviceBus.DeleteTopicAsync(typeof(TestEvent).FullName);
			cleanupTask.Wait();
			cleanupTask = serviceBus.DeleteQueueAsync(typeof(TestEvent).FullName);
			cleanupTask.Wait();
		}
	}

	public class TestEvent : IEvent
	{
		public string Content;
	}

	public class TestEventHandler : IEventHandler<TestEvent>
	{
		public async Task HandleAsync(TestEvent evt)
		{
			Program.Logger.Information("TestEventHandler", "Handling: {0}", evt.Content);
		}
	}

	public class SecondTestEventHandler : IEventHandler<TestEvent>
	{
		public async Task HandleAsync(TestEvent evt)
		{
			Program.Logger.Information("SecondTestEventHandler", "Handling: {0}", evt.Content);
		}
	}

	public class HandlerActivator : IHandlerActivator
	{
		public object GetInstance(Type type)
		{
			switch (type.Name)
			{
				case "TestEventHandler":
					return new TestEventHandler();

				case "SecondTestEventHandler":
					return new SecondTestEventHandler();
			}

			throw new ArgumentException("Unknown handler to activate: " + type.Name);
		}
	}
}
