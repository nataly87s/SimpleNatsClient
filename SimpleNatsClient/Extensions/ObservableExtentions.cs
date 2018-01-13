using System;
using System.Reactive.Disposables;
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

        public static IObservable<TResult> With<TResult, TOther>(this IObservable<TResult> @this, IObservable<TOther> context)
        {
            return Observable.Create<TResult>(observer =>
            {
                var mainSubscription = @this.Subscribe(observer);
                var contextSubscription = context.Subscribe(_ => { }, observer.OnError);
                return new CompositeDisposable(contextSubscription, mainSubscription);
            });
        }
    }
}