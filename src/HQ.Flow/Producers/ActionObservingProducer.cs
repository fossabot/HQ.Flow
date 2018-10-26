// Copyright (c) HQ.IO Corporation. All rights reserved.
// Licensed under the Reciprocal Public License, Version 1.5. See LICENSE.md in the project root for license terms.

using System;
using System.Threading;
using HQ.Flow.Extensions;

namespace HQ.Flow.Producers
{
	/// <summary>
	///     A producer that emits the results of a delegate, converting it to an observable.
	///     The delegate is continously invoked until cancelled internally. In other words,
	///     it can be used to produce continuous sequences.
	///     <remarks>
	///         It's helpful to use Rx's Window or Buffer extension methods to reduce busy-waiting on
	///         these continous sequences.
	///     </remarks>
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class ActionObservingProducer<T> : IProduce<T>, IDisposable
	{
		private ObservingProducer<T> _producer;

		public ActionObservingProducer(Func<T> @delegate)
		{
			_producer = new ObservingProducer<T>();
			_producer.Produces(@delegate.AsContinuousObservable());
		}

		public ActionObservingProducer(Func<CancellationToken, T> @delegate)
		{
			_producer = new ObservingProducer<T>();
			_producer.Produces(@delegate.AsContinuousObservable());
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		public void Attach(IConsume<T> consumer)
		{
			_producer.Attach(consumer);
		}

		public void Start()
		{
			_producer.Start();
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposing) return;
			if (_producer == null) return;
			_producer.Dispose();
			_producer = null;
		}
	}
}