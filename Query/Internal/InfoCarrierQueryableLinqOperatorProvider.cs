﻿namespace InfoCarrier.Core.Client.Query.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.EntityFrameworkCore.Query.Internal;
    using Utils;

    internal class InfoCarrierQueryableLinqOperatorProvider : LinqOperatorProvider,
        IInfoCarrierLinqOperatorProvider
    {
        private static readonly Expression<Func<object, bool>> DummyPredicate = null;
        private static readonly Expression<Func<object, IEnumerable<object>>> DummyPredicateEnum = null;

        private static readonly Lazy<IInfoCarrierLinqOperatorProvider> Inst =
            new Lazy<IInfoCarrierLinqOperatorProvider>(() => new InfoCarrierQueryableLinqOperatorProvider());

        private InfoCarrierQueryableLinqOperatorProvider()
        {
        }

        public static IInfoCarrierLinqOperatorProvider Instance => Inst.Value;

        public override MethodInfo All { get; } =
            GetMethod(() => Queryable.All<object>(null, null));

        public override MethodInfo Any { get; } =
            GetMethod(() => Queryable.Any<object>(null));

        public override MethodInfo Cast { get; } =
            GetMethod(() => Queryable.Cast<object>(null));

        public override MethodInfo Concat { get; } =
            GetMethod(() => Queryable.Concat<object>(null, null));

        public override MethodInfo Contains { get; } =
            GetMethod(() => Queryable.Contains<object>(null, null));

        public override MethodInfo Count { get; } =
            GetMethod(() => Queryable.Count<object>(null));

        public override MethodInfo DefaultIfEmpty { get; } =
            GetMethod(() => Queryable.DefaultIfEmpty<object>(null));

        public override MethodInfo DefaultIfEmptyArg { get; } =
            GetMethod(() => Queryable.DefaultIfEmpty<object>(null, null));

        public override MethodInfo Distinct { get; } =
            GetMethod(() => Queryable.Distinct<object>(null));

        public override MethodInfo Except { get; } =
            GetMethod(() => Queryable.Except<object>(null, null));

        public override MethodInfo First { get; } =
            GetMethod(() => Queryable.First<object>(null));

        public override MethodInfo FirstOrDefault { get; } =
            GetMethod(() => Queryable.FirstOrDefault<object>(null));

        public override MethodInfo GroupBy { get; } =
            GetMethod(() => Queryable.GroupBy<object, object, bool>(null, null, DummyPredicate));

        public override MethodInfo GroupJoin { get; } =
            GetMethod(() => Queryable.GroupJoin<object, object, object, object>(null, null, null, null, null));

        public override MethodInfo Intersect { get; } =
            GetMethod(() => Queryable.Intersect<object>(null, null));

        public override MethodInfo Join { get; } =
            GetMethod(() => Queryable.Join<object, object, object, object>(null, null, null, null, null));

        public override MethodInfo Last { get; } =
            GetMethod(() => Queryable.Last<object>(null));

        public override MethodInfo LastOrDefault { get; } =
            GetMethod(() => Queryable.LastOrDefault<object>(null));

        public override MethodInfo LongCount { get; } =
            GetMethod(() => Queryable.LongCount<object>(null));

        public override MethodInfo OfType { get; } =
            GetMethod(() => Queryable.OfType<object>(null));

        public override MethodInfo OrderBy { get; } =
            GetMethod(() => Queryable.OrderBy<object, object>(null, null));

        public virtual MethodInfo OrderByDescending { get; } =
            GetMethod(() => Queryable.OrderByDescending<object, object>(null, null));

        public override MethodInfo Select { get; } =
            GetMethod(() => Queryable.Select(null, DummyPredicate));

        public override MethodInfo SelectMany { get; } =
            GetMethod(() => Queryable.SelectMany<object, object, object>(null, DummyPredicateEnum, null));

        public override MethodInfo Single { get; } =
            GetMethod(() => Queryable.Single<object>(null));

        public override MethodInfo SingleOrDefault { get; } =
            GetMethod(() => Queryable.SingleOrDefault<object>(null));

        public override MethodInfo Skip { get; } =
            GetMethod(() => Queryable.Skip<object>(null, 1));

        public override MethodInfo Take { get; } =
            GetMethod(() => Queryable.Take<object>(null, 1));

        public override MethodInfo ThenBy { get; } =
            GetMethod(() => Queryable.ThenBy<object, object>(null, null));

        public virtual MethodInfo ThenByDescending { get; } =
            GetMethod(() => Queryable.ThenByDescending<object, object>(null, null));

        public override MethodInfo Union { get; } =
            GetMethod(() => Queryable.Union<object>(null, null));

        public override MethodInfo Where { get; } =
            GetMethod(() => Queryable.Where(null, DummyPredicate));

        private static MethodInfo GetMethod(Expression<Action> expression)
        {
            MethodInfo mi = SymbolExtensions.GetMethodInfo(expression);
            return mi.IsGenericMethod ? mi.GetGenericMethodDefinition() : mi;
        }
    }
}