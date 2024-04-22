using Microsoft.EntityFrameworkCore.Query.Internal;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace InfoCarrier.Core.Client.Extensions
{
    public static class InfoCarrierConstantExpressionExtensions
    {
        public static bool IsEntityQueryable([NotNull] this Expression constantExpression)
             => constantExpression.Type.GetTypeInfo().IsGenericType
                && constantExpression.Type.GetGenericTypeDefinition() == typeof(IQueryable<>);

    }
}
