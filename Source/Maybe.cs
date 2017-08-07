using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace NavBallTextureChanger.Extensions
{
	public struct Maybe<T> : IEnumerable<T>
	{
		public readonly static Maybe<T> Nothing = new Maybe<T>();


		private IEnumerable<T> _values;

		public static Maybe<T> None { get { return Nothing; } }

		public static Maybe<T> With(T value)
		{
			var maybe = new Maybe<T> { _values = new T[] { value } };
			return maybe;
		}


		public IEnumerator<T> GetEnumerator()
		{
			LazyInitialize();

			return _values.GetEnumerator();
		}

		private void LazyInitialize()
		{
			if (_values == null)
				_values = new T[] { };
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public T Or(T other)
		{
			return this.Any() ? this.Single() : other;
		}

		public T Or(Func<T> other)
		{
			return this.Any() ? this.Single() : other();
		}

		public Maybe<T> Or(Maybe<T> other)
		{
			return this.Any() ? this : other;
		}

		public T Value
		{
			get { return this.SingleOrDefault(); }
		}
	}


	public static class MaybeExtension
	{
		public static Maybe<T> ToMaybe<T>(this T value)
		{
			if (!typeof(T).IsValueType)
				// ReSharper disable once CompareNonConstrainedGenericWithNull
				return value != null ? Maybe<T>.With(value) : Maybe<T>.None;

			return Maybe<T>.With(value);
		}


		public static TSource If<TSource>(this TSource source, Func<TSource, bool> condition)
			where TSource : class
		{
			if ((source != default(TSource)) && condition(source))
			{
				return source;
			}
			return default(TSource);
		}

		public static Maybe<TSource> If<TSource>(this Maybe<TSource> source, Func<TSource, bool> condition)
		{
			if (source.Any() && condition(source.Value))
				return source;
			return Maybe<TSource>.None;
		}


		public static TSource IfNull<TSource>(this TSource source, Action func)
			where TSource : class
		{
			if (source == null)
				func();
			return source;
		}

		public static Maybe<TSource> IfNull<TSource>(this Maybe<TSource> source, Action func)
		{
			if (!source.Any())
				func();
			return source;
		}


		public static TInput Unless<TInput>(this TInput o, Func<TInput, bool> evaluator)
			   where TInput : class
		{
			if (o == null) return null;
			return evaluator(o) ? null : o;
		}

		public static Maybe<TInput> Unless<TInput>(this Maybe<TInput> o, Func<TInput, bool> evaluator)
		{
			if (!o.Any()) return Maybe<TInput>.None;

			return evaluator(o.Single()) ? Maybe<TInput>.None : o;
		}



		public static TInput Do<TInput>(this TInput o, Action<TInput> action)
			where TInput : class
		{
			if (o == null) return null;
			action(o);
			return o;
		}


		public static Maybe<TInput> Do<TInput>(this Maybe<TInput> o, Action<TInput> action)
		{
			if (!o.Any()) return Maybe<TInput>.None;
			action(o.Single());

			return o;
		}


		public static TResult Return<TInput, TResult>(this TInput o,
			Func<TInput, TResult> evaluator, TResult failureValue) where TInput : class
		{
			if (o == null) return failureValue;
			return evaluator(o);
		}

		public static TResult Return<TInput, TResult>(this Maybe<TInput> o,
			Func<TInput, TResult> evaulator, TResult failureValue)
		{
			return !o.Any() ? failureValue : evaulator(o.Single());
		}


		public static TResult With<TInput, TResult>(this TInput o,
		   Func<TInput, TResult> evaluator)
			where TResult : class
			where TInput : class
		{
			if (o == null) return null;

			return evaluator(o);
		}


		public static Maybe<TResult> With<TInput, TResult>(this Maybe<TInput> o,
			Func<TInput, TResult> evaulator)
		{
			if (!o.Any()) return Maybe<TResult>.None;
			return evaulator(o.Single()).ToMaybe();
		}

		public static Maybe<TResult> With<TInput, TResult>(this Maybe<TInput> o, Func<TInput, Maybe<TResult>> evalulator)
		{
			if (!o.Any()) return Maybe<TResult>.None;
			return evalulator(o.Value);
		}
	}
}