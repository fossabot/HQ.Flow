// Copyright (c) HQ.IO Corporation. All rights reserved.
// Licensed under the Reciprocal Public License, Version 1.5. See LICENSE.md in the project root for license terms.

using System;
using System.Threading.Tasks;

namespace HQ.Flow.Producers
{
	/// <summary>
	///     A base implementation for a producer that uses the default background producer as its worker thread.
	///     <remarks>
	///         See implementers for reference implementation; basically you subscribe a production to the Background directly
	///         in the constructor
	///     </remarks>
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public abstract class BackgroundProducer<T> : IProduce<T>, IDisposable
	{
		protected BackgroundProducer()
		{
			Background = new BackgroundThreadProducer<T>();
		}

		protected BackgroundThreadProducer<T> Background { get; private set; }

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		public virtual void Attach(IConsume<T> consumer)
		{
			Background.Attach(consumer);
		}

		public virtual Task Start(bool immediate = false)
		{
			return Background.Start(immediate);
		}

		public virtual Task Stop(bool immediate = false)
		{
			return Background.Stop(immediate);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposing) return;
			Background?.Dispose();
			Background = null;
		}
	}
}