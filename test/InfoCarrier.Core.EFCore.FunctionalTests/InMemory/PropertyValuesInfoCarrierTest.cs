﻿namespace InfoCarrier.Core.EFCore.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class PropertyValuesInfoCarrierTest
        : PropertyValuesTestBase<TestStore, PropertyValuesInfoCarrierTest.PropertyValuesInfoCarrierFixture>
    {
        public PropertyValuesInfoCarrierTest(PropertyValuesInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        public class PropertyValuesInfoCarrierFixture : PropertyValuesFixtureBase
        {
            private readonly InfoCarrierInMemoryTestHelper<AdvancedPatternsMasterContext> helper;

            public PropertyValuesInfoCarrierFixture()
            {
                this.helper = InfoCarrierTestHelper.CreateInMemory(
                    this.OnModelCreating,
                    (opt, _) => new AdvancedPatternsMasterContext(opt));
            }

            public override TestStore CreateTestStore()
                => this.helper.CreateTestStore(this.Seed);

            public override DbContext CreateContext(TestStore testStore)
                => this.helper.CreateInfoCarrierContext();
        }
    }
}