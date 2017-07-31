﻿namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.ComplexNavigationsModel;

    public class ComplexNavigationsOwnedQueryInfoCarrierFixture : ComplexNavigationsOwnedQueryFixtureBase<TestStoreBase>
    {
        private readonly InfoCarrierTestHelper<ComplexNavigationsContext> helper;

        public ComplexNavigationsOwnedQueryInfoCarrierFixture()
        {
            this.helper = InMemoryTestStore<ComplexNavigationsContext>.CreateHelper(
                this.OnModelCreating,
                opt => new ComplexNavigationsContext(opt),
                ComplexNavigationsModelInitializer.Seed);
        }

        public override TestStoreBase CreateTestStore()
            => this.helper.CreateTestStore();

        public override ComplexNavigationsContext CreateContext(TestStoreBase testStore)
            => this.helper.CreateInfoCarrierContext(testStore);
    }
}