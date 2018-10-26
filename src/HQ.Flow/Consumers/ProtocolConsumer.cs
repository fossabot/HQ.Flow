// Copyright (c) HQ.IO Corporation. All rights reserved.
// Licensed under the Reciprocal Public License, Version 1.5. See LICENSE.md in the project root for license terms.

using System;
using System.IO;
using System.Threading.Tasks;

namespace HQ.Flow.Consumers
{
	/// <summary>
	///     The consumption end of a pipe that abstracts away the serialization mechanism from downstream consumers
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class ProtocolConsumer<T> : IPipe<T, Stream>
	{
		private readonly ISerializer _serializer;

		private Func<T, Task<bool>> _handler;

		public ProtocolConsumer(ISerializer serializer)
		{
			_serializer = serializer;
			_handler = t => Task.FromResult(true);
		}

		public async Task<bool> HandleAsync(Stream stream)
		{
			var deserialized = _serializer.DeserializeFromStream<T>(stream);
			return await Task.Run(() =>
			{
				_handler(deserialized);
				return true;
			});
		}

		public void Attach(IConsume<T> consumer)
		{
			_handler = consumer.HandleAsync;
		}
	}
}