﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.Azure.WebJobs.Host.Tables
{
    internal class QueryableArgumentBindingProvider : IStorageTableArgumentBindingProvider
    {
        public IStorageTableArgumentBinding TryCreate(ParameterInfo parameter)
        {
            if (!parameter.ParameterType.IsGenericType || parameter.ParameterType.GetGenericTypeDefinition() != typeof(IQueryable<>))
            {
                return null;
            }

            Type entityType = GetQueryableItemType(parameter.ParameterType);

            if (!TableClient.ImplementsITableEntity(entityType))
            {
                throw new InvalidOperationException("IQueryable is only supported on types that implement ITableEntity.");
            }

            TableClient.VerifyDefaultConstructor(entityType);

            return CreateBinding(entityType);
        }

        private static Type GetQueryableItemType(Type queryableType)
        {
            Type[] genericArguments = queryableType.GetGenericArguments();
            var itemType = genericArguments[0];
            return itemType;
        }

        private static IStorageTableArgumentBinding CreateBinding(Type entityType)
        {
            Type genericType = typeof(QueryableArgumentBinding<>).MakeGenericType(entityType);
            return (IStorageTableArgumentBinding)Activator.CreateInstance(genericType);
        }

        private class QueryableArgumentBinding<TElement> : IStorageTableArgumentBinding
            where TElement : ITableEntity, new()
        {
            public Type ValueType
            {
                get { return typeof(IQueryable<TElement>); }
            }

            public FileAccess Access
            {
                get
                {
                    return FileAccess.Read;
                }
            }

            public async Task<IValueProvider> BindAsync(IStorageTable value, ValueBindingContext context)
            {
                IQueryable<TElement> queryable;

                if (!await value.ExistsAsync(context.CancellationToken))
                {
                    queryable = Enumerable.Empty<TElement>().AsQueryable();
                }
                else
                {
                    queryable = value.CreateQuery<TElement>();
                }

                return new TableValueProvider(value, queryable, typeof(IQueryable<TElement>));
            }
        }
    }
}
