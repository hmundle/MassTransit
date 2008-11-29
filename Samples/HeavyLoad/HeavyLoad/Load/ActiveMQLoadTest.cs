// Copyright 2007-2008 The Apache Software Foundation.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use 
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed 
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace HeavyLoad.Load
{
	using System;
	using System.Threading;
	using Castle.Windsor;
	using Magnum.Common.ObjectExtensions;
	using MassTransit;
	using MassTransit.Transports.Msmq;
	using MassTransit.Transports.Nms;
	using MassTransit.WindsorIntegration;

	public class ActiveMQLoadTest : IDisposable
	{
		private const int _repeatCount = 5000;
		private static readonly ManualResetEvent _completeEvent = new ManualResetEvent(false);
		private readonly IWindsorContainer _container;
		private static readonly ManualResetEvent _responseEvent = new ManualResetEvent(false);

		private IServiceBus _bus;
		private static int _counter = 0;
		private static int _responseCounter = 0;

		public ActiveMQLoadTest()
		{
			_container = new DefaultMassTransitContainer("activemq.castle.xml");

			_bus = _container.Resolve<IServiceBus>();

			NmsEndpoint endpoint = _bus.Endpoint as NmsEndpoint;
			endpoint.MustNotBeNull();
		}

		public void Dispose()
		{
			_bus.Dispose();
			_container.Dispose();
		}

		public void Run(StopWatch stopWatch)
		{
			_container.AddComponent<RequestConsumer>();
			_container.AddComponent<ResponseConsumer>();

			_bus.Subscribe<RequestConsumer>();
			_bus.Subscribe<ResponseConsumer>();

			stopWatch.Start();

			CheckPoint publishCheckpoint = stopWatch.Mark("Publishing " + _repeatCount + " messages");
			CheckPoint receiveCheckpoint = stopWatch.Mark("Receiving " + _repeatCount + " messages");

			for (int index = 0; index < _repeatCount; index++)
			{
				_bus.Publish(new GeneralMessage());
			}

			publishCheckpoint.Complete(_repeatCount);

			bool completed = _completeEvent.WaitOne(TimeSpan.FromSeconds(60), true);

			bool responseCompleted = _responseEvent.WaitOne(TimeSpan.FromSeconds(60), true);

			receiveCheckpoint.Complete(_counter + _responseCounter);

			stopWatch.Stop();
		}

		internal class ResponseConsumer : Consumes<SimpleResponse>.All
		{
			public void Consume(SimpleResponse message)
			{
				Interlocked.Increment(ref _responseCounter);
				if (_responseCounter == _repeatCount)
					_responseEvent.Set();
			}
		}

		internal class RequestConsumer :
			Consumes<GeneralMessage>.All
		{
			private IServiceBus _bus = ServiceBus.Null;

			public void Consume(GeneralMessage message)
			{
				Interlocked.Increment(ref _counter);
				if (_counter == _repeatCount)
					_completeEvent.Set();

				_bus.Publish(new SimpleResponse());
			}

			public IServiceBus Bus
			{
				set { _bus = value; }
			}
		}
	}
}