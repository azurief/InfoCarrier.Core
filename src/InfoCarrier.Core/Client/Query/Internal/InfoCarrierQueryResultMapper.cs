﻿// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Client.Query.Internal
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Aqua.Dynamic;
    using Aqua.TypeSystem;
    using InfoCarrier.Core.Common;
    using InfoCarrier.Core.Properties;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Metadata.Internal;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Storage;
    using MethodInfo = System.Reflection.MethodInfo;

    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class InfoCarrierQueryResultMapper : DynamicObjectMapper
    {
        private static readonly MethodInfo AddElementsToCollectionMethod
            = Utils.GetMethodInfo(() => AddElementsToCollection<object>(null, null))
                .GetGenericMethodDefinition();

        private static readonly MethodInfo MakeGenericGroupingMethod
            = Utils.GetMethodInfo(() => MakeGenericGrouping<object, object>(null, null))
                .GetGenericMethodDefinition();

        private readonly QueryContext queryContext;
        private readonly ITypeResolver typeResolver;
        private readonly IReadOnlyDictionary<string, IEntityType> entityTypeMap;
        private readonly IEntityMaterializerSource entityMaterializerSource;
        private readonly Dictionary<DynamicObject, object> map = new Dictionary<DynamicObject, object>();
        private readonly List<Action<IStateManager>> trackEntityActions = new List<Action<IStateManager>>();

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "InfoCarrier.Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1642:ConstructorSummaryDocumentationMustBeginWithStandardText", Justification = "InfoCarrier.Core internal.")]
        public InfoCarrierQueryResultMapper(
            QueryContext queryContext,
            ITypeResolver typeResolver,
            ITypeInfoProvider typeInfoProvider,
            IReadOnlyDictionary<string, IEntityType> entityTypeMap = null)
            : base(typeResolver, typeInfoProvider, new DynamicObjectMapperSettings { FormatPrimitiveTypesAsString = true })
        {
            this.queryContext = queryContext;
            this.typeResolver = typeResolver;
            this.entityTypeMap = entityTypeMap ?? BuildEntityTypeMap(queryContext.Context);
            this.entityMaterializerSource = queryContext.Context.GetService<IEntityMaterializerSource>();
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "InfoCarrier.Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:ElementReturnValueMustBeDocumented", Justification = "InfoCarrier.Core internal.")]
        internal static IReadOnlyDictionary<string, IEntityType> BuildEntityTypeMap(DbContext context)
            => context.Model.GetEntityTypes().ToDictionary(x => x.DisplayName());

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "InfoCarrier.Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:ElementReturnValueMustBeDocumented", Justification = "InfoCarrier.Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1618:GenericTypeParametersMustBeDocumented", Justification = "InfoCarrier.Core internal.")]
        public IEnumerable<TResult> MapAndTrackResults<TResult>(IEnumerable<DynamicObject> dataRecords)
        {
            var result = this.Map<TResult>(dataRecords);

            this.queryContext.BeginTrackingQuery();

            foreach (var action in this.trackEntityActions)
            {
                action(this.queryContext.StateManager);
            }

            return result;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "InfoCarrier.Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:ElementReturnValueMustBeDocumented", Justification = "InfoCarrier.Core internal.")]
        protected override object MapFromDynamicObjectGraph(object obj, Type targetType)
        {
            object BaseImpl() => base.MapFromDynamicObjectGraph(obj, targetType);

            // mapping required?
            if (obj == null || targetType == obj.GetType())
            {
                return BaseImpl();
            }

            if (obj is DynamicObject dobj)
            {
                if (this.map.TryGetValue(dobj, out object cached))
                {
                    return cached;
                }

                // is obj an entity?
                if (this.TryMapEntity(dobj, out object entity))
                {
                    return entity;
                }

                // is obj an array
                if (this.TryMapArray(dobj, targetType, out object array))
                {
                    return array;
                }

                // is obj a grouping
                if (this.TryMapGrouping(dobj, targetType, out object grouping))
                {
                    return grouping;
                }

                // is obj a collection
                if (this.TryMapCollection(dobj, targetType, out object collection))
                {
                    return collection;
                }
            }

            return BaseImpl();
        }

        private bool TryMapArray(DynamicObject dobj, Type targetType, out object array)
        {
            array = null;

            if (dobj.Type != null)
            {
                // Our custom mapping of arrays doesn't contain Type
                return false;
            }

            if (!dobj.TryGet(@"Elements", out object elements))
            {
                return false;
            }

            if (!dobj.TryGet(@"ArrayType", out object arrayTypeObj))
            {
                return false;
            }

            if (targetType.IsArray)
            {
                array = this.MapFromDynamicObjectGraph(elements, targetType);
                this.map.Add(dobj, array);
                return true;
            }

            if (arrayTypeObj is Aqua.TypeSystem.TypeInfo typeInfo)
            {
                array = this.MapFromDynamicObjectGraph(elements, typeInfo.ResolveType(this.typeResolver));
                this.map.Add(dobj, array);
                return true;
            }

            return false;
        }

        private bool TryMapCollection(DynamicObject dobj, Type targetType, out object collection)
        {
            collection = null;

            Type type = dobj.Type?.ResolveType(this.typeResolver);

            if (type == null
                || !type.GetTypeInfo().IsGenericType
                || type.GetGenericTypeDefinition() != typeof(IEnumerable<>))
            {
                return false;
            }

            if (!dobj.TryGet(@"Elements", out object elements))
            {
                return false;
            }

            Type elementType = type.GenericTypeArguments[0];

            // instantiate collection and add it to map
            Type resultType =
                dobj.TryGet(@"CollectionType", out object collTypeObj) && collTypeObj is Aqua.TypeSystem.TypeInfo typeInfo
                ? typeInfo.ResolveType(this.typeResolver)
                : typeof(OrderedQueryableList<>).MakeGenericType(elementType);
            collection = Activator.CreateInstance(resultType);
            this.map.Add(dobj, collection);

            // map elements to list AFTER adding to map to avoid endless recursion
            Type listType = typeof(List<>).MakeGenericType(elementType);
            elements = this.MapFromDynamicObjectGraph(elements, listType);

            // copy elements from list to resulting collection
            try
            {
                AddElementsToCollectionMethod.MakeGenericMethod(elementType).Invoke(null, new[] { collection, elements });
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                throw e.InnerException;
            }

            return true;
        }

        private bool TryMapGrouping(DynamicObject dobj, Type targetType, out object grouping)
        {
            grouping = null;

            Type type = dobj.Type?.ResolveType(this.typeResolver) ?? targetType;

            if (type == null
                || !type.GetTypeInfo().IsGenericType
                || type.GetGenericTypeDefinition() != typeof(IGrouping<,>))
            {
                return false;
            }

            if (!dobj.TryGet(@"Key", out object key))
            {
                return false;
            }

            if (!dobj.TryGet(@"Elements", out object elements))
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
            this.map.Add(dobj, grouping);

            return true;
        }

        private bool TryMapEntity(DynamicObject dobj, out object entity)
        {
            entity = null;

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

            // Map only scalar properties for now, navigations have to be set later
            var valueBuffer = new ValueBuffer(
                entityType
                    .GetProperties()
                    .Select(p =>
                    {
                        object value = dobj.Get(p.Name);
                        if (p.GetValueConverter() != null)
                        {
                            value = this.MapFromDynamicObjectGraph(value, typeof(object));
                            value = Utils.ConvertFromProvider(value, p);
                        }

                        return this.MapFromDynamicObjectGraph(value, p.ClrType);
                    })
                    .ToArray());

            bool entityIsTracked = dobj.PropertyNames.Contains(@"__EntityLoadedNavigations");

            // Get entity instance from EFC's identity map, or create a new one
            Func<MaterializationContext, object> materializer = this.entityMaterializerSource.GetMaterializer(entityType);
            var materializationContext = new MaterializationContext(valueBuffer, this.queryContext.Context);

            IKey pk = entityType.FindPrimaryKey();
            if (pk != null)
            {
                entity = this.queryContext
                    .QueryBuffer
                    .GetEntity(
                        pk,
                        new EntityLoadInfo(
                            materializationContext,
                            materializer),
                        queryStateManager: entityIsTracked,
                        throwOnNullKey: false);
            }

            if (entity == null)
            {
                entity = materializer.Invoke(materializationContext);
            }

            this.map.Add(dobj, entity);
            object entityNoRef = entity;

            if (entityIsTracked)
            {
                var loadedNavigations = this.Map<List<string>>(dobj.Get<DynamicObject>(@"__EntityLoadedNavigations"));

                this.trackEntityActions.Add(sm =>
                {
                    InternalEntityEntry entry
                        = sm.StartTrackingFromQuery(entityType, entityNoRef, valueBuffer, handledForeignKeys: null);

                    foreach (INavigation nav in loadedNavigations.Select(name => entry.EntityType.FindNavigation(name)))
                    {
                        entry.SetIsLoaded(nav);
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

        private static void AddElementsToCollection<TElement>(object collection, List<TElement> elements)
        {
            switch (collection)
            {
                case ISet<TElement> set:
                    set.UnionWith(elements);
                    break;

                case List<TElement> list:
                    list.AddRange(elements);
                    break;

                case ICollection<TElement> coll:
                    foreach (var element in elements)
                    {
                        coll.Add(element);
                    }

                    break;

                default:
                    throw new NotSupportedException(
                        InfoCarrierStrings.CannotAddElementsToCollection(
                            typeof(TElement),
                            collection.GetType()));
            }
        }

        private static IGrouping<TKey, TElement> MakeGenericGrouping<TKey, TElement>(TKey key, IEnumerable<TElement> elements)
        {
            return elements.GroupBy(x => key).Single();
        }

        private class OrderedQueryableList<T> : List<T>, IOrderedEnumerable<T>, IOrderedQueryable<T>
        {
            private readonly IQueryable<T> queryable;

            public OrderedQueryableList()
            {
                this.queryable = new EnumerableQuery<T>(this);
            }

            public Type ElementType => queryable.ElementType;

            public Expression Expression => queryable.Expression;

            public IQueryProvider Provider => queryable.Provider;

            public IOrderedEnumerable<T> CreateOrderedEnumerable<TKey>(
                Func<T, TKey> keySelector,
                IComparer<TKey> comparer,
                bool descending)
            {
                return descending ? this.OrderByDescending(keySelector, comparer) : this.OrderBy(keySelector, comparer);
            }
        }
    }
}
