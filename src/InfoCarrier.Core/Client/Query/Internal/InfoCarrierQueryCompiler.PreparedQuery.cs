namespace InfoCarrier.Core.Client.Query.Internal
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Threading.Tasks;
    using Aqua.Dynamic;
    using Common;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Metadata.Internal;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.Internal;
    using Microsoft.EntityFrameworkCore.Storage;
    using Remote.Linq;
    using Remote.Linq.ExpressionVisitors;

    public partial class InfoCarrierQueryCompiler
    {
        private sealed class PreparedQuery
        {
            private static readonly MethodInfo MakeGenericGroupingMethod
                = Utils.GetMethodInfo(() => MakeGenericGrouping<object, object>(null, null))
                    .GetGenericMethodDefinition();

            public PreparedQuery(Expression expression)
            {
                // Replace NullConditionalExpression with NullConditionalExpressionStub MethodCallExpression
                expression = Utils.ReplaceNullConditional(expression, true);

                // Replace EntityQueryable with stub
                expression = EntityQueryableStubVisitor.Replace(expression);

                this.Expression = expression;
            }

            private Expression Expression { get; }

            private Aqua.TypeSystem.ITypeResolver TypeResolver { get; } = new Aqua.TypeSystem.TypeResolver();

            public IEnumerable<TResult> Execute<TResult>(QueryContext queryContext)
                => new QueryExecutor<TResult>(this, queryContext).ExecuteQuery();

            public IAsyncEnumerable<TResult> ExecuteAsync<TResult>(QueryContext queryContext)
                => new QueryExecutor<TResult>(this, queryContext).ExecuteAsyncQuery();

            private static IGrouping<TKey, TElement> MakeGenericGrouping<TKey, TElement>(TKey key, IEnumerable<TElement> elements)
            {
                return elements.GroupBy(x => key).Single();
            }

            private sealed class QueryExecutor<TResult> : DynamicObjectMapper
            {
                private readonly QueryContext queryContext;
                private readonly IReadOnlyDictionary<string, IEntityType> entityTypeMap;
                private readonly IEntityMaterializerSource entityMaterializerSource;
                private readonly Dictionary<DynamicObject, object> map = new Dictionary<DynamicObject, object>();
                private readonly List<Action<IStateManager>> trackEntityActions = new List<Action<IStateManager>>();
                private readonly IInfoCarrierBackend infoCarrierBackend;
                private readonly Remote.Linq.Expressions.Expression rlinq;

                public QueryExecutor(PreparedQuery preparedQuery, QueryContext queryContext)
                    : base(new DynamicObjectMapperSettings { FormatPrimitiveTypesAsString = true }, preparedQuery.TypeResolver)
                {
                    this.queryContext = queryContext;
                    this.entityTypeMap = queryContext.Context.Model.GetEntityTypes().ToDictionary(x => x.DisplayName());
                    this.entityMaterializerSource = queryContext.Context.GetService<IEntityMaterializerSource>();
                    this.infoCarrierBackend = ((InfoCarrierQueryContext)queryContext).InfoCarrierBackend;

                    Expression expression = preparedQuery.Expression;

                    // Substitute query parameters
                    expression = new SubstituteParametersExpressionVisitor(queryContext).Visit(expression);

                    // UGLY: this resembles Remote.Linq.DynamicQuery.RemoteQueryProvider<>.TranslateExpression()
                    this.rlinq = expression
                        .SimplifyIncorporationOfRemoteQueryables()
                        .ToRemoteLinqExpression()
                        .ReplaceQueryableByResourceDescriptors(preparedQuery.TypeResolver)
                        .ReplaceGenericQueryArgumentsByNonGenericArguments();
                }

                public IEnumerable<TResult> ExecuteQuery()
                {
                    IEnumerable<DynamicObject> dataRecords = this.infoCarrierBackend.QueryData(
                        this.rlinq,
                        this.queryContext.Context.ChangeTracker.QueryTrackingBehavior);
                    return this.MapAndTrackResults(dataRecords);
                }

                public IAsyncEnumerable<TResult> ExecuteAsyncQuery()
                {
                    async Task<IEnumerable<TResult>> MapAndTrackResultsAsync()
                    {
                        IEnumerable<DynamicObject> dataRecords = await this.infoCarrierBackend.QueryDataAsync(
                            this.rlinq,
                            this.queryContext.Context.ChangeTracker.QueryTrackingBehavior);
                        return this.MapAndTrackResults(dataRecords);
                    }

                    return new AsyncEnumerableAdapter<TResult>(MapAndTrackResultsAsync());
                }

                private IEnumerable<TResult> MapAndTrackResults(IEnumerable<DynamicObject> dataRecords)
                {
                    if (dataRecords == null)
                    {
                        return Enumerable.Repeat(default(TResult), 1);
                    }

                    var result = this.Map<TResult>(dataRecords);

                    this.queryContext.BeginTrackingQuery();

                    foreach (var action in this.trackEntityActions)
                    {
                        action(this.queryContext.StateManager);
                    }

                    return result;
                }

                protected override object MapFromDynamicObjectGraph(object obj, Type targetType)
                {
                    Func<object> baseImpl = () => base.MapFromDynamicObjectGraph(obj, targetType);

                    // mapping required?
                    if (obj == null || targetType == obj.GetType())
                    {
                        return baseImpl();
                    }

                    // is obj an entity?
                    if (this.TryMapEntity(obj, out object entity))
                    {
                        return entity;
                    }

                    // is obj an array
                    if (this.TryMapArray(obj, targetType, out object array))
                    {
                        return array;
                    }

                    // is obj a grouping
                    if (this.TryMapGrouping(obj, targetType, out object grouping))
                    {
                        return grouping;
                    }

                    // is targetType a collection?
                    Type elementType = Utils.TryGetQueryResultSequenceType(targetType);
                    if (elementType == null)
                    {
                        return baseImpl();
                    }

                    // map to list (supported directly by aqua-core)
                    Type listType = typeof(List<>).MakeGenericType(elementType);
                    object list = base.MapFromDynamicObjectGraph(obj, listType) ?? Activator.CreateInstance(listType);

                    // determine concrete collection type
                    Type collType = new CollectionTypeFactory().TryFindTypeToInstantiate(elementType, targetType) ?? targetType;
                    if (listType == collType)
                    {
                        return list; // no further mapping required
                    }

                    // materialize IOrderedEnumerable<>
                    if (collType.GetTypeInfo().IsGenericType && collType.GetGenericTypeDefinition() == typeof(IOrderedEnumerable<>))
                    {
                        return new LinqOperatorProvider().ToOrdered.MakeGenericMethod(collType.GenericTypeArguments)
                            .Invoke(null, new[] { list });
                    }

                    // materialize IQueryable<> / IOrderedQueryable<>
                    if (collType.GetTypeInfo().IsGenericType
                        && (collType.GetGenericTypeDefinition() == typeof(IQueryable<>)
                            || collType.GetGenericTypeDefinition() == typeof(IOrderedQueryable<>)))
                    {
                        return new LinqOperatorProvider().ToQueryable.MakeGenericMethod(collType.GenericTypeArguments)
                            .Invoke(null, new[] { list, this.queryContext });
                    }

                    // materialize concrete collection
                    return Activator.CreateInstance(collType, list);
                }

                private bool TryMapArray(object obj, Type targetType, out object array)
                {
                    array = null;

                    if (obj is DynamicObject dobj)
                    {
                        if (dobj.Type != null)
                        {
                            // Our custom mapping of arrays doesn't contain Type
                            return false;
                        }

                        if (!dobj.TryGet("Elements", out object elements))
                        {
                            return false;
                        }

                        if (!dobj.TryGet("ArrayType", out object arrayTypeObj))
                        {
                            return false;
                        }

                        if (targetType.IsArray)
                        {
                            array = this.MapFromDynamicObjectGraph(elements, targetType);
                            return true;
                        }

                        if (arrayTypeObj is Aqua.TypeSystem.TypeInfo typeInfo)
                        {
                            array = this.MapFromDynamicObjectGraph(elements, typeInfo.Type);
                            return true;
                        }
                    }

                    return false;
                }

                private bool TryMapGrouping(object obj, Type targetType, out object grouping)
                {
                    grouping = null;

                    var dobj = obj as DynamicObject;
                    if (dobj == null)
                    {
                        return false;
                    }

                    Type type = dobj.Type?.Type ?? targetType;

                    if (type == null
                        || !type.GetTypeInfo().IsGenericType
                        || type.GetGenericTypeDefinition() != typeof(IGrouping<,>))
                    {
                        return false;
                    }

                    if (!dobj.TryGet("Key", out object key))
                    {
                        return false;
                    }

                    if (!dobj.TryGet("Elements", out object elements))
                    {
                        return false;
                    }

                    Type keyType = type.GenericTypeArguments[0];
                    Type elementType = type.GenericTypeArguments[1];

                    key = this.MapFromDynamicObjectGraph(key, keyType);
                    elements = this.MapFromDynamicObjectGraph(elements, typeof(List<>).MakeGenericType(elementType));

                    grouping = MakeGenericGroupingMethod
                        .MakeGenericMethod(keyType, elementType)
                        .Invoke(null, new[] { key, elements });
                    return true;
                }

                private bool TryMapEntity(object obj, out object entity)
                {
                    entity = null;

                    var dobj = obj as DynamicObject;
                    if (dobj == null)
                    {
                        return false;
                    }

                    if (!dobj.TryGet(@"__EntityType", out object entityTypeName))
                    {
                        return false;
                    }

                    if (!(entityTypeName is string))
                    {
                        return false;
                    }

                    if (!this.entityTypeMap.TryGetValue(entityTypeName.ToString(), out IEntityType entityType))
                    {
                        return false;
                    }

                    if (this.map.TryGetValue(dobj, out entity))
                    {
                        return true;
                    }

                    // Map only scalar properties for now, navigations must be set later
                    var valueBuffer = new ValueBuffer(
                        entityType
                            .GetProperties()
                            .Select(p => this.MapFromDynamicObjectGraph(dobj.Get(p.Name), p.ClrType))
                            .ToArray());

                    bool entityIsTracked = dobj.PropertyNames.Contains(@"__EntityIsTracked");

                    // Get entity instance from EFC's identity map, or create a new one
                    Func<ValueBuffer, object> materializer = this.entityMaterializerSource.GetMaterializer(entityType);
                    entity =
                        this.queryContext
                            .QueryBuffer
                            .GetEntity(
                                entityType.FindPrimaryKey(),
                                new EntityLoadInfo(
                                    valueBuffer,
                                    materializer),
                                queryStateManager: entityIsTracked,
                                throwOnNullKey: false)
                        ?? materializer.Invoke(valueBuffer);

                    this.map.Add(dobj, entity);
                    object entityNoRef = entity;

                    if (entityIsTracked)
                    {
                        this.trackEntityActions.Add(
                            sm => sm.StartTrackingFromQuery(entityType, entityNoRef, valueBuffer, handledForeignKeys: null));
                    }

                    if (dobj.TryGet(@"__EntityLoadedNavigations", out object ln))
                    {
                        var loadedNavigations = new HashSet<string>(
                            ln as IEnumerable<string> ?? Enumerable.Empty<string>());

                        this.trackEntityActions.Add(stateManager =>
                        {
                            var entry = stateManager.TryGetEntry(entityNoRef);
                            if (entry == null)
                            {
                                return;
                            }

                            foreach (INavigation nav in entry.EntityType.GetNavigations())
                            {
                                bool loaded = loadedNavigations.Contains(nav.Name);
                                if (!loaded && !nav.IsCollection() && nav.GetGetter().GetClrValue(entityNoRef) != null)
                                {
                                    continue;
                                }

                                entry.SetIsLoaded(nav, loaded);
                            }
                        });
                    }

                    // Set navigation properties AFTER adding to map to avoid endless recursion
                    foreach (INavigation navigation in entityType.GetNavigations())
                    {
                        // TODO: shall we skip already loaded navigations if the entity is already tracked?
                        if (dobj.TryGet(navigation.Name, out object value) && value != null)
                        {
                            value = this.MapFromDynamicObjectGraph(value, navigation.ClrType);
                            if (navigation.IsCollection())
                            {
                                // TODO: clear or skip collection if it already contains something?
                                navigation.GetCollectionAccessor().AddRange(entity, ((IEnumerable)value).Cast<object>());
                            }
                            else
                            {
                                navigation.GetSetter().SetClrValue(entity, value);
                            }
                        }
                    }

                    return true;
                }
            }

            private class SubstituteParametersExpressionVisitor : Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.ExpressionVisitorBase
            {
                private static readonly MethodInfo WrapMethod
                    = Utils.GetMethodInfo(() => Wrap<object>(null)).GetGenericMethodDefinition();

                private readonly QueryContext queryContext;

                public SubstituteParametersExpressionVisitor(QueryContext queryContext)
                {
                    this.queryContext = queryContext;
                }

                protected override Expression VisitParameter(ParameterExpression node)
                {
                    if (node.Name?.StartsWith(CompiledQueryCache.CompiledQueryParameterPrefix, StringComparison.Ordinal) == true)
                    {
                        object paramValue =
                            WrapMethod
                                .MakeGenericMethod(node.Type)
                                .Invoke(null, new[] { this.queryContext.ParameterValues[node.Name] });

                        return Expression.Property(
                            Expression.Constant(paramValue),
                            paramValue.GetType(),
                            nameof(ValueWrapper<object>.Value));
                    }

                    return base.VisitParameter(node);
                }

                private static object Wrap<T>(T value) => new ValueWrapper<T> { Value = value };

                private struct ValueWrapper<T>
                {
                    public T Value { get; set; }
                }
            }

            private class EntityQueryableStubVisitor : ExpressionVisitorBase
            {
                private static readonly MethodInfo RemoteQueryableStubCreateMethod
                    = Utils.GetMethodInfo(() => RemoteQueryableStub.Create<object>())
                        .GetGenericMethodDefinition();

                internal static Expression Replace(Expression expression)
                    => new EntityQueryableStubVisitor().Visit(expression);

                protected override Expression VisitConstant(ConstantExpression constantExpression)
                    => constantExpression.IsEntityQueryable()
                        ? this.VisitEntityQueryable(((IQueryable)constantExpression.Value).ElementType)
                        : constantExpression;

                private Expression VisitEntityQueryable(Type elementType)
                {
                    IQueryable stub = RemoteQueryableStubCreateMethod
                        .MakeGenericMethod(elementType)
                        .ToDelegate<Func<IQueryable>>()
                        .Invoke();

                    return Expression.Constant(stub);
                }

                private abstract class RemoteQueryableStub : IRemoteQueryable
                {
                    public abstract Type ElementType { get; }

                    protected static dynamic NotImplemented => throw new NotImplementedException();

                    public Expression Expression => NotImplemented;

                    public IQueryProvider Provider => NotImplemented;

                    public IEnumerator GetEnumerator() => NotImplemented;

                    internal static IQueryable<T> Create<T>()
                    {
                        return new RemoteQueryableStub<T>();
                    }
                }

                private class RemoteQueryableStub<T> : RemoteQueryableStub, IQueryable<T>
                {
                    public override Type ElementType => typeof(T);

                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => NotImplemented;
                }
            }
        }
    }
}