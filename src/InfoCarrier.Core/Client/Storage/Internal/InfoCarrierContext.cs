using Aqua.Dynamic;
using Aqua.TypeSystem;
using Remote.Linq;
using Remote.Linq.DynamicQuery;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace InfoCarrier.Core.Client.Storage.Internal
{
    internal class InfoCarrierToRemoteContext : IExpressionToRemoteLinqContext
    {
        public ITypeInfoProvider TypeInfoProvider { get; set; }

        public Func<object, bool> NeedsMapping { get; set; }

        public IExpressionTranslator ExpressionTranslator { get; set; }

        public IDynamicObjectMapper ValueMapper { get; set; }

        public Func<Expression, bool>? CanBeEvaluatedLocally { get; set; }
    }

    internal class InfoCarrierFromRemoteContext : IExpressionFromRemoteLinqContext
    {
        public ITypeResolver TypeResolver { get; set; }

        public IDynamicObjectMapper ValueMapper { get; set; }

        public Func<Expression, bool>? CanBeEvaluatedLocally { get; set; }
    }
}
