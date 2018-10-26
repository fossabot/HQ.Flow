// Copyright (c) HQ.IO Corporation. All rights reserved.
// Licensed under the Reciprocal Public License, Version 1.5. See LICENSE.md in the project root for license terms.

using System;
using System.IO;
using System.Threading.Tasks;

namespace HQ.Flow.Producers
{
	/// <summary>
	///     The production end of a pipe that abstracts away the serialization mechanism from downstream consumers
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class ProtocolProducer<T> : IPipe<Stream, T>
	{
		private readonly ISerializer _serializer;
		private Func<Stream, Task<bool>> _handler;

		public ProtocolProducer(ISerializer serializer)
		{
			_serializer = serializer;
			_handler = stream => Task.FromResult(true);
		}

		public void Attach(IConsume<Stream> consumer)
		{
			_handler = consumer.HandleAsync;
		}

		public async Task<bool> HandleAsync(T message)
		{
			return await Task.Run(() =>
			{
				var serialized = _serializer.SerializeToStream(message);
				_handler(serialized);
				return true;
			});
		}
	}
}