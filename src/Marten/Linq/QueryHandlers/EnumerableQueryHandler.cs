using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Marten.Schema;
using Marten.Services;
using Marten.Services.Includes;
using Npgsql;
using Remotion.Linq;

namespace Marten.Linq.QueryHandlers
{
    public class EnumerableQueryHandler<T> : IQueryHandler<IEnumerable<T>>
    {
        private readonly ListQueryHandler<T> _inner;

        public EnumerableQueryHandler(IDocumentSchema schema, QueryModel query, IIncludeJoin[] joins)
        {
            _inner = new ListQueryHandler<T>(schema, query, joins);
        }

        public Type SourceType => typeof (IEnumerable<T>);
        public void ConfigureCommand(NpgsqlCommand command)
        {
            _inner.ConfigureCommand(command);
        }

        public IEnumerable<T> Handle(DbDataReader reader, IIdentityMap map)
        {
            return _inner.Handle(reader, map);
        }

        public async Task<IEnumerable<T>> HandleAsync(DbDataReader reader, IIdentityMap map, CancellationToken token)
        {
            return (await _inner.HandleAsync(reader, map, token).ConfigureAwait(false));
        }
    }
}