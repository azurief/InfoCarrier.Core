﻿// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using TestUtilities;

    public class IncludeOneToOneInfoCarrierTest : IncludeOneToOneTestBase<IncludeOneToOneInfoCarrierTest.TestFixture>
    {
        public IncludeOneToOneInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        public class TestFixture : OneToOneQueryFixtureBase
        {
            private ITestStoreFactory testStoreFactory;

            protected override ITestStoreFactory TestStoreFactory =>
                InfoCarrierTestStoreFactory.CreateOrGet(
                    ref this.testStoreFactory,
                    this.ContextType,
                    this.OnModelCreating,
                    InfoCarrierTestStoreFactory.InMemory);
        }
    }
}
