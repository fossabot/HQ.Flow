// Copyright (c) HQ.IO Corporation. All rights reserved.
// Licensed under the Reciprocal Public License, Version 1.5. See LICENSE.md in the project root for license terms.

using System;
using System.Collections.Generic;

namespace HQ.Flow.Consumers
{
	/// <summary>
	///     A <see cref="BatchingConsumer{T}" /> that forwards batches to a delegate
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class ActionBatchingConsumer<T> : BatchingConsumer<T>
	{
		private Func<IList<T>, bool> _delegate;

		public ActionBatchingConsumer(Action<IList<T>> @delegate)
		{
			DelegateForAction(@delegate);
		}

		public ActionBatchingConsumer(Action<IList<T>> @delegate, int itemsPerBatch)
			: base(itemsPerBatch)
		{
			DelegateForAction(@delegate);
		}

		public ActionBatchingConsumer(Action<IList<T>> @delegate, TimeSpan interval)
			: base(interval)
		{
			DelegateForAction(@delegate);
		}

		public ActionBatchingConsumer(Action<IList<T>> @delegate, int itemsPerBatch, TimeSpan orInterval)
			: base(itemsPerBatch, orInterval)
		{
			DelegateForAction(@delegate);
		}

		public ActionBatchingConsumer(Func<IList<T>, bool> @delegate)
		{
			_delegate = @delegate;
		}

		public ActionBatchingConsumer(Func<IList<T>, bool> @delegate, int itemsPerBatch) : base(itemsPerBatch)
		{
			_delegate = @delegate;
		}

		public ActionBatchingConsumer(Func<IList<T>, bool> @delegate, TimeSpan interval) : base(interval)
		{
			_delegate = @delegate;
		}

		public ActionBatchingConsumer(Func<IList<T>, bool> @delegate, int itemsPerBatch, TimeSpan orInterval) : base(
			itemsPerBatch, orInterval)
		{
			_delegate = @delegate;
		}

		public override bool Handle(IList<T> batch)
		{
			return _delegate(batch);
		}

		private void DelegateForAction(Action<IList<T>> @delegate)
		{
			_delegate = list =>
			{
				try
				{
					@delegate(list);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			};
		}
	}
}