﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Baseline;
using Marten.Schema;
using Marten.Services.Includes;
using Npgsql;
using Remotion.Linq;
using Remotion.Linq.Clauses.ResultOperators;

namespace Marten.Linq.QueryHandlers
{
    public interface IQueryHandlerFactory
    {
        IQueryHandler<T> HandlerForScalarQuery<T>(QueryModel model);
        IQueryHandler<T> HandlerForSingleQuery<T>(QueryModel model, IIncludeJoin[] joins, bool returnDefaultWhenEmpty);


        IQueryHandler<T> BuildHandler<T>(QueryModel model, IIncludeJoin[] joins);

        IQueryHandler<TOut> HandlerFor<TDoc, TOut>(ICompiledQuery<TDoc, TOut> query);
    }

    public class QueryHandlerFactory : IQueryHandlerFactory
    {
        private readonly IDocumentSchema _schema;
        private readonly ConcurrentCache<Type, CachedQuery> _cache = new ConcurrentCache<Type, CachedQuery>();

        public QueryHandlerFactory(IDocumentSchema schema)
        {
            _schema = schema;
        }

        public IQueryHandler<T> BuildHandler<T>(QueryModel model, IIncludeJoin[] joins)
        {
            return tryFindScalarQuery<T>(model) ?? tryFindSingleQuery<T>(model, joins) ?? listHandlerFor<T>(model, joins);
        }

        private IQueryHandler<T> listHandlerFor<T>(QueryModel model, IIncludeJoin[] joins)
        {
            if (!typeof (T).IsGenericEnumerable())
            {
                return null;
            }

            var elementType = typeof (T).GetGenericArguments().First();
            var handlerType = typeof(ListQueryHandler<>);

            if (typeof (T).GetGenericTypeDefinition() == typeof (IEnumerable<>))
            {
                handlerType = typeof (EnumerableQueryHandler<>);
            }
            return Activator.CreateInstance(handlerType.MakeGenericType(elementType), new object[] {_schema, model, joins}).As<IQueryHandler<T>>();
        }

        public IQueryHandler<T> HandlerForScalarQuery<T>(QueryModel model)
        {
            _schema.EnsureStorageExists(model.SourceType());

            return tryFindScalarQuery<T>(model);
        }

        private IQueryHandler<T> tryFindScalarQuery<T>(QueryModel model)
        {
            if (model.HasOperator<CountResultOperator>() || model.HasOperator<LongCountResultOperator>())
            {
                return new CountQueryHandler<T>(model, _schema);
            }

            if (model.HasOperator<SumResultOperator>())
            {
                return AggregateQueryHandler<T>.Sum(_schema, model);
            }

            if (model.HasOperator<AverageResultOperator>())
            {
                return AggregateQueryHandler<T>.Average(_schema, model);
            }

            if (model.HasOperator<AnyResultOperator>())
            {
                return new AnyQueryHandler(model, _schema).As<IQueryHandler<T>>();
            }

            return null;
        }

        public IQueryHandler<T> HandlerForSingleQuery<T>(QueryModel model, IIncludeJoin[] joins, bool returnDefaultWhenEmpty)
        {
            _schema.EnsureStorageExists(model.SourceType());

            return tryFindSingleQuery<T>(model, joins);
        }

        private IQueryHandler<T> tryFindSingleQuery<T>(QueryModel model, IIncludeJoin[] joins)
        {
            var choice = model.FindOperators<ChoiceResultOperatorBase>().FirstOrDefault();

            if (choice == null) return null;

            if (choice is FirstResultOperator)
            {
                return choice.ReturnDefaultWhenEmpty
                    ? OneResultHandler<T>.FirstOrDefault(_schema, model, joins)
                    : OneResultHandler<T>.First(_schema, model, joins);
            }

            if (choice is SingleResultOperator)
            {
                return choice.ReturnDefaultWhenEmpty
                    ? OneResultHandler<T>.SingleOrDefault(_schema, model, joins)
                    : OneResultHandler<T>.Single(_schema, model, joins);
            }

            if (choice is MinResultOperator)
            {
                return AggregateQueryHandler<T>.Min(_schema, model);
            }

            if (choice is MaxResultOperator)
            {
                return AggregateQueryHandler<T>.Max(_schema, model);
            }

            if (model.HasOperator<LastResultOperator>())
            {
                throw new InvalidOperationException(
                    "Marten does not support Last()/LastOrDefault(). Use reverse ordering and First()/FirstOrDefault() instead");
            }

            return null;
        }

        public IQueryHandler<TOut> HandlerFor<TDoc, TOut>(ICompiledQuery<TDoc, TOut> query)
        {
            var queryType = query.GetType();
            CachedQuery cachedQuery;
            if (!_cache.Has(queryType))
            {
                cachedQuery = buildCachedQuery<TDoc, TOut>(queryType, query.QueryIs());

                _cache[queryType] = cachedQuery;
            }
            else
            {
                cachedQuery = _cache[queryType];
            }

            return cachedQuery.CreateHandler<TOut>(query);
        }


        private CachedQuery buildCachedQuery<TDoc, TOut>(Type queryType, Expression expression)
        {
            var invocation = Expression.Invoke(expression, Expression.Parameter(typeof(IMartenQueryable<TDoc>)));

            var setters = findSetters(queryType, expression);

            var model = MartenQueryParser.Flyweight.GetParsedQuery(invocation);
            _schema.EnsureStorageExists(typeof(TDoc));

            // TODO -- someday we'll add Include()'s to compiled queries
            var handler = _schema.HandlerFactory.BuildHandler<TOut>(model, new IIncludeJoin[0]);
            var cmd = new NpgsqlCommand();
            handler.ConfigureCommand(cmd);

            return new CachedQuery
            {
                Command = cmd,
                ParameterSetters = setters,
                Handler = handler
            };
        }

        private static IList<IDbParameterSetter> findSetters(Type queryType, Expression expression)
        {
            var visitor = new CompiledQueryMemberExpressionVisitor(queryType);
            visitor.Visit(expression);
            var parameterSetters = visitor.ParameterSetters;
            return parameterSetters;
        }
    }
}
