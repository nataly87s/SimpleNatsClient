using System;
using System.Reactive.Linq;

namespace SimpleNatsClient.Extensions
{
    internal static class ObservableExtentions
    {
        public static IObservable<TResult> Catch<TResult>(this IObservable<TResult> @this)
        {
            return @this.OnErrorResumeNext(Observable.Empty<TResult>());
        }

        public static IObservable<TResult> Catch<TResult>(this IObservable<TResult> @this, Action<Exception> onError)
        {
            return @this.Catch((Exception e) =>
            {
                onError(e);
                return Observable.Empty<TResult>();
            });
        }
    }
}