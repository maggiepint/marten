using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baseline;
using Marten.Schema;
using Marten.Services;
using Marten.Services.Includes;
using Marten.Util;
using Npgsql;
using Remotion.Linq;

namespace Marten.Linq.QueryHandlers
{
    public class ListQueryHandler<T> : IQueryHandler<IList<T>>
    {
        private readonly IDocumentSchema _schema;
        private readonly QueryModel _query;
        private readonly ISelector<T> _selector;
        private readonly IDocumentMapping _mapping;

        public ListQueryHandler(IDocumentSchema schema, QueryModel query, IIncludeJoin[] joins)
        {
            _mapping = schema.MappingFor(query);
            _schema = schema;
            _query = query;

            var selector = _schema.BuildSelector<T>(_mapping, _query);

            if (joins.Any())
            {
                selector = new IncludeSelector<T>(schema, selector, joins);
            }

            _selector = selector;
        }


        public Type SourceType => _query.SourceType();
        public void ConfigureCommand(NpgsqlCommand command)
        {
            var sql = _selector.ToSelectClause(_mapping);
            var @where = _schema.BuildWhereFragment(_mapping, _query);



            sql = sql.AppendWhere(@where, command);

            var orderBy = _query.ToOrderClause(_mapping);
            if (orderBy.IsNotEmpty()) sql += orderBy;

            sql = _query.AppendLimit(sql);
            sql = _query.AppendOffset(sql);

            command.AppendQuery(sql);
        }

        public IList<T> Handle(DbDataReader reader, IIdentityMap map)
        {
            return _selector.Read(reader, map);
        }

        public async Task<IList<T>> HandleAsync(DbDataReader reader, IIdentityMap map, CancellationToken token)
        {
            return await _selector.ReadAsync(reader, map, token).ConfigureAwait(false);
        }
    }
}