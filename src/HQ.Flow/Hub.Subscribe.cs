﻿// Copyright (c) HQ.IO Corporation. All rights reserved.
// Licensed under the Reciprocal Public License, Version 1.5. See LICENSE.md in the project root for license terms.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Threading;
using Enumerable = System.Linq.Enumerable;

namespace HQ.Flow
{
	/// <summary>
	///     Allows subscribing handlers to centralized publishing events, and allows subscription by topic.
	///     Basically, it is both an aggregator and a publisher, and is typically used to provide pub-sub services in-process.
	/// </summary>
	public partial class Hub : IMessageAggregator
	{
		private static readonly Action<Exception> NoopErrorHandler = e => { };
		private readonly Hashtable _byTypeDispatch = new Hashtable();

		private readonly ConcurrentDictionary<Type, IDisposable> _subscriptions =
			new ConcurrentDictionary<Type, IDisposable>();

		private readonly ITypeResolver _typeResolver;

		private readonly ConcurrentDictionary<Type, CancellationTokenSource> _unsubscriptions =
			new ConcurrentDictionary<Type, CancellationTokenSource>();

		public Hub() : this(GetDefaultAssemblies())
		{
		}

		public Hub(IEnumerable<Assembly> assemblies)
		{
			_typeResolver = new DefaultTypeResolver(assemblies);
		}

		public Hub(ITypeResolver typeResolver)
		{
			_typeResolver = typeResolver;
		}

		/// <summary> Subscribes a manifold handler. This is required if a handler acts as a consumer for more than one event type. </summary>
		public void Subscribe(object handler, Action<Exception> onError = null)
		{
			var type = handler.GetType();
			var interfaces = type.GetTypeInfo().GetInterfaces();
			var consumers = Enumerable.Where(interfaces,
				i => typeof(IConsume<>).GetTypeInfo().IsAssignableFrom(i.GetGenericTypeDefinition()));

			const BindingFlags binding = BindingFlags.Instance | BindingFlags.NonPublic;
			var subscribeByInterface = typeof(Hub).GetTypeInfo().GetMethod(nameof(SubscribeByInterface), binding);
			Debug.Assert(subscribeByInterface != null);

			// void Subscribe<T>(IConsume<T> consumer)
			foreach (var consumer in consumers)
			{
				// SubscribeByInterface(consumer);
				var handlerType = consumer.GetTypeInfo().GetGenericArguments()[0];
				subscribeByInterface?.MakeGenericMethod(handlerType)
					.Invoke(this, new[] {handler, onError ?? NoopErrorHandler});
			}
		}

		public void Subscribe<T>(Action<T> handler, Action<Exception> onError = null)
		{
			SubscribeByDelegate(handler, onError ?? NoopErrorHandler);
		}

		public void Subscribe<T>(Action<T> handler, Func<T, bool> topic, Action<Exception> onError = null)
		{
			SubscribeByDelegateAndTopic(handler, topic, onError ?? NoopErrorHandler);
		}

		public void Subscribe<T>(IConsume<T> consumer, Action<Exception> onError = null)
		{
			SubscribeByInterface(consumer, onError ?? NoopErrorHandler);
		}

		public void Subscribe<T>(IConsume<T> consumer, Func<T, bool> topic, Action<Exception> onError)
		{
			SubscribeByInterfaceAndTopic(consumer, topic, onError ?? NoopErrorHandler);
		}

