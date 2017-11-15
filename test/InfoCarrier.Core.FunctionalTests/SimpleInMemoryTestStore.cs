﻿// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure.Internal;

    public class SimpleInMemoryTestStore : TestStoreImplBase
    {
        public SimpleInMemoryTestStore(string databaseName)
        {
            this.DbContextOptions = new DbContextOptionsBuilder().UseInMemoryDatabase(databaseName).Options;
        }

        protected override DbContextOptions DbContextOptions { get; }

        public override string ServerUrl
            => this.DbContextOptions.GetExtension<InMemoryOptionsExtension>().StoreName;

        public override TestStoreBase FromShared()
            => throw new NotImplementedException();

        protected override DbContext CreateStoreContextInternal(DbContext clientDbContext)
            => throw new NotImplementedException();
    }
}