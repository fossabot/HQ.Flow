﻿// Copyright (c) HQ.IO Corporation. All rights reserved.
// Licensed under the Reciprocal Public License, Version 1.5. See LICENSE.md in the project root for license terms.

using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace HQ.Flow.Serializers
{
	public class JsonSerializer : ISerializer
	{
		private readonly Newtonsoft.Json.JsonSerializer _serializer;

		public JsonSerializer()
		{
			_serializer = new Newtonsoft.Json.JsonSerializer();
		}

		public Stream SerializeToStream<T>(T message)
		{
			var s = new MemoryStream();
			using (var sw = new StreamWriter(s, Encoding.UTF8, 4096, true))
			{
				using (var tw = new JsonTextWriter(sw))
				{
					_serializer.Serialize(tw, message);
					tw.Flush();
				}
			}

			return s;
		}

		public T DeserializeFromStream<T>(Stream stream)
		{
			using (var sr = new StreamReader(stream, Encoding.UTF8, false, 4096, true))
			{
				using (var tr = new JsonTextReader(sr))
				{
					return _serializer.Deserialize<T>(tr);
				}
			}
		}

		public void Dispose()
		{
		}

		public void SerializeToStream<T>(Stream stream, T message)
		{
			using (var sw = new StreamWriter(stream, Encoding.UTF8, 4096, true))
			{
				using (var tw = new JsonTextWriter(sw))
				{
					_serializer.Serialize(tw, message);
					tw.Flush();
				}
			}
		}
	}
}