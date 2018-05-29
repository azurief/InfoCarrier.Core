// <auto-generated />
#pragma warning disable IDE0005

using System;
using System.Reflection;
using System.Resources;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace InfoCarrier.Core.Properties
{
    /// <summary>
    ///		This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [InfoCarrier.Core.ExcludeFromCoverage]
    public static class InfoCarrierStrings
    {
        private static readonly ResourceManager _resourceManager
            = new ResourceManager("InfoCarrier.Core.Properties.InfoCarrierStrings", typeof(InfoCarrierStrings).GetTypeInfo().Assembly);

        /// <summary>
        ///     The connection is already in a transaction and cannot participate in another transaction.
        /// </summary>
        public static string TransactionAlreadyStarted
            => GetString("TransactionAlreadyStarted");

        /// <summary>
        ///     The connection does not have any active transactions.
        /// </summary>
        public static string NoActiveTransaction
            => GetString("NoActiveTransaction");

        /// <summary>
        ///     The expression '{expression}' is not a valid method call expression.  The expression should represent a method call: 't =&gt; t.MyMethod(...)'.
        /// </summary>
        public static string InvalidMethodCallExpression(object expression)
            => string.Format(
                GetString("InvalidMethodCallExpression", nameof(expression)),
                expression);

        /// <summary>
        ///     InfoCarrier.Core is not using EntityQueryableExpressionVisitor
        /// </summary>
        public static string NotUsingEntityQueryableExpressionVisitor
            => GetString("NotUsingEntityQueryableExpressionVisitor");

        /// <summary>
        ///     InfoCarrier.Core is not using EntityQueryModelVisitor.
        /// </summary>
        public static string NotUsingEntityQueryModelVisitor
            => GetString("NotUsingEntityQueryModelVisitor");

        /// <summary>
        ///     The NullConditionalExpressionStub&lt;T&gt; method may only be used within LINQ queries.
        /// </summary>
        public static string NullConditionalExpressionStubMethodInvoked
            => GetString("NullConditionalExpressionStubMethodInvoked");

        private static string GetString(string name, params string[] formatterNames)
        {
            var value = _resourceManager.GetString(name);
            for (var i = 0; i < formatterNames.Length; i++)
            {
                value = value.Replace("{" + formatterNames[i] + "}", "{" + i + "}");
            }

            return value;
        }
    }
}