		public void Unsubscribe<T>()
		{
			IDisposable reference;
			_subscriptions.TryRemove(typeof(T), out reference);

			CancellationTokenSource cancel;
			if (_unsubscriptions.TryGetValue(typeof(T), out cancel))
				cancel.Cancel();
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		public static IEnumerable<Assembly> GetDefaultAssemblies()
		{
			var assemblies = new List<Assembly>();
			assemblies.AddRange(AppDomain.CurrentDomain.GetAssemblies());
			return assemblies;
		}

		private void SubscribeByDelegate<T>(Action<T> handler, Action<Exception> onError)
		{
			var subscription = GetSubscriptionSubject<T>();
			var subject = (IObservableWithOutcomes<T>) subscription;

			SubscribeWithDelegate(handler, subject, onError);
		}

		private void SubscribeByDelegateAndTopic<T>(Action<T> handler, Func<T, bool> topic, Action<Exception> onError)
		{
			var subscription = GetSubscriptionSubject<T>();
			var subject = (IObservableWithOutcomes<T>) subscription;

			SubscribeWithDelegate(handler, subject, onError, topic);
		}

		private void SubscribeByInterface<T>(IConsume<T> consumer, Action<Exception> onError)
		{
			var subscription = GetSubscriptionSubject<T>();
			var subject = (IObservableWithOutcomes<T>) subscription;

			SubscribeWithInterface(consumer, subject, onError);
		}

		private void SubscribeByInterfaceAndTopic<T>(IConsume<T> consumer, Func<T, bool> topic,
			Action<Exception> onError)
		{
			var subscription = GetSubscriptionSubject<T>();
			var subject = (IObservableWithOutcomes<T>) subscription;

			SubscribeWithInterface(consumer, subject, onError, topic);
		}

		private void SubscribeWithDelegate<T>(Action<T> handler, IObservableWithOutcomes<T> observable,
			Action<Exception> onError, Func<T, bool> filter = null)
		{
			var subscriptionType = typeof(T);
			var unsubscription = _unsubscriptions.GetOrAdd(subscriptionType, t => new CancellationTokenSource());

			if (filter != null)
				observable.Subscribe(@event =>
				{
					try
					{
						if (!filter(@event))
						{
							observable.Outcomes.Add(new ObservableOutcome {Result = false});
						}
						else
						{
							handler(@event);
							observable.Outcomes.Add(new ObservableOutcome {Result = true});
						}
					}
					catch (Exception ex)
					{
						OnHandlerError(onError, ex, observable);
					}
				}, e => OnError(e, observable), () => OnCompleted(observable), unsubscription.Token);
			else
				observable.Subscribe(@event =>
				{
					try
					{
						handler(@event);
						observable.Outcomes.Add(new ObservableOutcome {Result = true});
					}
					catch (Exception ex)
					{
						OnHandlerError(onError, ex, observable);
					}
				}, e => OnError(e, observable), () => OnCompleted(observable), unsubscription.Token);
		}

		private void SubscribeWithInterface<T>(IConsume<T> consumer, IObservableWithOutcomes<T> observable,
			Action<Exception> onError, Func<T, bool> filter = null)
		{
			var subscriptionType = typeof(T);
			var unsubscription = _unsubscriptions.GetOrAdd(subscriptionType, t => new CancellationTokenSource());

			if (filter != null)
				observable.Subscribe(@event =>
				{
					try
					{
						if (!filter(@event))
						{
							observable.Outcomes.Add(new ObservableOutcome {Result = false});
						}
						else
						{
							var result = Handle(consumer, @event);
							observable.Outcomes.Add(new ObservableOutcome {Result = result});
						}
					}
					catch (Exception ex)
					{
						OnHandlerError(onError, ex, observable);
					}
				}, e => OnError(e, observable), () => OnCompleted(observable), unsubscription.Token);
			else
				observable.Subscribe(@event =>
				{
					try
					{
						var result = Handle(consumer, @event);
						observable.Outcomes.Add(new ObservableOutcome {Result = result});
					}
					catch (Exception ex)
					{
						OnHandlerError(onError, ex, observable);
					}
				}, e => OnError(e, observable), () => OnCompleted(observable), unsubscription.Token);
		}

		private static bool Handle<T>(IConsume<T> consumer, T @event)
		{
			if (consumer is IConsumeScoped<T> before)
				if (!before.Before())
					return false;

			var result = consumer.HandleAsync(@event).ConfigureAwait(false).GetAwaiter().GetResult();

			if (consumer is IConsumeScoped<T> after)
				result = after.After(result);

			return result;
		}

		/// <summary>
		///     The observable sequence has completed; technically, this should never happen unless disposing, so we should
		///     treat this similarly to a fault since we can't guarantee delivery.
		/// </summary>
		private static void OnCompleted<T>(IObservableWithOutcomes<T> observable)
		{
			observable.Outcomes.Add(new ObservableOutcome {Result = false});
		}

		/// <summary> An error has been caught coming from the user handler; we need to allow a hook to pipeline it. </summary>
		private static void OnHandlerError<T>(Action<Exception> a, Exception e, IObservableWithOutcomes<T> observable)
		{
			a(e);
			observable.Outcomes.Add(new ObservableOutcome {Error = e, Result = false});
		}

		/// <summary>
		///     The observable sequence falted; technically this should never happen, since it would also cause observances
		///     to fail going forward on this thread, so we should treat this similarly to a fault, since delivery is shut down.
		/// </summary>
		private static void OnError<T>(Exception e, IObservableWithOutcomes<T> observable)
		{
			observable.Outcomes.Add(new ObservableOutcome {Error = e, Result = false});
		}

		private IDisposable GetSubscriptionSubject<T>()
		{
			var subscriptionType = typeof(T);

			return _subscriptions.GetOrAdd(subscriptionType, t =>
			{
				ISubject<T> subject = new Subject<T>();

				return new WrappedSubject<T>(subject, OutcomePolicy.Pessimistic);
			});
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposing || _subscriptions == null || _subscriptions.Count == 0)
				return;

			foreach (var subscription in _subscriptions.Where(subscription => subscription.Value != null))
			{
				IDisposable reference;
				if (_subscriptions.TryRemove(subscription.Key, out reference))
				{
					CancellationTokenSource cancel;
					if (_unsubscriptions.TryGetValue(subscription.Key, out cancel))
						cancel.Cancel();

					subscription.Value.Dispose();
				}
			}
		}
	}
}